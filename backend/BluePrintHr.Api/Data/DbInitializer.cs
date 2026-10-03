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

        if (db.Database.IsRelational())
        {
            if (configuration.GetValue<bool>("Database:ApplyMigrations"))
                await db.Database.MigrateAsync();
        }
        else
        {
            await db.Database.EnsureCreatedAsync();
        }

        if (!configuration.GetValue<bool>("Database:SeedDemoData")) return;

        // Optional one-time production bootstrap: replace the password of the
        // configured admin account. This is deliberately server-side and only
        // runs when explicitly enabled through configuration.
        if (configuration.GetValue<bool>("Bootstrap:ResetAdminPassword"))
        {
            var bootstrapEmail = configuration["Seed:AdminEmail"]?.Trim().ToLowerInvariant();
            var bootstrapPassword = configuration["Seed:AdminPassword"];

            if (string.IsNullOrWhiteSpace(bootstrapEmail) || string.IsNullOrWhiteSpace(bootstrapPassword))
                throw new InvalidOperationException("Seed:AdminEmail and Seed:AdminPassword are required when Bootstrap:ResetAdminPassword is enabled.");

            var bootstrapUser = await db.Users.SingleOrDefaultAsync(x => x.Email.ToLower() == bootstrapEmail);
            if (bootstrapUser is null)
                throw new InvalidOperationException($"Bootstrap admin user '{bootstrapEmail}' was not found.");

            if (bootstrapUser.Role is not UserRole.SuperAdmin and not UserRole.CompanyAdmin)
                throw new InvalidOperationException("Bootstrap password reset is only permitted for an administrator account.");

            bootstrapUser.PasswordHash = passwordService.Hash(bootstrapPassword);
            await db.SaveChangesAsync();
        }

        // The first bootstrap attempt can fail after creating the tenant but before
        // creating users. Recover that partial seed on the next startup so the
        // administrator bootstrap can complete cleanly.
        if (!await db.Users.AnyAsync())
        {
            var partialTenant = await db.Tenants.SingleOrDefaultAsync(x =>
                x.Subdomain == "blueprint" && x.CompanyName == "BluePrint Kenya Ltd");

            if (partialTenant is not null)
            {
                await db.LeaveBalances.Where(x => x.TenantId == partialTenant.Id).ExecuteDeleteAsync();
                await db.LeaveRequests.Where(x => x.TenantId == partialTenant.Id).ExecuteDeleteAsync();
                await db.PayrollTransactions.Where(x => x.TenantId == partialTenant.Id).ExecuteDeleteAsync();
                await db.Employees.Where(x => x.TenantId == partialTenant.Id).ExecuteDeleteAsync();
                await db.PayrollPeriods.Where(x => x.TenantId == partialTenant.Id).ExecuteDeleteAsync();
                await db.LeaveTypes.Where(x => x.TenantId == partialTenant.Id).ExecuteDeleteAsync();
                await db.Departments.Where(x => x.TenantId == partialTenant.Id).ExecuteDeleteAsync();
                await db.Branches.Where(x => x.TenantId == partialTenant.Id).ExecuteDeleteAsync();
                await db.Designations.Where(x => x.TenantId == partialTenant.Id).ExecuteDeleteAsync();
                await db.Grades.Where(x => x.TenantId == partialTenant.Id).ExecuteDeleteAsync();
                await db.EmploymentTypes.Where(x => x.TenantId == partialTenant.Id).ExecuteDeleteAsync();
                await db.AuditLogs.Where(x => x.TenantId == partialTenant.Id).ExecuteDeleteAsync();
                await db.Tenants.Where(x => x.Id == partialTenant.Id).ExecuteDeleteAsync();
            }
        }

        if (await db.Users.AnyAsync()) return;
        if (await db.Tenants.AnyAsync()) return;

        var adminEmail = configuration["Seed:AdminEmail"] ?? "admin@blueprinthr.co.ke";
        var adminPassword = configuration["Seed:AdminPassword"];
        var employeePassword = configuration["Seed:EmployeePassword"];
        if (string.IsNullOrWhiteSpace(adminPassword) || string.IsNullOrWhiteSpace(employeePassword))
            throw new InvalidOperationException("Seed passwords are required when Database:SeedDemoData is enabled.");

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
            EmploymentDate = new DateTime(2023, 1, 9, 0, 0, 0, DateTimeKind.Utc),
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
                Email = adminEmail,
                PasswordHash = passwordService.Hash(adminPassword),
                Role = UserRole.CompanyAdmin
            },
            new User
            {
                TenantId = tenant.Id,
                Name = "Amina Njoroge",
                Email = employee.Email!,
                PasswordHash = passwordService.Hash(employeePassword),
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
