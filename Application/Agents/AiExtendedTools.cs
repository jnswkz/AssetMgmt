using AssetMgmt.Domain.Enums;
using AssetMgmt.Domain.Exceptions;

namespace AssetMgmt.Application.Agents;

public class ListAssetsTool : IAiToolHandler
{
    private readonly AiOperationsService _operations;

    public ListAssetsTool(AiOperationsService operations)
    {
        _operations = operations;
    }

    public string ToolName => AiToolNames.ListAssets;

    public async Task<AiToolExecutionResult> ExecuteAsync(AiToolExecutionContext context, CancellationToken ct)
    {
        var payload = AiJson.Deserialize<ListAssetsPayload>(context.Decision.Arguments)
            ?? new ListAssetsPayload("available", null, null, null, null, null, 5);
        var displayPayload = payload with { PageSize = Math.Min(payload.PageSize ?? 5, 5) };

        var assets = await _operations.ListAssetsAsync(displayPayload, ct);
        var scope = NormalizeScope(payload.Scope, context.UserRole);

        var answer = assets.Count == 0
            ? scope switch
            {
                "assigned_to_me" => "Mình chưa tìm thấy thiết bị phù hợp trong danh sách của bạn.",
                "department" => "Mình chưa tìm thấy thiết bị phù hợp trong phòng ban.",
                "all" => "Mình chưa tìm thấy thiết bị phù hợp.",
                _ => "Hiện chưa có thiết bị sẵn sàng theo tiêu chí này."
            }
            : BuildAssetAnswer(scope, assets);

        return new AiToolExecutionResult(
            AiIntents.ListAssets,
            ToolName,
            AiJson.ToElement(displayPayload with { Scope = scope }),
            answer,
            [
                "Xem các dòng thiết bị phù hợp",
                "Tạo yêu cầu cấp phát"
            ],
            [],
            true,
            false);
    }

    private static string NormalizeScope(string? scope, string userRole)
    {
        var normalized = string.IsNullOrWhiteSpace(scope) ? "available" : scope.Trim().ToLowerInvariant();
        if (string.Equals(userRole, nameof(UserRole.Employee), StringComparison.Ordinal) &&
            normalized is "all" or "department")
        {
            return "available";
        }

        return normalized;
    }

    private static string BuildAssetAnswer(string scope, IReadOnlyList<AiAssetCatalogItem> assets)
    {
        var title = scope switch
        {
            "assigned_to_me" => $"Bạn có {assets.Count} thiết bị phù hợp:",
            "department" => $"Có {assets.Count} thiết bị phù hợp trong phòng ban:",
            "all" => $"Có {assets.Count} thiết bị phù hợp:",
            _ => $"Có {assets.Count} thiết bị sẵn sàng cấp phát:"
        };
        return title + "\n" + string.Join("\n", assets.Select(asset => FormatAsset(asset, scope)));
    }

    private static string FormatAsset(AiAssetCatalogItem asset, string scope)
    {
        var details = new List<string> { asset.ModelName };
        if (scope is "department" or "all") details.Add(StatusLabel(asset.Status));
        if (!string.IsNullOrWhiteSpace(asset.Location)) details.Add(asset.Location);
        return $"• {asset.AssetCode} — {string.Join(" · ", details)}";
    }

    private static string StatusLabel(AssetStatus status) => status switch
    {
        AssetStatus.InStock => "Sẵn sàng",
        AssetStatus.Allocated => "Đang sử dụng",
        AssetStatus.LockedTemp => "Đang được giữ",
        AssetStatus.Maintenance => "Đang bảo trì",
        _ => status.ToString()
    };
}

public class ListAssetModelsTool : IAiToolHandler
{
    private readonly AiOperationsService _operations;

    public ListAssetModelsTool(AiOperationsService operations)
    {
        _operations = operations;
    }

    public string ToolName => AiToolNames.ListAssetModels;

    public async Task<AiToolExecutionResult> ExecuteAsync(AiToolExecutionContext context, CancellationToken ct)
    {
        var payload = AiJson.Deserialize<ListAssetModelsPayload>(context.Decision.Arguments)
            ?? new ListAssetModelsPayload("available", null, null, null, null, 5);
        var displayPayload = payload with { PageSize = Math.Min(payload.PageSize ?? 5, 5) };

        var models = await _operations.ListAssetModelsAsync(displayPayload, ct);
        var scope = NormalizeScope(payload.Scope, context.UserRole, payload.AvailableOnly == true);

        var answer = models.Count == 0
            ? scope switch
            {
                "all" => "Mình chưa tìm thấy dòng thiết bị phù hợp.",
                "department" => "Mình chưa tìm thấy dòng thiết bị phù hợp trong phòng ban.",
                _ => "Hiện chưa có dòng thiết bị sẵn sàng theo tiêu chí này."
            }
            : $"Có {models.Count} dòng thiết bị phù hợp:\n" + string.Join("\n", models.Select(FormatModel));

        return new AiToolExecutionResult(
            AiIntents.ListAssetModels,
            ToolName,
            AiJson.ToElement(displayPayload with { Scope = scope }),
            answer,
            [
                "Xem thiết bị đang sẵn sàng",
                "Tạo yêu cầu cấp phát theo dòng này"
            ],
            [],
            true,
            false);
    }

    private static string NormalizeScope(string? scope, string userRole, bool availableOnly)
    {
        if (availableOnly)
            return "available";

        var normalized = string.IsNullOrWhiteSpace(scope) ? "available" : scope.Trim().ToLowerInvariant();
        if (string.Equals(userRole, nameof(UserRole.Employee), StringComparison.Ordinal) &&
            normalized is "all" or "department")
        {
            return "available";
        }

        return normalized;
    }

    private static string FormatModel(AiAssetModelCatalogItem model)
    {
        var availability = model.InStockCount > 0
            ? $"{model.InStockCount} thiết bị sẵn sàng"
            : "Chưa có thiết bị sẵn sàng";
        return $"• {model.Name} — {availability}";
    }
}

public class CreateAllocationRequestTool : IAiToolHandler
{
    private readonly AiOperationsService _operations;

    public CreateAllocationRequestTool(AiOperationsService operations)
    {
        _operations = operations;
    }

    public string ToolName => AiToolNames.CreateAllocationRequest;

    public async Task<AiToolExecutionResult> ExecuteAsync(AiToolExecutionContext context, CancellationToken ct)
    {
        var payload = AiJson.Deserialize<CreateAllocationRequestPayload>(context.Decision.Arguments)
            ?? new CreateAllocationRequestPayload(null, null, null, null, null, null, null, null);

        try
        {
            var created = await _operations.CreateAllocationRequestFromNeedAsync(payload, context.ConversationId, ct);

            var answer =
                $"Đã gửi yêu cầu cấp phát {created.SelectedAsset.AssetCode} — {created.SelectedAsset.ModelName}.\n"
                + (created.Request.LockExpiresAt is null
                    ? "Yêu cầu đang chờ duyệt."
                    : $"Thiết bị được giữ đến {created.Request.LockExpiresAt:dd/MM HH:mm} UTC trong khi chờ duyệt.");

            return new AiToolExecutionResult(
                AiIntents.CreateAllocationRequest,
                ToolName,
                AiJson.ToElement(new
                {
                    requestId = created.Request.Id,
                    assetId = created.Request.AssetInstanceId,
                    assetCode = created.Request.AssetCode,
                    modelName = created.Request.ModelName,
                    lockExpiresAt = created.Request.LockExpiresAt,
                    reason = created.Request.Reason,
                    expectedDurationMonths = created.Request.ExpectedDurationMonths
                }),
                answer,
                [
                "Xem thêm thiết bị đang sẵn sàng",
                    "Điều chỉnh nhu cầu rồi tạo request khác"
                ],
                [],
                true,
                false);
        }
        catch (ConflictException ex)
        {
            return AiToolResultFactory.Clarification(ex.Message);
        }
        catch (DomainException ex)
        {
            return AiToolResultFactory.Clarification(ex.Message);
        }
    }
}

public class ListPendingRequestsTool : IAiToolHandler
{
    private readonly AiOperationsService _operations;

    public ListPendingRequestsTool(AiOperationsService operations)
    {
        _operations = operations;
    }

    public string ToolName => AiToolNames.ListPendingRequests;

    public async Task<AiToolExecutionResult> ExecuteAsync(AiToolExecutionContext context, CancellationToken ct)
    {
        var payload = AiJson.Deserialize<ListPendingRequestsPayload>(context.Decision.Arguments)
            ?? new ListPendingRequestsPayload(null, 5);
        payload = payload with { PageSize = Math.Min(payload.PageSize ?? 5, 5) };

        try
        {
            var requests = await _operations.ListPendingRequestsAsync(payload, ct);
            var answer = requests.Count == 0
                ? "Hiện không có request chờ duyệt nào phù hợp."
                : $"Có {requests.Count} yêu cầu đang chờ duyệt:\n"
                  + string.Join("\n", requests.Select(r =>
                      $"• {r.IdShort} — {r.RequesterName} · {r.AssetCode} ({r.ModelName})"));

            return new AiToolExecutionResult(
                AiIntents.ListPendingRequests,
                ToolName,
                AiJson.ToElement(payload),
                answer,
                BuildPendingRequestActions(requests),
                [],
                true,
                false);
        }
        catch (DomainException)
        {
            return AiToolResultFactory.PermissionDenied(AiIntents.ListPendingRequests, ToolName, context.Decision.Arguments);
        }
    }

    private static IReadOnlyList<string> BuildPendingRequestActions(IReadOnlyList<AiPendingRequestSummary> requests)
    {
        if (requests.Count == 0)
            return ["Xem lại request đang chờ duyệt"];

        var actions = new List<string>();
        foreach (var request in requests.Take(3))
        {
            actions.Add($"Duyệt request {request.IdShort}");
            actions.Add($"Từ chối request {request.IdShort} vì không phù hợp");
        }

        return actions;
    }
}

public class ApproveAllocationRequestTool : IAiToolHandler
{
    private readonly AiOperationsService _operations;

    public ApproveAllocationRequestTool(AiOperationsService operations)
    {
        _operations = operations;
    }

    public string ToolName => AiToolNames.ApproveAllocationRequest;

    public async Task<AiToolExecutionResult> ExecuteAsync(AiToolExecutionContext context, CancellationToken ct)
    {
        var payload = AiJson.Deserialize<ApproveAllocationRequestPayload>(context.Decision.Arguments)
            ?? new ApproveAllocationRequestPayload(null, null);

        try
        {
            var request = await _operations.ApprovePendingRequestAsync(payload, ct);
            var answer =
                $"Tôi đã duyệt request {request.Id} cho {request.RequesterName}. "
                + $"Asset {request.AssetCode} ({request.ModelName}) đã được cấp phát.";

            return new AiToolExecutionResult(
                AiIntents.ApproveAllocationRequest,
                ToolName,
                AiJson.ToElement(new
                {
                    requestId = request.Id,
                    assetId = request.AssetInstanceId,
                    assetCode = request.AssetCode,
                    requesterId = request.RequesterId,
                    requesterName = request.RequesterName,
                    approvedAt = request.ApprovedAt
                }),
                answer,
                ["Xem tiếp các request đang chờ duyệt"],
                [],
                true,
                false);
        }
        catch (DomainException ex) when (IsPermissionDenied(ex))
        {
            return AiToolResultFactory.PermissionDenied(AiIntents.ApproveAllocationRequest, ToolName, context.Decision.Arguments);
        }
        catch (DomainException ex)
        {
            return AiToolResultFactory.Clarification(ex.Message);
        }
        catch (ConflictException ex)
        {
            return AiToolResultFactory.Clarification(ex.Message);
        }
    }

    private static bool IsPermissionDenied(DomainException ex) =>
        ex.Message.Contains("không có quyền", StringComparison.OrdinalIgnoreCase);
}

public class RejectAllocationRequestTool : IAiToolHandler
{
    private readonly AiOperationsService _operations;

    public RejectAllocationRequestTool(AiOperationsService operations)
    {
        _operations = operations;
    }

    public string ToolName => AiToolNames.RejectAllocationRequest;

    public async Task<AiToolExecutionResult> ExecuteAsync(AiToolExecutionContext context, CancellationToken ct)
    {
        var payload = AiJson.Deserialize<RejectAllocationRequestPayload>(context.Decision.Arguments)
            ?? new RejectAllocationRequestPayload(null, null, null);

        try
        {
            var request = await _operations.RejectPendingRequestAsync(payload, ct);
            var answer =
                $"Tôi đã từ chối request {request.Id} của {request.RequesterName} cho asset {request.AssetCode} ({request.ModelName}). "
                + $"Lý do: {request.RejectedReason}.";

            return new AiToolExecutionResult(
                AiIntents.RejectAllocationRequest,
                ToolName,
                AiJson.ToElement(new
                {
                    requestId = request.Id,
                    assetId = request.AssetInstanceId,
                    assetCode = request.AssetCode,
                    requesterId = request.RequesterId,
                    requesterName = request.RequesterName,
                    rejectedReason = request.RejectedReason
                }),
                answer,
                ["Xem tiếp các request đang chờ duyệt"],
                [],
                true,
                false);
        }
        catch (DomainException ex) when (IsPermissionDenied(ex))
        {
            return AiToolResultFactory.PermissionDenied(AiIntents.RejectAllocationRequest, ToolName, context.Decision.Arguments);
        }
        catch (DomainException ex)
        {
            return AiToolResultFactory.Clarification(ex.Message);
        }
        catch (ConflictException ex)
        {
            return AiToolResultFactory.Clarification(ex.Message);
        }
    }

    private static bool IsPermissionDenied(DomainException ex) =>
        ex.Message.Contains("không có quyền", StringComparison.OrdinalIgnoreCase);
}
