using BluePrintHr.Api.Contracts;
using BluePrintHr.Api.Data;
using BluePrintHr.Api.Models;
using BluePrintHr.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BluePrintHr.Api.Controllers;

[ApiController]
[Route("api/payroll/tax-tables")]
[Authorize(Policy = "CanManagePayroll")]
public class PayrollTaxController(BluePrintHrDbContext db, IRequestContext context) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PayrollTaxTableDto>>> List()
    {
        var tables = await db.PayrollTaxTables
            .AsNoTracking()
            .Include(x => x.Bands.OrderBy(b => b.SortOrder))
            .Where(x => x.TenantId == null || x.TenantId == context.TenantId)
            .OrderBy(x => x.TaxType)
            .ThenByDescending(x => x.EffectiveFrom)
            .ToListAsync();

        return Ok(tables.Select(ToDto).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<PayrollTaxTableDto>> Create(CreatePayrollTaxTableRequest request)
    {
        if (request.EffectiveTo.HasValue && request.EffectiveTo.Value.Date < request.EffectiveFrom.Date)
            return BadRequest(new { message = "EffectiveTo cannot be before EffectiveFrom." });

        var table = new PayrollTaxTable
        {
            TenantId = context.TenantId,
            TaxType = request.TaxType,
            Name = request.Name.Trim(),
            EffectiveFrom = request.EffectiveFrom.Date,
            EffectiveTo = request.EffectiveTo?.Date,
            Rate = request.Rate,
            EmployerRate = request.EmployerRate,
            PersonalRelief = request.PersonalRelief,
            MinimumAmount = request.MinimumAmount,
            MaximumAmount = request.MaximumAmount,
            Active = request.Active
        };

        foreach (var band in request.Bands.OrderBy(x => x.SortOrder))
            table.Bands.Add(new PayrollTaxBand
            {
                LowerBound = band.LowerBound,
                UpperBound = band.UpperBound,
                Rate = band.Rate,
                SortOrder = band.SortOrder
            });

        db.PayrollTaxTables.Add(table);
        await db.SaveChangesAsync();
        return Ok(ToDto(table));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<PayrollTaxTableDto>> Update(int id, UpdatePayrollTaxTableRequest request)
    {
        var table = await db.PayrollTaxTables.Include(x => x.Bands)
            .SingleOrDefaultAsync(x => x.Id == id && x.TenantId == context.TenantId);
        if (table is null) return NotFound();

        table.Name = request.Name.Trim();
        table.EffectiveFrom = request.EffectiveFrom.Date;
        table.EffectiveTo = request.EffectiveTo?.Date;
        table.Rate = request.Rate;
        table.EmployerRate = request.EmployerRate;
        table.PersonalRelief = request.PersonalRelief;
        table.MinimumAmount = request.MinimumAmount;
        table.MaximumAmount = request.MaximumAmount;
        table.Active = request.Active;

        db.PayrollTaxBands.RemoveRange(table.Bands);
        table.Bands.Clear();
        foreach (var band in request.Bands.OrderBy(x => x.SortOrder))
            table.Bands.Add(new PayrollTaxBand
            {
                LowerBound = band.LowerBound,
                UpperBound = band.UpperBound,
                Rate = band.Rate,
                SortOrder = band.SortOrder
            });

        await db.SaveChangesAsync();
        return Ok(ToDto(table));
    }

    private static PayrollTaxTableDto ToDto(PayrollTaxTable x) =>
        new(x.Id, x.TenantId, x.TaxType.ToString(), x.Name, x.EffectiveFrom, x.EffectiveTo,
            x.Rate, x.EmployerRate, x.PersonalRelief, x.MinimumAmount, x.MaximumAmount, x.Active,
            x.Bands.OrderBy(b => b.SortOrder).Select(b => new PayrollTaxBandDto(b.LowerBound, b.UpperBound, b.Rate, b.SortOrder)).ToList());
}
