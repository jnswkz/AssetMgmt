namespace AssetMgmt.Domain.Entities;

public class AiAgentMessage
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public string Role { get; set; } = null!;
    public string Content { get; set; } = null!;
    public string? Intent { get; set; }
    public string? ToolCallsJson { get; set; }
    public bool RequiresConfirmation { get; set; }
    public string? PendingActionId { get; set; }
    public DateTime CreatedAt { get; set; }

    public AiAgentConversation? Conversation { get; set; }
}
