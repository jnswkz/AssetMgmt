using AssetMgmt.Domain.Enums;

namespace AssetMgmt.Domain.Entities;

public class User
{
    public Guid Id { get; set; }

    public string UserName { get; set; } = null!;
    public string NormalizedUserName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string NormalizedEmail { get; set; } = null!;
    public bool EmailConfirmed { get; set; }
    public string PasswordHash { get; set; } = null!;
    public string? SecurityStamp { get; set; }
    public string? PhoneNumber { get; set; }
    public bool PhoneNumberConfirmed { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public DateTimeOffset? LockoutEnd { get; set; }
    public bool LockoutEnabled { get; set; } = true;
    public int AccessFailedCount { get; set; }

    public string EmployeeCode { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public Guid? DepartmentId { get; set; }
    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    public Department? Department { get; set; }
}
