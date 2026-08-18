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
    public DbSet<ReportDefinition> ReportDefinitions => Set<ReportDefinition>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

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
            entity.HasIndex(x => x.Subdomain).IsUnique().HasFilter("[Subdomain] IS NOT NULL");
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

        modelBuilder.Entity<AuditLog>().HasIndex(x => new { x.TenantId, x.CreatedAt });
        modelBuilder.Entity<ReportDefinition>().HasIndex(x => x.Name).IsUnique();
    }
}
