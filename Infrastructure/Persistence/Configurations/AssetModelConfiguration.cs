using AssetMgmt.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssetMgmt.Infrastructure.Persistence.Configurations;

public class AssetModelConfiguration : IEntityTypeConfiguration<AssetModel>
{
    public void Configure(EntityTypeBuilder<AssetModel> b)
    {
        b.ToTable("asset_models", "asset", t => t.HasTrigger("trg_asset_models_updated_at"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("NEWSEQUENTIALID()");
        b.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        b.Property(x => x.Category).HasColumnName("category").HasMaxLength(100).HasConversion<string>().IsRequired();
        b.Property(x => x.Manufacturer).HasColumnName("manufacturer").HasMaxLength(200);
        b.Property(x => x.ModelNumber).HasColumnName("model_number").HasMaxLength(100);
        b.Property(x => x.Specs).HasColumnName("specs");
        b.Property(x => x.DefaultUsefulLifeMonths).HasColumnName("default_useful_life_months").HasDefaultValue(36);
        b.Property(x => x.DefaultDepreciationMethod).HasColumnName("default_depreciation_method").HasMaxLength(50).HasConversion<string>().HasDefaultValue(Domain.Enums.DepreciationMethod.StraightLine);
        b.Property(x => x.ImageUrl).HasColumnName("image_url").HasMaxLength(500);

        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.CreatedBy).HasColumnName("created_by");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.UpdatedBy).HasColumnName("updated_by");
        b.Property(x => x.DeletedAt).HasColumnName("deleted_at");

        b.HasQueryFilter(x => x.DeletedAt == null);
    }
}
