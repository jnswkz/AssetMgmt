using AssetMgmt.Application.Agents;
using AssetMgmt.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssetMgmt.Infrastructure.Persistence.Configurations;

public class AiAgentMessageConfiguration : IEntityTypeConfiguration<AiAgentMessage>
{
    public void Configure(EntityTypeBuilder<AiAgentMessage> b)
    {
        b.ToTable("agent_messages", "ai");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("NEWSEQUENTIALID()");
        b.Property(x => x.ConversationId).HasColumnName("conversation_id").IsRequired();
        b.Property(x => x.Role).HasColumnName("role").HasMaxLength(20).IsRequired();
        b.Property(x => x.Content).HasColumnName("content").IsRequired();
        b.Property(x => x.Intent).HasColumnName("intent").HasMaxLength(80);
        b.Property(x => x.ToolCallsJson).HasColumnName("tool_calls_json");
        b.Property(x => x.RequiresConfirmation).HasColumnName("requires_confirmation").HasDefaultValue(false);
        b.Property(x => x.PendingActionId).HasColumnName("pending_action_id").HasMaxLength(100);
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()");

        b.HasIndex(x => new { x.ConversationId, x.CreatedAt });

        b.ToTable(tableBuilder => tableBuilder.HasCheckConstraint(
            "CK_agent_messages_role",
            $"[role] IN ('{AiConversationRoles.User}', '{AiConversationRoles.Assistant}')"));
    }
}
