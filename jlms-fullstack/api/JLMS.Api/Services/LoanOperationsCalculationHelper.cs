namespace JLMS.Api.Services;

/// <summary>
/// Pure, stateless financial calculations for the Loan Operations page.
/// Independent of LoanCalculationService — does not modify or call it.
///
/// INTEREST MODEL:
///   - OverallInterest is fixed ONCE at loan creation: Principal x Rate%.
///   - OutstandingInterest only ever DECREASES via interest-first payment
///     allocation. It is never increased by anything in this class.
///   - Daily/Accrued Interest figures below are INFORMATIONAL ONLY (Rule 7 +
///     your follow-up: shown to staff for reference, but never added to
///     Outstanding Interest, never part of Max Payable, never collected).
/// </summary>
public class LoanOperationsCalculationHelper
{
    public const string PaymentTransactionType = "LoanOpsPayment";
    public const string ClosureTransactionType = "LoanOpsClosure";

    private const int DaysInYear = 365;

    /// <summary>Fixed Overall Interest formula, used at loan creation. Kept here so
    /// any caller needing to (re)compute it uses the exact same math.</summary>
    public decimal CalculateOverallInterest(decimal principal, decimal annualInterestRatePct)
        => Math.Round(principal * annualInterestRatePct / 100m, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// INFO-ONLY reference figure for the Payment Modal. Shows what interest
    /// would be under a standard daily-accrual scheme, purely for staff
    /// visibility. Does NOT feed into Outstanding Interest or Max Payable —
    /// see LoanOperationsService.BuildInterestCalculationAsync.
    /// </summary>
    public (int noOfDays, decimal dailyInterestRate, decimal dailyInterestAmount, decimal accruedInterestInfoOnly) CalculateAccruedInterestInfoOnly(
        decimal outstandingPrincipal, decimal annualInterestRatePct, DateTime lastReferenceDate, DateTime asOfDate)
    {
        var noOfDays = Math.Max(0, (asOfDate.Date - lastReferenceDate.Date).Days);

        var dailyRate = annualInterestRatePct / 100m / DaysInYear;
        var dailyInterestAmount = outstandingPrincipal * dailyRate;
        var accruedInfoOnly = Math.Round(dailyInterestAmount * noOfDays, 2, MidpointRounding.AwayFromZero);

        return (noOfDays, dailyRate, dailyInterestAmount, accruedInfoOnly);
    }

    /// <summary>
    /// INTEREST-FIRST ALLOCATION RULE. The amount received is applied to
    /// outstanding interest before a single rupee reduces principal. Interest
    /// is never recalculated or increased here — only ever paid down.
    /// </summary>
    public (decimal interestPaid, decimal principalPaid, decimal remainingInterest, decimal remainingPrincipal) AllocatePayment(
        decimal amountReceived, decimal outstandingInterest, decimal outstandingPrincipal)
    {
        if (amountReceived < 0)
            throw new ArgumentOutOfRangeException(nameof(amountReceived), "Amount received cannot be negative.");

        var interestPaid = Math.Round(Math.Min(amountReceived, outstandingInterest), 2, MidpointRounding.AwayFromZero);
        var remainderAfterInterest = Math.Round(amountReceived - interestPaid, 2, MidpointRounding.AwayFromZero);
        var principalPaid = Math.Round(Math.Min(Math.Max(0m, remainderAfterInterest), outstandingPrincipal), 2, MidpointRounding.AwayFromZero);

        var remainingInterest = Math.Round(Math.Max(0m, outstandingInterest - interestPaid), 2, MidpointRounding.AwayFromZero);
        var remainingPrincipal = Math.Round(Math.Max(0m, outstandingPrincipal - principalPaid), 2, MidpointRounding.AwayFromZero);

        return (interestPaid, principalPaid, remainingInterest, remainingPrincipal);
    }

    /// <summary>"Gold Loan Standard (12% - 12 Months)"</summary>
    public string FormatSchemeDisplay(string schemeName, decimal annualInterestRatePct, int tenureMonths)
    {
        var rateStr = annualInterestRatePct == Math.Floor(annualInterestRatePct)
            ? annualInterestRatePct.ToString("0")
            : annualInterestRatePct.ToString("0.##");
        return $"{schemeName} ({rateStr}% - {tenureMonths} Months)";
    }

    public string GenerateReceiptNumber(long sequence, string prefix = "LOP") => $"{prefix}-{DateTime.UtcNow:yyyyMM}-{sequence:D6}";
}