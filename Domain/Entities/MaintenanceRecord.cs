using AssetMgmt.Domain.Enums;

namespace AssetMgmt.Domain.Entities;

public class MaintenanceRecord
{
    public Guid Id { get; set; }
    public Guid AssetInstanceId { get; set; }
    public MaintenanceType MaintenanceType { get; set; }
    public string Description { get; set; } = null!;
    public decimal Cost { get; set; }
    public string? Vendor { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public MaintenanceStatus Status { get; set; } = MaintenanceStatus.InProgress;
    public string? InvoicePath { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }

    public AssetInstance AssetInstance { get; set; } = null!;
}
