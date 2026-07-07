using AssetMgmt.Domain.Enums;

namespace AssetMgmt.Domain.Entities;

public class InventoryScan
{
    public Guid Id { get; set; }
    public Guid? DepartmentId { get; set; }
    public InventoryScanStatus Status { get; set; } = InventoryScanStatus.Open;
    public DateTime StartedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }

    public Department? Department { get; set; }
    public ICollection<InventoryScanItem> Items { get; set; } = new List<InventoryScanItem>();
}
