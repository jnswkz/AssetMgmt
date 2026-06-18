namespace AssetMgmt.Domain.Entities;

public class HandoverDocument
{
    public Guid Id { get; set; }
    public string DocumentNumber { get; set; } = null!;
    public Guid AllocationId { get; set; }
    public string FilePath { get; set; } = null!;
    public long? FileSizeBytes { get; set; }
    public string? FileHashSha256 { get; set; }
    public DateTime GeneratedAt { get; set; }
    public Guid GeneratedBy { get; set; }
    public DateTime? SignedAt { get; set; }
    public bool SignedByEmployee { get; set; }
    public bool SignedByIt { get; set; }
    public string? EmployeeSignaturePath { get; set; }
    public string? ItSignaturePath { get; set; }

    public DateTime CreatedAt { get; set; }

    public Allocation Allocation { get; set; } = null!;
    public User Generator { get; set; } = null!;
}
