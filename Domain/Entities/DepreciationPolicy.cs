using AssetMgmt.Domain.Enums;

namespace AssetMgmt.Domain.Entities;

public class DepreciationPolicy
{
    public Guid Id { get; set; }
    public Guid AssetModelId { get; set; }
    public DepreciationMethod Method { get; set; }
    public int UsefulLifeMonths { get; set; }
    public decimal? AnnualDeclineRate { get; set; }
    public decimal SalvageValuePercent { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public AssetModel AssetModel { get; set; } = null!;
}
