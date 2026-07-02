using AssetMgmt.Domain.Enums;

namespace AssetMgmt.Domain.Entities;

public class AssetInstance
{
    public Guid Id { get; set; }
    public string AssetCode { get; set; } = null!;
    public string Serial { get; set; } = null!;
    public Guid ModelId { get; set; }
    public AssetStatus Status { get; set; } = AssetStatus.InStock;
    public Guid? CurrentHolderId { get; set; }
    public decimal AcquisitionCost { get; set; }
    public DateTime AcquisitionDate { get; set; }
    public decimal SalvageValue { get; set; }
    public string? Location { get; set; }
    public DateTime? WarrantyExpiresAt { get; set; }
    public string? QrCodePath { get; set; }
    public string? Notes { get; set; }

    public DateTime? LockExpiresAt { get; set; }
    public string? LockToken { get; set; }
    public Guid? LockHolderUserId { get; set; }

    public int Version { get; set; } = 1;
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime? DeletedAt { get; set; }

    public AssetModel Model { get; set; } = null!;
    public User? CurrentHolder { get; set; }
}
