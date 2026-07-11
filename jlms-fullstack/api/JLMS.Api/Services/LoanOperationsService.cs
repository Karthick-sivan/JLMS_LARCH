using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using JLMS.Api.Data;
using JLMS.Api.Models;
using JLMS.Api.DTOs;

namespace JLMS.Api.Services;

/// <summary>
/// Business logic for the merged Loan Operations page (Interest Collection +
/// Principal Collection + Loan Closure in one screen).
///
/// INTEREST MODEL:
///   - loan.OverallInterest is fixed at loan creation (Principal x Rate%) and
///     never changes.
///   - loan.OutstandingInterest starts equal to OverallInterest and only ever
///     decreases via interest-first payment allocation.
///   - The Daily Interest Rate / Accrued Interest figures shown to staff are
///     INFO-ONLY — they never affect OutstandingInterest, MaxPayable, or what
///     gets collected/persisted.
///   - A loan NEVER auto-closes. Status only changes inside CloseLoanAsync,
///     which requires an explicit user action from the Loan Closure modal.
/// </summary>
public class LoanOperationsService
{
    private readonly JlmsDbContext _db;
    private readonly LoanOperationsCalculationHelper _calc;

    private static readonly string[] CollectionTransactionTypes =
    {
        "InterestCollection",
        "PrincipalCollection",
        "Closure",
        LoanOperationsCalculationHelper.PaymentTransactionType,
        LoanOperationsCalculationHelper.ClosureTransactionType
    };

    public LoanOperationsService(JlmsDbContext db, LoanOperationsCalculationHelper calc)
    {
        _db = db;
        _calc = calc;
    }

    // ===================================================================
    // GRID
    // ===================================================================

    public async Task<LoanOperationsGridResultDto> GetGridAsync(LoanOperationsGridQueryDto query)
    {
        var q = _db.Loans.AsNoTracking()
            .Include(l => l.Customer)
            .Include(l => l.LoanScheme)
            .AsQueryable();

        var statusFilter = string.IsNullOrWhiteSpace(query.Status) ? "Active" : query.Status;

        if (statusFilter == "Overdue")
        {
            var today = DateTime.UtcNow.Date;
            q = q.Where(l => l.Status == "Active" && l.MaturityDate != null && l.MaturityDate.Value.Date < today);
        }
        else if (!string.Equals(statusFilter, "All", StringComparison.OrdinalIgnoreCase))
        {
            q = q.Where(l => l.Status == statusFilter);
        }

        if (!string.IsNullOrWhiteSpace(query.LoanNo))
            q = q.Where(l => l.LoanNumber.Contains(query.LoanNo));
        if (!string.IsNullOrWhiteSpace(query.CustomerName))
            q = q.Where(l => l.Customer!.CustomerName.Contains(query.CustomerName));
        if (!string.IsNullOrWhiteSpace(query.Mobile))
            q = q.Where(l => l.Customer!.Mobile.Contains(query.Mobile));
        if (!string.IsNullOrWhiteSpace(query.Aadhaar))
            q = q.Where(l => l.Customer!.AadhaarNumber != null && l.Customer.AadhaarNumber.Contains(query.Aadhaar));
        if (!string.IsNullOrWhiteSpace(query.Pan))
            q = q.Where(l => l.Customer!.PanNumber != null && l.Customer.PanNumber.Contains(query.Pan));
        if (!string.IsNullOrWhiteSpace(query.Scheme))
            q = q.Where(l => l.LoanScheme!.SchemeName == query.Scheme);
        if (query.FromDate.HasValue)
            q = q.Where(l => l.LoanDate != null && l.LoanDate.Value.Date >= query.FromDate.Value.Date);
        if (query.ToDate.HasValue)
            q = q.Where(l => l.LoanDate != null && l.LoanDate.Value.Date <= query.ToDate.Value.Date);

        q = ApplySort(q, query.SortBy, query.SortDir);

        var totalCount = await q.CountAsync();

        var page = Math.Max(1, query.Page);
        var pageSize = query.PageSize is > 0 and <= 200 ? query.PageSize : 10;

        var loans = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        var loanIds = loans.Select(l => l.LoanId).ToList();

        var paidTotals = await _db.LoanTransactions.AsNoTracking()
            .Where(t => loanIds.Contains(t.LoanId) && CollectionTransactionTypes.Contains(t.TransactionType))
            .GroupBy(t => t.LoanId)
            .Select(g => new { LoanId = g.Key, Total = g.Sum(x => x.PrincipalAmount + x.InterestAmount + x.PenaltyAmount) })
            .ToListAsync();
        var paidLookup = paidTotals.ToDictionary(x => x.LoanId, x => x.Total);

        var items = loans.Select(l => new LoanOperationsGridRowDto(
            l.LoanId,
            l.LoanNumber,
            l.Customer?.CustomerName ?? "",
            FormatScheme(l),
            l.LoanAmount,
            l.OverallInterest,
            l.OutstandingPrincipal,
            l.OutstandingInterest,
            Round2(l.OutstandingPrincipal + l.OutstandingInterest),
            paidLookup.TryGetValue(l.LoanId, out var paid) ? Round2(paid) : 0m,
            l.InterestRatePct,
            l.Status,
            l.LoanDate,
            l.MaturityDate
        )).ToList();

        return new LoanOperationsGridResultDto(items, totalCount, page, pageSize);
    }

    private static IQueryable<Loan> ApplySort(IQueryable<Loan> q, string? sortBy, string? sortDir)
    {
        var desc = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);

        Expression<Func<Loan, object>> keySelector = (sortBy ?? "").ToLowerInvariant() switch
        {
            "loanno" => l => l.LoanNumber,
            "customername" => l => l.Customer!.CustomerName,
            "principal" => l => l.LoanAmount,
            "overallinterest" => l => l.OverallInterest,
            "outstandingprincipal" => l => l.OutstandingPrincipal,
            "outstandinginterest" => l => l.OutstandingInterest,
            "loandate" => l => l.LoanDate!,
            "maturitydate" => l => l.MaturityDate!,
            "status" => l => l.Status,
            _ => l => l.LoanId
        };

        return desc ? q.OrderByDescending(keySelector) : q.OrderBy(keySelector);
    }

    /// <summary>"Gold Loan Standard (12% - 12 Months)"</summary>
    private string FormatScheme(Loan loan)
    {
        var schemeName = loan.LoanScheme?.SchemeName ?? "";
        if (loan.LoanScheme == null) return schemeName;
        return _calc.FormatSchemeDisplay(schemeName, loan.InterestRatePct, loan.LoanScheme.TenureMonths);
    }

    // ===================================================================
    // INTEREST SNAPSHOT (shared by payment-details / interest-preview)
    // ===================================================================

    public async Task<LoanOperationsInterestCalculationDto> GetInterestPreviewAsync(int loanId, DateTime? asOfDate)
    {
        var loan = await _db.Loans.AsNoTracking().FirstOrDefaultAsync(l => l.LoanId == loanId);
        if (loan == null) throw new KeyNotFoundException($"Loan {loanId} not found.");

        return await BuildInterestCalculationAsync(loan, (asOfDate ?? DateTime.UtcNow.Date).Date);
    }

    /// <summary>
    /// Authoritative Outstanding Interest is NOT time-based — it is exactly
    /// what's persisted on the loan (OverallInterest minus everything paid).
    /// The Daily/Accrued figures returned alongside it are informational only
    /// (Rule 7 + your follow-up) and never feed back into the authoritative
    /// numbers or MaxPayable.
    /// </summary>
    private async Task<LoanOperationsInterestCalculationDto> BuildInterestCalculationAsync(Loan loan, DateTime asOfDate)
    {
        var outstandingInterest = Round2(loan.OutstandingInterest);
        var interestPaidToDate = Round2(loan.OverallInterest - outstandingInterest);
        var maxPayable = Round2(outstandingInterest + loan.OutstandingPrincipal);

        var lastReferenceDate = await GetLastInterestReferenceDateAsync(loan.LoanId, loan.LoanDate);
        var (noOfDays, dailyRate, dailyAmount, accruedInfoOnly) = _calc.CalculateAccruedInterestInfoOnly(
            loan.OutstandingPrincipal, loan.InterestRatePct, lastReferenceDate, asOfDate);

        return new LoanOperationsInterestCalculationDto(
            loan.OverallInterest, interestPaidToDate, outstandingInterest,
            loan.OutstandingPrincipal, maxPayable, asOfDate,
            lastReferenceDate, noOfDays,
            dailyRate, Math.Round(dailyAmount, 4, MidpointRounding.AwayFromZero), accruedInfoOnly);
    }

    // ===================================================================
    // PAYMENT DETAILS (modal load)
    // ===================================================================

    public async Task<LoanOperationsPaymentDetailsDto> GetPaymentDetailsAsync(int loanId, DateTime? asOfDate)
    {
        var loan = await _db.Loans.AsNoTracking()
            .Include(l => l.Customer)
            .Include(l => l.LoanScheme)
            .FirstOrDefaultAsync(l => l.LoanId == loanId);

        if (loan == null) throw new KeyNotFoundException($"Loan {loanId} not found.");
        if (loan.Customer == null) throw new InvalidOperationException($"Loan {loan.LoanNumber} has no linked customer record.");

        var asOf = (asOfDate ?? DateTime.UtcNow.Date).Date;
        var interestCalc = await BuildInterestCalculationAsync(loan, asOf);
        var lastPaymentDate = await GetLastCollectionDateAsync(loanId);
        var branchName = await GetBranchNameAsync(loan.BranchId);
        var createdByName = loan.CreatedBy.HasValue ? await GetUserNameAsync(loan.CreatedBy.Value) : null;

        return new LoanOperationsPaymentDetailsDto(
            loan.LoanId, loan.LoanNumber, loan.Status,
            loan.Customer.CustomerName, loan.Customer.CustomerCode,
            loan.Customer.AadhaarNumber, loan.Customer.PanNumber, loan.Customer.Mobile, loan.Customer.Address,
            FormatScheme(loan), loan.InterestRatePct, loan.LoanDate, loan.MaturityDate,
            loan.LoanAmount, loan.OverallInterest, loan.OutstandingPrincipal, loan.OutstandingInterest,
            Round2(loan.OutstandingPrincipal + loan.OutstandingInterest),
            lastPaymentDate, branchName, createdByName, interestCalc);
    }

    // ===================================================================
    // SAVE PAYMENT
    // ===================================================================

    public async Task<LoanOperationsPaymentResponseDto> SavePaymentAsync(int loanId, LoanOperationsPaymentRequestDto request)
    {
        if (request.AmountReceived <= 0)
            throw new InvalidOperationException("Amount received must be greater than zero.");

        await using var dbTransaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var loan = await _db.Loans
                .Include(l => l.Customer)
                .Include(l => l.LoanScheme)
                .FirstOrDefaultAsync(l => l.LoanId == loanId);

            if (loan == null) throw new KeyNotFoundException($"Loan {loanId} not found.");
            if (loan.Status == "Closed") throw new InvalidOperationException($"Loan {loan.LoanNumber} is already closed.");
            if (loan.Status != "Active") throw new InvalidOperationException($"Loan {loan.LoanNumber} is not active (current status: {loan.Status}).");

            var paymentDate = request.PaymentDate.Date;

            // Authoritative interest figure — exactly what's persisted. NOT
            // recomputed from days elapsed (Rule 7).
            var outstandingInterest = Round2(loan.OutstandingInterest);
            var maxPayable = Round2(outstandingInterest + loan.OutstandingPrincipal);
            var amountReceived = Round2(request.AmountReceived);

            if (amountReceived > maxPayable + 0.01m)
                throw new InvalidOperationException($"Amount received (\u20b9{amountReceived:N2}) exceeds the total payable amount of \u20b9{maxPayable:N2}.");

            var (interestPaid, principalPaid, remainingInterest, remainingPrincipal) =
                _calc.AllocatePayment(amountReceived, outstandingInterest, loan.OutstandingPrincipal);

            loan.OutstandingPrincipal = remainingPrincipal;
            loan.OutstandingInterest = remainingInterest;
            loan.UpdatedAt = DateTime.UtcNow;

            // Rule 12: NEVER auto-close, even if both balances hit zero.
            // Status changes ONLY inside CloseLoanAsync via an explicit user action.

            var sequence = await _db.LoanTransactions.CountAsync() + 1;
            var receiptNumber = _calc.GenerateReceiptNumber(sequence);

            var txn = new LoanTransaction
            {
                LoanId = loan.LoanId,
                TransactionType = LoanOperationsCalculationHelper.PaymentTransactionType,
                ReceiptNumber = receiptNumber,
                TransactionDate = paymentDate == DateTime.UtcNow.Date ? DateTime.UtcNow : paymentDate,
                PrincipalAmount = principalPaid,
                InterestAmount = interestPaid,
                PenaltyAmount = 0,
                ChargesAmount = 0,
                TotalAmount = amountReceived,
                PaymentMode = request.PaymentMode,
                ReferenceNo = request.ReferenceNo,
                BalancePrincipalAfter = remainingPrincipal,
                NextDueDate = null,
                ProcessedBy = request.ProcessedByUserId,
                BranchId = loan.BranchId,
                Remarks = (remainingPrincipal == 0m && remainingInterest == 0m)
                    ? "Balance fully cleared by this payment. Loan remains Active until closed via Loan Closure."
                    : null,
                CreatedAt = DateTime.UtcNow
            };

            _db.LoanTransactions.Add(txn);
            await _db.SaveChangesAsync();
            await dbTransaction.CommitAsync();

            return new LoanOperationsPaymentResponseDto(
                true, receiptNumber, txn.TransactionId.ToString(), txn.TransactionDate,
                loan.LoanNumber, loan.Customer?.CustomerName ?? "", request.PaymentMode,
                amountReceived, interestPaid, principalPaid, remainingInterest, remainingPrincipal,
                remainingPrincipal, remainingInterest, loan.Status);
        }
        catch
        {
            await dbTransaction.RollbackAsync();
            throw;
        }
    }

    // ===================================================================
    // CLOSURE
    // ===================================================================

    public async Task<LoanOperationsClosureDetailsDto> GetClosureDetailsAsync(int loanId)
    {
        var loan = await _db.Loans.AsNoTracking()
            .Include(l => l.Customer).Include(l => l.LoanScheme)
            .FirstOrDefaultAsync(l => l.LoanId == loanId);
        if (loan == null) throw new KeyNotFoundException($"Loan {loanId} not found.");

        var totalAmountPaid = await GetTotalAmountPaidAsync(loanId);

        const decimal otherCharges = 0m;
        var grandTotal = Round2(loan.OutstandingInterest + loan.OutstandingPrincipal + otherCharges);

        return new LoanOperationsClosureDetailsDto(
            loan.LoanId, loan.LoanNumber, loan.Customer?.CustomerName ?? "", loan.Customer?.Mobile,
            FormatScheme(loan), totalAmountPaid,
            loan.OutstandingPrincipal, loan.OutstandingInterest, otherCharges,
            grandTotal, grandTotal <= 0.009m);
    }

    public async Task<LoanOperationsClosureResponseDto> CloseLoanAsync(int loanId, LoanOperationsClosureRequestDto request)
    {
        await using var dbTransaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var loan = await _db.Loans
                .Include(l => l.Customer).Include(l => l.LoanScheme)
                .FirstOrDefaultAsync(l => l.LoanId == loanId);
            if (loan == null) throw new KeyNotFoundException($"Loan {loanId} not found.");
            if (loan.Status == "Closed") throw new InvalidOperationException($"Loan {loan.LoanNumber} is already closed.");

            var grandTotal = Round2(loan.OutstandingInterest + loan.OutstandingPrincipal);

            if (grandTotal > 0.009m)
                throw new InvalidOperationException(
                    $"Loan cannot be closed. Outstanding balance of \u20b9{grandTotal:N2} must be cleared first. Partial closure is not allowed.");

            loan.OutstandingPrincipal = 0;
            loan.OutstandingInterest = 0;
            loan.Status = "Closed";
            loan.ClosedAt = DateTime.UtcNow;
            loan.ClosedBy = request.ProcessedByUserId;
            loan.UpdatedAt = DateTime.UtcNow;

            var sequence = await _db.LoanTransactions.CountAsync() + 1;
            var receiptNumber = _calc.GenerateReceiptNumber(sequence, "CLS");

            var txn = new LoanTransaction
            {
                LoanId = loan.LoanId,
                TransactionType = LoanOperationsCalculationHelper.ClosureTransactionType,
                ReceiptNumber = receiptNumber,
                TransactionDate = DateTime.UtcNow,
                PrincipalAmount = 0,
                InterestAmount = 0,
                PenaltyAmount = 0,
                ChargesAmount = 0,
                TotalAmount = 0,
                PaymentMode = request.PaymentMode,
                ReferenceNo = request.ReferenceNo,
                BalancePrincipalAfter = 0,
                ProcessedBy = request.ProcessedByUserId,
                BranchId = loan.BranchId,
                Remarks = "Loan closed by user - full settlement confirmed (balance was \u20b90.00 at time of closure).",
                CreatedAt = DateTime.UtcNow
            };
            _db.LoanTransactions.Add(txn);
            await _db.SaveChangesAsync();
            await dbTransaction.CommitAsync();

            return new LoanOperationsClosureResponseDto(
                true, receiptNumber, txn.TransactionDate, loan.LoanNumber, loan.Customer?.CustomerName ?? "",
                FormatScheme(loan), 0, 0, 0, grandTotal, loan.Status);
        }
        catch
        {
            await dbTransaction.RollbackAsync();
            throw;
        }
    }

    // ===================================================================
    // LEDGER (computed LIVE from LoanTransactions — no accrual, ever)
    // ===================================================================

    public async Task<LoanOperationsLedgerResponseDto> GetLedgerAsync(int loanId, int page, int pageSize)
    {
        var loan = await _db.Loans.AsNoTracking()
            .Include(l => l.Customer).Include(l => l.LoanScheme)
            .FirstOrDefaultAsync(l => l.LoanId == loanId);
        if (loan == null) throw new KeyNotFoundException($"Loan {loanId} not found.");

        var transactions = await _db.LoanTransactions.AsNoTracking()
            .Where(t => t.LoanId == loanId)
            .OrderBy(t => t.TransactionDate).ThenBy(t => t.TransactionId)
            .ToListAsync();

        var userIds = transactions.Where(t => t.ProcessedBy.HasValue).Select(t => t.ProcessedBy!.Value).Distinct().ToList();
        var userNames = await _db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.UserId))
            .ToDictionaryAsync(u => u.UserId, u => u.FullName);

        var rows = new List<LoanOperationsLedgerRowDto>();
        var principalBal = 0m;
        var interestBal = 0m;

        foreach (var t in transactions)
        {
            var userName = t.ProcessedBy.HasValue && userNames.TryGetValue(t.ProcessedBy.Value, out var n) ? n : null;

            if (t.TransactionType == "Disbursement")
            {
                principalBal += t.PrincipalAmount;
                // Full scheme interest is due the moment the loan is disbursed —
                // it does not build up day by day.
                interestBal = loan.OverallInterest;

                rows.Add(new LoanOperationsLedgerRowDto(
                    t.TransactionDate, t.TransactionType, "Loan disbursed to customer",
                    t.PrincipalAmount, 0, principalBal, interestBal, principalBal + interestBal,
                    t.ReceiptNumber, userName, t.Remarks));
                continue;
            }

            var creditAmount = Round2(t.PrincipalAmount + t.InterestAmount + t.PenaltyAmount);
            interestBal = Math.Max(0m, Round2(interestBal - (t.InterestAmount + t.PenaltyAmount)));
            principalBal = Math.Max(0m, Round2(principalBal - t.PrincipalAmount));

            rows.Add(new LoanOperationsLedgerRowDto(
                t.TransactionDate, t.TransactionType, DescribeTransaction(t),
                0, creditAmount, principalBal, interestBal, principalBal + interestBal,
                t.ReceiptNumber, userName, t.Remarks));
        }

        var totalCount = rows.Count;
        var safePage = Math.Max(1, page);
        var safePageSize = pageSize is > 0 and <= 100 ? pageSize : 10;
        var pagedRows = rows.Skip((safePage - 1) * safePageSize).Take(safePageSize).ToList();

        var totalInterestCollected = Round2(transactions.Where(t => t.TransactionType != "Disbursement").Sum(t => t.InterestAmount + t.PenaltyAmount));
        var totalPrincipalCollected = Round2(transactions.Where(t => t.TransactionType != "Disbursement").Sum(t => t.PrincipalAmount));

        return new LoanOperationsLedgerResponseDto(
            loan.LoanId, loan.LoanNumber, loan.Customer?.CustomerName ?? "", FormatScheme(loan),
            loan.LoanDate, loan.MaturityDate, loan.LoanAmount, loan.OverallInterest, loan.InterestRatePct, loan.Status,
            pagedRows, totalCount, safePage, safePageSize,
            totalInterestCollected, totalPrincipalCollected,
            Round2(loan.OutstandingPrincipal + loan.OutstandingInterest));
    }

    private static string DescribeTransaction(LoanTransaction t) => t.TransactionType switch
    {
        "InterestCollection" => "Interest payment received",
        "PrincipalCollection" => "Principal payment received",
        "Renewal" => "Loan renewed",
        "Closure" => "Loan closed - full settlement",
        LoanOperationsCalculationHelper.PaymentTransactionType => "Payment received (Interest + Principal)",
        LoanOperationsCalculationHelper.ClosureTransactionType => "Loan closed - full settlement",
        _ => t.TransactionType
    };

    // ===================================================================
    // Shared helpers
    // ===================================================================

    /// <summary>Used ONLY to anchor the info-only daily/accrued reference figure —
    /// has no effect on Outstanding Interest or anything payable.</summary>
    private async Task<DateTime> GetLastInterestReferenceDateAsync(int loanId, DateTime? loanDate)
    {
        var lastDate = await _db.LoanTransactions.AsNoTracking()
            .Where(t => t.LoanId == loanId && CollectionTransactionTypes.Contains(t.TransactionType))
            .OrderByDescending(t => t.TransactionDate)
            .Select(t => (DateTime?)t.TransactionDate)
            .FirstOrDefaultAsync();

        return (lastDate ?? loanDate ?? DateTime.UtcNow.Date).Date;
    }

    private async Task<DateTime?> GetLastCollectionDateAsync(int loanId)
    {
        return await _db.LoanTransactions.AsNoTracking()
            .Where(t => t.LoanId == loanId && CollectionTransactionTypes.Contains(t.TransactionType))
            .OrderByDescending(t => t.TransactionDate)
            .Select(t => (DateTime?)t.TransactionDate)
            .FirstOrDefaultAsync();
    }

    private async Task<decimal> GetTotalAmountPaidAsync(int loanId)
    {
        var sum = await _db.LoanTransactions.AsNoTracking()
            .Where(t => t.LoanId == loanId && CollectionTransactionTypes.Contains(t.TransactionType))
            .SumAsync(t => (decimal?)(t.PrincipalAmount + t.InterestAmount + t.PenaltyAmount)) ?? 0m;
        return Round2(sum);
    }

    private async Task<string> GetBranchNameAsync(int branchId)
    {
        var name = await _db.Branches.AsNoTracking().Where(b => b.BranchId == branchId).Select(b => b.BranchName).FirstOrDefaultAsync();
        return name ?? "";
    }

    private async Task<string?> GetUserNameAsync(int userId)
    {
        return await _db.Users.AsNoTracking().Where(u => u.UserId == userId).Select(u => u.FullName).FirstOrDefaultAsync();
    }

    private static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}