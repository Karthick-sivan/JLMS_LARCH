namespace JLMS.Api.DTOs;

using Microsoft.AspNetCore.Http;



// ---------- Auth ----------
public record LoginRequest(string Username, string Password, int? BranchId);
public record LoginResponse(int UserId, string FullName, string Username, string RoleName, string BranchName, string Token);

// ---------- Common ----------
public record PagedResultDto<T>(List<T> Items, int TotalCount, int Page, int PageSize);

// ---------- Customers ----------
public class CustomerCreateDto
{
    public string CustomerName { get; set; } = "";
    public string? Gender { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string Mobile { get; set; } = "";
    public string? AlternateMobile { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Pincode { get; set; }
    public string? AadhaarNumber { get; set; }
    public string? PanNumber { get; set; }
    public int? BranchId { get; set; }

    // Uploaded files
    public IFormFile? CustomerPhoto { get; set; }
    public IFormFile? AadhaarFile { get; set; }
    public IFormFile? PanFile { get; set; }
}

public record CustomerUpdateDto(
    string CustomerName,
    string? Gender,
    DateTime? DateOfBirth,
    string Mobile,
    string? AlternateMobile,
    string? Address,
    string? City,
    string? State,
    string? Pincode,
    string? AadhaarNumber,
    string? PanNumber,
    bool KycVerified
);

public record CustomerListItemDto(
    int CustomerId, string CustomerCode, string CustomerName, string Mobile,
    int ActiveLoans, decimal TotalOutstanding, string Status, string? LoanNumbers = null
);

public record CustomerDetailDto(
    int CustomerId, string CustomerCode, string CustomerName, string? Gender,
    DateTime? DateOfBirth, string Mobile, string? AlternateMobile, string? Address,
    string? City, string? State, string? Pincode, string? AadhaarNumber, string? PanNumber,
    bool KycVerified, int ActiveLoans, decimal TotalOutstanding, int ClosedLoans, DateTime CreatedAt,
    string? PhotoPath = null, string? AadhaarDocPath = null, string? PanDocPath = null
);

// ---------- Jewel Types / Gold Rates / Schemes (Masters) ----------
public record JewelTypeDto(int JewelTypeId, string JewelTypeName, string Category, string? DefaultPurity, decimal WastagePercent, bool IsActive);
public record JewelTypeCreateDto(string JewelTypeName, string Category, string? DefaultPurity, decimal WastagePercent, bool IsActive);

public record GoldRateDto(int GoldRateId, DateTime EffectiveDate, decimal Rate24K, decimal Rate22K, decimal Rate18K, decimal SilverRate);
public record GoldRateCreateDto(decimal Rate24K, decimal Rate22K, decimal Rate18K, decimal SilverRate);

public record LoanSchemeDto(int LoanSchemeId, string SchemeName, decimal InterestRatePct, int TenureMonths, decimal MaxLtvPercent, decimal ProcessingFee, decimal PenaltyRatePerDay, bool IsActive);
public record LoanSchemeCreateDto(string SchemeName, decimal InterestRatePct, int TenureMonths, decimal MaxLtvPercent, decimal ProcessingFee, decimal PenaltyRatePerDay, bool IsActive);

// ---------- Jewel Appraisal ----------
public record JewelItemInputDto(int JewelTypeId, int Quantity, decimal GrossWeightGrams, decimal StoneWeightGrams, string? Purity);
public record JewelItemResultDto(int JewelItemId, string JewelTypeName, int Quantity, decimal GrossWeightGrams, decimal StoneWeightGrams, decimal NetWeightGrams, string? Purity, decimal MarketValue);

public record AppraisalRequestDto(int CustomerId, List<JewelItemInputDto> Items);
public record AppraisalResultDto(
    List<JewelItemResultDto> Items,
    decimal TotalGrossWeight, decimal TotalStoneWeight, decimal TotalNetWeight,
    decimal GoldRateUsed, decimal TotalMarketValue
);

// ---------- New Loan ----------
public record NewLoanRequestDto(
    int CustomerId, int LoanSchemeId, List<JewelItemInputDto> JewelItems,
    decimal RequestedLoanAmount, string? Remarks
);

public record LoanSummaryDto(
    int LoanId, string LoanNumber, string CustomerName, string Status,
    decimal MarketValue, decimal EligibleAmount, decimal LoanAmount,
    decimal OutstandingPrincipal, decimal OutstandingInterest,
    DateTime? LoanDate, DateTime? MaturityDate
);

// ---------- Approval / Disbursement ----------
public record ApprovalDecisionDto(bool Approved, string? Remarks, int ApprovedByUserId);
public record DisbursementRequestDto(string PaymentMode, string? ReferenceNo, int ProcessedByUserId);

// ---------- Collections ----------
public record InterestCollectionRequestDto(decimal AmountReceived, string PaymentMode, string? ReferenceNo, bool IsPartial, int ProcessedByUserId);
public record PrincipalCollectionRequestDto(decimal PrincipalAmount, string PaymentMode, string? ReferenceNo, int ProcessedByUserId);
public record ReceiptDto(string ReceiptNumber, DateTime TransactionDate, string LoanNumber, string CustomerName,
    decimal InterestCollected, decimal PenaltyCollected, decimal TotalReceived, string PaymentMode,
    decimal BalancePrincipal, DateTime? NextDueDate);

// ---------- Renewal / Closure ----------
public record RenewalRequestDto(decimal RenewalCharges, int NewTenureMonths, int ProcessedByUserId);
public record ClosureCalculationDto(decimal OutstandingPrincipal, decimal OutstandingInterest, decimal Penalty, decimal TotalClosureAmount);
public record ClosureRequestDto(string PaymentMode, string? ReferenceNo, int ProcessedByUserId);

// ---------- Dashboard ----------
public record DashboardSummaryDto(
    int ActiveLoans, decimal OutstandingAmount, decimal TodaysCollections, decimal TodaysDisbursement,
    int OverdueLoans, int AuctionEligible, int RenewalsThisMonth, int ClosuresThisMonth
);

public record CollectionTrendPointDto(DateTime CollectionDate, decimal TotalCollected);

public record LoanDueRowDto(
    string LoanNumber,
    string CustomerName,
    decimal OutstandingPrincipal,
    DateTime MaturityDate,
    bool IsOverdue
);
// ---------- User Master ----------
public record UserMasterDto(
    int UserId,
    string EmployeeCode,
    string FullName,
    string Username,
    int RoleId,
    string RoleName,
    int BranchId,
    string BranchName,
    string? Mobile,
    string? Email,
    bool IsActive,
    DateTime CreatedAt
);

public record BranchOptionDto(int BranchId, string BranchName, string BranchCode);
public record RoleOptionDto(int RoleId, string RoleName);

public record UserCreateDto(
    string EmployeeCode,
    string FullName,
    string Username,
    string Password,
    int RoleId,
    int BranchId,
    string? Mobile,
    string? Email,
    bool IsActive
);

public record UserUpdateDto(
    string EmployeeCode,
    string FullName,
    string Username,
    string? Password,
    int RoleId,
    int BranchId,
    string? Mobile,
    string? Email,
    bool IsActive
);

// ---------- Outstanding Reports ----------
public record OutstandingReportRowDto(
    int LoanId,
    string LoanNumber,
    string CustomerName,
    string CustomerCode,
    string CustomerMobile,
    string SchemeName,
    DateTime? LoanDate,
    DateTime? MaturityDate,
    decimal LoanAmount,
    decimal OutstandingPrincipal,
    decimal OutstandingInterest,
    //decimal PenaltyAccrued,
    decimal TotalOutstanding,
    int DaysOverdue,
    string Status
);

public record OutstandingReportPagedDto(
    List<OutstandingReportRowDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    decimal TotalLoanAmount,
    decimal TotalOutstandingPrincipal,
    decimal TotalOutstandingInterest,
    //decimal TotalPenalty,
    decimal GrandTotalOutstanding
);


//// ---------- Collection Reports ----------
//public record CollectionReportRowDto(
//    int LoanId,
//    string LoanNumber,
//    string CustomerName,
//    string CustomerCode,
//    string CustomerMobile,
//    string SchemeName,
//    DateTime? LoanDate,
//    DateTime? MaturityDate,
//    decimal LoanAmount,
//    decimal OutstandingPrincipal,
//    decimal OutstandingInterest,
//    //decimal PenaltyAccrued,
//    decimal TotalOutstanding,
//    int DaysOverdue,
//    string Status
//);

//public record CollectionReportPagedDto(
//    List<CollectionReportRowDto> Items,
//    int TotalCount,
//    int Page,
//    int PageSize,
//    decimal TotalLoanAmount,
//    decimal TotalOutstandingPrincipal,
//    decimal TotalOutstandingInterest,
//    //decimal TotalPenalty,
//    decimal GrandTotalOutstanding
//);

// ---------- Collection Reports ----------
public record CollectionReportRowDto(
    int TransactionId,
    int LoanId,
    string LoanNumber,
    string CustomerName,
    string CustomerCode,
    string CustomerMobile,
    string SchemeName,
    DateTime TransactionDate,
    string TransactionType,
    decimal LoanAmount,
    decimal PrincipalAmount,
    decimal InterestAmount,
    decimal TotalAmount,
    decimal BalanceAmount
);

public record CollectionReportPagedDto(
    List<CollectionReportRowDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    decimal TotalPrincipalCollected,
    decimal TotalInterestCollected,
    decimal GrandTotalCollected
);