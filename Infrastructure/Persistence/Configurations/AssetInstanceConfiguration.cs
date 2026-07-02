using AssetMgmt.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssetMgmt.Infrastructure.Persistence.Configurations;

public class AssetInstanceConfiguration : IEntityTypeConfiguration<AssetInstance>
{
    public void Configure(EntityTypeBuilder<AssetInstance> b)
    {
        b.ToTable("asset_instances", "asset", t => t.HasTrigger("trg_asset_instances_updated_at"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("NEWSEQUENTIALID()");
        b.Property(x => x.AssetCode).HasColumnName("asset_code").HasMaxLength(50).IsRequired();
        b.Property(x => x.Serial).HasColumnName("serial").HasMaxLength(100).IsRequired();
        b.Property(x => x.ModelId).HasColumnName("model_id");
        b.Property(x => x.Status).HasColumnName("status").HasMaxLength(50).HasConversion<string>().HasDefaultValue(Domain.Enums.AssetStatus.InStock);
        b.Property(x => x.CurrentHolderId).HasColumnName("current_holder_id");
        b.Property(x => x.AcquisitionCost).HasColumnName("acquisition_cost").HasColumnType("decimal(18,2)");
        b.Property(x => x.AcquisitionDate).HasColumnName("acquisition_date").HasColumnType("date");
        b.Property(x => x.SalvageValue).HasColumnName("salvage_value").HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        b.Property(x => x.Location).HasColumnName("location").HasMaxLength(200);
        b.Property(x => x.WarrantyExpiresAt).HasColumnName("warranty_expires_at").HasColumnType("date");
        b.Property(x => x.QrCodePath).HasColumnName("qr_code_path").HasMaxLength(500);
        b.Property(x => x.Notes).HasColumnName("notes");

        b.Property(x => x.LockExpiresAt).HasColumnName("lock_expires_at");
        b.Property(x => x.LockToken).HasColumnName("lock_token").HasMaxLength(100);
        b.Property(x => x.LockHolderUserId).HasColumnName("lock_holder_user_id");

        b.Property(x => x.Version).HasColumnName("version").HasDefaultValue(1);
        b.Property(x => x.RowVersion).HasColumnName("row_version").IsRowVersion();

        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.CreatedBy).HasColumnName("created_by");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.UpdatedBy).HasColumnName("updated_by");
        b.Property(x => x.DeletedAt).HasColumnName("deleted_at");

        b.HasIndex(x => x.AssetCode).IsUnique();
        b.HasIndex(x => x.Serial).IsUnique();

        b.HasOne(x => x.Model)
            .WithMany(m => m.Instances)
            .HasForeignKey(x => x.ModelId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasOne(x => x.CurrentHolder)
            .WithMany()
            .HasForeignKey(x => x.CurrentHolderId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasQueryFilter(x => x.DeletedAt == null);
    }
}
