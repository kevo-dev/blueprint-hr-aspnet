using BluePrintHr.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BluePrintHr.Api.Data;

public class BluePrintHrDbContext(DbContextOptions<BluePrintHrDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Designation> Designations => Set<Designation>();
    public DbSet<Grade> Grades => Set<Grade>();
    public DbSet<EmploymentType> EmploymentTypes => Set<EmploymentType>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<PayrollPeriod> PayrollPeriods => Set<PayrollPeriod>();
    public DbSet<PayrollTransaction> PayrollTransactions => Set<PayrollTransaction>();
    public DbSet<LeaveType> LeaveTypes => Set<LeaveType>();
    public DbSet<LeaveBalance> LeaveBalances => Set<LeaveBalance>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<EmployeePayrollComponent> EmployeePayrollComponents => Set<EmployeePayrollComponent>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<ReportDefinition> ReportDefinitions => Set<ReportDefinition>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<PayrollTaxTable> PayrollTaxTables => Set<PayrollTaxTable>();
    public DbSet<PayrollTaxBand> PayrollTaxBands => Set<PayrollTaxBand>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // The Supabase schema uses plural snake_case table names.
        // EF Core does not pluralize table names by default, so map them explicitly.
        modelBuilder.Entity<User>().ToTable("users");
        modelBuilder.Entity<Tenant>().ToTable("tenants");
        modelBuilder.Entity<Branch>().ToTable("branches");
        modelBuilder.Entity<Department>().ToTable("departments");
        modelBuilder.Entity<Designation>().ToTable("designations");
        modelBuilder.Entity<Grade>().ToTable("grades");
        modelBuilder.Entity<EmploymentType>().ToTable("employment_types");
        modelBuilder.Entity<Employee>().ToTable("employees");
        modelBuilder.Entity<PayrollPeriod>().ToTable("payroll_periods");
        modelBuilder.Entity<PayrollTransaction>().ToTable("payroll_transactions");
        modelBuilder.Entity<LeaveType>().ToTable("leave_types");
        modelBuilder.Entity<LeaveBalance>().ToTable("leave_balances");
        modelBuilder.Entity<LeaveRequest>().ToTable("leave_requests");
        modelBuilder.Entity<AuditLog>().ToTable("audit_logs");
        modelBuilder.Entity<ReportDefinition>().ToTable("report_definitions");
        modelBuilder.Entity<AttendanceRecord>().ToTable("attendance_records");

        modelBuilder.Entity<PayrollTaxTable>(entity =>
        {
            entity.ToTable("payroll_tax_tables");
            entity.Property(x => x.TaxType).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.Rate).HasPrecision(9, 6);
            entity.Property(x => x.EmployerRate).HasPrecision(9, 6);
            entity.Property(x => x.PersonalRelief).HasPrecision(18, 2);
            entity.Property(x => x.MinimumAmount).HasPrecision(18, 2);
            entity.Property(x => x.MaximumAmount).HasPrecision(18, 2);
            entity.HasIndex(x => new { x.TenantId, x.TaxType, x.EffectiveFrom });
            entity.HasMany(x => x.Bands).WithOne(x => x.PayrollTaxTable).HasForeignKey(x => x.PayrollTaxTableId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<PayrollTaxBand>(entity =>
        {
            entity.ToTable("payroll_tax_bands");
            entity.Property(x => x.LowerBound).HasPrecision(18, 2);
            entity.Property(x => x.UpperBound).HasPrecision(18, 2);
            entity.Property(x => x.Rate).HasPrecision(9, 6);
            entity.HasIndex(x => new { x.PayrollTaxTableId, x.SortOrder }).IsUnique();
        });

        modelBuilder.Entity<PasswordResetToken>(entity =>
        {
            entity.ToTable("password_reset_tokens");
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => new { x.UserId, x.ExpiresAt });
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(x => x.Role).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(x => new { x.TenantId, x.Email }).IsUnique();
            entity.HasOne(x => x.Tenant).WithMany(x => x.Users).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            entity.HasIndex(x => x.Subdomain).IsUnique();
        });

        modelBuilder.Entity<Branch>().HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
        modelBuilder.Entity<Department>().HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
        modelBuilder.Entity<Designation>().HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
        modelBuilder.Entity<EmploymentType>().HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
        modelBuilder.Entity<Employee>().HasIndex(x => new { x.TenantId, x.EmployeeNo }).IsUnique();
        modelBuilder.Entity<Employee>().Property(x => x.BasicSalary).HasPrecision(18, 2);
        modelBuilder.Entity<Grade>().Property(x => x.MinSalary).HasPrecision(18, 2);
        modelBuilder.Entity<Grade>().Property(x => x.MaxSalary).HasPrecision(18, 2);

        modelBuilder.Entity<PayrollPeriod>(entity =>
        {
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            entity.HasIndex(x => new { x.TenantId, x.Year, x.Month }).IsUnique();
        });

        modelBuilder.Entity<PayrollTransaction>(entity =>
        {
            entity.Property(x => x.BasicSalary).HasPrecision(18, 2);
            entity.Property(x => x.Allowances).HasPrecision(18, 2);
            entity.Property(x => x.GrossPay).HasPrecision(18, 2);
            entity.Property(x => x.TaxablePay).HasPrecision(18, 2);
            entity.Property(x => x.Paye).HasPrecision(18, 2);
            entity.Property(x => x.PersonalRelief).HasPrecision(18, 2);
            entity.Property(x => x.Nssf).HasPrecision(18, 2);
            entity.Property(x => x.Shif).HasPrecision(18, 2);
            entity.Property(x => x.HousingLevy).HasPrecision(18, 2);
            entity.Property(x => x.OtherDeductions).HasPrecision(18, 2);
            entity.Property(x => x.TotalDeductions).HasPrecision(18, 2);
            entity.Property(x => x.NetPay).HasPrecision(18, 2);
            entity.HasIndex(x => new { x.TenantId, x.PayrollPeriodId, x.EmployeeId }).IsUnique();
            entity.HasOne(x => x.PayrollPeriod).WithMany(x => x.Transactions).HasForeignKey(x => x.PayrollPeriodId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LeaveType>().HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
        modelBuilder.Entity<LeaveType>().Property(x => x.Paid).HasDefaultValue(true);
        modelBuilder.Entity<LeaveBalance>(entity =>
        {
            entity.Ignore(x => x.AvailableDays);
            entity.Property(x => x.AllocatedDays).HasPrecision(9, 2);
            entity.Property(x => x.UsedDays).HasPrecision(9, 2);
            entity.HasIndex(x => new { x.TenantId, x.EmployeeId, x.LeaveTypeId, x.Year }).IsUnique();
        });
        modelBuilder.Entity<LeaveRequest>(entity =>
        {
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            entity.Property(x => x.DaysRequested).HasPrecision(9, 2);
        });

        modelBuilder.Entity<EmployeePayrollComponent>(entity =>
        {
            entity.ToTable("employee_payroll_components");
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.ComponentType).HasMaxLength(32);
            entity.HasIndex(x => new { x.TenantId, x.EmployeeId, x.Active });
            entity.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("notifications");
            entity.HasIndex(x => new { x.TenantId, x.UserId, x.IsRead, x.CreatedAt });
        });

        modelBuilder.Entity<AuditLog>().HasIndex(x => new { x.TenantId, x.CreatedAt });
        modelBuilder.Entity<ReportDefinition>().HasIndex(x => x.Name).IsUnique();
        modelBuilder.Entity<AttendanceRecord>(entity =>
        {
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            entity.Property(x => x.HoursWorked).HasPrecision(5, 2);
            entity.HasIndex(x => new { x.TenantId, x.EmployeeId, x.AttendanceDate }).IsUnique();
        });
    }
}
