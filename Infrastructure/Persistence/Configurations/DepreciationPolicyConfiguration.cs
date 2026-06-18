using AssetMgmt.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssetMgmt.Infrastructure.Persistence.Configurations;

public class DepreciationPolicyConfiguration : IEntityTypeConfiguration<DepreciationPolicy>
{
    public void Configure(EntityTypeBuilder<DepreciationPolicy> b)
    {
        b.ToTable("depreciation_policies", "asset");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("NEWSEQUENTIALID()");
        b.Property(x => x.AssetModelId).HasColumnName("asset_model_id");
        b.Property(x => x.Method).HasColumnName("method").HasMaxLength(50).HasConversion<string>().IsRequired();
        b.Property(x => x.UsefulLifeMonths).HasColumnName("useful_life_months");
        b.Property(x => x.AnnualDeclineRate).HasColumnName("annual_decline_rate").HasColumnType("decimal(5,4)");
        b.Property(x => x.SalvageValuePercent).HasColumnName("salvage_value_percent").HasColumnType("decimal(5,2)").HasDefaultValue(0m);
        b.Property(x => x.EffectiveFrom).HasColumnName("effective_from").HasColumnType("date");
        b.Property(x => x.EffectiveTo).HasColumnName("effective_to").HasColumnType("date");
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("SYSUTCDATETIME()");

        b.HasIndex(x => x.AssetModelId).IsUnique();

        b.HasOne(x => x.AssetModel)
            .WithOne(m => m.DepreciationPolicy)
            .HasForeignKey<DepreciationPolicy>(x => x.AssetModelId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
