using System.Text.Json;
using AssetMgmt.Domain.Entities;
using AssetMgmt.Domain.Enums;
using AssetMgmt.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AssetMgmt.Application.Agents;

public class GetMyAssetsTool : IAiToolHandler
{
    private static readonly IReadOnlyList<AiSourceReference> NoSources = [];
    private readonly AiAssetAccessService _assetAccess;

    public GetMyAssetsTool(AiAssetAccessService assetAccess)
    {
        _assetAccess = assetAccess;
    }

    public string ToolName => AiToolNames.GetMyAssets;

    public async Task<AiToolExecutionResult> ExecuteAsync(AiToolExecutionContext context, CancellationToken ct)
    {
        var payload = AiJson.Deserialize<GetMyAssetsPayload>(context.Decision.Arguments)
            ?? new GetMyAssetsPayload("assigned_to_me");
        var assets = await _assetAccess.GetAssignedAssetsAsync(ct);

        string answer;
        if (assets.Count == 0)
        {
            answer = "Bạn hiện chưa được cấp thiết bị nào.";
        }
        else
        {
            var visible = assets.Take(5).Select(FormatAssignedAsset);
            var remaining = assets.Count - 5;
            answer = $"Bạn đang sử dụng {assets.Count} thiết bị:\n"
                     + string.Join("\n", visible)
                     + (remaining > 0 ? $"\n• Và {remaining} thiết bị khác" : string.Empty);
        }

        return new AiToolExecutionResult(
            AiIntents.QueryMyAssets,
            ToolName,
            AiJson.ToElement(payload),
            answer,
            ["Kiểm tra chi tiết một thiết bị", "Tìm hướng dẫn sử dụng"],
            NoSources,
            true,
            false);
    }

    private static string FormatAssignedAsset(AiOwnedAssetSummary asset)
    {
        var warranty = asset.WarrantyExpiresAt is { } expiry && expiry.Date <= DateTime.UtcNow.Date.AddDays(30)
            ? expiry.Date < DateTime.UtcNow.Date
                ? $" · Hết bảo hành {expiry:dd/MM/yyyy}"
                : $" · Bảo hành đến {expiry:dd/MM/yyyy}"
            : string.Empty;
        return $"• {asset.AssetCode} — {asset.ModelName}{warranty}";
    }
}

public class GetAssetStatusTool : IAiToolHandler
{
    private readonly AiAssetAccessService _assetAccess;
    private readonly AppDbContext _db;

    public GetAssetStatusTool(AiAssetAccessService assetAccess, AppDbContext db)
    {
        _assetAccess = assetAccess;
        _db = db;
    }

    public string ToolName => AiToolNames.GetAssetStatus;

    public async Task<AiToolExecutionResult> ExecuteAsync(AiToolExecutionContext context, CancellationToken ct)
    {
        var payload = AiJson.Deserialize<GetAssetStatusPayload>(context.Decision.Arguments) ?? new(null, null, null, null);

        var resolution = await _assetAccess.ResolveAccessibleAssetAsync(
            context.RequestAssetId,
            payload.AssetId,
            payload.AssetCode,
            payload.SearchText,
            ct);

        if (!resolution.HasAsset)
            return BuildResolutionFailure(context.Decision, resolution);

        var asset = resolution.Asset!;
        var latestMaintenance = await _db.MaintenanceRecords.AsNoTracking()
            .Where(r => r.AssetInstanceId == asset.Id)
            .OrderByDescending(r => r.StartDate)
            .Select(r => new { r.Status, r.StartDate, r.Description })
            .FirstOrDefaultAsync(ct);

        var statusText = asset.Status switch
        {
            AssetStatus.InStock => "Sẵn sàng cấp phát",
            AssetStatus.Allocated => "Đang được sử dụng",
            AssetStatus.LockedTemp => "Đang được giữ cho một yêu cầu",
            AssetStatus.Maintenance => "Đang bảo trì",
            _ => asset.Status.ToString()
        };
        var warrantyText = asset.WarrantyExpiresAt is null
            ? null
            : asset.WarrantyExpiresAt.Value.Date >= DateTime.UtcNow.Date
                ? $"Còn bảo hành đến {asset.WarrantyExpiresAt:dd/MM/yyyy}"
                : $"Đã hết bảo hành từ {asset.WarrantyExpiresAt:dd/MM/yyyy}";
        var maintenanceText = latestMaintenance is null
            ? null
            : $"Bảo dưỡng gần nhất: {latestMaintenance.StartDate:dd/MM/yyyy}";

        var details = new List<string> { $"• Trạng thái: {statusText}" };
        if (asset.CurrentHolder is not null) details.Add($"• Người sử dụng: {asset.CurrentHolder.FullName}");
        if (warrantyText is not null) details.Add($"• {warrantyText}");
        if (maintenanceText is not null) details.Add($"• {maintenanceText}");
        var answer = $"{asset.AssetCode} — {asset.Model.Name}\n" + string.Join("\n", details);

        return new AiToolExecutionResult(
            AiIntents.QueryAssetStatus,
            ToolName,
            AiJson.ToElement(payload with
            {
                AssetId = asset.Id,
                AssetCode = asset.AssetCode,
                SearchText = payload.SearchText,
                Focus = payload.Focus
            }),
            answer,
            ["Tìm manual cho thiết bị này", "Tạo bản nháp yêu cầu bảo dưỡng"],
            [],
            true,
            false);
    }

    private static AiToolExecutionResult BuildResolutionFailure(AiRouteDecision decision, AiAssetResolution resolution)
    {
        if (resolution.IsForbidden)
            return AiToolResultFactory.PermissionDenied(AiIntents.QueryAssetStatus, AiToolNames.GetAssetStatus, decision.Arguments);

        var question = resolution.FailureCode == "ambiguous_asset"
            ? "Tôi thấy có nhiều thiết bị phù hợp. Vui lòng chọn một thiết bị hoặc cung cấp mã tài sản chính xác."
            : "Bạn muốn kiểm tra thiết bị nào? Vui lòng chọn một thiết bị hoặc cung cấp mã tài sản.";

        return AiToolResultFactory.Clarification(question, resolution.CandidateAssets);
    }
}

public class SearchManualSourcesTool : IAiToolHandler
{
    private readonly AiAssetAccessService _assetAccess;
    private readonly IOptions<AiRouterOptions> _options;

    public SearchManualSourcesTool(AiAssetAccessService assetAccess, IOptions<AiRouterOptions> options)
    {
        _assetAccess = assetAccess;
        _options = options;
    }

    public string ToolName => AiToolNames.SearchManualSources;

    public async Task<AiToolExecutionResult> ExecuteAsync(AiToolExecutionContext context, CancellationToken ct)
    {
        var payload = AiJson.Deserialize<SearchManualSourcesPayload>(context.Decision.Arguments)
            ?? new SearchManualSourcesPayload(null, null, null, null, null);

        string? manufacturerHint = payload.Manufacturer;
        string? modelHint = payload.ModelQuery;

        if (context.RequestAssetId is not null || payload.AssetId is not null || !string.IsNullOrWhiteSpace(payload.AssetCode))
        {
            var resolution = await _assetAccess.ResolveAccessibleAssetAsync(
                context.RequestAssetId,
                payload.AssetId,
                payload.AssetCode,
                null,
                ct);

            if (!resolution.HasAsset)
            {
                if (resolution.IsForbidden)
                    return AiToolResultFactory.PermissionDenied(AiIntents.SearchManual, ToolName, context.Decision.Arguments);

                return AiToolResultFactory.Clarification(
                    "Bạn muốn tìm manual cho thiết bị nào? Vui lòng cung cấp mã tài sản hoặc model thiết bị.",
                    resolution.CandidateAssets);
            }

            var asset = resolution.Asset!;
            manufacturerHint ??= asset.Model?.Manufacturer;
            modelHint ??= asset.Model?.Name;
        }

        var query = string.Join(' ', new[]
        {
            payload.Query,
            manufacturerHint,
            modelHint
        }.Where(x => !string.IsNullOrWhiteSpace(x)));

        var results = _options.Value.ManualSources
            .Select(item => new { Item = item, Score = Score(item, query) })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Item.Title)
            .Take(5)
            .Select(x => new AiSourceReference(x.Item.Title, x.Item.Url, x.Item.Kind))
            .ToList();

        var answer = results.Count == 0
            ? "Tôi chưa tìm thấy manual phù hợp trong danh mục tài liệu đã cấu hình. Vui lòng cung cấp rõ hơn hãng hoặc model thiết bị."
            : $"Mình tìm thấy {results.Count} tài liệu phù hợp:\n"
              + string.Join("\n", results.Select(r => $"• {r.Title}"));

        return new AiToolExecutionResult(
            AiIntents.SearchManual,
            ToolName,
            AiJson.ToElement(payload with
            {
                Manufacturer = manufacturerHint,
                ModelQuery = modelHint
            }),
            answer,
            results.Count == 0
                ? ["Cung cấp chính xác hãng hoặc model", "Yêu cầu kiểm tra trạng thái thiết bị trước"]
                : ["Mở một trong các nguồn tài liệu ở trên", "Nếu thiết bị đang lỗi, tạo bản nháp yêu cầu bảo dưỡng"],
            results,
            true,
            false);
    }

    private static int Score(ManualSourceCatalogItem item, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return 0;

        var haystack = string.Join(' ', new[]
        {
            item.Title,
            item.Manufacturer,
            item.Model,
            string.Join(' ', item.Keywords)
        }.Where(x => !string.IsNullOrWhiteSpace(x))).ToLowerInvariant();

        var tokens = query.ToLowerInvariant()
            .Split([' ', ',', '.', ';', ':', '-', '_', '/'], StringSplitOptions.RemoveEmptyEntries)
            .Distinct()
            .ToList();

        return tokens.Count(token => haystack.Contains(token, StringComparison.Ordinal));
    }

}

public class CreateMaintenanceDraftTool : IAiToolHandler
{
    private readonly AiAssetAccessService _assetAccess;

    public CreateMaintenanceDraftTool(AiAssetAccessService assetAccess)
    {
        _assetAccess = assetAccess;
    }

    public string ToolName => AiToolNames.CreateMaintenanceDraft;

    public async Task<AiToolExecutionResult> ExecuteAsync(AiToolExecutionContext context, CancellationToken ct)
    {
        var payload = AiJson.Deserialize<CreateMaintenanceDraftPayload>(context.Decision.Arguments)
            ?? new CreateMaintenanceDraftPayload(null, null, null, null);

        if (string.IsNullOrWhiteSpace(payload.IssueDescription))
        {
            return AiToolResultFactory.Clarification(
                "Bạn vui lòng mô tả rõ lỗi hoặc nhu cầu bảo dưỡng để tôi tạo bản nháp chính xác hơn.");
        }

        var resolution = await _assetAccess.ResolveAccessibleAssetAsync(
            context.RequestAssetId,
            payload.AssetId,
            payload.AssetCode,
            null,
            ct);

        if (!resolution.HasAsset)
        {
            if (resolution.IsForbidden)
                return AiToolResultFactory.PermissionDenied(AiIntents.CreateMaintenanceDraft, ToolName, context.Decision.Arguments);

            return AiToolResultFactory.Clarification(
                "Bạn muốn tạo yêu cầu bảo dưỡng cho thiết bị nào? Vui lòng cung cấp mã tài sản hoặc chọn thiết bị cụ thể.",
                resolution.CandidateAssets);
        }

        var asset = resolution.Asset!;
        var inferredType = InferMaintenanceType(payload.IssueCategory, payload.IssueDescription);
        var draftId = Guid.NewGuid();

        var normalizedPayload = new
        {
            draftId,
            assetId = asset.Id,
            assetCode = asset.AssetCode,
            modelName = asset.Model.Name,
            maintenanceType = inferredType.ToString(),
            issueDescription = payload.IssueDescription.Trim(),
            issueCategory = payload.IssueCategory,
            requiresConfirmation = true
        };

        var answer =
            $"Tôi đã tạo bản nháp yêu cầu bảo dưỡng cho thiết bị {asset.AssetCode} ({asset.Model.Name}). "
            + $"Loại xử lý dự kiến: {inferredType}. "
            + "Vui lòng xác nhận để gửi yêu cầu chính thức.";

        var intent = string.Equals(context.Decision.Intent, AiIntents.TroubleshootDevice, StringComparison.Ordinal)
            ? AiIntents.TroubleshootDevice
            : AiIntents.CreateMaintenanceDraft;

        return new AiToolExecutionResult(
            intent,
            ToolName,
            AiJson.ToElement(normalizedPayload),
            answer,
            ["Xác nhận gửi yêu cầu bảo dưỡng", "Kiểm tra lại trạng thái bảo hành trước khi gửi"],
            [],
            true,
            false);
    }

    private static MaintenanceType InferMaintenanceType(string? issueCategory, string? issueDescription)
    {
        var text = string.Join(' ', new[] { issueCategory, issueDescription })
            .ToLowerInvariant();

        if (text.Contains("bảo hành", StringComparison.Ordinal) || text.Contains("warranty", StringComparison.Ordinal))
            return MaintenanceType.WarrantyClaim;
        if (text.Contains("nâng cấp", StringComparison.Ordinal) || text.Contains("upgrade", StringComparison.Ordinal))
            return MaintenanceType.Upgrade;
        if (text.Contains("vệ sinh", StringComparison.Ordinal) || text.Contains("clean", StringComparison.Ordinal))
            return MaintenanceType.Cleaning;
        if (text.Contains("kiểm tra", StringComparison.Ordinal) || text.Contains("inspect", StringComparison.Ordinal))
            return MaintenanceType.Inspection;

        return MaintenanceType.Repair;
    }
}

public class AskClarifyingQuestionTool : IAiToolHandler
{
    private readonly AiAssetAccessService _assetAccess;

    public AskClarifyingQuestionTool(AiAssetAccessService assetAccess)
    {
        _assetAccess = assetAccess;
    }

    public string ToolName => AiToolNames.AskClarifyingQuestion;

    public async Task<AiToolExecutionResult> ExecuteAsync(AiToolExecutionContext context, CancellationToken ct)
    {
        var payload = AiJson.Deserialize<AskClarifyingQuestionPayload>(context.Decision.Arguments)
            ?? new AskClarifyingQuestionPayload(null, null, null);

        var candidates = payload.CandidateAssets?.Where(x => !string.IsNullOrWhiteSpace(x)).ToList() ?? [];
        var intent = string.Equals(context.Decision.Intent, AiIntents.GeneralHelp, StringComparison.Ordinal)
            ? AiIntents.GeneralHelp
            : AiIntents.NeedClarification;

        var question = string.IsNullOrWhiteSpace(payload.Question)
            ? intent == AiIntents.GeneralHelp
                ? "Hiện tại tôi có thể hỗ trợ kiểm tra tài sản được cấp, trạng thái thiết bị, manual/tài liệu hỗ trợ và tạo bản nháp yêu cầu bảo dưỡng."
                : "Bạn muốn kiểm tra thiết bị nào? Vui lòng chọn một thiết bị hoặc cung cấp mã tài sản."
            : payload.Question.Trim();

        if (candidates.Count == 0 && !string.Equals(intent, AiIntents.GeneralHelp, StringComparison.Ordinal))
        {
            var myAssets = await _assetAccess.GetAssignedAssetsAsync(ct);
            candidates = myAssets
                .Take(5)
                .Select(a => $"{a.AssetCode} - {a.ModelName}")
                .ToList();
        }

        return new AiToolExecutionResult(
            intent,
            ToolName,
            AiJson.ToElement(new AskClarifyingQuestionPayload(question, payload.MissingField, candidates)),
            question,
            intent == AiIntents.GeneralHelp
                ? [
                    "Xem các thiết bị đang được cấp cho tôi",
                    "Liệt kê các asset hoặc asset model khả dụng",
                    "Tạo request cấp phát thiết bị cho công việc",
                    "Kiểm tra trạng thái của một thiết bị",
                    "Tìm manual hoặc tài liệu hỗ trợ",
                    "Tạo bản nháp yêu cầu bảo dưỡng"
                ]
                : candidates.Count == 0
                    ? ["Tôi có thể hỗ trợ kiểm tra tài sản, liệt kê asset/model, tạo request cấp phát, kiểm tra trạng thái thiết bị, manual và tạo bản nháp bảo dưỡng"]
                    : candidates,
            [],
            true,
            false);
    }
}

internal static class AiToolResultFactory
{
    public static AiToolExecutionResult PermissionDenied(string intent, string toolName, JsonElement arguments) =>
        new(
            intent,
            toolName,
            arguments.Clone(),
            "Tôi không thể truy cập thiết bị này với quyền hiện tại của bạn.",
            ["Chọn một thiết bị bạn đang được cấp hoặc liên hệ quản trị viên IT nếu cần hỗ trợ thêm"],
            [],
            false,
            true);

    public static AiToolExecutionResult Clarification(string question, IReadOnlyList<string>? candidates = null) =>
        new(
            AiIntents.NeedClarification,
            AiToolNames.AskClarifyingQuestion,
            AiJson.ToElement(new AskClarifyingQuestionPayload(question, "asset", candidates ?? [])),
            question,
            candidates ?? [],
            [],
            true,
            false);
}
