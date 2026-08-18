using BluePrintHr.Api.Contracts;
using BluePrintHr.Api.Data;
using BluePrintHr.Api.Models;
using BluePrintHr.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BluePrintHr.Api.Controllers;

[ApiController]
[Route("api/payroll")]
[Authorize]
public class PayrollController(BluePrintHrDbContext db, IRequestContext context, IPayrollCalculator calculator) : ControllerBase
{
    [HttpGet("periods")]
    public async Task<ActionResult<IReadOnlyList<PayrollPeriodDto>>> Periods()
    {
        var periods = await db.PayrollPeriods.AsNoTracking().Where(x => x.TenantId == context.TenantId).OrderByDescending(x => x.Year).ThenByDescending(x => x.Month).ToListAsync();
        return Ok(periods.Select(x => new PayrollPeriodDto(x.Id, x.Name, x.Month, x.Year, x.Status.ToString(), x.ProcessedAt)).ToList());
    }

    [HttpGet("transactions")]
    public async Task<ActionResult<IReadOnlyList<PayrollTransactionDto>>> Transactions([FromQuery] int payrollPeriodId)
    {
        var query = db.PayrollTransactions.AsNoTracking().Include(x => x.Employee).Where(x => x.TenantId == context.TenantId && x.PayrollPeriodId == payrollPeriodId);
        if (!context.CanManagePayroll && context.EmployeeId.HasValue) query = query.Where(x => x.EmployeeId == context.EmployeeId.Value);
        var transactions = await query.OrderBy(x => x.Employee.FirstName).ThenBy(x => x.Employee.LastName).ToListAsync();
        return Ok(transactions.Select(ToDto).ToList());
    }

    [HttpPost("process")]
    [Authorize(Policy = "CanManagePayroll")]
    public async Task<ActionResult<IReadOnlyList<PayrollTransactionDto>>> Process(PayrollProcessRequest request)
    {
        var period = await db.PayrollPeriods.SingleOrDefaultAsync(x => x.Id == request.PayrollPeriodId && x.TenantId == context.TenantId);
        if (period is null) return NotFound(new { message = "Payroll period was not found." });
        if (period.Status == PayrollStatus.Locked) return Conflict(new { message = "Locked payroll periods cannot be reprocessed." });

        period.Status = PayrollStatus.Processing;
        await db.SaveChangesAsync();

        var employees = await db.Employees.Where(x => x.TenantId == context.TenantId && x.EmploymentStatus == "Active").ToListAsync();
        var oldTransactions = await db.PayrollTransactions.Where(x => x.TenantId == context.TenantId && x.PayrollPeriodId == period.Id).ToListAsync();
        db.PayrollTransactions.RemoveRange(oldTransactions);
        var transactions = new List<PayrollTransaction>();
        foreach (var employee in employees)
        {
            var result = calculator.Calculate(employee.BasicSalary, request.Allowances, request.OtherDeductions);
            transactions.Add(new PayrollTransaction
            {
                TenantId = context.TenantId,
                PayrollPeriodId = period.Id,
                EmployeeId = employee.Id,
                BasicSalary = employee.BasicSalary,
                Allowances = request.Allowances,
                GrossPay = result.GrossPay,
                TaxablePay = result.TaxablePay,
                Paye = result.Paye,
                PersonalRelief = result.PersonalRelief,
                Nssf = result.Nssf,
                Shif = result.Shif,
                HousingLevy = result.HousingLevy,
                OtherDeductions = request.OtherDeductions,
                TotalDeductions = result.TotalDeductions,
                NetPay = result.NetPay,
                Status = "Approved"
            });
        }
        db.PayrollTransactions.AddRange(transactions);
        period.Status = PayrollStatus.Approved;
        period.ProcessedAt = DateTime.UtcNow;
        db.AuditLogs.Add(new AuditLog { TenantId = context.TenantId, UserId = context.UserId, UserName = User.Identity?.Name, Action = "PROCESS", EntityType = "PayrollPeriod", EntityId = period.Id, Details = $"Processed {transactions.Count} employee transactions." });
        await db.SaveChangesAsync();
        return Ok(transactions.Select(x => ToDto(x, employees.Single(e => e.Id == x.EmployeeId))).ToList());
    }

    private static PayrollTransactionDto ToDto(PayrollTransaction x) => ToDto(x, x.Employee);
    private static PayrollTransactionDto ToDto(PayrollTransaction x, Employee employee) => new(x.Id, x.EmployeeId, $"{employee.FirstName} {employee.LastName}", x.GrossPay, x.Paye, x.Nssf, x.Shif, x.HousingLevy, x.TotalDeductions, x.NetPay, x.Status);
}
