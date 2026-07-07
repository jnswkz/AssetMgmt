using AssetMgmt.Domain.Enums;

namespace AssetMgmt.Application.Depreciation;

public record DepreciationPolicyDto(
    Guid Id,
    Guid AssetModelId,
    DepreciationMethod Method,
    int UsefulLifeMonths,
    decimal? AnnualDeclineRate,
    decimal SalvageValuePercent,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo);

public record PutDepreciationPolicyRequest(
    DepreciationMethod Method,
    int UsefulLifeMonths,
    decimal? AnnualDeclineRate,
    decimal SalvageValuePercent,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo);

public record AssetDepreciationDto(
    Guid AssetInstanceId,
    string AssetCode,
    decimal AcquisitionCost,
    decimal SalvageValue,
    decimal BookValue,
    decimal AccumulatedDepreciation,
    DateTime AsOfDate,
    bool IsLedgerValue,
    int RemainingUsefulLifeMonths,
    bool FullyDepreciated,
    bool NearEndOfLife,
    bool NeedsUpgrade,
    DepreciationPolicyDto Policy);

public record DepreciationAlertItem(
    Guid AssetInstanceId,
    string AssetCode,
    string ModelName,
    string? HolderName,
    Guid? DepartmentId,
    decimal BookValue,
    int RemainingUsefulLifeMonths,
    bool FullyDepreciated,
    bool NearEndOfLife,
    bool NeedsUpgrade);
