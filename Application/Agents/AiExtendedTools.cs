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

        var assets = await _operations.ListAssetsAsync(payload, ct);
        var scope = NormalizeScope(payload.Scope, context.UserRole);

        var answer = assets.Count == 0
            ? scope switch
            {
                "assigned_to_me" => "Hiện tại bạn không có asset nào trong phạm vi đang hỏi.",
                "department" => "Hiện chưa có asset phù hợp trong phạm vi phòng ban đang quản lý.",
                "all" => "Hiện chưa tìm thấy asset phù hợp trong toàn bộ hệ thống.",
                _ => "Hiện chưa có asset trống phù hợp với tiêu chí bạn đưa ra."
            }
            : scope switch
            {
                "assigned_to_me" => "Các asset phù hợp trong phạm vi của bạn: " + string.Join("; ", assets.Select(FormatAsset)) + ".",
                "department" => "Các asset phù hợp trong phạm vi phòng ban: " + string.Join("; ", assets.Select(FormatAsset)) + ".",
                "all" => "Các asset phù hợp trong hệ thống: " + string.Join("; ", assets.Select(FormatAsset)) + ".",
                _ => "Các asset trống phù hợp: " + string.Join("; ", assets.Select(FormatAsset)) + "."
            };

        return new AiToolExecutionResult(
            AiIntents.ListAssets,
            ToolName,
            AiJson.ToElement(payload with { Scope = scope }),
            answer,
            [
                "Liệt kê các asset models phù hợp",
                "Tạo request cấp phát cho asset phù hợp"
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

    private static string FormatAsset(AiAssetCatalogItem asset)
    {
        var holder = string.IsNullOrWhiteSpace(asset.CurrentHolderName) ? null : $"người giữ {asset.CurrentHolderName}";
        var location = string.IsNullOrWhiteSpace(asset.Location) ? null : $"vị trí {asset.Location}";
        return string.Join(", ", new[]
        {
            $"{asset.AssetCode} ({asset.ModelName})",
            $"trạng thái {asset.Status}",
            holder,
            location
        }.Where(x => !string.IsNullOrWhiteSpace(x)));
    }
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

        var models = await _operations.ListAssetModelsAsync(payload, ct);
        var scope = NormalizeScope(payload.Scope, context.UserRole, payload.AvailableOnly == true);

        var answer = models.Count == 0
            ? scope switch
            {
                "all" => "Hiện chưa tìm thấy asset model phù hợp trong toàn bộ hệ thống.",
                "department" => "Hiện chưa tìm thấy asset model phù hợp trong phạm vi phòng ban.",
                _ => "Hiện chưa có asset model khả dụng phù hợp với tiêu chí bạn đưa ra."
            }
            : scope switch
            {
                "all" => "Các asset model phù hợp trong toàn công ty: " + string.Join("; ", models.Select(FormatModel)) + ".",
                "department" => "Các asset model phù hợp trong phòng ban: " + string.Join("; ", models.Select(FormatModel)) + ".",
                _ => "Các asset model khả dụng phù hợp: " + string.Join("; ", models.Select(FormatModel)) + "."
            };

        return new AiToolExecutionResult(
            AiIntents.ListAssetModels,
            ToolName,
            AiJson.ToElement(payload with { Scope = scope }),
            answer,
            [
                "Liệt kê các asset trống thuộc model này",
                "Tạo request cấp phát theo model này"
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
        var manufacturer = string.IsNullOrWhiteSpace(model.Manufacturer) ? null : model.Manufacturer;
        var modelNumber = string.IsNullOrWhiteSpace(model.ModelNumber) ? null : model.ModelNumber;
        return string.Join(", ", new[]
        {
            $"{model.Name}",
            manufacturer,
            modelNumber,
            $"in-stock {model.InStockCount}",
            $"allocated {model.AllocatedCount}",
            $"locked {model.LockedCount}"
        }.Where(x => !string.IsNullOrWhiteSpace(x)));
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
                $"Tôi đã tạo request cấp phát cho bạn với asset {created.SelectedAsset.AssetCode} ({created.SelectedAsset.ModelName}). "
                + $"Mã request: {created.Request.Id}. "
                + (created.Request.LockExpiresAt is null
                    ? "Request đang chờ duyệt."
                    : $"Asset đang được giữ tạm đến {created.Request.LockExpiresAt:dd/MM/yyyy HH:mm} UTC.");

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
                    "Liệt kê thêm các asset khả dụng khác",
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

        try
        {
            var requests = await _operations.ListPendingRequestsAsync(payload, ct);
            var answer = requests.Count == 0
                ? "Hiện không có request chờ duyệt nào phù hợp."
                : "Các request đang chờ duyệt: "
                  + string.Join("; ", requests.Select(r =>
                      $"{r.IdShort} - {r.RequesterName} - {r.AssetCode} ({r.ModelName})"
                      + (r.LockExpiresAt is null ? string.Empty : $", giữ đến {r.LockExpiresAt:dd/MM HH:mm} UTC")))
                  + ".";

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
