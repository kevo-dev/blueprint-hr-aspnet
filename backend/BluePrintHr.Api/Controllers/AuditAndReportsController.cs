using BluePrintHr.Api.Contracts;
using BluePrintHr.Api.Data;
using BluePrintHr.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BluePrintHr.Api.Controllers;

[ApiController]
[Route("api/audit")]
[Authorize(Policy = "CanViewAudit")]
public class AuditController(BluePrintHrDbContext db, IRequestContext context) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AuditLogDto>>> List()
    {
        var rows = await db.AuditLogs.AsNoTracking().Where(x => x.TenantId == context.TenantId).OrderByDescending(x => x.CreatedAt).Take(200).ToListAsync();
        return Ok(rows.Select(x => new AuditLogDto(x.Id, x.Action, x.EntityType, x.EntityId, x.UserName, x.Details, x.CreatedAt)).ToList());
    }
}

[ApiController]
[Route("api/reports")]
[Authorize]
public class ReportsController(BluePrintHrDbContext db, IConfiguration configuration, IRequestContext context) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ReportDefinitionDto>>> List()
    {
        var baseUrl = configuration["Ssrs:ReportServerUrl"]?.TrimEnd('/');
        var reports = await db.ReportDefinitions.AsNoTracking().Where(x => x.Enabled).OrderBy(x => x.Name).ToListAsync();
        return Ok(reports.Select(x => new ReportDefinitionDto(x.Id, x.Name, x.Description, x.ReportPath, baseUrl is null ? null : $"{baseUrl}{x.ReportPath}")).ToList());
    }

    [HttpGet("{id:int}/data")]
    public async Task<ActionResult<ReportDataDto>> Data(int id)
    {
        var report = await db.ReportDefinitions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.Enabled);
        if (report is null) return NotFound(new { message = "Report not found." });

        var employees = await db.Employees.AsNoTracking().Where(x => x.TenantId == context.TenantId).OrderBy(x => x.EmployeeNo).ToListAsync();
        var employeeMap = employees.ToDictionary(x => x.Id);
        var departments = await db.Departments.AsNoTracking().Where(x => x.TenantId == context.TenantId).ToDictionaryAsync(x => x.Id, x => x.Name);
        var leaveTypes = await db.LeaveTypes.AsNoTracking().Where(x => x.TenantId == context.TenantId).ToDictionaryAsync(x => x.Id, x => x.Name);
        var balances = await db.LeaveBalances.AsNoTracking().Where(x => x.TenantId == context.TenantId && x.Year == DateTime.UtcNow.Year).ToListAsync();
        var requests = await db.LeaveRequests.AsNoTracking().Where(x => x.TenantId == context.TenantId).OrderByDescending(x => x.CreatedAt).ToListAsync();
        var transactions = await db.PayrollTransactions.AsNoTracking().Include(x => x.PayrollPeriod).Where(x => x.TenantId == context.TenantId).OrderByDescending(x => x.PayrollPeriod.Year).ThenByDescending(x => x.PayrollPeriod.Month).ToListAsync();

        var name = report.Name.ToLowerInvariant();
        IReadOnlyList<string> columns;
        IReadOnlyList<IReadOnlyList<string>> rows;

        if (name == "employee roster")
        {
            columns = ["Employee No", "Name", "Department", "Email", "Phone", "Employment Status", "Basic Salary"];
            rows = employees.Select(e => new[]
            {
                e.EmployeeNo, $"{e.FirstName} {e.LastName}", e.DepartmentId.HasValue && departments.TryGetValue(e.DepartmentId.Value, out var d) ? d : "—",
                e.Email ?? "—", e.Phone ?? "—", e.EmploymentStatus, e.BasicSalary.ToString("N2")
            }).Cast<IReadOnlyList<string>>().ToList();
        }
        else if (name == "leave utilization")
        {
            columns = ["Employee No", "Employee", "Leave Type", "Year", "Allocated Days", "Used Days", "Available Days"];
            rows = balances.Select(b => new[]
            {
                employeeMap.TryGetValue(b.EmployeeId, out var e) ? e.EmployeeNo : "—",
                employeeMap.TryGetValue(b.EmployeeId, out e) ? $"{e.FirstName} {e.LastName}" : "—",
                leaveTypes.TryGetValue(b.LeaveTypeId, out var lt) ? lt : "—", b.Year.ToString(),
                b.AllocatedDays.ToString("N2"), b.UsedDays.ToString("N2"), Math.Max(b.AllocatedDays - b.UsedDays, 0).ToString("N2")
            }).Cast<IReadOnlyList<string>>().ToList();
        }
        else if (name == "leave requests")
        {
            columns = ["Employee", "Leave Type", "Start", "End", "Days", "Status", "Reason"];
            rows = requests.Select(r => new[]
            {
                employeeMap.TryGetValue(r.EmployeeId, out var e) ? $"{e.FirstName} {e.LastName}" : "—",
                leaveTypes.TryGetValue(r.LeaveTypeId, out var lt) ? lt : "—",
                r.StartDate.ToString("yyyy-MM-dd"), r.EndDate.ToString("yyyy-MM-dd"), r.DaysRequested.ToString("N2"),
                r.Status.ToString(), r.Reason ?? "—"
            }).Cast<IReadOnlyList<string>>().ToList();
        }
        else if (name == "department headcount")
        {
            columns = ["Department", "Headcount", "Active", "Inactive"];
            rows = employees.GroupBy(e => e.DepartmentId.HasValue && departments.TryGetValue(e.DepartmentId.Value, out var d) ? d : "Unassigned")
                .OrderBy(g => g.Key).Select(g => new[]
                {
                    g.Key, g.Count().ToString(), g.Count(e => string.Equals(e.EmploymentStatus, "Active", StringComparison.OrdinalIgnoreCase)).ToString(),
                    g.Count(e => !string.Equals(e.EmploymentStatus, "Active", StringComparison.OrdinalIgnoreCase)).ToString()
                }).Cast<IReadOnlyList<string>>().ToList();
        }
        else if (name == "payroll summary")
        {
            columns = ["Period", "Employee", "Basic Salary", "Gross Pay", "PAYE", "NSSF", "SHIF", "Housing Levy", "Deductions", "Net Pay", "Status"];
            rows = transactions.Select(t => new[]
            {
                t.PayrollPeriod.Name, employeeMap.TryGetValue(t.EmployeeId, out var e) ? $"{e.FirstName} {e.LastName}" : "—",
                t.BasicSalary.ToString("N2"), t.GrossPay.ToString("N2"), t.Paye.ToString("N2"), t.Nssf.ToString("N2"),
                t.Shif.ToString("N2"), t.HousingLevy.ToString("N2"), t.TotalDeductions.ToString("N2"), t.NetPay.ToString("N2"), t.Status
            }).Cast<IReadOnlyList<string>>().ToList();
        }
        else
        {
            return BadRequest(new { message = "This report definition has no built-in data provider." });
        }

        return Ok(new ReportDataDto(report.Name, report.Description, columns, rows, DateTime.UtcNow));
    }
}
