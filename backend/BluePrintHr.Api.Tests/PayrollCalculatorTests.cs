using BluePrintHr.Api.Services;

namespace BluePrintHr.Api.Tests;

public class PayrollCalculatorTests
{
    private readonly KenyaPayrollCalculator calculator = new();

    [Fact]
    public void Calculate_applies_statutory_deductions_and_personal_relief()
    {
        var result = calculator.Calculate(85_000m, 5_000m, 1_000m);

        Assert.Equal(90_000m, result.GrossPay);
        Assert.Equal(5_400m, result.Nssf);
        Assert.Equal(2_475m, result.Shif);
        Assert.Equal(1_350m, result.HousingLevy);
        Assert.True(result.Paye >= 0);
        Assert.Equal(result.GrossPay - result.TotalDeductions, result.NetPay);
    }

    [Fact]
    public void Calculate_never_returns_negative_paye_after_personal_relief()
    {
        var result = calculator.Calculate(20_000m, 0, 0);

        Assert.Equal(0m, result.Paye);
        Assert.True(result.NetPay > 0);
    }

    [Fact]
    public void Calculate_caps_nssf_contribution_at_upper_limit()
    {
        var result = calculator.Calculate(300_000m, 0, 0);

        Assert.Equal(6_480m, result.Nssf);
    }
}
