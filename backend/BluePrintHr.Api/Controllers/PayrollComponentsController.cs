using BluePrintHr.Api.Contracts;
using BluePrintHr.Api.Data;
using BluePrintHr.Api.Models;
using BluePrintHr.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BluePrintHr.Api.Controllers;

[ApiController]
[Route("api/payroll/components")]
[Authorize(Policy = "CanManagePayroll")]
public class PayrollComponentsController(BluePrintHrDbContext db, IRequestContext context) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PayrollComponentDto>>> List([FromQuery] int? employeeId = null)
    {
        var query = db.EmployeePayrollComponents.AsNoTracking().Where(x => x.TenantId == context.TenantId);
        if (employeeId.HasValue) query = query.Where(x => x.EmployeeId == employeeId.Value);
        var rows = await query.OrderBy(x => x.EmployeeId).ThenBy(x => x.Name).ToListAsync();
        return Ok(rows.Select(ToDto).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<PayrollComponentDto>> Create(CreatePayrollComponentRequest request)
    {
        if (request.Amount < 0 || string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Component name and non-negative amount are required." });
        if (request.ComponentType is not ("Allowance" or "Deduction"))
            return BadRequest(new { message = "Component type must be Allowance or Deduction." });
        if (!await db.Employees.AnyAsync(x => x.Id == request.EmployeeId && x.TenantId == context.TenantId))
            return BadRequest(new { message = "Employee is outside the current tenant." });

        var row = new EmployeePayrollComponent
        {
            TenantId = context.TenantId, EmployeeId = request.EmployeeId, Name = request.Name.Trim(),
            ComponentType = request.ComponentType, Amount = request.Amount, Taxable = request.Taxable, Recurring = request.Recurring, Active = true
        };
        db.EmployeePayrollComponents.Add(row);
        await db.SaveChangesAsync();
        db.AuditLogs.Add(new AuditLog { TenantId = context.TenantId, UserId = context.UserId, UserName = User.Identity?.Name, Action = "CREATE", EntityType = "PayrollComponent", EntityId = row.Id, Details = row.Name });
        await db.SaveChangesAsync();
        return Ok(ToDto(row));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<PayrollComponentDto>> Update(int id, UpdatePayrollComponentRequest request)
    {
        var row = await db.EmployeePayrollComponents.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == context.TenantId);
        if (row is null) return NotFound();
        if (request.Amount < 0 || string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Component name and non-negative amount are required." });
        if (request.ComponentType is not ("Allowance" or "Deduction"))
            return BadRequest(new { message = "Component type must be Allowance or Deduction." });

        row.Name = request.Name.Trim(); row.ComponentType = request.ComponentType; row.Amount = request.Amount;
        row.Taxable = request.Taxable; row.Recurring = request.Recurring; row.Active = request.Active;
        await db.SaveChangesAsync();
        return Ok(ToDto(row));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var row = await db.EmployeePayrollComponents.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == context.TenantId);
        if (row is null) return NotFound();
        row.Active = false;
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static PayrollComponentDto ToDto(EmployeePayrollComponent x) => new(x.Id, x.EmployeeId, x.Name, x.ComponentType, x.Amount, x.Taxable, x.Recurring, x.Active);
}
