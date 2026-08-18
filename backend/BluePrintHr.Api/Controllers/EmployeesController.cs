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

    private async Task AuditAsync(string action, string entity, int id, string details)
    {
        db.AuditLogs.Add(new AuditLog { TenantId = context.TenantId, UserId = context.UserId, UserName = User.Identity?.Name, Action = action, EntityType = entity, EntityId = id, Details = details, IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() });
        await db.SaveChangesAsync();
    }

    private static EmployeeDto ToDto(Employee x) => new(x.Id, x.EmployeeNo, x.PayrollNo, string.Join(' ', new[] { x.FirstName, x.MiddleName, x.LastName }.Where(s => !string.IsNullOrWhiteSpace(s))), x.Email, x.Phone, x.KraPin, x.NssfNo, x.ShifNo, x.EmploymentStatus, x.BasicSalary, x.BankName, x.AccountNumber, x.DepartmentId, x.BranchId);
}
