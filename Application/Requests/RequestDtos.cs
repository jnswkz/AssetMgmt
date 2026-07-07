using AssetMgmt.Domain.Enums;

namespace AssetMgmt.Application.Requests;

public record CreateRequestDto(
    Guid AssetInstanceId,
    string? Reason,
    int? ExpectedDurationMonths,
    string IdempotencyKey);

public record RejectRequestDto(string Reason);

public record RequestListItem(
    Guid Id,
    Guid RequesterId,
    string RequesterName,
    Guid AssetInstanceId,
    string AssetCode,
    string ModelName,
    RequestStatus Status,
    int? ExpectedDurationMonths,
    DateTime? LockExpiresAt,
    DateTime HandoverDueAt,
    DateTime CreatedAt);

public record AllocationRequestDto(
    Guid Id,
    Guid RequesterId,
    string RequesterName,
    Guid AssetInstanceId,
    string AssetCode,
    string ModelName,
    RequestStatus Status,
    string? Reason,
    int? ExpectedDurationMonths,
    Guid? ApproverId,
    string? ApproverName,
    DateTime? ApprovedAt,
    string? RejectedReason,
    DateTime? LockExpiresAt,
    DateTime HandoverDueAt,
    DateTime CreatedAt,
    DateTime UpdatedAt);
