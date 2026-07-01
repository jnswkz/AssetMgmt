using AssetMgmt.Domain.Enums;

namespace AssetMgmt.Application.Reports;

/// <summary>Aggregate KPIs for the admin/manager dashboard.</summary>
public record DashboardStatsDto(
    int TotalAssets,
    int InStock,
    int Allocated,
    int LockedTemp,
    int Maintenance,
    int EndOfLife,               // Retired + Lost + Disposed
    int PendingRequests,
    decimal TotalAcquisitionCost,
    IReadOnlyList<StatusCount> ByStatus,
    IReadOnlyList<CategoryCount> ByCategory);

public record StatusCount(AssetStatus Status, int Count);

public record CategoryCount(AssetCategory Category, int Count);

/// <summary>An in-stock asset that has sat unused beyond the idle threshold.</summary>
public record IdleAssetItem(
    Guid AssetInstanceId,
    string AssetCode,
    string ModelName,
    AssetCategory Category,
    AssetStatus Status,
    string? Location,
    decimal AcquisitionCost,
    DateTime AcquisitionDate,
    DateTime? LastActivityAt,
    int IdleMonths);
