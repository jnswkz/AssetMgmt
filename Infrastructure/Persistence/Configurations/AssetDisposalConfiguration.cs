using AssetMgmt.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssetMgmt.Infrastructure.Persistence.Configurations;

public class AssetDisposalConfiguration : IEntityTypeConfiguration<AssetDisposal>
{
    public void Configure(EntityTypeBuilder<AssetDisposal> b)
    {
        b.ToTable("asset_disposals", "asset");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("NEWSEQUENTIALID()");
        b.Property(x => x.AssetInstanceId).HasColumnName("asset_instance_id");
        b.Property(x => x.DisposalType).HasColumnName("disposal_type").HasMaxLength(50).HasConversion<string>().IsRequired();
        b.Property(x => x.SoldToUserId).HasColumnName("sold_to_user_id");
        b.Property(x => x.SalePrice).HasColumnName("sale_price").HasColumnType("decimal(18,2)");
        b.Property(x => x.Reason).HasColumnName("reason");
        b.Property(x => x.DisposedAt).HasColumnName("disposed_at").HasColumnType("date");
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.CreatedBy).HasColumnName("created_by");

        b.HasOne(x => x.AssetInstance)
            .WithMany()
            .HasForeignKey(x => x.AssetInstanceId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasOne(x => x.SoldToUser)
            .WithMany()
            .HasForeignKey(x => x.SoldToUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
