using AssetMgmt.Domain.Enums;

namespace AssetMgmt.Domain.Entities;

public class ReturnObligation
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid AssetInstanceId { get; set; }
    public ReturnObligationReason Reason { get; set; }
    public DateTime DueAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public Guid? ResolvedBy { get; set; }
    public string? ResolutionNotes { get; set; }
    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;
    public AssetInstance AssetInstance { get; set; } = null!;
}
