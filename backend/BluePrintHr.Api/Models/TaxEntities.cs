namespace BluePrintHr.Api.Models;

public enum PayrollTaxType
{
    PAYE,
    NSSF,
    SHIF,
    HousingLevy
}

public class PayrollTaxTable
{
    public int Id { get; set; }
    public int? TenantId { get; set; }
    public PayrollTaxType TaxType { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public decimal? Rate { get; set; }
    public decimal? EmployerRate { get; set; }
    public decimal? PersonalRelief { get; set; }
    public decimal? MinimumAmount { get; set; }
    public decimal? MaximumAmount { get; set; }
    public bool Active { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<PayrollTaxBand> Bands { get; set; } = new List<PayrollTaxBand>();
}

public class PayrollTaxBand
{
    public int Id { get; set; }
    public int PayrollTaxTableId { get; set; }
    public PayrollTaxTable PayrollTaxTable { get; set; } = null!;
    public decimal LowerBound { get; set; }
    public decimal? UpperBound { get; set; }
    public decimal Rate { get; set; }
    public int SortOrder { get; set; }
}
