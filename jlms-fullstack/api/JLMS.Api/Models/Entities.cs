using System.ComponentModel.DataAnnotations;

namespace JLMS.Api.Models;

public class Branch
{
    public int BranchId { get; set; }
    public string BranchCode { get; set; } = "";
    public string BranchName { get; set; } = "";
    public string? City { get; set; }
    public string? State { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}

public class Role
{
    public int RoleId { get; set; }
    public string RoleName { get; set; } = "";
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

public class User
{
    public int UserId { get; set; }
    public string EmployeeCode { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public int RoleId { get; set; }
    public Role? Role { get; set; }
    public int BranchId { get; set; }
    public Branch? Branch { get; set; }
    public string? Mobile { get; set; }
    public string? Email { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}

public class Customer
{
    public int CustomerId { get; set; }
    public string CustomerCode { get; set; } = "";
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
    public string? PhotoPath { get; set; }
    public string? AadhaarDocPath { get; set; }
    public string? PanDocPath { get; set; }
    public bool KycVerified { get; set; }
    public int? BranchId { get; set; }
    public int? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ICollection<Loan> Loans { get; set; } = new List<Loan>();
}

public class JewelType
{
    public int JewelTypeId { get; set; }
    public string JewelTypeName { get; set; } = "";
    public string Category { get; set; } = "Ornament";
    public string? DefaultPurity { get; set; }
    public decimal WastagePercent { get; set; }
    public bool IsActive { get; set; } = true;
}

public class GoldRate
{
    public int GoldRateId { get; set; }
    public DateTime EffectiveDate { get; set; }
    public decimal Rate24K { get; set; }
    public decimal Rate22K { get; set; }
    public decimal Rate18K { get; set; }
    public int? UpdatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class LoanScheme
{
    public int LoanSchemeId { get; set; }
    public string SchemeName { get; set; } = "";
    public decimal InterestRatePct { get; set; }
    public int TenureMonths { get; set; }
    public decimal MaxLtvPercent { get; set; }
    public decimal ProcessingFee { get; set; }
    public decimal PenaltyRatePerDay { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}

public class Loan
{
    public int LoanId { get; set; }
    public string LoanNumber { get; set; } = "";
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public int LoanSchemeId { get; set; }
    public LoanScheme? LoanScheme { get; set; }
    public int BranchId { get; set; }

    public DateTime? LoanDate { get; set; }
    public DateTime? MaturityDate { get; set; }

    public decimal InterestRatePct { get; set; }
    public int TenureMonths { get; set; }

    public decimal MarketValue { get; set; }
    public decimal EligibleAmount { get; set; }
    public decimal LoanAmount { get; set; }
    public decimal ProcessingFee { get; set; }

    public decimal OutstandingPrincipal { get; set; }
    public decimal OutstandingInterest { get; set; }
    public decimal PenaltyAccrued { get; set; }

    public string Status { get; set; } = "Draft";
    public string? Remarks { get; set; }

    public int? CreatedBy { get; set; }
    public int? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public int? DisbursedBy { get; set; }
    public DateTime? DisbursedAt { get; set; }
    public int? ClosedBy { get; set; }
    public DateTime? ClosedAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ICollection<JewelItem> JewelItems { get; set; } = new List<JewelItem>();
    public ICollection<LoanTransaction> Transactions { get; set; } = new List<LoanTransaction>();
}

public class JewelItem
{
    public int JewelItemId { get; set; }
    public int LoanId { get; set; }
    public int JewelTypeId { get; set; }
    public JewelType? JewelType { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal GrossWeightGrams { get; set; }
    public decimal StoneWeightGrams { get; set; }
    public decimal NetWeightGrams { get; set; }
    public string? Purity { get; set; }
    public decimal MarketValue { get; set; }
    public string? PhotoPath { get; set; }
    public bool ConditionVerified { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class LoanTransaction
{
    [Key]
    public int TransactionId { get; set; }
    public int LoanId { get; set; }
    public string TransactionType { get; set; } = "";  // Disbursement / InterestCollection / PrincipalCollection / Renewal / Closure
    public string? ReceiptNumber { get; set; }
    public DateTime TransactionDate { get; set; }

    public decimal PrincipalAmount { get; set; }
    public decimal InterestAmount { get; set; }
    public decimal PenaltyAmount { get; set; }
    public decimal ChargesAmount { get; set; }
    public decimal TotalAmount { get; set; }

    public string? PaymentMode { get; set; }
    public string? ReferenceNo { get; set; }

    public decimal? BalancePrincipalAfter { get; set; }
    public DateTime? NextDueDate { get; set; }

    public int? ProcessedBy { get; set; }
    public int? BranchId { get; set; }
    public string? Remarks { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class JewelRelease
{
    public int JewelReleaseId { get; set; }
    public int LoanId { get; set; }
    public DateTime ReleaseDate { get; set; }
    public int? ReleasedBy { get; set; }
    public bool CustomerAcknowledged { get; set; }
    public string? SignaturePath { get; set; }
    public string? Remarks { get; set; }
}

public class Auction
{
    public int AuctionId { get; set; }
    public int LoanId { get; set; }
    public int OverdueDays { get; set; }
    public string Status { get; set; } = "Eligible";
    public DateTime? NoticeSentAt { get; set; }
    public int? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? AuctionDate { get; set; }
    public decimal? RecoveredAmount { get; set; }
    public string? Remarks { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AuditLog
{
    public long AuditLogId { get; set; }
    public DateTime EventTime { get; set; }
    public int? UserId { get; set; }
    public string? ModuleName { get; set; }
    public string ActionDescription { get; set; } = "";
    public string? RecordReference { get; set; }
    public string? IpAddress { get; set; }
}
