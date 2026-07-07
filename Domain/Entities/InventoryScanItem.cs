using AssetMgmt.Domain.Enums;

namespace AssetMgmt.Domain.Entities;

public class InventoryScanItem
{
    public Guid Id { get; set; }
    public Guid InventoryScanId { get; set; }
    public Guid? AssetInstanceId { get; set; }
    public string AssetCode { get; set; } = null!;
    public InventoryScanResult Result { get; set; }
    public DateTime ScannedAt { get; set; }

    public InventoryScan InventoryScan { get; set; } = null!;
    public AssetInstance? AssetInstance { get; set; }
}
