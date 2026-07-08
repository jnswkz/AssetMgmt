using System.Text.Json;
using AssetMgmt.Application.Auth;
using AssetMgmt.Domain.Entities;
using AssetMgmt.Domain.Enums;
using AssetMgmt.Domain.Exceptions;
using AssetMgmt.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AssetMgmt.Application.Agents;

public class AiPendingActionService
{
    private static readonly HashSet<string> MutatingTools =
    [
        AiToolNames.CreateAllocationRequest,
        AiToolNames.ApproveAllocationRequest,
        AiToolNames.RejectAllocationRequest
    ];

    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IReadOnlyDictionary<string, IAiToolHandler> _handlers;

    public AiPendingActionService(
        AppDbContext db,
        ICurrentUser currentUser,
        IEnumerable<IAiToolHandler> handlers)
    {
        _db = db;
        _currentUser = currentUser;
        _handlers = handlers.ToDictionary(x => x.ToolName, StringComparer.Ordinal);
    }

    public static bool RequiresServerConfirmation(string toolName) => MutatingTools.Contains(toolName);

    public async Task<(AiToolExecutionResult Execution, AiPendingActionDto Pending)> StageAsync(
        Guid userId,
        Guid conversationId,
        string userMessage,
        AiRouteDecision decision,
        CancellationToken ct)
    {
        var normalized = NormalizePayload(decision.ToolName, decision.Arguments);
        var summary = BuildSummary(decision.ToolName, normalized);

        var conversation = await _db.AiAgentConversations
            .FirstOrDefaultAsync(x => x.Id == conversationId && x.UserId == userId, ct);
        if (conversation is null)
        {
            conversation = new AiAgentConversation
            {
                Id = conversationId,
                UserId = userId,
                ConversationKey = conversationId.ToString("D"),
                Title = Truncate(userMessage, 200)
            };
            _db.AiAgentConversations.Add(conversation);
        }

        var action = new AiPendingAction
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ConversationId = conversationId,
            ToolName = decision.ToolName,
            PayloadJson = normalized.GetRawText(),
            Summary = summary,
            Status = AiPendingActionStatus.Pending,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            CreatedAt = DateTime.UtcNow
        };
        _db.AiPendingActions.Add(action);
        await _db.SaveChangesAsync(ct);

        var pending = Map(action);
        var execution = new AiToolExecutionResult(
            decision.Intent,
            decision.ToolName,
            normalized,
            $"Tôi đã chuẩn bị thao tác: {summary}. Vui lòng kiểm tra và xác nhận trước khi thực hiện.",
            [],
            [],
            true,
            false);
        return (execution, pending);
    }

    public async Task<AiAskResponse> ConfirmAsync(Guid actionId, CancellationToken ct)
    {
        var userId = _currentUser.Id ?? throw new NotFoundException("Pending action not found.");
        var action = await _db.AiPendingActions
            .FirstOrDefaultAsync(x => x.Id == actionId && x.UserId == userId, ct)
            ?? throw new NotFoundException("Pending action not found.");

        if (action.Status == AiPendingActionStatus.Executed && !string.IsNullOrWhiteSpace(action.ResultJson))
            return JsonSerializer.Deserialize<AiAskResponse>(action.ResultJson, AiJson.SerializerOptions)
                   ?? throw new ConflictException("The saved AI action result is invalid.");
        if (action.Status == AiPendingActionStatus.Cancelled)
            throw new ConflictException("This pending action was cancelled.");
        if (action.Status == AiPendingActionStatus.Expired || action.ExpiresAt <= DateTime.UtcNow)
        {
            action.Status = AiPendingActionStatus.Expired;
            await _db.SaveChangesAsync(ct);
            throw new ConflictException("This pending action has expired.");
        }
        if (!_handlers.TryGetValue(action.ToolName, out var handler))
            throw new ConflictException("The requested AI action is no longer available.");

        using var payload = JsonDocument.Parse(action.PayloadJson);
        var arguments = payload.RootElement.Clone();
        var decision = new AiRouteDecision(
            IntentFor(action.ToolName), action.ToolName, arguments, 1, false, "Explicit user confirmation");
        var execution = await handler.ExecuteAsync(new AiToolExecutionContext(
            action.ConversationId,
            userId,
            _currentUser.Role ?? string.Empty,
            $"Confirmed pending action {action.Id:D}",
            null,
            decision), ct);

        if (execution.PermissionDenied)
            throw new NotFoundException("Pending action not found.");
        if (!execution.Succeeded || !string.Equals(execution.ToolName, action.ToolName, StringComparison.Ordinal))
            throw new ConflictException(execution.Answer);

        var response = new AiAskResponse(
            action.ConversationId,
            execution.Intent,
            execution.ToolName,
            execution.ToolArguments.Clone(),
            execution.Answer,
            execution.SuggestedActions,
            execution.Sources);
        action.Status = AiPendingActionStatus.Executed;
        action.ExecutedAt = DateTime.UtcNow;
        action.ResultJson = JsonSerializer.Serialize(response, AiJson.SerializerOptions);
        await _db.SaveChangesAsync(ct);
        return response;
    }

    public async Task CancelAsync(Guid actionId, CancellationToken ct)
    {
        var userId = _currentUser.Id ?? throw new NotFoundException("Pending action not found.");
        var action = await _db.AiPendingActions
            .FirstOrDefaultAsync(x => x.Id == actionId && x.UserId == userId, ct)
            ?? throw new NotFoundException("Pending action not found.");
        if (action.Status == AiPendingActionStatus.Executed)
            throw new ConflictException("This pending action has already been executed.");
        if (action.Status == AiPendingActionStatus.Cancelled) return;
        action.Status = action.ExpiresAt <= DateTime.UtcNow
            ? AiPendingActionStatus.Expired
            : AiPendingActionStatus.Cancelled;
        action.CancelledAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private static JsonElement NormalizePayload(string toolName, JsonElement arguments) => toolName switch
    {
        AiToolNames.CreateAllocationRequest => AiJson.ToElement(
            AiJson.Deserialize<CreateAllocationRequestPayload>(arguments)
            ?? new CreateAllocationRequestPayload(null, null, null, null, null, null, null, null)),
        AiToolNames.ApproveAllocationRequest => AiJson.ToElement(
            AiJson.Deserialize<ApproveAllocationRequestPayload>(arguments)
            ?? new ApproveAllocationRequestPayload(null, null)),
        AiToolNames.RejectAllocationRequest => AiJson.ToElement(
            AiJson.Deserialize<RejectAllocationRequestPayload>(arguments)
            ?? new RejectAllocationRequestPayload(null, null, null)),
        _ => throw new ConflictException("This AI tool cannot be staged.")
    };

    private static string BuildSummary(string toolName, JsonElement payload)
    {
        return toolName switch
        {
            AiToolNames.CreateAllocationRequest => BuildCreateSummary(
                AiJson.Deserialize<CreateAllocationRequestPayload>(payload)!),
            AiToolNames.ApproveAllocationRequest => BuildRequestSummary(
                "Duyệt request", AiJson.Deserialize<ApproveAllocationRequestPayload>(payload)?.RequestId,
                AiJson.Deserialize<ApproveAllocationRequestPayload>(payload)?.SearchText),
            AiToolNames.RejectAllocationRequest => BuildRequestSummary(
                "Từ chối request", AiJson.Deserialize<RejectAllocationRequestPayload>(payload)?.RequestId,
                AiJson.Deserialize<RejectAllocationRequestPayload>(payload)?.SearchText),
            _ => "Thao tác thay đổi dữ liệu"
        };
    }

    private static string BuildCreateSummary(CreateAllocationRequestPayload payload)
    {
        var target = payload.AssetCode ?? payload.ModelQuery ?? payload.Category ?? payload.AssetId?.ToString("D") ?? "thiết bị phù hợp";
        return $"Tạo request cấp phát cho {Truncate(target, 120)}";
    }

    private static string BuildRequestSummary(string prefix, Guid? id, string? searchText) =>
        $"{prefix} {Truncate(id?.ToString("D") ?? searchText ?? "đã chọn", 120)}";

    private static string IntentFor(string toolName) => toolName switch
    {
        AiToolNames.CreateAllocationRequest => AiIntents.CreateAllocationRequest,
        AiToolNames.ApproveAllocationRequest => AiIntents.ApproveAllocationRequest,
        AiToolNames.RejectAllocationRequest => AiIntents.RejectAllocationRequest,
        _ => AiIntents.NeedClarification
    };

    private static AiPendingActionDto Map(AiPendingAction action) => new(
        action.Id, action.ToolName, action.Summary, action.ExpiresAt, action.Status.ToString());

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
