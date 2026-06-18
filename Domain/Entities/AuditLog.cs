namespace AssetMgmt.Domain.Entities;

public class AuditLog
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string Action { get; set; } = null!;
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public string? Metadata { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public Guid? CorrelationId { get; set; }
    public string Severity { get; set; } = "Info";
    public string Result { get; set; } = "Success";
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }

    public User? User { get; set; }
}
