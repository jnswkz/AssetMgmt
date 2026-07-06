using AssetMgmt.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssetMgmt.Infrastructure.Persistence.Configurations;

public class AiAgentConversationConfiguration : IEntityTypeConfiguration<AiAgentConversation>
{
    public void Configure(EntityTypeBuilder<AiAgentConversation> b)
    {
        b.ToTable("agent_conversations", "ai", t => t.HasTrigger("trg_agent_conversations_updated_at"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("NEWSEQUENTIALID()");
        b.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        b.Property(x => x.ConversationKey).HasColumnName("conversation_key").HasMaxLength(100).IsRequired();
        b.Property(x => x.Title).HasColumnName("title").HasMaxLength(200);
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("SYSUTCDATETIME()");

        b.HasIndex(x => new { x.UserId, x.ConversationKey }).IsUnique();

        b.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasMany(x => x.Messages)
            .WithOne(x => x.Conversation)
            .HasForeignKey(x => x.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
