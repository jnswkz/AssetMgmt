using AssetMgmt.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssetMgmt.Infrastructure.Persistence.Configurations;

public class AllocationConfiguration : IEntityTypeConfiguration<Allocation>
{
    public void Configure(EntityTypeBuilder<Allocation> b)
    {
        b.ToTable("allocations", "asset", t => t.HasTrigger("trg_allocations_no_modify"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("NEWSEQUENTIALID()");
        b.Property(x => x.AssetInstanceId).HasColumnName("asset_instance_id");
        b.Property(x => x.UserId).HasColumnName("user_id");
        b.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(50).HasConversion<string>().IsRequired();
        b.Property(x => x.StartDate).HasColumnName("start_date").HasColumnType("date");
        b.Property(x => x.EndDate).HasColumnName("end_date").HasColumnType("date");
        b.Property(x => x.FromUserId).HasColumnName("from_user_id");
        b.Property(x => x.ToUserId).HasColumnName("to_user_id");
        b.Property(x => x.AllocationRequestId).HasColumnName("allocation_request_id");
        b.Property(x => x.HandoverDocId).HasColumnName("handover_doc_id");
        b.Property(x => x.Notes).HasColumnName("notes");

        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.CreatedBy).HasColumnName("created_by");

        b.HasOne(x => x.AssetInstance)
            .WithMany()
            .HasForeignKey(x => x.AssetInstanceId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasOne(x => x.FromUser)
            .WithMany()
            .HasForeignKey(x => x.FromUserId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasOne(x => x.ToUser)
            .WithMany()
            .HasForeignKey(x => x.ToUserId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasOne(x => x.AllocationRequest)
            .WithMany()
            .HasForeignKey(x => x.AllocationRequestId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasOne(x => x.HandoverDocument)
            .WithMany()
            .HasForeignKey(x => x.HandoverDocId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
