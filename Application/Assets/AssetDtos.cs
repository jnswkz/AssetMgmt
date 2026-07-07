using AssetMgmt.Domain.Enums;

namespace AssetMgmt.Application.Assets;

// ---------- Asset Models ----------

public record AssetModelListItem(
    Guid Id,
    string Name,
    AssetCategory Category,
    string? Manufacturer,
    string? ModelNumber,
    int DefaultUsefulLifeMonths,
    int InstanceCount);

public record AssetModelDto(
    Guid Id,
    string Name,
    AssetCategory Category,
    string? Manufacturer,
    string? ModelNumber,
    string? Specs,
    string? SpecsJson,
    int DefaultUsefulLifeMonths,
    DepreciationMethod DefaultDepreciationMethod,
    string? ImageUrl,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record CreateAssetModelRequest(
    string Name,
    AssetCategory Category,
    string? Manufacturer,
    string? ModelNumber,
    string? Specs,
    int? DefaultUsefulLifeMonths,
    DepreciationMethod? DefaultDepreciationMethod,
    string? ImageUrl);

public record UpdateAssetModelRequest(
    string Name,
    AssetCategory Category,
    string? Manufacturer,
    string? ModelNumber,
    string? Specs,
    int DefaultUsefulLifeMonths,
    DepreciationMethod DefaultDepreciationMethod,
    string? ImageUrl);

// ---------- Asset Instances ----------

public record AssetInstanceListItem(
    Guid Id,
    string AssetCode,
    string Serial,
    Guid ModelId,
    string ModelName,
    AssetStatus Status,
    Guid? CurrentHolderId,
    string? CurrentHolderName,
    string? Location,
    string? QrCodePath,
    string? QrCodeUrl);

public record AvailableAssetItem(
    Guid Id,
    string AssetCode,
    Guid ModelId,
    string ModelName,
    AssetCategory Category,
    string? SpecsSummary,
    string? Location);

public record AssetInstanceDto(
    Guid Id,
    string AssetCode,
    string Serial,
    Guid ModelId,
    string ModelName,
    AssetStatus Status,
    Guid? CurrentHolderId,
    string? CurrentHolderName,
    decimal AcquisitionCost,
    DateTime AcquisitionDate,
    decimal SalvageValue,
    string? Location,
    DateTime? WarrantyExpiresAt,
    string? QrCodePath,
    string? QrCodeUrl,
    string? Notes,
    int Version,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record CreateAssetInstanceRequest(
    Guid ModelId,
    string Serial,
    decimal AcquisitionCost,
    DateTime AcquisitionDate,
    decimal? SalvageValue,
    string? Location,
    DateTime? WarrantyExpiresAt,
    string? Notes);

public record UpdateAssetInstanceRequest(
    string Serial,
    decimal AcquisitionCost,
    DateTime AcquisitionDate,
    decimal SalvageValue,
    string? Location,
    DateTime? WarrantyExpiresAt,
    string? Notes);
