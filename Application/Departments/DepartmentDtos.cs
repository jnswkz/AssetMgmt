namespace AssetMgmt.Application.Departments;

public record DepartmentListItem(
    Guid Id,
    string Code,
    string Name,
    Guid? ManagerId,
    string? ManagerName,
    bool IsActive,
    int UserCount);

public record DepartmentDto(
    Guid Id,
    string Code,
    string Name,
    Guid? ManagerId,
    string? ManagerName,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record CreateDepartmentRequest(string Code, string Name, Guid? ManagerId);

public record UpdateDepartmentRequest(string Name, Guid? ManagerId, bool IsActive);

public record AssignManagerRequest(Guid ManagerId);
