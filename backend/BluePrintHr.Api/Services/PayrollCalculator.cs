namespace BluePrintHr.Api.Services;

public sealed record PayrollCalculation(
    decimal GrossPay,
    decimal TaxablePay,
    decimal Paye,
    decimal PersonalRelief,
    decimal Nssf,
    decimal Shif,
    decimal HousingLevy,
    decimal TotalDeductions,
    decimal NetPay);

public interface IPayrollCalculator
{
    PayrollCalculation Calculate(decimal basicSalary, decimal allowances, decimal otherDeductions);
}

public sealed class KenyaPayrollCalculator : IPayrollCalculator
{
    private const decimal PersonalRelief = 2_400m;
    private const decimal NssfRate = 0.06m;
    private const decimal NssfUpperLimit = 108_000m;
    private const decimal ShifRate = 0.0275m;
    private const decimal HousingLevyRate = 0.015m;

    public PayrollCalculation Calculate(decimal basicSalary, decimal allowances, decimal otherDeductions)
    {
        var gross = Math.Round(basicSalary + allowances, 2);
        var nssf = Math.Round(Math.Min(gross, NssfUpperLimit) * NssfRate, 2);
        var shif = Math.Round(gross * ShifRate, 2);
        var housingLevy = Math.Round(gross * HousingLevyRate, 2);
        var taxablePay = Math.Max(gross - nssf, 0);
        var payeBeforeRelief = CalculatePaye(taxablePay);
        var paye = Math.Max(payeBeforeRelief - PersonalRelief, 0);
        var deductions = Math.Round(paye + nssf + shif + housingLevy + otherDeductions, 2);
        return new PayrollCalculation(gross, taxablePay, paye, PersonalRelief, nssf, shif, housingLevy, deductions, Math.Round(gross - deductions, 2));
    }

    private static decimal CalculatePaye(decimal taxablePay)
    {
        var remaining = taxablePay;
        var tax = 0m;
        var bands = new[]
        {
            (limit: 24_000m, rate: 0.10m),
            (limit: 8_333m, rate: 0.25m),
            (limit: 467_667m, rate: 0.30m),
            (limit: 300_000m, rate: 0.325m),
            (limit: decimal.MaxValue, rate: 0.35m)
        };
        foreach (var (limit, rate) in bands)
        {
            var slice = Math.Min(remaining, limit);
            tax += slice * rate;
            remaining -= slice;
            if (remaining <= 0) break;
        }
        return Math.Round(tax, 2);
    }
}
