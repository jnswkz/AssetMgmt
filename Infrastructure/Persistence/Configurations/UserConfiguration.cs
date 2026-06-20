using AssetMgmt.Domain.Entities;
using AssetMgmt.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssetMgmt.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users", "asset", t => t.HasTrigger("trg_users_updated_at"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("NEWSEQUENTIALID()");

        b.Property(x => x.UserName).HasColumnName("user_name").HasMaxLength(100).IsRequired();
        b.Property(x => x.NormalizedUserName).HasColumnName("normalized_user_name").HasMaxLength(100).IsRequired();
        b.Property(x => x.Email).HasColumnName("email").HasMaxLength(255).IsRequired();
        b.Property(x => x.NormalizedEmail).HasColumnName("normalized_email").HasMaxLength(255).IsRequired();
        b.Property(x => x.EmailConfirmed).HasColumnName("email_confirmed").HasDefaultValue(false);
        b.Property(x => x.PasswordHash).HasColumnName("password_hash").HasMaxLength(500).IsRequired();
        b.Property(x => x.SecurityStamp).HasColumnName("security_stamp").HasMaxLength(500);
        b.Property(x => x.PhoneNumber).HasColumnName("phone_number").HasMaxLength(20);
        b.Property(x => x.PhoneNumberConfirmed).HasColumnName("phone_number_confirmed").HasDefaultValue(false);
        b.Property(x => x.TwoFactorEnabled).HasColumnName("two_factor_enabled").HasDefaultValue(false);
        b.Property(x => x.LockoutEnd).HasColumnName("lockout_end");
        b.Property(x => x.LockoutEnabled).HasColumnName("lockout_enabled").HasDefaultValue(true);
        b.Property(x => x.AccessFailedCount).HasColumnName("access_failed_count").HasDefaultValue(0);

        b.Property(x => x.EmployeeCode).HasColumnName("employee_code").HasMaxLength(50).IsRequired();
        b.Property(x => x.FullName).HasColumnName("full_name").HasMaxLength(200).IsRequired();
        b.Property(x => x.DepartmentId).HasColumnName("department_id");
        b.Property(x => x.Role).HasColumnName("role").HasMaxLength(50).HasConversion<string>().IsRequired();
        b.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        b.Property(x => x.LastLoginAt).HasColumnName("last_login_at");

        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.DeletedAt).HasColumnName("deleted_at");

        b.HasIndex(x => x.NormalizedUserName).IsUnique();
        b.HasIndex(x => x.NormalizedEmail).IsUnique();
        b.HasIndex(x => x.EmployeeCode).IsUnique();

        b.HasOne(x => x.Department)
            .WithMany(d => d.Users)
            .HasForeignKey(x => x.DepartmentId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasQueryFilter(x => x.DeletedAt == null);
    }
}
