using AssetMgmt.Domain.Enums;

namespace AssetMgmt.Domain.Entities;

public class AssetModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public AssetCategory Category { get; set; }
    public string? Manufacturer { get; set; }
    public string? ModelNumber { get; set; }
    public string? Specs { get; set; }
    public int DefaultUsefulLifeMonths { get; set; } = 36;
    public DepreciationMethod DefaultDepreciationMethod { get; set; } = DepreciationMethod.StraightLine;
    public string? ImageUrl { get; set; }

    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime? DeletedAt { get; set; }

    public ICollection<AssetInstance> Instances { get; set; } = new List<AssetInstance>();
    public DepreciationPolicy? DepreciationPolicy { get; set; }
}
