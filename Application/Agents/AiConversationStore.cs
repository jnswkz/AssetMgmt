using AssetMgmt.Domain.Entities;
using AssetMgmt.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AssetMgmt.Application.Agents;

public class AiConversationStore
{
    private const int MaxHistoryMessages = 8;
    private readonly AppDbContext _db;
    private readonly ILogger<AiConversationStore> _logger;

    public AiConversationStore(AppDbContext db, ILogger<AiConversationStore> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AiConversationTurn>> LoadRecentHistoryAsync(
        Guid userId,
        Guid conversationId,
        CancellationToken ct)
    {
        try
        {
            return await _db.AiAgentMessages.AsNoTracking()
                .Where(x => x.ConversationId == conversationId && x.Conversation != null && x.Conversation.UserId == userId)
                .OrderByDescending(x => x.CreatedAt)
                .Take(MaxHistoryMessages)
                .OrderBy(x => x.CreatedAt)
                .Select(x => new AiConversationTurn(
                    x.Role,
                    TrimForPrompt(x.Content, 400)))
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to load AI conversation history for conversation {ConversationId}. Continuing without history.",
                conversationId);

            return [];
        }
    }

    public async Task AppendExchangeAsync(
        Guid userId,
        Guid conversationId,
        string userMessage,
        AiRouteDecision decision,
        AiToolExecutionResult execution,
        Guid? pendingActionId,
        CancellationToken ct)
    {
        try
        {
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

            _db.AiAgentMessages.Add(new AiAgentMessage
            {
                ConversationId = conversationId,
                Role = AiConversationRoles.User,
                Content = userMessage
            });

            _db.AiAgentMessages.Add(new AiAgentMessage
            {
                ConversationId = conversationId,
                Role = AiConversationRoles.Assistant,
                Content = execution.Answer,
                Intent = execution.Intent,
                ToolCallsJson = BuildToolCallJson(decision, execution),
                RequiresConfirmation = decision.RequiresConfirmation,
                PendingActionId = pendingActionId?.ToString("D")
            });

            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to persist AI conversation history for conversation {ConversationId}.",
                conversationId);
        }
    }

    private static string BuildToolCallJson(AiRouteDecision decision, AiToolExecutionResult execution)
    {
        var payload = new
        {
            intent = decision.Intent,
            selectedTool = decision.ToolName,
            toolArguments = execution.ToolArguments,
            confidence = decision.Confidence,
            requiresConfirmation = decision.RequiresConfirmation,
            reason = decision.Reason
        };

        return AiJson.ToElement(payload).GetRawText();
    }

    private static string TrimForPrompt(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
