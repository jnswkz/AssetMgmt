using AssetMgmt.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssetMgmt.Infrastructure.Persistence.Configurations;

public class AiPendingActionConfiguration : IEntityTypeConfiguration<AiPendingAction>
{
    public void Configure(EntityTypeBuilder<AiPendingAction> b)
    {
        b.ToTable("pending_actions", "ai");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("NEWSEQUENTIALID()");
        b.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        b.Property(x => x.ConversationId).HasColumnName("conversation_id").IsRequired();
        b.Property(x => x.ToolName).HasColumnName("tool_name").HasMaxLength(100).IsRequired();
        b.Property(x => x.PayloadJson).HasColumnName("payload_json").IsRequired();
        b.Property(x => x.Summary).HasColumnName("summary").HasMaxLength(500).IsRequired();
        b.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).HasConversion<string>().IsRequired();
        b.Property(x => x.ResultJson).HasColumnName("result_json");
        b.Property(x => x.ExpiresAt).HasColumnName("expires_at").IsRequired();
        b.Property(x => x.ExecutedAt).HasColumnName("executed_at");
        b.Property(x => x.CancelledAt).HasColumnName("cancelled_at");
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.RowVersion).HasColumnName("row_version").IsRowVersion();

        b.HasIndex(x => new { x.UserId, x.Status, x.ExpiresAt });
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.Conversation).WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
    }
}
