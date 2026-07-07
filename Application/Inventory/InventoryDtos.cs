using AssetMgmt.Domain.Enums;

namespace AssetMgmt.Application.Inventory;

public record CreateInventoryScanRequest(Guid? DepartmentId);
public record AddInventoryScanItemRequest(string AssetCode);

public record InventoryScanItemDto(
    Guid Id,
    Guid? AssetInstanceId,
    string AssetCode,
    InventoryScanResult Result,
    DateTime ScannedAt);

public record InventoryScanDto(
    Guid Id,
    Guid? DepartmentId,
    string? DepartmentName,
    InventoryScanStatus Status,
    DateTime StartedAt,
    DateTime? ClosedAt,
    int Found,
    int Missing,
    int Unexpected,
    IReadOnlyList<InventoryScanItemDto> Items);
