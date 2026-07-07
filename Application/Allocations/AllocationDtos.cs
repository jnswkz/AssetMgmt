using AssetMgmt.Domain.Enums;

namespace AssetMgmt.Application.Allocations;

public record AllocationHistoryItem(
    Guid Id,
    Guid AssetInstanceId,
    string AssetCode,
    string ModelName,
    Guid UserId,
    string UserName,
    AllocationEventType EventType,
    DateTime StartDate,
    DateTime? EndDate,
    DateTime? ExpectedReturnAt,
    Guid? AllocationRequestId,
    string? Notes,
    DateTime CreatedAt);

public record MyAssetItem(
    Guid AssetInstanceId,
    string AssetCode,
    string ModelName,
    AssetStatus Status,
    string? Location,
    DateTime StartDate,
    DateTime? ExpectedReturnAt,
    Guid? AllocationRequestId,
    string? HandoverDocumentNumber,
    string? HandoverFilePath,
    string? HandoverFileUrl);

public record MyAssetHandoverFile(
    string DocumentNumber,
    string FilePath);
