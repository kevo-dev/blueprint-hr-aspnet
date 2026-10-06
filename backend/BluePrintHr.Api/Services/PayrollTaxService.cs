using BluePrintHr.Api.Data;
using BluePrintHr.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BluePrintHr.Api.Services;

public sealed record PayrollTaxConfiguration(
    IReadOnlyList<PayrollTaxBand> PayeBands,
    decimal PersonalRelief,
    decimal NssfRate,
    decimal NssfMaximumPensionablePay,
    decimal ShifRate,
    decimal HousingLevyRate);

public interface IPayrollTaxService
{
    Task<PayrollTaxConfiguration> GetConfigurationAsync(int? tenantId, DateTime effectiveDate, CancellationToken cancellationToken = default);
}

public sealed class PayrollTaxService(BluePrintHrDbContext db) : IPayrollTaxService
{
    public async Task<PayrollTaxConfiguration> GetConfigurationAsync(int? tenantId, DateTime effectiveDate, CancellationToken cancellationToken = default)
    {
        var date = effectiveDate.Date;
        var tables = await db.PayrollTaxTables
            .AsNoTracking()
            .Include(x => x.Bands)
            .Where(x => x.Active &&
                (x.TenantId == null || x.TenantId == tenantId) &&
                x.EffectiveFrom <= date &&
                (x.EffectiveTo == null || x.EffectiveTo >= date))
            .OrderByDescending(x => x.TenantId.HasValue)
            .ThenByDescending(x => x.EffectiveFrom)
            .ToListAsync(cancellationToken);

        PayrollTaxTable Pick(PayrollTaxType type) =>
            tables.FirstOrDefault(x => x.TaxType == type)
            ?? throw new InvalidOperationException($"No active {type} tax table exists for {date:yyyy-MM-dd}.");

        var paye = Pick(PayrollTaxType.PAYE);
        var nssf = Pick(PayrollTaxType.NSSF);
        var shif = Pick(PayrollTaxType.SHIF);
        var housing = Pick(PayrollTaxType.HousingLevy);

        return new PayrollTaxConfiguration(
            paye.Bands.OrderBy(x => x.SortOrder).ToList(),
            paye.PersonalRelief ?? 0m,
            nssf.Rate ?? 0m,
            nssf.MaximumAmount ?? 0m,
            shif.Rate ?? 0m,
            housing.Rate ?? 0m);
    }
}
