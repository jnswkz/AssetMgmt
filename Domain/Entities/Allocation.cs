using AssetMgmt.Domain.Enums;

namespace AssetMgmt.Domain.Entities;

public class Allocation
{
    public Guid Id { get; set; }
    public Guid AssetInstanceId { get; set; }
    public Guid UserId { get; set; }
    public AllocationEventType EventType { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? ExpectedReturnAt { get; set; }
    public Guid? FromUserId { get; set; }
    public Guid? ToUserId { get; set; }
    public Guid? AllocationRequestId { get; set; }
    public Guid? HandoverDocId { get; set; }
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }

    public AssetInstance AssetInstance { get; set; } = null!;
    public User User { get; set; } = null!;
    public User? FromUser { get; set; }
    public User? ToUser { get; set; }
    public AllocationRequest? AllocationRequest { get; set; }
    public HandoverDocument? HandoverDocument { get; set; }
}
