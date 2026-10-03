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

        if (await db.Users.AnyAsync())
        {
            await SeedPortalDemoDataAsync(db);
            return;
        }

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

        await SeedPortalDemoDataAsync(db);
    }

    private static async Task SeedPortalDemoDataAsync(BluePrintHrDbContext db)
    {
        var tenant = await db.Tenants.OrderBy(x => x.Id).FirstOrDefaultAsync();
        var admin = await db.Users.OrderBy(x => x.Id).FirstOrDefaultAsync(x => x.Role == UserRole.CompanyAdmin || x.Role == UserRole.SuperAdmin);
        var employee = await db.Employees.OrderBy(x => x.Id).FirstOrDefaultAsync(x => x.EmployeeNo == "EMP-001");
        if (tenant is null || admin is null || employee is null) return;

        var branch = await db.Branches.FirstOrDefaultAsync(x => x.TenantId == tenant.Id && x.Code == "NBO");
        var department = await db.Departments.FirstOrDefaultAsync(x => x.TenantId == tenant.Id && x.Code == "HR");
        var designation = await db.Designations.FirstOrDefaultAsync(x => x.TenantId == tenant.Id && x.Name == "HR Manager");
        var employmentType = await db.EmploymentTypes.FirstOrDefaultAsync(x => x.TenantId == tenant.Id && x.Name == "Permanent");

        if (branch is null || department is null || designation is null || employmentType is null) return;

        var demoEmployees = new[]
        {
            new Employee
            {
                TenantId = tenant.Id, EmployeeNo = "EMP-002", PayrollNo = "PAY-002",
                FirstName = "Brian", LastName = "Otieno", Gender = "Male",
                KraPin = "A112233445C", NssfNo = "NSSF-002", ShifNo = "SHIF-002",
                Phone = "+254 700 000 002", Email = "brian.otieno@blueprinthr.co.ke",
                BranchId = branch.Id, DepartmentId = department.Id, DesignationId = designation.Id,
                EmploymentTypeId = employmentType.Id, EmploymentDate = new DateTime(2022, 5, 16, 0, 0, 0, DateTimeKind.Utc),
                BasicSalary = 120_000m, BankName = "Equity Bank", BankBranch = "Westlands", AccountNumber = "0002234567"
            },
            new Employee
            {
                TenantId = tenant.Id, EmployeeNo = "EMP-003", PayrollNo = "PAY-003",
                FirstName = "Grace", LastName = "Wambui", Gender = "Female",
                KraPin = "A223344556D", NssfNo = "NSSF-003", ShifNo = "SHIF-003",
                Phone = "+254 700 000 003", Email = "grace.wambui@blueprinthr.co.ke",
                BranchId = branch.Id, DepartmentId = department.Id, DesignationId = designation.Id,
                EmploymentTypeId = employmentType.Id, EmploymentDate = new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc),
                BasicSalary = 68_000m, BankName = "Co-operative Bank", BankBranch = "Westlands", AccountNumber = "0003234567"
            },
            new Employee
            {
                TenantId = tenant.Id, EmployeeNo = "EMP-004", PayrollNo = "PAY-004",
                FirstName = "David", LastName = "Kamau", Gender = "Male",
                KraPin = "A334455667E", NssfNo = "NSSF-004", ShifNo = "SHIF-004",
                Phone = "+254 700 000 004", Email = "david.kamau@blueprinthr.co.ke",
                BranchId = branch.Id, DepartmentId = department.Id, DesignationId = designation.Id,
                EmploymentTypeId = employmentType.Id, EmploymentDate = new DateTime(2021, 9, 20, 0, 0, 0, DateTimeKind.Utc),
                BasicSalary = 150_000m, BankName = "KCB Bank", BankBranch = "Westlands", AccountNumber = "0004234567"
            },
            new Employee
            {
                TenantId = tenant.Id, EmployeeNo = "EMP-005", PayrollNo = "PAY-005",
                FirstName = "Faith", LastName = "Achieng", Gender = "Female",
                KraPin = "A445566778F", NssfNo = "NSSF-005", ShifNo = "SHIF-005",
                Phone = "+254 700 000 005", Email = "faith.achieng@blueprinthr.co.ke",
                BranchId = branch.Id, DepartmentId = department.Id, DesignationId = designation.Id,
                EmploymentTypeId = employmentType.Id, EmploymentDate = new DateTime(2025, 3, 10, 0, 0, 0, DateTimeKind.Utc),
                BasicSalary = 58_000m, BankName = "NCBA Bank", BankBranch = "Westlands", AccountNumber = "0005234567"
            }
        };

        foreach (var candidate in demoEmployees)
        {
            if (!await db.Employees.AnyAsync(x => x.TenantId == tenant.Id && x.EmployeeNo == candidate.EmployeeNo))
                db.Employees.Add(candidate);
        }
        await db.SaveChangesAsync();

        var employees = await db.Employees.Where(x => x.TenantId == tenant.Id).OrderBy(x => x.Id).ToListAsync();
        var leaveTypes = await db.LeaveTypes.Where(x => x.TenantId == tenant.Id).OrderBy(x => x.Id).ToListAsync();

        foreach (var emp in employees)
        {
            foreach (var leaveType in leaveTypes)
            {
                if (!await db.LeaveBalances.AnyAsync(x => x.TenantId == tenant.Id && x.EmployeeId == emp.Id && x.LeaveTypeId == leaveType.Id && x.Year == 2026))
                {
                    var used = emp.EmployeeNo switch
                    {
                        "EMP-001" when leaveType.Name == "Annual Leave" => 3m,
                        "EMP-002" when leaveType.Name == "Annual Leave" => 7m,
                        "EMP-003" when leaveType.Name == "Sick Leave" => 2m,
                        "EMP-004" when leaveType.Name == "Annual Leave" => 4m,
                        "EMP-005" when leaveType.Name == "Annual Leave" => 1m,
                        _ => 0m
                    };

                    db.LeaveBalances.Add(new LeaveBalance
                    {
                        TenantId = tenant.Id,
                        EmployeeId = emp.Id,
                        LeaveTypeId = leaveType.Id,
                        Year = 2026,
                        AllocatedDays = leaveType.DefaultDays,
                        UsedDays = used
                    });
                }
            }
        }
        await db.SaveChangesAsync();

        var annual = leaveTypes.FirstOrDefault(x => x.Name == "Annual Leave");
        var sick = leaveTypes.FirstOrDefault(x => x.Name == "Sick Leave");
        if (annual is not null && sick is not null)
        {
            await AddLeaveRequestIfMissing(db, tenant.Id, employees, admin, "EMP-001", annual, new DateTime(2026, 7, 6), new DateTime(2026, 7, 8), 3, "Family holiday", LeaveRequestStatus.Approved, new DateTime(2026, 6, 20));
            await AddLeaveRequestIfMissing(db, tenant.Id, employees, admin, "EMP-002", annual, new DateTime(2026, 8, 17), new DateTime(2026, 8, 21), 5, "Annual vacation", LeaveRequestStatus.Approved, new DateTime(2026, 8, 1));
            await AddLeaveRequestIfMissing(db, tenant.Id, employees, admin, "EMP-003", sick, new DateTime(2026, 9, 14), new DateTime(2026, 9, 15), 2, "Medical appointment", LeaveRequestStatus.Rejected, new DateTime(2026, 9, 10));
            await AddLeaveRequestIfMissing(db, tenant.Id, employees, admin, "EMP-004", annual, new DateTime(2026, 10, 12), new DateTime(2026, 10, 16), 5, "Planned annual leave", LeaveRequestStatus.Pending, null);
            await AddLeaveRequestIfMissing(db, tenant.Id, employees, admin, "EMP-005", annual, new DateTime(2026, 11, 2), new DateTime(2026, 11, 4), 3, "Personal travel", LeaveRequestStatus.Pending, null);
        }

        var periods = await db.PayrollPeriods.Where(x => x.TenantId == tenant.Id).OrderBy(x => x.Year).ThenBy(x => x.Month).ToListAsync();
        var periodSeeds = new[]
        {
            (Month: 7, Year: 2026, Name: "July 2026", Status: PayrollStatus.Locked, ProcessedAt: new DateTime(2026, 7, 31, 17, 0, 0, DateTimeKind.Utc)),
            (Month: 9, Year: 2026, Name: "September 2026", Status: PayrollStatus.Approved, ProcessedAt: new DateTime(2026, 9, 30, 17, 0, 0, DateTimeKind.Utc))
        };

        foreach (var seed in periodSeeds)
        {
            if (!periods.Any(x => x.Month == seed.Month && x.Year == seed.Year))
            {
                db.PayrollPeriods.Add(new PayrollPeriod
                {
                    TenantId = tenant.Id,
                    Name = seed.Name,
                    Month = seed.Month,
                    Year = seed.Year,
                    Status = seed.Status,
                    ProcessedAt = seed.ProcessedAt
                });
            }
        }
        await db.SaveChangesAsync();

        periods = await db.PayrollPeriods.Where(x => x.TenantId == tenant.Id).OrderBy(x => x.Year).ThenBy(x => x.Month).ToListAsync();
        foreach (var period in periods.Where(x => x.Year == 2026))
        {
            foreach (var emp in employees)
            {
                if (await db.PayrollTransactions.AnyAsync(x => x.TenantId == tenant.Id && x.PayrollPeriodId == period.Id && x.EmployeeId == emp.Id))
                    continue;

                var basic = emp.BasicSalary;
                var allowances = emp.EmployeeNo switch
                {
                    "EMP-001" => 10_000m,
                    "EMP-002" => 15_000m,
                    "EMP-003" => 8_000m,
                    "EMP-004" => 20_000m,
                    _ => 6_000m
                };
                var gross = basic + allowances;
                var nssf = Math.Min(gross * 0.06m, 10_800m);
                var shif = gross * 0.0275m;
                var housing = gross * 0.015m;
                var paye = Math.Max(gross * 0.18m - 2_400m, 0m);
                var relief = 2_400m;
                var total = paye + nssf + shif + housing;
                var net = gross - total;

                db.PayrollTransactions.Add(new PayrollTransaction
                {
                    TenantId = tenant.Id,
                    PayrollPeriodId = period.Id,
                    EmployeeId = emp.Id,
                    BasicSalary = basic,
                    Allowances = allowances,
                    GrossPay = gross,
                    TaxablePay = gross,
                    Paye = paye,
                    PersonalRelief = relief,
                    Nssf = nssf,
                    Shif = shif,
                    HousingLevy = housing,
                    OtherDeductions = 0m,
                    TotalDeductions = total,
                    NetPay = net,
                    Status = period.Status == PayrollStatus.Locked ? "Final" : period.Status == PayrollStatus.Approved ? "Approved" : "Draft"
                });
            }
        }
        await db.SaveChangesAsync();

        // Seed a small attendance history so the attendance dashboard is useful immediately.
        var attendanceStart = new DateTime(2026, 9, 1);
        for (var day = attendanceStart; day <= new DateTime(2026, 10, 2); day = day.AddDays(1))
        {
            if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
            foreach (var emp in employees)
            {
                if (await db.AttendanceRecords.AnyAsync(x => x.TenantId == tenant.Id && x.EmployeeId == emp.Id && x.AttendanceDate == day)) continue;
                var late = emp.EmployeeNo == "EMP-003" && day.Day % 5 == 0;
                var absent = emp.EmployeeNo == "EMP-005" && day.Day == 18;
                var onLeave = emp.EmployeeNo == "EMP-002" && day.Day is 17 or 18 or 19 or 20 or 21;
                var status = absent ? AttendanceStatus.Absent : onLeave ? AttendanceStatus.Leave : late ? AttendanceStatus.Late : AttendanceStatus.Present;
                DateTime? checkIn = status is AttendanceStatus.Absent or AttendanceStatus.Leave ? null : day.AddHours(late ? 9.25 : 8.5);
                DateTime? checkOut = status is AttendanceStatus.Absent or AttendanceStatus.Leave ? null : day.AddHours(17.5);
                db.AttendanceRecords.Add(new AttendanceRecord
                {
                    TenantId = tenant.Id, EmployeeId = emp.Id, AttendanceDate = day,
                    CheckIn = checkIn, CheckOut = checkOut, Status = status,
                    HoursWorked = checkIn.HasValue && checkOut.HasValue ? (decimal)(checkOut.Value - checkIn.Value).TotalHours : 0,
                    Notes = absent ? "Reported absent" : onLeave ? "Approved annual leave" : late ? "Late arrival" : null
                });
            }
        }
        await db.SaveChangesAsync();

        var reports = new[]
        {
            ("Payroll Summary", "Monthly gross pay, statutory deductions, and net pay by employee.", "/BluePrintHR/PayrollSummary"),
            ("Employee Roster", "Tenant-scoped active employee roster with statutory identifiers.", "/BluePrintHR/EmployeeRoster"),
            ("Leave Utilization", "Annual leave allocation, usage, and available balances by employee.", "/BluePrintHR/LeaveUtilization"),
            ("Leave Requests", "Leave requests grouped by employee, leave type, date, and approval status.", "/BluePrintHR/LeaveRequests"),
            ("Department Headcount", "Employee headcount grouped by department and employment status.", "/BluePrintHR/DepartmentHeadcount")
        };

        foreach (var report in reports)
        {
            if (!await db.ReportDefinitions.AnyAsync(x => x.Name == report.Item1))
            {
                db.ReportDefinitions.Add(new ReportDefinition
                {
                    Name = report.Item1,
                    Description = report.Item2,
                    ReportPath = report.Item3,
                    Enabled = true
                });
            }
        }
        await db.SaveChangesAsync();

        var existingAuditCount = await db.AuditLogs.CountAsync(x => x.TenantId == tenant.Id);
        if (existingAuditCount < 10)
        {
            var now = DateTime.UtcNow;
            db.AuditLogs.AddRange(
                new AuditLog { TenantId = tenant.Id, UserId = admin.Id, UserName = admin.Name, Action = "Login", EntityType = "Authentication", Details = "Administrator signed in successfully.", IpAddress = "102.68.10.21", CreatedAt = now.AddMinutes(-25) },
                new AuditLog { TenantId = tenant.Id, UserId = admin.Id, UserName = admin.Name, Action = "Create", EntityType = "Employee", EntityId = employees.First(x => x.EmployeeNo == "EMP-002").Id, Details = "Created employee Brian Otieno.", IpAddress = "102.68.10.21", CreatedAt = now.AddMinutes(-22) },
                new AuditLog { TenantId = tenant.Id, UserId = admin.Id, UserName = admin.Name, Action = "Create", EntityType = "Employee", EntityId = employees.First(x => x.EmployeeNo == "EMP-003").Id, Details = "Created employee Grace Wambui.", IpAddress = "102.68.10.21", CreatedAt = now.AddMinutes(-20) },
                new AuditLog { TenantId = tenant.Id, UserId = admin.Id, UserName = admin.Name, Action = "Create", EntityType = "Employee", EntityId = employees.First(x => x.EmployeeNo == "EMP-004").Id, Details = "Created employee David Kamau.", IpAddress = "102.68.10.21", CreatedAt = now.AddMinutes(-18) },
                new AuditLog { TenantId = tenant.Id, UserId = admin.Id, UserName = admin.Name, Action = "Approve", EntityType = "LeaveRequest", Details = "Approved Amina Njoroge annual leave request for 3 days.", IpAddress = "102.68.10.21", CreatedAt = now.AddMinutes(-15) },
                new AuditLog { TenantId = tenant.Id, UserId = admin.Id, UserName = admin.Name, Action = "Approve", EntityType = "LeaveRequest", Details = "Approved Brian Otieno annual leave request for 5 days.", IpAddress = "102.68.10.21", CreatedAt = now.AddMinutes(-13) },
                new AuditLog { TenantId = tenant.Id, UserId = admin.Id, UserName = admin.Name, Action = "Reject", EntityType = "LeaveRequest", Details = "Rejected Grace Wambui sick leave request after review.", IpAddress = "102.68.10.21", CreatedAt = now.AddMinutes(-10) },
                new AuditLog { TenantId = tenant.Id, UserId = admin.Id, UserName = admin.Name, Action = "Process", EntityType = "Payroll", Details = "September 2026 payroll processed for 5 employees.", IpAddress = "102.68.10.21", CreatedAt = now.AddMinutes(-7) },
                new AuditLog { TenantId = tenant.Id, UserId = admin.Id, UserName = admin.Name, Action = "View", EntityType = "Report", Details = "Viewed Payroll Summary report.", IpAddress = "102.68.10.21", CreatedAt = now.AddMinutes(-4) },
                new AuditLog { TenantId = tenant.Id, UserId = admin.Id, UserName = admin.Name, Action = "View", EntityType = "Report", Details = "Viewed Leave Utilization report.", IpAddress = "102.68.10.21", CreatedAt = now.AddMinutes(-2) },
                new AuditLog { TenantId = tenant.Id, UserId = admin.Id, UserName = admin.Name, Action = "Update", EntityType = "Employee", EntityId = employee.Id, Details = "Updated Amina Njoroge contact information.", IpAddress = "102.68.10.21", CreatedAt = now.AddMinutes(-1) }
            );
            await db.SaveChangesAsync();
        }
    }

    private static async Task AddLeaveRequestIfMissing(
        BluePrintHrDbContext db,
        int tenantId,
        IReadOnlyCollection<Employee> employees,
        User admin,
        string employeeNo,
        LeaveType leaveType,
        DateTime start,
        DateTime end,
        decimal days,
        string reason,
        LeaveRequestStatus status,
        DateTime? reviewedAt)
    {
        var employee = employees.FirstOrDefault(x => x.EmployeeNo == employeeNo);
        if (employee is null) return;

        var exists = await db.LeaveRequests.AnyAsync(x =>
            x.TenantId == tenantId &&
            x.EmployeeId == employee.Id &&
            x.LeaveTypeId == leaveType.Id &&
            x.StartDate == start &&
            x.EndDate == end);

        if (exists) return;

        db.LeaveRequests.Add(new LeaveRequest
        {
            TenantId = tenantId,
            EmployeeId = employee.Id,
            LeaveTypeId = leaveType.Id,
            StartDate = start,
            EndDate = end,
            DaysRequested = days,
            Reason = reason,
            Status = status,
            ReviewedBy = status == LeaveRequestStatus.Pending ? null : admin.Id,
            CreatedAt = start.AddDays(-14),
            ReviewedAt = reviewedAt
        });

        await db.SaveChangesAsync();
    }
}
