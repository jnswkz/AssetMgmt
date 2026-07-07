using AssetMgmt.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssetMgmt.Infrastructure.Persistence.Configurations;

public class InventoryScanItemConfiguration : IEntityTypeConfiguration<InventoryScanItem>
{
    public void Configure(EntityTypeBuilder<InventoryScanItem> b)
    {
        b.ToTable("inventory_scan_items", "asset");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("NEWSEQUENTIALID()");
        b.Property(x => x.InventoryScanId).HasColumnName("inventory_scan_id");
        b.Property(x => x.AssetInstanceId).HasColumnName("asset_instance_id");
        b.Property(x => x.AssetCode).HasColumnName("asset_code").HasMaxLength(50);
        b.Property(x => x.Result).HasColumnName("result").HasMaxLength(20).HasConversion<string>();
        b.Property(x => x.ScannedAt).HasColumnName("scanned_at");
        b.HasIndex(x => new { x.InventoryScanId, x.AssetCode }).IsUnique();
        b.HasOne(x => x.InventoryScan).WithMany(x => x.Items).HasForeignKey(x => x.InventoryScanId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.AssetInstance).WithMany().HasForeignKey(x => x.AssetInstanceId).OnDelete(DeleteBehavior.NoAction);
    }
}
