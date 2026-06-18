using AssetMgmt.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssetMgmt.Infrastructure.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("audit_logs", "audit");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("NEWSEQUENTIALID()");
        b.Property(x => x.UserId).HasColumnName("user_id");
        b.Property(x => x.Action).HasColumnName("action").HasMaxLength(100).IsRequired();
        b.Property(x => x.EntityType).HasColumnName("entity_type").HasMaxLength(100);
        b.Property(x => x.EntityId).HasColumnName("entity_id");
        b.Property(x => x.Metadata).HasColumnName("metadata");
        b.Property(x => x.IpAddress).HasColumnName("ip_address").HasMaxLength(50);
        b.Property(x => x.UserAgent).HasColumnName("user_agent").HasMaxLength(500);
        b.Property(x => x.CorrelationId).HasColumnName("correlation_id");
        b.Property(x => x.Severity).HasColumnName("severity").HasMaxLength(20).HasDefaultValue("Info");
        b.Property(x => x.Result).HasColumnName("result").HasMaxLength(20).HasDefaultValue("Success");
        b.Property(x => x.ErrorMessage).HasColumnName("error_message");
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()");

        b.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        // Computed columns are managed by the DB (database-init.sql); ignore for inserts/updates.
        b.Ignore("metadata_action");
        b.Ignore("metadata_asset_id");
    }
}
