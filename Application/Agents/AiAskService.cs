using System.Text.Json;
using AssetMgmt.Application.Auth;
using AssetMgmt.Domain.Entities;
using AssetMgmt.Domain.Exceptions;
using AssetMgmt.Infrastructure.Persistence;

namespace AssetMgmt.Application.Agents;

public class AiAskService
{
    private readonly IAiRouterService _router;
    private readonly IReadOnlyDictionary<string, IAiToolHandler> _toolHandlers;
    private readonly ICurrentUser _currentUser;
    private readonly AiConversationStore _conversationStore;
    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AiAskService> _logger;

    public AiAskService(
        IAiRouterService router,
        IEnumerable<IAiToolHandler> toolHandlers,
        ICurrentUser currentUser,
        AiConversationStore conversationStore,
        AppDbContext db,
        IHttpContextAccessor httpContextAccessor,
        ILogger<AiAskService> logger)
    {
        _router = router;
        _toolHandlers = toolHandlers.ToDictionary(x => x.ToolName, StringComparer.Ordinal);
        _currentUser = currentUser;
        _conversationStore = conversationStore;
        _db = db;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<AiAskResponse> AskAsync(AiAskRequest request, CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.Id is not Guid userId)
            throw new DomainException("Not authenticated.");

        var conversationId = request.ConversationId ?? Guid.NewGuid();
        var trimmedMessage = request.Message.Trim();
        AiRouteDecision? decision = null;
        AiToolExecutionResult? execution = null;

        try
        {
            var history = await _conversationStore.LoadRecentHistoryAsync(userId, conversationId, ct);

            decision = await _router.RouteAsync(
                new AiRouteRequest(
                    trimmedMessage,
                    conversationId,
                    request.AssetId,
                    userId,
                    _currentUser.Role ?? string.Empty,
                    history),
                ct);

            if (!_toolHandlers.TryGetValue(decision.ToolName, out var handler))
            {
                execution = AiToolResultFactory.Clarification(
                    "Tôi cần thêm thông tin để xử lý yêu cầu này. Vui lòng diễn đạt rõ hơn hoặc cung cấp mã tài sản.");
            }
            else
            {
                execution = await handler.ExecuteAsync(
                    new AiToolExecutionContext(
                        conversationId,
                        userId,
                        _currentUser.Role ?? string.Empty,
                        trimmedMessage,
                        request.AssetId,
                        decision),
                    ct);
            }

            var response = new AiAskResponse(
                conversationId,
                execution.Intent,
                execution.ToolName,
                execution.ToolArguments.Clone(),
                execution.Answer,
                execution.SuggestedActions,
                execution.Sources);

            await WriteAuditAsync(
                userId,
                request,
                response.Intent,
                response.SelectedTool,
                response.ToolArguments,
                conversationId,
                execution.Succeeded && !execution.PermissionDenied,
                null,
                decision,
                ct);

            await _conversationStore.AppendExchangeAsync(
                userId,
                conversationId,
                trimmedMessage,
                decision,
                execution,
                ct);

            return response;
        }
        catch (Exception ex)
        {
            await WriteAuditAsync(
                userId,
                request,
                decision?.Intent,
                decision?.ToolName,
                decision?.Arguments,
                conversationId,
                false,
                ex.Message,
                decision,
                ct);

            _logger.LogError(ex, "AI router ask failed for conversation {ConversationId}", conversationId);
            throw;
        }
    }

    private async Task WriteAuditAsync(
        Guid userId,
        AiAskRequest request,
        string? detectedIntent,
        string? selectedTool,
        JsonElement? toolArguments,
        Guid conversationId,
        bool success,
        string? errorMessage,
        AiRouteDecision? decision,
        CancellationToken ct)
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;

            var metadata = new
            {
                message = request.Message,
                detectedIntent,
                selectedTool,
                toolArguments = toolArguments,
                assetId = request.AssetId,
                conversationId,
                confidence = decision?.Confidence,
                requiresConfirmation = decision?.RequiresConfirmation,
                reason = decision?.Reason,
                success
            };

            _db.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                Action = "AI.Router.Ask",
                EntityType = "ai_router",
                EntityId = conversationId,
                Metadata = AiJson.ToElement(metadata).GetRawText(),
                IpAddress = httpContext?.Connection.RemoteIpAddress?.ToString(),
                UserAgent = Truncate(httpContext?.Request.Headers.UserAgent.ToString(), 500),
                Severity = success ? "Info" : "Warning",
                Result = success ? "Success" : "Failed",
                ErrorMessage = errorMessage
            });

            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write AI router audit log for conversation {ConversationId}", conversationId);
        }
    }

    private static string? Truncate(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Length <= max ? value : value[..max];
}
