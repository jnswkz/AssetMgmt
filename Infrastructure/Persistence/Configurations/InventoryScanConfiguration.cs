using AssetMgmt.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssetMgmt.Infrastructure.Persistence.Configurations;

public class InventoryScanConfiguration : IEntityTypeConfiguration<InventoryScan>
{
    public void Configure(EntityTypeBuilder<InventoryScan> b)
    {
        b.ToTable("inventory_scans", "asset");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("NEWSEQUENTIALID()");
        b.Property(x => x.DepartmentId).HasColumnName("department_id");
        b.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).HasConversion<string>();
        b.Property(x => x.StartedAt).HasColumnName("started_at");
        b.Property(x => x.ClosedAt).HasColumnName("closed_at");
        b.Property(x => x.CreatedBy).HasColumnName("created_by");
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()");
        b.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.NoAction);
    }
}
