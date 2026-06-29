using AssetMgmt.Domain.Enums;

namespace AssetMgmt.Application.Users;

public record UserListItem(
    Guid Id,
    string UserName,
    string Email,
    string FullName,
    string EmployeeCode,
    UserRole Role,
    Guid? DepartmentId,
    string? DepartmentName,
    bool IsActive);

public record UserDto(
    Guid Id,
    string UserName,
    string Email,
    string FullName,
    string EmployeeCode,
    UserRole Role,
    Guid? DepartmentId,
    string? DepartmentName,
    bool IsActive,
    DateTime? LastLoginAt,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record CreateUserRequest(
    string UserName,
    string Email,
    string Password,
    string FullName,
    string EmployeeCode,
    UserRole Role,
    Guid? DepartmentId);

public record UpdateUserRequest(
    string Email,
    string FullName,
    UserRole Role,
    Guid? DepartmentId,
    bool IsActive);

public record ResetPasswordRequest(string NewPassword);
