using AssetMgmt.Domain.Enums;

namespace AssetMgmt.Domain.Entities;

public class AssetDisposal
{
    public Guid Id { get; set; }
    public Guid AssetInstanceId { get; set; }
    public DisposalType DisposalType { get; set; }
    public Guid? SoldToUserId { get; set; }
    public decimal? SalePrice { get; set; }
    public string? Reason { get; set; }
    public DateTime DisposedAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }

    public AssetInstance AssetInstance { get; set; } = null!;
    public User? SoldToUser { get; set; }
}
