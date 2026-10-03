using System.Text.Json;
using BluePrintHr.Api.Contracts;
using BluePrintHr.Api.Data;
using BluePrintHr.Api.Models;
using BluePrintHr.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BluePrintHr.Api.Controllers;

[ApiController]
[Route("api/employees")]
[Authorize]
public class EmployeesController(BluePrintHrDbContext db, IRequestContext context) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EmployeeDto>>> List()
    {
        var query = db.Employees.AsNoTracking().Where(x => x.TenantId == context.TenantId);
        if (!context.CanManageEmployees && context.EmployeeId.HasValue)
            query = query.Where(x => x.Id == context.EmployeeId.Value);
        var employees = await query.OrderBy(x => x.FirstName).ThenBy(x => x.LastName).ToListAsync();
        return Ok(employees.Select(ToDto).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<EmployeeDto>> Get(int id)
    {
        var employee = await db.Employees.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.TenantId == context.TenantId);
        if (employee is null || (!context.CanManageEmployees && employee.Id != context.EmployeeId)) return NotFound();
        return Ok(ToDto(employee));
    }

    [HttpPost]
    [Authorize(Policy = "CanManageEmployees")]
    public async Task<ActionResult<EmployeeDto>> Create(CreateEmployeeRequest request)
    {
        if (await db.Employees.AnyAsync(x => x.TenantId == context.TenantId && x.EmployeeNo == request.EmployeeNo))
            return Conflict(new { message = "Employee number already exists for this tenant." });
        var employee = new Employee
        {
            TenantId = context.TenantId,
            EmployeeNo = request.EmployeeNo.Trim(),
            PayrollNo = request.PayrollNo,
            FirstName = request.FirstName.Trim(),
            MiddleName = request.MiddleName,
            LastName = request.LastName.Trim(),
            KraPin = request.KraPin.Trim().ToUpperInvariant(),
            BasicSalary = request.BasicSalary,
            Email = request.Email,
            Phone = request.Phone,
            NssfNo = request.NssfNo,
            ShifNo = request.ShifNo,
            BranchId = request.BranchId,
            DepartmentId = request.DepartmentId,
            BankName = request.BankName,
            BankBranch = request.BankBranch,
            AccountNumber = request.AccountNumber,
            EmploymentStatus = "Active",
            EmploymentDate = DateTime.UtcNow.Date
        };
        db.Employees.Add(employee);
        await db.SaveChangesAsync();
        await AuditAsync("CREATE", "Employee", employee.Id, JsonSerializer.Serialize(new { employee.EmployeeNo, employee.FirstName, employee.LastName }));
        return CreatedAtAction(nameof(Get), new { id = employee.Id }, ToDto(employee));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = "CanManageEmployees")]
    public async Task<ActionResult<EmployeeDto>> Update(int id, UpdateEmployeeRequest request)
    {
        var employee = await db.Employees.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == context.TenantId);
        if (employee is null) return NotFound();

        var duplicate = await db.Employees.AnyAsync(x =>
            x.TenantId == context.TenantId && x.EmployeeNo == request.EmployeeNo.Trim() && x.Id != id);
        if (duplicate) return Conflict(new { message = "Employee number already exists for this tenant." });

        employee.EmployeeNo = request.EmployeeNo.Trim();
        employee.PayrollNo = request.PayrollNo;
        employee.FirstName = request.FirstName.Trim();
        employee.MiddleName = request.MiddleName;
        employee.LastName = request.LastName.Trim();
        employee.KraPin = request.KraPin.Trim().ToUpperInvariant();
        employee.BasicSalary = request.BasicSalary;
        employee.Email = request.Email;
        employee.Phone = request.Phone;
        employee.NssfNo = request.NssfNo;
        employee.ShifNo = request.ShifNo;
        employee.BranchId = request.BranchId;
        employee.DepartmentId = request.DepartmentId;
        employee.DesignationId = request.DesignationId;
        employee.GradeId = request.GradeId;
        employee.EmploymentTypeId = request.EmploymentTypeId;
        employee.EmploymentDate = request.EmploymentDate;
        employee.TerminationDate = request.TerminationDate;
        employee.EmploymentStatus = string.IsNullOrWhiteSpace(request.EmploymentStatus) ? "Active" : request.EmploymentStatus.Trim();
        employee.BankName = request.BankName;
        employee.BankBranch = request.BankBranch;
        employee.AccountNumber = request.AccountNumber;

        await db.SaveChangesAsync();
        await AuditAsync("UPDATE", "Employee", employee.Id, JsonSerializer.Serialize(new { employee.EmployeeNo, employee.EmploymentStatus }));
        return Ok(ToDto(employee));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = "CanManageEmployees")]
    public async Task<IActionResult> Deactivate(int id)
    {
        var employee = await db.Employees.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == context.TenantId);
        if (employee is null) return NotFound();

        if (await db.Users.AnyAsync(x => x.TenantId == context.TenantId && x.EmployeeId == id && x.Role == UserRole.Employee))
            return Conflict(new { message = "This employee has a linked login. Disable or reassign the login before deactivation." });

        employee.EmploymentStatus = "Inactive";
        employee.TerminationDate ??= DateTime.UtcNow.Date;
        await db.SaveChangesAsync();
        await AuditAsync("DEACTIVATE", "Employee", employee.Id, $"Employee {employee.EmployeeNo} deactivated.");
        return NoContent();
    }

    private async Task AuditAsync(string action, string entity, int id, string details)
    {
        db.AuditLogs.Add(new AuditLog { TenantId = context.TenantId, UserId = context.UserId, UserName = User.Identity?.Name, Action = action, EntityType = entity, EntityId = id, Details = details, IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() });
        await db.SaveChangesAsync();
    }

    private static EmployeeDto ToDto(Employee x) => new(x.Id, x.EmployeeNo, x.PayrollNo, string.Join(' ', new[] { x.FirstName, x.MiddleName, x.LastName }.Where(s => !string.IsNullOrWhiteSpace(s))), x.FirstName, x.MiddleName, x.LastName, x.Email, x.Phone, x.KraPin, x.NssfNo, x.ShifNo, x.EmploymentStatus, x.BasicSalary, x.BankName, x.BankBranch, x.AccountNumber, x.DepartmentId, x.BranchId, x.DesignationId, x.GradeId, x.EmploymentTypeId, x.EmploymentDate, x.TerminationDate);
}
