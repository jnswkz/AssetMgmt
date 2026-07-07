using AssetMgmt.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssetMgmt.Infrastructure.Persistence.Configurations;

public class AllocationRequestConfiguration : IEntityTypeConfiguration<AllocationRequest>
{
    public void Configure(EntityTypeBuilder<AllocationRequest> b)
    {
        b.ToTable("allocation_requests", "asset", t => t.HasTrigger("trg_allocation_requests_updated_at"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("NEWSEQUENTIALID()");
        b.Property(x => x.RequesterId).HasColumnName("requester_id");
        b.Property(x => x.AssetInstanceId).HasColumnName("asset_instance_id");
        b.Property(x => x.Status).HasColumnName("status").HasMaxLength(50).HasConversion<string>().HasDefaultValue(Domain.Enums.RequestStatus.Pending);
        b.Property(x => x.Reason).HasColumnName("reason");
        b.Property(x => x.ExpectedDurationMonths).HasColumnName("expected_duration_months");
        b.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(100).IsRequired();
        b.Property(x => x.LockToken).HasColumnName("lock_token").HasMaxLength(100);
        b.Property(x => x.LockExpiresAt).HasColumnName("lock_expires_at");
        b.Property(x => x.HandoverDueAt).HasColumnName("handover_due_at");
        b.Property(x => x.ApproverId).HasColumnName("approver_id");
        b.Property(x => x.ApprovedAt).HasColumnName("approved_at");
        b.Property(x => x.RejectedReason).HasColumnName("rejected_reason");
        b.Property(x => x.ExpiredAt).HasColumnName("expired_at");
        b.Property(x => x.CancelledAt).HasColumnName("cancelled_at");
        b.Property(x => x.CancellationReason).HasColumnName("cancellation_reason");

        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.RowVersion).HasColumnName("row_version").IsRowVersion();

        b.HasIndex(x => x.IdempotencyKey).IsUnique();

        b.HasOne(x => x.Requester)
            .WithMany()
            .HasForeignKey(x => x.RequesterId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasOne(x => x.AssetInstance)
            .WithMany()
            .HasForeignKey(x => x.AssetInstanceId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasOne(x => x.Approver)
            .WithMany()
            .HasForeignKey(x => x.ApproverId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
