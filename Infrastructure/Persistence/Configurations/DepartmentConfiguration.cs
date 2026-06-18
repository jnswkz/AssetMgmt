using AssetMgmt.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssetMgmt.Infrastructure.Persistence.Configurations;

public class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> b)
    {
        b.ToTable("departments", "ref");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("NEWSEQUENTIALID()");
        b.Property(x => x.Code).HasColumnName("code").HasMaxLength(50).IsRequired();
        b.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        b.Property(x => x.ManagerId).HasColumnName("manager_id");
        b.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.DeletedAt).HasColumnName("deleted_at");

        b.HasIndex(x => x.Code).IsUnique();

        b.HasOne(x => x.Manager)
            .WithMany()
            .HasForeignKey(x => x.ManagerId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasQueryFilter(x => x.DeletedAt == null);
    }
}
