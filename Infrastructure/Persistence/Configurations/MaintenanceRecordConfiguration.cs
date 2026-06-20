using AssetMgmt.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssetMgmt.Infrastructure.Persistence.Configurations;

public class MaintenanceRecordConfiguration : IEntityTypeConfiguration<MaintenanceRecord>
{
    public void Configure(EntityTypeBuilder<MaintenanceRecord> b)
    {
        b.ToTable("maintenance_records", "asset", t => t.HasTrigger("trg_maintenance_records_updated_at"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("NEWSEQUENTIALID()");
        b.Property(x => x.AssetInstanceId).HasColumnName("asset_instance_id");
        b.Property(x => x.MaintenanceType).HasColumnName("maintenance_type").HasMaxLength(50).HasConversion<string>().IsRequired();
        b.Property(x => x.Description).HasColumnName("description").IsRequired();
        b.Property(x => x.Cost).HasColumnName("cost").HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        b.Property(x => x.Vendor).HasColumnName("vendor").HasMaxLength(200);
        b.Property(x => x.StartDate).HasColumnName("start_date").HasColumnType("date");
        b.Property(x => x.EndDate).HasColumnName("end_date").HasColumnType("date");
        b.Property(x => x.Status).HasColumnName("status").HasMaxLength(50).HasConversion<string>().HasDefaultValue(Domain.Enums.MaintenanceStatus.InProgress);
        b.Property(x => x.InvoicePath).HasColumnName("invoice_path").HasMaxLength(500);
        b.Property(x => x.Notes).HasColumnName("notes");
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.CreatedBy).HasColumnName("created_by");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("SYSUTCDATETIME()");

        b.HasOne(x => x.AssetInstance)
            .WithMany()
            .HasForeignKey(x => x.AssetInstanceId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
