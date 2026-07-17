using JLMS.Api.Models;

namespace JLMS.Api.Services;

public class LoanCalculationService
{
    /// <summary>
    /// Calculates net weight and market value for a single jewel item.
    /// NetWeight = GrossWeight - StoneWeight.
    /// MarketValue = NetWeight * rate-per-gram-for-purity (uses 22K rate by default
    /// unless purity string contains "24K" or "18K").
    /// </summary>
    public (decimal netWeight, decimal marketValue) CalculateJewelValue(
        decimal grossWeight, decimal stoneWeight, string? purity,
        decimal rate24K, decimal rate22K, decimal rate18K)
    {
        var netWeight = Math.Max(0, grossWeight - stoneWeight);

        decimal ratePerGram = rate22K; // default / most common
        if (!string.IsNullOrWhiteSpace(purity))
        {
            if (purity.Contains("24")) ratePerGram = rate24K;
            else if (purity.Contains("18")) ratePerGram = rate18K;
            else if (purity.Contains("22")) ratePerGram = rate22K;
        }

        var marketValue = Math.Round(netWeight * ratePerGram, 2);
        return (netWeight, marketValue);
    }

    /// <summary>
    /// Eligible loan amount = total market value * scheme's max LTV%.
    /// </summary>
    public decimal CalculateEligibleAmount(decimal totalMarketValue, decimal maxLtvPercent)
        => Math.Round(totalMarketValue * (maxLtvPercent / 100m), 2);

    /// <summary>
    /// Simple-interest accrual from loan/last-payment date to "asOf" date.
    /// Interest = Principal * (AnnualRate/100) * (DaysElapsed/365)
    /// Matches "Daily Reducing Balance" style used in the system settings default.
    /// </summary>
    public decimal CalculateAccruedInterest(decimal principal, decimal annualRatePct, DateTime fromDate, DateTime asOfDate)
    {
        if (asOfDate <= fromDate) return 0;
        var days = (asOfDate.Date - fromDate.Date).Days;
        var interest = principal * (annualRatePct / 100m) * (days / 365m);
        return Math.Round(interest, 2);
    }

    /// <summary>
    /// Penalty accrues only after the grace period, at PenaltyRatePerDay% of
    /// outstanding principal, for each day beyond the maturity/due date + grace.
    /// </summary>
    public decimal CalculatePenalty(decimal outstandingPrincipal, decimal penaltyRatePerDay, DateTime dueDate, DateTime asOfDate, int graceDays = 3)
    {
        var graceCutoff = dueDate.AddDays(graceDays);
        if (asOfDate.Date <= graceCutoff.Date) return 0;

        var overdueDays = (asOfDate.Date - graceCutoff.Date).Days;
        var penalty = outstandingPrincipal * (penaltyRatePerDay / 100m) * overdueDays;
        return Math.Round(penalty, 2);
    }

    public int CalculateOverdueDays(DateTime dueDate, DateTime asOfDate)
    {
        if (asOfDate.Date <= dueDate.Date) return 0;
        return (asOfDate.Date - dueDate.Date).Days;
    }

    //public string GenerateLoanNumber(int sequence) => $"JL-{DateTime.UtcNow:yyyy}{sequence:D4}";

    //public string GenerateLoanNumber(int sequence, string branchCode)
    //    => $"{branchCode}-JL-{DateTime.UtcNow:yyyy}{sequence:D4}";


    public string GenerateLoanNumber(int sequence) => $"JL-{DateTime.UtcNow:yyyy}{sequence:D5}";
    public string GenerateReceiptNumber(long sequence) => $"RC-{sequence:D5}";
    public string GenerateCustomerCode(int sequence) => $"JLCUS-{DateTime.UtcNow:yyyy}{sequence:D6}";
}
