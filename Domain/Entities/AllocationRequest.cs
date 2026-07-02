using AssetMgmt.Domain.Enums;

namespace AssetMgmt.Domain.Entities;

public class AllocationRequest
{
    public Guid Id { get; set; }
    public Guid RequesterId { get; set; }
    public Guid AssetInstanceId { get; set; }
    public RequestStatus Status { get; set; } = RequestStatus.Pending;
    public string? Reason { get; set; }
    public int? ExpectedDurationMonths { get; set; }
    public string IdempotencyKey { get; set; } = null!;
    public string? LockToken { get; set; }
    public DateTime? LockExpiresAt { get; set; }
    public Guid? ApproverId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectedReason { get; set; }
    public DateTime? ExpiredAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public User Requester { get; set; } = null!;
    public AssetInstance AssetInstance { get; set; } = null!;
    public User? Approver { get; set; }
}
