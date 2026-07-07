using AssetMgmt.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssetMgmt.Infrastructure.Persistence.Configurations;

public class ReturnObligationConfiguration : IEntityTypeConfiguration<ReturnObligation>
{
    public void Configure(EntityTypeBuilder<ReturnObligation> b)
    {
        b.ToTable("return_obligations", "asset");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("NEWSEQUENTIALID()");
        b.Property(x => x.UserId).HasColumnName("user_id");
        b.Property(x => x.AssetInstanceId).HasColumnName("asset_instance_id");
        b.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(50).HasConversion<string>();
        b.Property(x => x.DueAt).HasColumnName("due_at");
        b.Property(x => x.ResolvedAt).HasColumnName("resolved_at");
        b.Property(x => x.ResolvedBy).HasColumnName("resolved_by");
        b.Property(x => x.ResolutionNotes).HasColumnName("resolution_notes");
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()");
        b.HasIndex(x => new { x.UserId, x.AssetInstanceId }).HasFilter("[resolved_at] IS NULL").IsUnique();
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.AssetInstance).WithMany().HasForeignKey(x => x.AssetInstanceId).OnDelete(DeleteBehavior.NoAction);
    }
}
