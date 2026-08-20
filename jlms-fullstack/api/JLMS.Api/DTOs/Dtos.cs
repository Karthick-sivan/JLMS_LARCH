namespace JLMS.Api.DTOs;

using Microsoft.AspNetCore.Http;



// ---------- Auth ----------
public record LoginRequest(string Username, string Password, int? BranchId);
public record LoginResponse(int UserId, string FullName, string Username, string RoleName, string BranchName, string Token, int? BranchId, int RoleId);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

// ---------- Common ----------
public record PagedResultDto<T>(List<T> Items, int TotalCount, int Page, int PageSize);

// ---------- Customers ----------
public class CustomerCreateDto
{
    public string CustomerName { get; set; } = "";
    public string? GuardianName { get; set; }
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

    public string? NomineeName { get; set; }
    public string? NomineeMobile { get; set; }
    public string? NomineeAddress { get; set; }
    public string? NomineeCity { get; set; }
    public string? NomineeAadhaarNumber { get; set; }
    public IFormFile? NomineePhoto { get; set; }
    public IFormFile? NomineeAadhaarFile { get; set; }
}

public record CustomerUpdateDto(
    string CustomerName,
    string? GuardianName,
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
    bool KycVerified,
 string? NomineeName,
 string? NomineeMobile,
 string? NomineeAddress,
 string? NomineeCity,
 string? NomineeAadhaarNumber
);

public record CustomerListItemDto(
    int CustomerId, string CustomerCode, string CustomerName, string? AadhaarNumber, string Mobile,int?branchId,
    int ActiveLoans, decimal TotalOutstanding, string Status, string? LoanNumbers = null
);

public record CustomerDetailDto(
    int CustomerId, string CustomerCode, string CustomerName, string? GuardianName, string? Gender,
    DateTime? DateOfBirth, string Mobile, string? AlternateMobile, string? Address,
    string? City, string? State, string? Pincode, string? AadhaarNumber, string? PanNumber,
    bool KycVerified, int ActiveLoans, decimal TotalOutstanding, int ClosedLoans, DateTime CreatedAt,
    string? NomineeName, string? NomineeMobile, string? NomineeAddress, string? NomineeCity,
string? NomineeAadhaarNumber, string? NomineePhotoPath, string? NomineeAadhaarDocPath,
    string? PhotoPath = null, string? AadhaarDocPath = null, string? PanDocPath = null
);

// ---------- Jewel Types / Gold Rates / Schemes (Masters) ----------
public record JewelTypeDto(int JewelTypeId, string JewelTypeName, string Category, string? DefaultPurity, decimal WastagePercent, bool IsActive, int? BranchId);
public record JewelTypeCreateDto(string JewelTypeName, string Category, string? DefaultPurity, decimal WastagePercent, bool IsActive, int? BranchId = null);

public record GoldRateDto(int GoldRateId, DateTime EffectiveDate, decimal Rate24K, decimal Rate22K, decimal Rate18K, decimal SilverRate, int? BranchId);
public record GoldRateCreateDto(decimal Rate24K, decimal Rate22K, decimal Rate18K, decimal SilverRate, int? BranchId = null);

public record LoanSchemeDto(int LoanSchemeId, string SchemeName, decimal InterestRatePct, int TenureMonths, decimal MaxLtvPercent, decimal ProcessingFee, decimal PenaltyRatePerDay, bool IsActive, int? BranchId);
public record LoanSchemeCreateDto(string SchemeName, decimal InterestRatePct, int TenureMonths, decimal MaxLtvPercent, decimal ProcessingFee, decimal PenaltyRatePerDay, bool IsActive, int? BranchId = null);

// ---------- Jewel Appraisal ----------
public record JewelItemInputDto(int JewelTypeId, int Quantity, decimal GrossWeightGrams, decimal StoneWeightGrams, string? Purity, string? Model = null, string? Varient = null);
public record JewelItemResultDto(int JewelItemId, string JewelTypeName, int Quantity, decimal GrossWeightGrams, decimal StoneWeightGrams, decimal NetWeightGrams, string? Purity, decimal MarketValue);

public record AppraisalRequestDto(int CustomerId, List<JewelItemInputDto> Items);
public record AppraisalResultDto(
    List<JewelItemResultDto> Items,
    decimal TotalGrossWeight, decimal TotalStoneWeight, decimal TotalNetWeight,
    decimal GoldRateUsed, decimal TotalMarketValue
);

// ---------- New Loan ----------
// AllowExceedEligible: when true (the "Don't validate" checkbox on New Loan), the server
// skips the RequestedLoanAmount > EligibleAmount check. Defaults to false so existing
// callers that don't send this field keep the old, validated behavior.
public record NewLoanRequestDto(
    int CustomerId, int LoanSchemeId, List<JewelItemInputDto> JewelItems, decimal ProcessingFee,
    decimal RequestedLoanAmount, string? Remarks, bool AllowExceedEligible = false
);

public record LoanSummaryDto(
    int LoanId, string LoanNumber, string CustomerName, string Status,
    decimal MarketValue, decimal EligibleAmount, decimal LoanAmount,
    decimal OutstandingPrincipal, decimal OutstandingInterest,
    DateTime? LoanDate, DateTime? MaturityDate, string? BookNo
);

// ---------- New Loan creation response (includes created jewel item ids so the
// front end can attach photos to the right item right after creation) ----------
public record NewLoanJewelItemRefDto(int JewelItemId, int JewelTypeId);
public record NewLoanResponseDto(
    int LoanId, string LoanNumber, string Status,
    decimal MarketValue, decimal EligibleAmount, decimal LoanAmount, decimal OverallInterest,
    List<NewLoanJewelItemRefDto> JewelItems, string? BookNo
);

// ---------- Approval / Disbursement ----------
public record ApprovalDecisionDto(bool Approved, string? Remarks, int ApprovedByUserId);
public record DisbursementRequestDto(string PaymentMode, string? ReferenceNo, int ProcessedByUserId);

// ---------- Collections ----------
public record InterestCollectionRequestDto(decimal AmountReceived, string PaymentMode, string? ReferenceNo, bool IsPartial, int ProcessedByUserId);
public record PrincipalCollectionRequestDto(decimal PrincipalAmount, string PaymentMode, string? ReferenceNo, int ProcessedByUserId);
public record ReceiptDto(string ReceiptNumber, DateTime TransactionDate, string LoanNumber, string CustomerName,
    decimal InterestCollected, decimal PenaltyCollected, decimal TotalReceived, string PaymentMode,
    decimal BalancePrincipal, DateTime? NextDueDate, string? BookNo);

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
    bool IsOverdue,
    string? BookNo
);
// ---------- User Master ----------
public record UserMasterDto(
    int UserId,
    string? EmployeeCode,
    //string EmployeeCode,
    string FullName,
    string Username,
    int RoleId,
    string RoleName,
    int? BranchId,
    string BranchName,
    string? Mobile,
    string? Email,
    bool IsActive,
    DateTime CreatedAt
);

public record BranchOptionDto(int BranchId, string BranchName, string BranchCode);
public record BranchDto(int BranchId, string BranchCode, string BranchName, string? City, string? State, bool IsActive, DateTime CreatedAt);
public record BranchCreateDto(string BranchCode, string BranchName, string? City, string? State, bool IsActive);
public record RoleOptionDto(int RoleId, string RoleName);
public record RoleCreateDto(string RoleName, string? Description, bool IsActive);

public record UserCreateDto(
        string? EmployeeCode,
    string FullName,
    string Username,
    string Password,
    int RoleId,
    int? BranchId,
    string? Mobile,
    string? Email,
    bool IsActive
);

public record UserUpdateDto(
        string? EmployeeCode,
    string FullName,
    string Username,
    string? Password,
    int RoleId,
    int? BranchId,
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
    string Status,
    string? BookNo
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
    DateTime? LoanDate,
    string TransactionType,
    decimal LoanAmount,
    decimal PrincipalAmount,
    decimal InterestAmount,
    decimal TotalAmount,
    decimal BalanceAmount,
    string? BookNo
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
// ============================================================================
// Loan Operations � DTOs
// ============================================================================

// ---------------------------- Grid ----------------------------------------

public record LoanOperationsGridQueryDto(
    string? LoanNo,
    string? CustomerName,
    string? Mobile,
    string? Aadhaar,
    string? Pan,
    string? Scheme,
    string? Status,
    DateTime? FromDate,
    DateTime? ToDate,
    int Page = 1,
    int PageSize = 10,
    string? SortBy = null,
    string? SortDir = "asc"
);

public record LoanOperationsGridRowDto(
    int LoanId,
    string LoanNo,
    string CustomerName,
    string LoanScheme,           // formatted: "Gold Loan Standard (12% - 12 Months)"
    decimal Principal,
    decimal OverallInterest,
    decimal OutstandingPrincipal,
    decimal OutstandingInterest,
    decimal TotalOutstanding,
    decimal TotalAmountPaid,
    decimal AnnualInterestRate,
    string Status,
    DateTime? LoanDate,
    DateTime? MaturityDate,
    string? BookNo
);

public record LoanOperationsGridResultDto(
    List<LoanOperationsGridRowDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);

// ---------------------------- Interest snapshot -----------------------------

public record LoanOperationsInterestCalculationDto(
    decimal OverallInterest,
    decimal InterestPaidToDate,
    decimal OutstandingInterest,      // authoritative: OverallInterest - InterestPaidToDate
    decimal OutstandingPrincipal,
    decimal MaxPayable,               // authoritative: OutstandingInterest + OutstandingPrincipal
    DateTime AsOfDate,

    // ---- INFO-ONLY reference figures below. NOT charged, NOT part of
    // MaxPayable, NOT added to OutstandingInterest. Display purposes only. ----
    DateTime LastInterestReferenceDate,
    int NoOfDaysInfoOnly,
    decimal DailyInterestRateInfoOnly,
    decimal DailyInterestAmountInfoOnly,
    decimal AccruedInterestInfoOnly,
     bool HasPriorPayment
);


// ---------------------------- Payment save ----------------------------------

public record LoanOperationsPaymentRequestDto(
    DateTime PaymentDate,
    string PaymentMode,
    string? ReferenceNo,
    decimal AmountReceived,
    int ProcessedByUserId
);

public record LoanOperationsPaymentResponseDto(
    bool Success,
    string ReceiptNumber,
    string TransactionId,
    DateTime TransactionDate,
    string LoanNo,
    string CustomerName,
    string PaymentMode,
    decimal AmountReceived,
    decimal InterestPaid,
    decimal PrincipalPaid,
    decimal RemainingInterest,
    decimal RemainingPrincipal,
    decimal CurrentOutstanding,
    decimal CurrentInterest,
    string LoanStatus
);

// ---------------------------- Closure ----------------------------------------



public record LoanOperationsClosureRequestDto(
    string PaymentMode,
    string? ReferenceNo,
    int ProcessedByUserId
);

public record LoanOperationsClosureResponseDto(
    bool Success,
    string ReceiptNumber,
    DateTime TransactionDate,
    string LoanNo,
    string CustomerName,
    string LoanScheme,
    decimal OutstandingPrincipal,
    decimal OutstandingInterest,
    decimal OtherCharges,
    decimal GrandTotal,
    string LoanStatus
);


public class ClosureRequestWithPhotoDto
{
    public string PaymentMode { get; set; } = "Cash";
    public string? ReferenceNo { get; set; }
    public int ProcessedByUserId { get; set; }
    public IFormFile? ClosePhoto { get; set; }
}

// ---------------------------- Ledger ------------------------------------------

public record LoanOperationsLedgerRowDto(
    int TransactionId,
    DateTime Date,
    string TransactionType,
    string Description,
    decimal Debit,
    decimal Credit,
    decimal PrincipalBalance,
    decimal InterestBalance,
    decimal RunningBalance,
    string? ReceiptNumber,
    string? UserName,
    string? Remarks,
    bool CanDownloadReceipt
);


public record SubmitForApprovalRequestDto(int SubmittedByUserId);

public record SubmitForApprovalResponseDto(
    int LoanId, string LoanNumber, string Status, string CustomerName,
    int ApprovedByUserId, DateTime ApprovedAt, string ReceiptNumber, DateTime DisbursedAt,
    DateTime? LoanDate, DateTime? MaturityDate, decimal LoanAmount, decimal ProcessingFee,
    decimal NetDisbursedAmount, decimal OutstandingPrincipal, decimal OutstandingInterest,
    string PaymentMode, string? BookNo
);




public record LoanOperationsLedgerResponseDto(
    int LoanId,
    string LoanNo,
    string CustomerName,
    string LoanScheme,           // formatted display string
    DateTime? LoanDate,
    DateTime? MaturityDate,
    decimal Principal,
    decimal OverallInterest,
    decimal AnnualInterestRate,
    decimal ProcessingFee,
    string Status,
    List<LoanOperationsLedgerRowDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    decimal TotalInterestCollected,
    decimal TotalPrincipalCollected,
    decimal CurrentOutstanding,
    string? BookNo
);



public record LoanOperationsClosureDetailsDto(
    int LoanId,
    string LoanNo,
    string CustomerName,
    string? Mobile,
    string LoanScheme,           // formatted display string
    decimal ProcessingFee,
    decimal TotalAmountPaid,
    decimal OutstandingPrincipal,
    decimal OutstandingInterest,
    decimal OtherCharges,
    decimal GrandTotal,
    bool IsClosable,
    string? BookNo
);


public record LoanOperationsPaymentDetailsDto(
    int LoanId,
    string LoanNo,
    string Status,
    string CustomerName,
    string CustomerCode,
    string? Aadhaar,
    string? Pan,
    string? Mobile,
    string? Address,
    string LoanScheme,           // formatted display string
    decimal AnnualInterestRate,
    decimal ProcessingFee,
    DateTime? LoanDate,
    DateTime? MaturityDate,
    decimal Principal,
    decimal OverallInterest,
    decimal OutstandingPrincipal,
    decimal OutstandingInterest,
    decimal TotalOutstanding,
    DateTime? LastPaymentDate,
    string BranchName,
    string? CreatedByName,
    LoanOperationsInterestCalculationDto InterestCalculation,
    string? BookNo
);

// ---------- Customers: lightweight list for the "browse customers" picker ----------
public record CustomerActiveListItemDto(int CustomerId, string CustomerCode, string CustomerName, string Mobile,int? branchId);




// ---------- Active Loans Report ----------
public record ActiveLoanReportRowDto(
    int LoanId,
    string LoanNumber,
    string CustomerCode,
    string CustomerName,
    string Mobile,
    string SchemeDisplay,          // "SchemeName (X% - Y Months)"
    DateTime? LoanDate,
    DateTime? MaturityDate,
    string JewelTypes,             // distinct jewel type names, comma joined
    decimal GrossWeight,
    decimal NetWeight,
    string Purity,                 // distinct purities, comma joined
    decimal PrincipalAmount,
    decimal OverallInterest,
    decimal OutstandingPrincipal,
    decimal OutstandingInterest,
    decimal TotalOutstanding,
    DateTime? LastPaymentDate,
    int DaysOverdue,
    string Status,                 // derived: Active / Due / Overdue
    string BranchName,
    string? BookNo
);

public record ActiveLoanReportPagedDto(
    List<ActiveLoanReportRowDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalActiveLoans,
    decimal TotalPrincipal,
    decimal TotalOverallInterest,
    decimal TotalOutstandingPrincipal,
    decimal TotalOutstandingInterest,
    decimal GrandOutstanding,
   int TotalQuantity,
    decimal TotalWeight,
    decimal TotalLoanAmount
);

// ----------  Loan Details Report ----------
public record LoanDetailsReportRowDto(
    int LoanId,
    string LoanNumber,
    string CustomerCode,
    string CustomerName,
    string Mobile,
        string Address,
    string SchemeDisplay,
    DateTime? LoanDate,
    DateTime? MaturityDate,
    string JewelTypes,
    int Quantity,
    decimal GrossWeight,
    decimal NetWeight,
    string Purity,
    string Model,
    string Variant,
    decimal PrincipalAmount,
    decimal OverallInterest,
    decimal OutstandingPrincipal,
    decimal OutstandingInterest,
    decimal TotalOutstanding,
    DateTime? LastPaymentDate,
    int DaysOverdue,
    string Status,
    string BranchName,
    string? BookNo
);
public record LoanDetailsReportPagedDto(
    List<LoanDetailsReportRowDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalActiveLoans,
    decimal TotalPrincipal,
    decimal TotalOverallInterest,
    decimal TotalOutstandingPrincipal,
    decimal TotalOutstandingInterest,
    decimal GrandOutstanding,
   int TotalQuantity,
    decimal TotalWeight,
    decimal TotalLoanAmount
);

// ---------- Closed Loans Report ----------
public record ClosedLoanReportRowDto(
    int LoanId,
    string LoanNumber,
    string CustomerCode,
    string CustomerName,
    string Mobile,
    string SchemeDisplay,
    DateTime? LoanDate,
    DateTime? MaturityDate,
    DateTime? ClosureDate,
    int LoanDurationDays,
    string JewelTypes,
    decimal GrossWeight,
    decimal NetWeight,
    string Purity,
    decimal PrincipalAmount,
    decimal OverallInterest,
    decimal TotalInterestCollected,
    decimal TotalPrincipalCollected,
    decimal TotalAmountCollected,
    decimal ClosureCharges,
    decimal FinalSettlementAmount,
    string? PaymentMode,
    string? ReferenceNo,
    string? ClosedByName,
    string BranchName,
    string? ClosureReceiptNo,
    string Status,
    string? Remarks,
    string? BookNo
);

public record ClosedLoanReportPagedDto(
    List<ClosedLoanReportRowDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalClosedLoans,
    decimal TotalPrincipalDisbursed,
    decimal TotalPrincipalCollected,
    decimal TotalInterestCollected,
    decimal GrandTotalCollected
);

// ---- Payment Receipt PDF ----
// Sourced from LoanOperationsPaymentResponseDto fields (already returned to frontend)
public record PaymentReceiptPdfDto(
    string ReceiptNumber,
    string LoanNo,
    string CustomerName,
    string? Mobile,
    DateTime TransactionDate,
    string PaymentMode,
    string? LoanScheme,
    DateTime? MaturityDate,
    decimal InterestPaid,
    decimal PrincipalPaid,
    decimal AmountReceived,
    decimal RemainingInterest,
    decimal RemainingPrincipal,
    string? GuardianName = null,
    int BranchId = 1,
    string? BranchName = null,
    string? City = null,
    string? State = null,
    string? BookNo = null
);

// ---- Closure Receipt PDF ----
// Sourced from LoanOperationsClosureResponseDto fields (already returned to frontend)
public record ClosureReceiptPdfDto(
    string ReceiptNumber,
    string LoanNo,
    string CustomerName,
    string? Mobile,
    string? LoanScheme,
    DateTime TransactionDate,
    decimal OutstandingPrincipal,
    decimal OutstandingInterest,
    decimal OtherCharges,
    decimal GrandTotal,
      string? GuardianName = null,
    int BranchId = 1,
    string? BranchName = null,
    string? City = null,
    string? State = null,
    string? BookNo = null
);



public record FinancialYearDto(
    int FinancialYearId,
    string Code,
    string GoldLoanType,
    DateTime FromDt,
    DateTime ToDt,
    int GoldLoanNoStartsFrom,
    string Prefix,
    string? Suffix,
    string Status,
    DateTime CreatedDt,
    int CreatedBy,
    int? BranchId);

public record FinancialYearCreateDto(
    string Code,
    string GoldLoanType,
    DateTime FromDt,
    DateTime ToDt,
    int GoldLoanNoStartsFrom,
    string Prefix,
    string? Suffix,
    int CreatedBy,
    int? BranchId);

public record FinancialYearUpdateDto(
    string Code,
    string GoldLoanType,
    DateTime FromDt,
    DateTime ToDt,
    int GoldLoanNoStartsFrom,
    string Prefix,
    string? Suffix,
    string Status,
    int? BranchId
    );

// ---------- Loan Payment Report ----------
public record LoanPaymentReportRowDto(
    int LoanId,
    string LoanNumber,
    DateTime? LoanDate,
    decimal LoanAmount,
    string CustomerName,
    string? GuardianName,
    string Mobile,
    DateTime PaymentDate,
    decimal PrincipalAmount,
    decimal InterestAmount,
    decimal PaidAmount,
    decimal BalanceAmount,
    string? BookNo
);

public record LoanPaymentReportPagedDto(
    List<LoanPaymentReportRowDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int Year,
    int Month,
    decimal TotalLoanAmount,
    decimal TotalPrincipalAmount,
    decimal TotalInterestAmount,
    decimal TotalPaidAmount,
    decimal TotalBalanceAmount
);



public record LoanListGridQueryDto(
    string? LoanNo,
    string? CustomerName,
    string? Mobile,
    string? Status,
    int Page = 1,
    int PageSize = 10
);

public record LoanListJewelItemDto(
    string JewelTypeName,
    int Quantity,
    string? Model,
    string? Variant,
    decimal GrossWeightGrams
);

public record LoanListRowDto(
    int LoanId,
    string LoanNumber,
    string BookNo,
    string CustomerName,
    string Mobile,
    string SchemeName,
    string Status,
    decimal LoanAmount,
    decimal MarketValue,
    decimal EligibleAmount,
    decimal FirstMonthInt,
    DateTime LoanDate,
    bool IsEditable,   // false the moment any transaction other than "Disbursement" exists
    List<LoanListJewelItemDto> JewelItems
);

public record LoanListGridResultDto(
    List<LoanListRowDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);

// ---------- Full edit page (edit-loan.html) ----------

public record LoanEditJewelItemDto(
    int? JewelItemId,      // null = brand-new row added while editing
    int JewelTypeId,
    int Quantity,
    decimal GrossWeightGrams,
    decimal StoneWeightGrams,
    string? Purity,
    string? Model,
    string? Varient
);

// Everything the edit page needs to render itself pre-filled, same shape of
// information new-loan.html gathers when creating — customer, scheme,
// jewel items, processing fee, remarks — plus the editable/lock flag.
public record LoanEditDetailsDto(
    int LoanId,
    string LoanNumber,
    string BookNo,

    bool IsEditable,
    int CustomerId,
    string CustomerName,
    string CustomerCode,
    string CustomerMobile,
    int LoanSchemeId,
    decimal InterestRatePct,
    int TenureMonths,
    decimal ProcessingFee,
    string? Remarks,
    decimal RequestedLoanAmount,
    decimal EligibleAmount,
    decimal MarketValue,
    decimal FirstMonthInt,
    List<LoanEditJewelItemDto> JewelItems
);

public record LoanEditRequestDto(
    int CustomerId,
    int LoanSchemeId,
    List<LoanEditJewelItemDto> JewelItems,
    decimal RequestedLoanAmount,
    decimal ProcessingFee,
    string? Remarks,
    bool AllowExceedEligible,
    int EditedByUserId
);

public record LoanEditResponseDto(
    int LoanId,
    string LoanNumber,
    string BookNo,

    decimal MarketValue,
    decimal EligibleAmount,
    decimal LoanAmount,
    decimal OverallInterest,
    decimal FirstMonthInt
);
