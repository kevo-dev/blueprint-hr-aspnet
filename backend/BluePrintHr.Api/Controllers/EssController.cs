using BluePrintHr.Api.Contracts;
using BluePrintHr.Api.Data;
using BluePrintHr.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BluePrintHr.Api.Controllers;

[ApiController]
[Route("api/ess")]
[Authorize]
public class EssController(BluePrintHrDbContext db, IRequestContext context) : ControllerBase
{
    [HttpGet("profile")]
    public async Task<ActionResult<EmployeeDto>> Profile()
    {
        if (!context.EmployeeId.HasValue) return Ok(null);
        var employee = await db.Employees.AsNoTracking().SingleOrDefaultAsync(x => x.Id == context.EmployeeId && x.TenantId == context.TenantId);
        if (employee is null) return NotFound();
        return Ok(new EmployeeDto(employee.Id, employee.EmployeeNo, employee.PayrollNo, $"{employee.FirstName} {employee.LastName}", employee.Email, employee.Phone, employee.KraPin, employee.NssfNo, employee.ShifNo, employee.EmploymentStatus, employee.BasicSalary, employee.BankName, employee.AccountNumber, employee.DepartmentId, employee.BranchId));
    }

    [HttpGet("payslips")]
    public async Task<ActionResult<IReadOnlyList<PayrollTransactionDto>>> Payslips()
    {
        if (!context.EmployeeId.HasValue) return Ok(Array.Empty<PayrollTransactionDto>());
        var transactions = await db.PayrollTransactions.AsNoTracking().Include(x => x.Employee)
            .Where(x => x.TenantId == context.TenantId && x.EmployeeId == context.EmployeeId.Value)
            .OrderByDescending(x => x.CreatedAt).ToListAsync();
        return Ok(transactions.Select(x => new PayrollTransactionDto(x.Id, x.EmployeeId, $"{x.Employee.FirstName} {x.Employee.LastName}", x.GrossPay, x.Paye, x.Nssf, x.Shif, x.HousingLevy, x.TotalDeductions, x.NetPay, x.Status)).ToList());
    }
}
