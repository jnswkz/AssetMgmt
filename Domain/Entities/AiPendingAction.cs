using AssetMgmt.Domain.Enums;

namespace AssetMgmt.Domain.Entities;

public class AiPendingAction
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ConversationId { get; set; }
    public string ToolName { get; set; } = null!;
    public string PayloadJson { get; set; } = null!;
    public string Summary { get; set; } = null!;
    public AiPendingActionStatus Status { get; set; } = AiPendingActionStatus.Pending;
    public string? ResultJson { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? ExecutedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public User User { get; set; } = null!;
    public AiAgentConversation Conversation { get; set; } = null!;
}
