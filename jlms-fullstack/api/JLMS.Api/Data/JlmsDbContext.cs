using Microsoft.EntityFrameworkCore;
using JLMS.Api.Models;

namespace JLMS.Api.Data;

public class JlmsDbContext : DbContext
{
    public JlmsDbContext(DbContextOptions<JlmsDbContext> options) : base(options) { }

    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<JewelType> JewelTypes => Set<JewelType>();
    public DbSet<GoldRate> GoldRates => Set<GoldRate>();
    public DbSet<LoanScheme> LoanSchemes => Set<LoanScheme>();
    public DbSet<Loan> Loans => Set<Loan>();
    public DbSet<JewelItem> JewelItems => Set<JewelItem>();
    public DbSet<LoanTransaction> LoanTransactions => Set<LoanTransaction>();
    public DbSet<JewelRelease> JewelReleases => Set<JewelRelease>();
    public DbSet<Auction> Auctions => Set<Auction>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Table name mapping (PascalCase plural matches the SQL script exactly)
        modelBuilder.Entity<Branch>().ToTable("Branches");
        modelBuilder.Entity<Role>().ToTable("Roles");
        modelBuilder.Entity<User>().ToTable("Users");
        modelBuilder.Entity<Customer>().ToTable("Customers");
        modelBuilder.Entity<JewelType>().ToTable("JewelTypes");
        modelBuilder.Entity<GoldRate>().ToTable("GoldRates");
        modelBuilder.Entity<LoanScheme>().ToTable("LoanSchemes");
        modelBuilder.Entity<Loan>().ToTable("Loans");
        modelBuilder.Entity<JewelItem>().ToTable("JewelItems");
        modelBuilder.Entity<LoanTransaction>().ToTable("LoanTransactions");
        modelBuilder.Entity<JewelRelease>().ToTable("JewelReleases");
        modelBuilder.Entity<Auction>().ToTable("Auctions");
        modelBuilder.Entity<AuditLog>().ToTable("AuditLogs");

        // Decimal precision (avoid EF Core warnings / silent truncation)
        modelBuilder.Entity<JewelType>().Property(p => p.WastagePercent).HasColumnType("decimal(5,2)");
        modelBuilder.Entity<GoldRate>().Property(p => p.Rate24K).HasColumnType("decimal(10,2)");
        modelBuilder.Entity<GoldRate>().Property(p => p.Rate22K).HasColumnType("decimal(10,2)");
        modelBuilder.Entity<GoldRate>().Property(p => p.Rate18K).HasColumnType("decimal(10,2)");

        modelBuilder.Entity<LoanScheme>().Property(p => p.InterestRatePct).HasColumnType("decimal(5,2)");
        modelBuilder.Entity<LoanScheme>().Property(p => p.MaxLtvPercent).HasColumnType("decimal(5,2)");
        modelBuilder.Entity<LoanScheme>().Property(p => p.ProcessingFee).HasColumnType("decimal(10,2)");
        modelBuilder.Entity<LoanScheme>().Property(p => p.PenaltyRatePerDay).HasColumnType("decimal(5,2)");

        modelBuilder.Entity<Loan>().Property(p => p.InterestRatePct).HasColumnType("decimal(5,2)");
        modelBuilder.Entity<Loan>().Property(p => p.MarketValue).HasColumnType("decimal(12,2)");
        modelBuilder.Entity<Loan>().Property(p => p.EligibleAmount).HasColumnType("decimal(12,2)");
        modelBuilder.Entity<Loan>().Property(p => p.LoanAmount).HasColumnType("decimal(12,2)");
        modelBuilder.Entity<Loan>().Property(p => p.ProcessingFee).HasColumnType("decimal(10,2)");
        modelBuilder.Entity<Loan>().Property(p => p.OutstandingPrincipal).HasColumnType("decimal(12,2)");
        modelBuilder.Entity<Loan>().Property(p => p.OutstandingInterest).HasColumnType("decimal(12,2)");
        modelBuilder.Entity<Loan>().Property(p => p.PenaltyAccrued).HasColumnType("decimal(12,2)");

        modelBuilder.Entity<JewelItem>().Property(p => p.GrossWeightGrams).HasColumnType("decimal(8,3)");
        modelBuilder.Entity<JewelItem>().Property(p => p.StoneWeightGrams).HasColumnType("decimal(8,3)");
        modelBuilder.Entity<JewelItem>().Property(p => p.NetWeightGrams).HasColumnType("decimal(8,3)");
        modelBuilder.Entity<JewelItem>().Property(p => p.MarketValue).HasColumnType("decimal(12,2)");

        modelBuilder.Entity<LoanTransaction>().Property(p => p.PrincipalAmount).HasColumnType("decimal(12,2)");
        modelBuilder.Entity<LoanTransaction>().Property(p => p.InterestAmount).HasColumnType("decimal(12,2)");
        modelBuilder.Entity<LoanTransaction>().Property(p => p.PenaltyAmount).HasColumnType("decimal(12,2)");
        modelBuilder.Entity<LoanTransaction>().Property(p => p.ChargesAmount).HasColumnType("decimal(12,2)");
        modelBuilder.Entity<LoanTransaction>().Property(p => p.TotalAmount).HasColumnType("decimal(12,2)");
        modelBuilder.Entity<LoanTransaction>().Property(p => p.BalancePrincipalAfter).HasColumnType("decimal(12,2)");

        modelBuilder.Entity<Auction>().Property(p => p.RecoveredAmount).HasColumnType("decimal(12,2)");

        // Relationships (mirrors FKs in the SQL script)
        modelBuilder.Entity<Loan>()
            .HasOne(l => l.Customer)
            .WithMany(c => c.Loans)
            .HasForeignKey(l => l.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Loan>()
            .HasOne(l => l.LoanScheme)
            .WithMany()
            .HasForeignKey(l => l.LoanSchemeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<JewelItem>()
            .HasOne(ji => ji.JewelType)
            .WithMany()
            .HasForeignKey(ji => ji.JewelTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<JewelItem>()
            .HasOne<Loan>()
            .WithMany(l => l.JewelItems)
            .HasForeignKey(ji => ji.LoanId)
            .OnDelete(DeleteBehavior.Cascade);

        //modelBuilder.Entity<LoanTransaction>()
        //    .HasOne<Loan>()
        //    .WithMany(l => l.Transactions)
        //    .HasForeignKey(t => t.LoanId)
        //    .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<LoanTransaction>()
.HasOne(t => t.Loan)
.WithMany(l => l.Transactions)
.HasForeignKey(t => t.LoanId)
.OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<User>()
            .HasOne(u => u.Role)
            .WithMany()
            .HasForeignKey(u => u.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<User>()
            .HasOne(u => u.Branch)
            .WithMany()
            .HasForeignKey(u => u.BranchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
