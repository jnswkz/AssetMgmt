namespace AssetMgmt.Domain.Entities;

public class DepreciationLedger
{
    public Guid Id { get; set; }
    public Guid AssetInstanceId { get; set; }
    public DateTime PeriodDate { get; set; }
    public decimal OpeningBookValue { get; set; }
    public decimal PeriodDepreciation { get; set; }
    public decimal AccumulatedDepreciation { get; set; }
    public decimal ClosingBookValue { get; set; }
    public Guid PolicyId { get; set; }
    public DateTime PostedAt { get; set; }
    public Guid? PostedBy { get; set; }
    public DateTime CreatedAt { get; set; }

    public AssetInstance AssetInstance { get; set; } = null!;
    public DepreciationPolicy Policy { get; set; } = null!;
}
