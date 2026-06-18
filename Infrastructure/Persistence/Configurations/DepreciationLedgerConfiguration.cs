using AssetMgmt.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssetMgmt.Infrastructure.Persistence.Configurations;

public class DepreciationLedgerConfiguration : IEntityTypeConfiguration<DepreciationLedger>
{
    public void Configure(EntityTypeBuilder<DepreciationLedger> b)
    {
        b.ToTable("depreciation_ledger", "asset");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("NEWSEQUENTIALID()");
        b.Property(x => x.AssetInstanceId).HasColumnName("asset_instance_id");
        b.Property(x => x.PeriodDate).HasColumnName("period_date").HasColumnType("date");
        b.Property(x => x.OpeningBookValue).HasColumnName("opening_book_value").HasColumnType("decimal(18,2)");
        b.Property(x => x.PeriodDepreciation).HasColumnName("period_depreciation").HasColumnType("decimal(18,2)");
        b.Property(x => x.AccumulatedDepreciation).HasColumnName("accumulated_depreciation").HasColumnType("decimal(18,2)");
        b.Property(x => x.ClosingBookValue).HasColumnName("closing_book_value").HasColumnType("decimal(18,2)");
        b.Property(x => x.PolicyId).HasColumnName("policy_id");
        b.Property(x => x.PostedAt).HasColumnName("posted_at").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.PostedBy).HasColumnName("posted_by");
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()");

        b.HasIndex(x => new { x.AssetInstanceId, x.PeriodDate }).IsUnique();

        b.HasOne(x => x.AssetInstance)
            .WithMany()
            .HasForeignKey(x => x.AssetInstanceId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasOne(x => x.Policy)
            .WithMany()
            .HasForeignKey(x => x.PolicyId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
