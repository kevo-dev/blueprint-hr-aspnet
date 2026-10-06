using System.Text;
using BluePrintHr.Api.Data;
using BluePrintHr.Api.Models;
using BluePrintHr.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BluePrintHr.Api.Controllers;

[ApiController]
[Route("api/payroll/statutory")]
[Authorize(Policy = "CanManagePayroll")]
public class PayrollStatutoryController(BluePrintHrDbContext db, IRequestContext context) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int payrollPeriodId, [FromQuery] string type = "all", [FromQuery] bool csv = false)
    {
        var period = await db.PayrollPeriods.AsNoTracking().SingleOrDefaultAsync(x => x.Id == payrollPeriodId && x.TenantId == context.TenantId);
        if (period is null) return NotFound(new { message = "Payroll period was not found." });
        var rows = await db.PayrollTransactions.AsNoTracking().Include(x => x.Employee)
            .Where(x => x.TenantId == context.TenantId && x.PayrollPeriodId == payrollPeriodId)
            .OrderBy(x => x.Employee.EmployeeNo).ToListAsync();

        var selected = type.Trim().ToLowerInvariant();
        var columns = selected switch
        {
            "paye" => new[] { "Employee No", "Employee", "KRA PIN", "Taxable Pay", "PAYE", "Personal Relief" },
            "nssf" => new[] { "Employee No", "Employee", "NSSF No", "Gross Pay", "NSSF" },
            "shif" => new[] { "Employee No", "Employee", "SHIF No", "Gross Pay", "SHIF" },
            "housing" or "housing_levy" => new[] { "Employee No", "Employee", "Gross Pay", "Housing Levy" },
            _ => new[] { "Employee No", "Employee", "KRA PIN", "NSSF No", "SHIF No", "Gross Pay", "Taxable Pay", "PAYE", "NSSF", "SHIF", "Housing Levy" }
        };

        var data = rows.Select(x => selected switch
        {
            "paye" => new[] { x.Employee.EmployeeNo, $"{x.Employee.FirstName} {x.Employee.LastName}", x.Employee.KraPin, x.TaxablePay.ToString("F2"), x.Paye.ToString("F2"), x.PersonalRelief.ToString("F2") },
            "nssf" => new[] { x.Employee.EmployeeNo, $"{x.Employee.FirstName} {x.Employee.LastName}", x.Employee.NssfNo ?? "", x.GrossPay.ToString("F2"), x.Nssf.ToString("F2") },
            "shif" => new[] { x.Employee.EmployeeNo, $"{x.Employee.FirstName} {x.Employee.LastName}", x.Employee.ShifNo ?? "", x.GrossPay.ToString("F2"), x.Shif.ToString("F2") },
            "housing" or "housing_levy" => new[] { x.Employee.EmployeeNo, $"{x.Employee.FirstName} {x.Employee.LastName}", x.GrossPay.ToString("F2"), x.HousingLevy.ToString("F2") },
            _ => new[] { x.Employee.EmployeeNo, $"{x.Employee.FirstName} {x.Employee.LastName}", x.Employee.KraPin, x.Employee.NssfNo ?? "", x.Employee.ShifNo ?? "", x.GrossPay.ToString("F2"), x.TaxablePay.ToString("F2"), x.Paye.ToString("F2"), x.Nssf.ToString("F2"), x.Shif.ToString("F2"), x.HousingLevy.ToString("F2") }
        }).ToList();

        if (!csv) return Ok(new { period = period.Name, type = selected, columns, rows = data });
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", columns.Select(Escape)));
        foreach (var row in data) sb.AppendLine(string.Join(",", row.Select(Escape)));
        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"statutory-{selected}-{period.Year}-{period.Month:D2}.csv");
    }

    private static string Escape(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
}
