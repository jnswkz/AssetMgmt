using AssetMgmt.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssetMgmt.Infrastructure.Persistence.Configurations;

public class HandoverDocumentConfiguration : IEntityTypeConfiguration<HandoverDocument>
{
    public void Configure(EntityTypeBuilder<HandoverDocument> b)
    {
        b.ToTable("handover_documents", "asset");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("NEWSEQUENTIALID()");
        b.Property(x => x.DocumentNumber).HasColumnName("document_number").HasMaxLength(50).IsRequired();
        b.Property(x => x.AllocationId).HasColumnName("allocation_id");
        b.Property(x => x.FilePath).HasColumnName("file_path").HasMaxLength(500).IsRequired();
        b.Property(x => x.FileSizeBytes).HasColumnName("file_size_bytes");
        b.Property(x => x.FileHashSha256).HasColumnName("file_hash_sha256").HasMaxLength(64);
        b.Property(x => x.GeneratedAt).HasColumnName("generated_at").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.GeneratedBy).HasColumnName("generated_by");
        b.Property(x => x.SignedAt).HasColumnName("signed_at");
        b.Property(x => x.SignedByEmployee).HasColumnName("signed_by_employee").HasDefaultValue(false);
        b.Property(x => x.SignedByIt).HasColumnName("signed_by_it").HasDefaultValue(false);
        b.Property(x => x.EmployeeSignaturePath).HasColumnName("employee_signature_path").HasMaxLength(500);
        b.Property(x => x.ItSignaturePath).HasColumnName("it_signature_path").HasMaxLength(500);

        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()");

        b.HasIndex(x => x.DocumentNumber).IsUnique();

        b.HasOne(x => x.Allocation)
            .WithMany()
            .HasForeignKey(x => x.AllocationId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasOne(x => x.Generator)
            .WithMany()
            .HasForeignKey(x => x.GeneratedBy)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
