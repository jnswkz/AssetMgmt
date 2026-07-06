using System.Text.Json;
using AssetMgmt.Domain.Enums;

namespace AssetMgmt.Application.Agents;

public record AiAskRequest(string Message, Guid? AssetId, Guid? ConversationId);

public record AiAskResponse(
    Guid ConversationId,
    string Intent,
    string SelectedTool,
    JsonElement ToolArguments,
    string Answer,
    IReadOnlyList<string> SuggestedActions,
    IReadOnlyList<AiSourceReference> Sources);

public record AiSourceReference(string Title, string Url, string? Kind);

public record AiRouteDecision(
    string Intent,
    string ToolName,
    JsonElement Arguments,
    double Confidence,
    bool RequiresConfirmation,
    string Reason);

public sealed record AiConversationTurn(
    string Role,
    string Content);

public interface IAiRouterService
{
    Task<AiRouteDecision> RouteAsync(AiRouteRequest request, CancellationToken ct);
}

public sealed record AiRouteRequest(
    string Message,
    Guid ConversationId,
    Guid? AssetId,
    Guid UserId,
    string UserRole,
    IReadOnlyList<AiConversationTurn> History);

public interface IAiToolHandler
{
    string ToolName { get; }
    Task<AiToolExecutionResult> ExecuteAsync(AiToolExecutionContext context, CancellationToken ct);
}

public sealed record AiToolExecutionContext(
    Guid ConversationId,
    Guid UserId,
    string UserRole,
    string Message,
    Guid? RequestAssetId,
    AiRouteDecision Decision);

public sealed record AiToolExecutionResult(
    string Intent,
    string ToolName,
    JsonElement ToolArguments,
    string Answer,
    IReadOnlyList<string> SuggestedActions,
    IReadOnlyList<AiSourceReference> Sources,
    bool Succeeded,
    bool PermissionDenied);

public static class AiIntents
{
    public const string QueryMyAssets = "QueryMyAssets";
    public const string QueryAssetStatus = "QueryAssetStatus";
    public const string SearchManual = "SearchManual";
    public const string TroubleshootDevice = "TroubleshootDevice";
    public const string CreateMaintenanceDraft = "CreateMaintenanceDraft";
    public const string ListAssets = "ListAssets";
    public const string ListAssetModels = "ListAssetModels";
    public const string CreateAllocationRequest = "CreateAllocationRequest";
    public const string ListPendingRequests = "ListPendingRequests";
    public const string ApproveAllocationRequest = "ApproveAllocationRequest";
    public const string RejectAllocationRequest = "RejectAllocationRequest";
    public const string GeneralHelp = "GeneralHelp";
    public const string NeedClarification = "NeedClarification";
}

public static class AiToolNames
{
    public const string GetMyAssets = "get_my_assets";
    public const string GetAssetStatus = "get_asset_status";
    public const string SearchManualSources = "search_manual_sources";
    public const string CreateMaintenanceDraft = "create_maintenance_draft";
    public const string ListAssets = "list_assets";
    public const string ListAssetModels = "list_asset_models";
    public const string CreateAllocationRequest = "create_allocation_request";
    public const string ListPendingRequests = "list_pending_requests";
    public const string ApproveAllocationRequest = "approve_allocation_request";
    public const string RejectAllocationRequest = "reject_allocation_request";
    public const string AskClarifyingQuestion = "ask_clarifying_question";
}

public static class AiConversationRoles
{
    public const string User = "user";
    public const string Assistant = "assistant";
}

public sealed class AiRouterOptions
{
    public const string SectionName = "Ai";
    public string RouterModel { get; set; } = "gpt-5.4-nano";
    public List<ManualSourceCatalogItem> ManualSources { get; set; } = [];
}

public sealed class ManualSourceCatalogItem
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Kind { get; set; } = "manual";
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public List<string> Keywords { get; set; } = [];
}

public sealed record GetMyAssetsPayload(string? Scope);

public sealed record GetAssetStatusPayload(
    Guid? AssetId,
    string? AssetCode,
    string? SearchText,
    string? Focus);

public sealed record SearchManualSourcesPayload(
    Guid? AssetId,
    string? AssetCode,
    string? Manufacturer,
    string? ModelQuery,
    string? Query);

public sealed record CreateMaintenanceDraftPayload(
    Guid? AssetId,
    string? AssetCode,
    string? IssueDescription,
    string? IssueCategory);

public sealed record ListAssetsPayload(
    string? Scope,
    string? SearchText,
    string? Manufacturer,
    string? ModelQuery,
    string? Status,
    string? Category,
    int? PageSize);

public sealed record ListAssetModelsPayload(
    string? Scope,
    string? SearchText,
    string? Manufacturer,
    string? Category,
    bool? AvailableOnly,
    int? PageSize);

public sealed record CreateAllocationRequestPayload(
    Guid? AssetId,
    string? AssetCode,
    string? ModelQuery,
    string? Manufacturer,
    string? Category,
    string? Reason,
    int? ExpectedDurationMonths,
    string? DesiredUse);

public sealed record ListPendingRequestsPayload(
    string? SearchText,
    int? PageSize);

public sealed record ApproveAllocationRequestPayload(
    Guid? RequestId,
    string? SearchText);

public sealed record RejectAllocationRequestPayload(
    Guid? RequestId,
    string? SearchText,
    string? Reason);

public sealed record AskClarifyingQuestionPayload(
    string? Question,
    string? MissingField,
    IReadOnlyList<string>? CandidateAssets);

public sealed record AiAssetCatalogItem(
    Guid Id,
    string AssetCode,
    string ModelName,
    string? Manufacturer,
    string? ModelNumber,
    AssetCategory Category,
    AssetStatus Status,
    string? CurrentHolderName,
    string? Location);

public sealed record AiAssetModelCatalogItem(
    Guid Id,
    string Name,
    AssetCategory Category,
    string? Manufacturer,
    string? ModelNumber,
    int TotalCount,
    int InStockCount,
    int AllocatedCount,
    int LockedCount);

public sealed record AiPendingRequestSummary(
    Guid Id,
    string IdShort,
    string RequesterName,
    string AssetCode,
    string ModelName,
    DateTime CreatedAt,
    DateTime? LockExpiresAt);

public static class AiJson
{
    public static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static JsonElement ToElement<T>(T value) =>
        JsonSerializer.SerializeToElement(value, SerializerOptions);

    public static T? Deserialize<T>(JsonElement element) =>
        element.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
            ? default
            : JsonSerializer.Deserialize<T>(element.GetRawText(), SerializerOptions);
}
