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
/// INTEREST MODEL (updated):
///   - loan.OverallInterest is still set at loan creation (Principal x
///     Rate%) and kept only as a display/reference figure — it is NOT used
///     for payment allocation anymore.
///   - Payment allocation now uses DAILY ACCRUED INTEREST: Outstanding
///     Principal x Annual Rate% / 365 x days since the last payment (or
///     loan start date for the first payment). See BuildInterestCalculationAsync.
///   - loan.OutstandingInterest holds only the unpaid carry-forward portion
///     of accrued interest (0 in the normal case where a payment covers the
///     full accrued interest in one go). It is added to the newly accrued
///     amount on every recalculation, then reset to whatever is left unpaid.
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
            //var today = DateTime.UtcNow.Date;
            var today = IstClock.Today;
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

        //var items = loans.Select(l => new LoanOperationsGridRowDto(
        //    l.LoanId,
        //    l.LoanNumber,
        //    l.Customer?.CustomerName ?? "",
        //    FormatScheme(l),
        //    l.LoanAmount,
        //    l.OverallInterest,
        //    l.OutstandingPrincipal,
        //    l.OutstandingInterest,
        //    Round2(l.OutstandingPrincipal + l.OutstandingInterest),
        //    paidLookup.TryGetValue(l.LoanId, out var paid) ? Round2(paid) : 0m,
        //    l.InterestRatePct,
        //    l.Status,
        //    l.LoanDate,
        //    l.MaturityDate
        //)).ToList();
        var items = loans.Select(l => {
            var hasPriorPayment = paidLookup.ContainsKey(l.LoanId);
            var genuineOutstandingInterest = hasPriorPayment ? l.OutstandingInterest : 0m;
            var paidAmount = hasPriorPayment ? Round2(paidLookup[l.LoanId]) : 0m;

            return new LoanOperationsGridRowDto(
                l.LoanId,
                l.LoanNumber,
                l.Customer?.CustomerName ?? "",
                FormatScheme(l),
                l.LoanAmount,
                l.OverallInterest,
                l.OutstandingPrincipal,
                genuineOutstandingInterest,
                Round2(l.OutstandingPrincipal + genuineOutstandingInterest),
                paidAmount,
                l.InterestRatePct,
                l.Status,
                l.LoanDate,
                l.MaturityDate
            );
        }).ToList();

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
            "createdat" => l => l.CreatedAt,
            _ => l => l.CreatedAt   // default: newest loans first
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

        //return await BuildInterestCalculationAsync(loan, (asOfDate ?? DateTime.UtcNow.Date).Date);
        return await BuildInterestCalculationAsync(loan, (asOfDate ?? IstClock.Today).Date);
    }

    /// <summary>
    /// Outstanding Interest is now the DAILY ACCRUED figure (Outstanding
    /// Principal x Annual Rate% / 365 x days since last payment), plus any
    /// unpaid interest carried forward from a prior underpayment
    /// (loan.OutstandingInterest). This combined amount is what's actually
    /// authoritative for MaxPayable and payment allocation — the fixed
    /// OverallInterest schedule is display-only now.
    /// </summary>
    private async Task<LoanOperationsInterestCalculationDto> BuildInterestCalculationAsync(Loan loan, DateTime asOfDate)
    {
        //var lastReferenceDate = await GetLastInterestReferenceDateAsync(loan.LoanId, loan.LoanDate);
        //var (noOfDays, dailyRate, dailyAmount, accruedInterest) = _calc.CalculateAccruedInterestInfoOnly(
        //    loan.OutstandingPrincipal, loan.InterestRatePct, lastReferenceDate, asOfDate);

        //// loan.OutstandingInterest here is only the carried-forward unpaid
        //// portion from a previous underpayment (0 in the normal case).
        //var outstandingInterest = Round2(loan.OutstandingInterest + accruedInterest);
        // lastAnyPaymentDate anchors the daily-accrual window (days since last payment
        // of ANY type, including old-system InterestCollection/PrincipalCollection).
        var lastAnyPaymentDate = await GetLastCollectionDateAsync(loan.LoanId);
        var lastReferenceDate = (lastAnyPaymentDate ?? loan.LoanDate ?? IstClock.Today).Date;

        var (noOfDays, dailyRate, dailyAmount, accruedInterest) = _calc.CalculateAccruedInterestInfoOnly(
            loan.OutstandingPrincipal, loan.InterestRatePct, lastReferenceDate, asOfDate);

        // Only trust loan.OutstandingInterest as a genuine carry-forward if a NEW-SYSTEM
        // payment (LoanOpsPayment) already ran — it's the only thing that sets
        // OutstandingInterest to a correct residual. Old-system loans have
        // OutstandingInterest seeded with the full year interest at creation, which
        // must NOT be carried forward under the daily-accrual model.
        var lastNewSystemPaymentDate = await GetLastNewSystemPaymentDateAsync(loan.LoanId);
        var genuineCarryForward = lastNewSystemPaymentDate.HasValue ? loan.OutstandingInterest : 0m;

        var outstandingInterest = Round2(genuineCarryForward + accruedInterest);
        var interestPaidToDate = Round2(loan.OverallInterest - outstandingInterest);
        var maxPayable = Round2(outstandingInterest + loan.OutstandingPrincipal);

        return new LoanOperationsInterestCalculationDto(
            loan.OverallInterest, interestPaidToDate, outstandingInterest,
            loan.OutstandingPrincipal, maxPayable, asOfDate,
            lastReferenceDate, noOfDays,
            dailyRate, Math.Round(dailyAmount, 4, MidpointRounding.AwayFromZero), accruedInterest);
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

        //var asOf = (asOfDate ?? DateTime.UtcNow.Date).Date;
        var asOf = (asOfDate ?? IstClock.Today).Date;
        var interestCalc = await BuildInterestCalculationAsync(loan, asOf);
        var lastPaymentDate = await GetLastCollectionDateAsync(loanId);
        var branchName = await GetBranchNameAsync(loan.BranchId);
        var createdByName = loan.CreatedBy.HasValue ? await GetUserNameAsync(loan.CreatedBy.Value) : null;

        return new LoanOperationsPaymentDetailsDto(
            loan.LoanId, loan.LoanNumber, loan.Status,
            loan.Customer.CustomerName, loan.Customer.CustomerCode,
            loan.Customer.AadhaarNumber, loan.Customer.PanNumber, loan.Customer.Mobile, loan.Customer.Address,
            FormatScheme(loan), loan.InterestRatePct,
            loan.ProcessingFee,
            loan.LoanDate, loan.MaturityDate,
            loan.LoanAmount, loan.OverallInterest, loan.OutstandingPrincipal,
            interestCalc.OutstandingInterest,  // daily-accrued total (carry-forward + newly accrued), not raw DB value
            interestCalc.MaxPayable,           // outstandingInterest + outstandingPrincipal
            lastPaymentDate, branchName, createdByName, interestCalc);
    }

    // ===================================================================
    // SAVE PAYMENT
    // ===================================================================
    public static class IstClock
    {
        private static readonly TimeZoneInfo IstZone = TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "India Standard Time" : "Asia/Kolkata");

        public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, IstZone);
        public static DateTime Today => Now.Date;
    }
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

            // Interest due for THIS payment = daily accrued interest since the
            // last payment (or loan start, for the first payment), computed
            // on the current outstanding principal, plus any unpaid interest
            // carried forward from a prior underpayment. This replaces the
            // old fixed OverallInterest-based figure for allocation purposes.


            //var lastReferenceDate = await GetLastInterestReferenceDateAsync(loan.LoanId, loan.LoanDate);
            //var (noOfDays, dailyRate, dailyAmount, accruedInterest) = _calc.CalculateAccruedInterestInfoOnly(
            //    loan.OutstandingPrincipal, loan.InterestRatePct, lastReferenceDate, paymentDate);

            //var interestDue = Round2(loan.OutstandingInterest + accruedInterest);
            //var maxPayable = Round2(interestDue + loan.OutstandingPrincipal);
            var lastAnyPaymentDate = await GetLastCollectionDateAsync(loan.LoanId);
            var lastReferenceDate = (lastAnyPaymentDate ?? loan.LoanDate ?? IstClock.Today).Date;

            var (noOfDays, dailyRate, dailyAmount, accruedInterest) = _calc.CalculateAccruedInterestInfoOnly(
                loan.OutstandingPrincipal, loan.InterestRatePct, lastReferenceDate, paymentDate);

            var lastNewSystemPaymentDate = await GetLastNewSystemPaymentDateAsync(loan.LoanId);
            var genuineCarryForward = lastNewSystemPaymentDate.HasValue ? loan.OutstandingInterest : 0m;
            var interestDue = Round2(genuineCarryForward + accruedInterest);
            var maxPayable = Round2(interestDue + loan.OutstandingPrincipal);
            var amountReceived = Round2(request.AmountReceived);

            if (amountReceived > maxPayable + 0.01m)
                throw new InvalidOperationException($"Amount received (\u20b9{amountReceived:N2}) exceeds the total payable amount of \u20b9{maxPayable:N2}.");

            var (interestPaid, principalPaid, remainingInterest, remainingPrincipal) =
                _calc.AllocatePayment(amountReceived, interestDue, loan.OutstandingPrincipal);

            loan.OutstandingPrincipal = remainingPrincipal;
            // Whatever of interestDue wasn't covered by this payment carries
            // forward and is added to the next accrual the next time this
            // loan is calculated (see BuildInterestCalculationAsync above).
            loan.OutstandingInterest = remainingInterest;
            //loan.UpdatedAt = DateTime.UtcNow;
            loan.UpdatedAt = IstClock.Now;

            // Rule 12: NEVER auto-close, even if both balances hit zero.
            // Status changes ONLY inside CloseLoanAsync via an explicit user action.

            var sequence = await _db.LoanTransactions.CountAsync() + 1;
            var receiptNumber = _calc.GenerateReceiptNumber(sequence);

            var txn = new LoanTransaction
            {
                LoanId = loan.LoanId,
                TransactionType = LoanOperationsCalculationHelper.PaymentTransactionType,
                ReceiptNumber = receiptNumber,
                //TransactionDate = paymentDate == DateTime.UtcNow.Date ? DateTime.UtcNow : paymentDate,
                TransactionDate = paymentDate == IstClock.Today ? IstClock.Now : paymentDate,
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
                //CreatedAt = DateTime.UtcNow
                CreatedAt = IstClock.Now
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

    // === CHANGE (closure fix): GetClosureDetailsAsync now recomputes
    // outstanding interest live (carry-forward + daily accrual as of today)
    // instead of reading loan.OutstandingInterest directly, which only
    // reflects interest realized as of the last payment/preview call and
    // would otherwise under-state what's owed if days have passed since. ===
    public async Task<LoanOperationsClosureDetailsDto> GetClosureDetailsAsync(int loanId)
    {
        var loan = await _db.Loans.AsNoTracking()
            .Include(l => l.Customer).Include(l => l.LoanScheme)
            .FirstOrDefaultAsync(l => l.LoanId == loanId);
        if (loan == null) throw new KeyNotFoundException($"Loan {loanId} not found.");

        var totalAmountPaid = await GetTotalAmountPaidAsync(loanId);
        var outstandingInterest = await GetCurrentOutstandingInterestAsync(loan, IstClock.Today);

        const decimal otherCharges = 0m;
        var grandTotal = Round2(outstandingInterest + loan.OutstandingPrincipal + otherCharges);

        return new LoanOperationsClosureDetailsDto(
            loan.LoanId, loan.LoanNumber, loan.Customer?.CustomerName ?? "", loan.Customer?.Mobile,
            FormatScheme(loan),
            loan.ProcessingFee, totalAmountPaid,
            loan.OutstandingPrincipal, outstandingInterest, otherCharges,
            grandTotal, grandTotal <= 0.009m);
    }

    public async Task<LoanOperationsClosureResponseDto> CloseLoanAsync(int loanId, LoanOperationsClosureRequestDto request, string? closePhotoPath = null)
    {
        await using var dbTransaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var loan = await _db.Loans
                .Include(l => l.Customer).Include(l => l.LoanScheme)
                .FirstOrDefaultAsync(l => l.LoanId == loanId);
            if (loan == null) throw new KeyNotFoundException($"Loan {loanId} not found.");
            if (loan.Status == "Closed") throw new InvalidOperationException($"Loan {loan.LoanNumber} is already closed.");

            // === CHANGE (closure fix): recompute outstanding interest live
            // (carry-forward + daily accrual as of today) instead of trusting
            // loan.OutstandingInterest, which is only realized on the last
            // payment/preview call and would let a loan close while days of
            // unrecorded accrued interest are still outstanding. ===
            var outstandingInterest = await GetCurrentOutstandingInterestAsync(loan, IstClock.Today);
            var grandTotal = Round2(outstandingInterest + loan.OutstandingPrincipal);

            if (grandTotal > 0.009m)
                throw new InvalidOperationException(
                    $"Loan cannot be closed. Outstanding balance of \u20b9{grandTotal:N2} must be cleared first. Partial closure is not allowed.");

            loan.OutstandingPrincipal = 0;
            loan.OutstandingInterest = 0;
            loan.Status = "Closed";
            //loan.ClosedAt = DateTime.UtcNow;
            loan.ClosedAt = IstClock.Now;
            loan.ClosedBy = request.ProcessedByUserId;
            //loan.UpdatedAt = DateTime.UtcNow;
            loan.UpdatedAt = IstClock.Now;
            loan.ClosePhotoPath = closePhotoPath;

            var sequence = await _db.LoanTransactions.CountAsync() + 1;
            var receiptNumber = _calc.GenerateReceiptNumber(sequence, "CLS");

            var txn = new LoanTransaction
            {
                LoanId = loan.LoanId,
                TransactionType = LoanOperationsCalculationHelper.ClosureTransactionType,
                ReceiptNumber = receiptNumber,
                //TransactionDate = DateTime.UtcNow,
                TransactionDate = IstClock.Now,
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
                //CreatedAt = DateTime.UtcNow
                CreatedAt = IstClock.Now
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
                // CHANGE (daily-interest): interestBal stays 0 at disbursement.
                // Under daily accrual no interest is pre-loaded — it builds up
                // day by day from Outstanding Principal x Rate% / 365. Running
                // balance at disbursement is principal only.
                interestBal = 0m;

                rows.Add(new LoanOperationsLedgerRowDto(
                    t.TransactionId, t.TransactionDate, t.TransactionType, "Loan disbursed to customer",
                    t.PrincipalAmount, 0, principalBal, interestBal, principalBal,
                    t.ReceiptNumber, userName, t.Remarks, false));

                // 2. Add Processing Fee Rows (if a fee exists on this loan)
                if (loan.ProcessingFee > 0m)
                {
                    var fee = loan.ProcessingFee;

                    // Row A: Debit Processing Fee (Charges Debited)
                    rows.Add(new LoanOperationsLedgerRowDto(
                        t.TransactionId,
                        t.TransactionDate,
                        "Charges",
                        "Processing Fee Debited",
                        fee,
                        0m,
                        principalBal,
                        interestBal,
                        principalBal + fee, // Temporarily increases total running balance (interest not pre-loaded)
                        t.ReceiptNumber,
                        userName,
                        "Processing fee charged at initiation",
                        false
                    ));

                    // Row B: Credit Processing Fee (Upfront Collection)
                    rows.Add(new LoanOperationsLedgerRowDto(
                        t.TransactionId,
                        t.TransactionDate,
                        "Charges",
                        "Processing Fee Collected",
                        0m,
                        fee,
                        principalBal,
                        interestBal,
                        principalBal, // Resets total running balance back (interest not pre-loaded)
                        t.ReceiptNumber,
                        userName,
                        "Processing fee collected upfront",
                        false
                    ));
                }
                continue;
            }

            var creditAmount = Round2(t.PrincipalAmount + t.InterestAmount + t.PenaltyAmount);
            interestBal = Math.Max(0m, Round2(interestBal - (t.InterestAmount + t.PenaltyAmount)));
            principalBal = Math.Max(0m, Round2(principalBal - t.PrincipalAmount));

            rows.Add(new LoanOperationsLedgerRowDto(
                t.TransactionId, t.TransactionDate, t.TransactionType, DescribeTransaction(t),
                0, creditAmount, principalBal, interestBal, principalBal + interestBal,
                t.ReceiptNumber, userName, t.Remarks, true));
        }

        var totalCount = rows.Count;
        var safePage = Math.Max(1, page);
        var safePageSize = pageSize is > 0 and <= 100 ? pageSize : 10;
        var pagedRows = rows.Skip((safePage - 1) * safePageSize).Take(safePageSize).ToList();

        var totalInterestCollected = Round2(transactions.Where(t => t.TransactionType != "Disbursement").Sum(t => t.InterestAmount + t.PenaltyAmount));
        var totalPrincipalCollected = Round2(transactions.Where(t => t.TransactionType != "Disbursement").Sum(t => t.PrincipalAmount));

        return new LoanOperationsLedgerResponseDto(
            loan.LoanId, loan.LoanNumber, loan.Customer?.CustomerName ?? "", FormatScheme(loan),
            loan.LoanDate, loan.MaturityDate, loan.LoanAmount, loan.OverallInterest, loan.InterestRatePct,
            loan.ProcessingFee, loan.Status,
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

    // === NEW (closure fix): shared by GetClosureDetailsAsync and
    // CloseLoanAsync. Same math as BuildInterestCalculationAsync but returns
    // just the combined figure — avoids depending on the full
    // LoanOperationsInterestCalculationDto shape from closure code paths. ===
    private async Task<decimal> GetCurrentOutstandingInterestAsync(Loan loan, DateTime asOfDate)
    {
        //var lastReferenceDate = await GetLastInterestReferenceDateAsync(loan.LoanId, loan.LoanDate);
        //var (_, _, _, accruedInterest) = _calc.CalculateAccruedInterestInfoOnly(
        //    loan.OutstandingPrincipal, loan.InterestRatePct, lastReferenceDate, asOfDate);
        //return Round2(loan.OutstandingInterest + accruedInterest);
        var lastAnyPaymentDate = await GetLastCollectionDateAsync(loan.LoanId);
        var lastReferenceDate = (lastAnyPaymentDate ?? loan.LoanDate ?? IstClock.Today).Date;

        var (_, _, _, accruedInterest) = _calc.CalculateAccruedInterestInfoOnly(
            loan.OutstandingPrincipal, loan.InterestRatePct, lastReferenceDate, asOfDate);

        var lastNewSystemPaymentDate = await GetLastNewSystemPaymentDateAsync(loan.LoanId);
        var genuineCarryForward = lastNewSystemPaymentDate.HasValue ? loan.OutstandingInterest : 0m;
        return Round2(genuineCarryForward + accruedInterest);
    }

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

    /// <summary>
    /// Returns the date of the most recent NEW-SYSTEM payment (LoanOpsPayment or
    /// LoanOpsClosure only). Used to decide whether loan.OutstandingInterest is a
    /// genuine carry-forward set by the new system, or the old seeded/legacy value
    /// that should be ignored for daily-accrual purposes.
    /// Old-system transactions (InterestCollection, PrincipalCollection, Closure)
    /// do NOT qualify — they may have set OutstandingInterest to the full year
    /// interest at loan creation, which would incorrectly inflate the carry-forward.
    /// </summary>
    private async Task<DateTime?> GetLastNewSystemPaymentDateAsync(int loanId)
    {
        var newSystemTypes = new[]
        {
            LoanOperationsCalculationHelper.PaymentTransactionType,   // "LoanOpsPayment"
            LoanOperationsCalculationHelper.ClosureTransactionType    // "LoanOpsClosure"
        };
        return await _db.LoanTransactions.AsNoTracking()
            .Where(t => t.LoanId == loanId && newSystemTypes.Contains(t.TransactionType))
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