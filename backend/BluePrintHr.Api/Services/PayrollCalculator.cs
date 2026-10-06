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
    PayrollCalculation Calculate(decimal basicSalary, decimal allowances, decimal otherDeductions, PayrollTaxConfiguration tax, decimal? taxableAllowances = null);
}

public sealed class KenyaPayrollCalculator : IPayrollCalculator
{
    public PayrollCalculation Calculate(decimal basicSalary, decimal allowances, decimal otherDeductions, PayrollTaxConfiguration tax, decimal? taxableAllowances = null)
    {
        var gross = Math.Round(basicSalary + allowances, 2);
        var nssf = Math.Round(Math.Min(gross, tax.NssfMaximumPensionablePay) * tax.NssfRate, 2);
        var shif = Math.Round(gross * tax.ShifRate, 2);
        var housingLevy = Math.Round(gross * tax.HousingLevyRate, 2);
        var taxableGross = basicSalary + (taxableAllowances ?? allowances);
        var taxablePay = Math.Max(taxableGross - nssf - shif - housingLevy, 0);
        var payeBeforeRelief = CalculatePaye(taxablePay, tax.PayeBands);
        var paye = Math.Max(payeBeforeRelief - tax.PersonalRelief, 0);
        var deductions = Math.Round(paye + nssf + shif + housingLevy + otherDeductions, 2);
        return new PayrollCalculation(gross, taxablePay, paye, tax.PersonalRelief, nssf, shif, housingLevy, deductions, Math.Round(gross - deductions, 2));
    }

    private static decimal CalculatePaye(decimal taxablePay, IReadOnlyList<BluePrintHr.Api.Models.PayrollTaxBand> bands)
    {
        var tax = 0m;
        foreach (var band in bands.OrderBy(x => x.SortOrder))
        {
            if (taxablePay <= band.LowerBound) break;
            var upper = band.UpperBound ?? decimal.MaxValue;
            var taxableSlice = Math.Min(taxablePay, upper) - band.LowerBound;
            if (taxableSlice > 0) tax += taxableSlice * band.Rate;
        }
        return Math.Round(tax, 2);
    }
}
