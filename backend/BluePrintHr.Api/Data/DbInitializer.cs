using BluePrintHr.Api.Models;
using BluePrintHr.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace BluePrintHr.Api.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, IConfiguration configuration)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BluePrintHrDbContext>();
        var passwordService = scope.ServiceProvider.GetRequiredService<IPasswordService>();

        if (db.Database.IsRelational() && configuration.GetValue<bool>("Database:ApplyMigrations"))
            await db.Database.MigrateAsync();
        else
            await db.Database.EnsureCreatedAsync();

        if (await db.Tenants.AnyAsync()) return;

        var tenant = new Tenant
        {
            CompanyName = "BluePrint Kenya Ltd",
            KraPin = "P051234567X",
            Email = "hr@blueprinthr.co.ke",
            Phone = "+254 712 345 678",
            Address = "Delta Towers, Westlands, Nairobi",
            Subdomain = "blueprint"
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var branch = new Branch { TenantId = tenant.Id, Name = "Nairobi HQ", Code = "NBO", Location = "Westlands" };
        var department = new Department { TenantId = tenant.Id, Name = "People & Culture", Code = "HR", BranchId = branch.Id };
        db.Branches.Add(branch);
        await db.SaveChangesAsync();
        department.BranchId = branch.Id;
        db.Departments.Add(department);

        var designation = new Designation { TenantId = tenant.Id, Name = "HR Manager" };
        var employmentType = new EmploymentType { TenantId = tenant.Id, Name = "Permanent", Description = "Permanent full-time employment" };
        db.Designations.Add(designation);
        db.EmploymentTypes.Add(employmentType);
        await db.SaveChangesAsync();

        var employee = new Employee
        {
            TenantId = tenant.Id,
            EmployeeNo = "EMP-001",
            PayrollNo = "PAY-001",
            FirstName = "Amina",
            LastName = "Njoroge",
            Gender = "Female",
            KraPin = "A012345678B",
            NssfNo = "NSSF-001",
            ShifNo = "SHIF-001",
            Phone = "+254 700 000 001",
            Email = "amina.njoroge@blueprinthr.co.ke",
            BranchId = branch.Id,
            DepartmentId = department.Id,
            DesignationId = designation.Id,
            EmploymentTypeId = employmentType.Id,
            EmploymentDate = new DateTime(2023, 1, 9),
            BasicSalary = 85_000m,
            BankName = "KCB Bank",
            BankBranch = "Westlands",
            AccountNumber = "0001234567"
        };
        db.Employees.Add(employee);
        await db.SaveChangesAsync();

        db.Users.AddRange(
            new User
            {
                TenantId = tenant.Id,
                Name = "BluePrint Administrator",
                Email = "admin@blueprinthr.co.ke",
                PasswordHash = passwordService.Hash("BluePrint!2026"),
                Role = UserRole.CompanyAdmin
            },
            new User
            {
                TenantId = tenant.Id,
                Name = "Amina Njoroge",
                Email = employee.Email!,
                PasswordHash = passwordService.Hash("Employee!2026"),
                Role = UserRole.Employee,
                EmployeeId = employee.Id
            });

        db.LeaveTypes.AddRange(
            new LeaveType { TenantId = tenant.Id, Name = "Annual Leave", DefaultDays = 21, Paid = true },
            new LeaveType { TenantId = tenant.Id, Name = "Sick Leave", DefaultDays = 14, Paid = true },
            new LeaveType { TenantId = tenant.Id, Name = "Compassionate Leave", DefaultDays = 5, Paid = true },
            new LeaveType { TenantId = tenant.Id, Name = "Study Leave", DefaultDays = 10, Paid = false });

        db.PayrollPeriods.Add(new PayrollPeriod
        {
            TenantId = tenant.Id,
            Name = "August 2026",
            Month = 8,
            Year = 2026,
            Status = PayrollStatus.Open
        });

        db.ReportDefinitions.AddRange(
            new ReportDefinition { Name = "Payroll Summary", Description = "Monthly gross pay, statutory deductions, and net pay by employee.", ReportPath = "/BluePrintHR/PayrollSummary" },
            new ReportDefinition { Name = "Employee Roster", Description = "Tenant-scoped active employee roster with statutory identifiers.", ReportPath = "/BluePrintHR/EmployeeRoster" });

        await db.SaveChangesAsync();

        var annual = await db.LeaveTypes.SingleAsync(x => x.TenantId == tenant.Id && x.Name == "Annual Leave");
        db.LeaveBalances.Add(new LeaveBalance
        {
            TenantId = tenant.Id,
            EmployeeId = employee.Id,
            LeaveTypeId = annual.Id,
            Year = 2026,
            AllocatedDays = annual.DefaultDays,
            UsedDays = 3
        });
        await db.SaveChangesAsync();
    }
}
