using AssetMgmt.Domain.Enums;

namespace AssetMgmt.Application.Allocations;

// ---------- Lifecycle request DTOs ----------

public record ReturnAssetDto(string? Notes);

public record TransferAssetDto(Guid ToUserId, string? Notes);

public record StartMaintenanceDto(
    MaintenanceType Type,
    string Description,
    string? Vendor,
    decimal? Cost);

public record CompleteMaintenanceDto(decimal? Cost, string? Notes);

public record DisposeAssetDto(
    DisposalType Type,
    Guid? SoldToUserId,
    decimal? SalePrice,
    string? Reason);

// ---------- Response DTOs ----------

public record MaintenanceRecordDto(
    Guid Id,
    Guid AssetInstanceId,
    string AssetCode,
    MaintenanceType MaintenanceType,
    string Description,
    decimal Cost,
    string? Vendor,
    DateTime StartDate,
    DateTime? EndDate,
    MaintenanceStatus Status,
    string? Notes,
    DateTime CreatedAt);

public record DisposalDto(
    Guid Id,
    Guid AssetInstanceId,
    string AssetCode,
    DisposalType DisposalType,
    Guid? SoldToUserId,
    string? SoldToUserName,
    decimal? SalePrice,
    string? Reason,
    DateTime DisposedAt,
    DateTime CreatedAt);
