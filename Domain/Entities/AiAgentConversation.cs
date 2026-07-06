namespace AssetMgmt.Domain.Entities;

public class AiAgentConversation
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string ConversationKey { get; set; } = null!;
    public string? Title { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User? User { get; set; }
    public ICollection<AiAgentMessage> Messages { get; set; } = [];
}
