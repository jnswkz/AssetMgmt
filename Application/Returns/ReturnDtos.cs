using AssetMgmt.Domain.Enums;

namespace AssetMgmt.Application.Returns;

public record ReturnObligationDto(
    Guid Id,
    Guid UserId,
    string UserName,
    Guid AssetInstanceId,
    string AssetCode,
    string ModelName,
    ReturnObligationReason Reason,
    DateTime DueAt,
    DateTime? ResolvedAt,
    string? ResolutionNotes,
    DateTime CreatedAt);

public record ResolveReturnObligationRequest(string? Notes);
