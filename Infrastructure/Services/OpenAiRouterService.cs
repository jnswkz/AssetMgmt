#pragma warning disable OPENAI001
using System.Text;
using System.Text.Json;
using AssetMgmt.Application.Agents;
using Microsoft.Extensions.Options;
using OpenAI.Responses;

namespace AssetMgmt.Infrastructure.Services;

public class OpenAiRouterService : IAiRouterService
{
    private readonly IConfiguration _configuration;
    private readonly IOptions<AiRouterOptions> _options;
    private readonly ILogger<OpenAiRouterService> _logger;

    public OpenAiRouterService(
        IConfiguration configuration,
        IOptions<AiRouterOptions> options,
        ILogger<OpenAiRouterService> logger)
    {
        _configuration = configuration;
        _options = options;
        _logger = logger;
    }

    public async Task<AiRouteDecision> RouteAsync(AiRouteRequest request, CancellationToken ct)
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("OPENAI_API_KEY is not configured.");

        var model = _configuration["OPENAI_ROUTER_MODEL"]
            ?? _configuration["OPENAI_PLANNER_MODEL"]
            ?? _options.Value.RouterModel;

        var client = CreateClient(apiKey);
        var options = new CreateResponseOptions(model, BuildInputItems(request))
        {
            ToolChoice = ResponseToolChoice.CreateRequiredChoice(),
            ParallelToolCallsEnabled = false,
            MaxToolCallCount = 1,
            StoredOutputEnabled = false,
            MaxOutputTokenCount = 250
        };

        foreach (var tool in BuildTools())
            options.Tools.Add(tool);

        var clientResult = await client.CreateResponseAsync(options, ct);
        var response = clientResult.Value;

        var functionCalls = response.OutputItems
            .OfType<FunctionCallResponseItem>()
            .ToList();

        if (functionCalls.Count != 1)
        {
            _logger.LogWarning(
                "AI router returned {Count} function calls for conversation {ConversationId}",
                functionCalls.Count,
                request.ConversationId);

            return new AiRouteDecision(
                AiIntents.NeedClarification,
                AiToolNames.AskClarifyingQuestion,
                AiJson.ToElement(new AskClarifyingQuestionPayload(
                    "Bạn muốn kiểm tra thiết bị nào? Vui lòng chọn một thiết bị hoặc cung cấp mã tài sản.",
                    "asset",
                    [])),
                0,
                false,
                "Model did not return exactly one tool call.");
        }

        try
        {
            return ParseDecision(functionCalls[0]);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI router returned an invalid tool payload for conversation {ConversationId}", request.ConversationId);

            return new AiRouteDecision(
                AiIntents.NeedClarification,
                AiToolNames.AskClarifyingQuestion,
                AiJson.ToElement(new AskClarifyingQuestionPayload(
                    "Bạn muốn kiểm tra thiết bị nào? Vui lòng chọn một thiết bị hoặc cung cấp mã tài sản.",
                    "asset",
                    [])),
                0,
                false,
                "Model returned an invalid tool payload.");
        }
    }

    private ResponsesClient CreateClient(string apiKey)
    {
        var baseUrl = _configuration["OPENAI_BASE_URL"];
        if (string.IsNullOrWhiteSpace(baseUrl))
            return new ResponsesClient(apiKey);

        return new ResponsesClient(
            new System.ClientModel.ApiKeyCredential(apiKey),
            new ResponsesClientOptions
            {
                Endpoint = new Uri(baseUrl)
            });
    }

    private static IEnumerable<ResponseItem> BuildInputItems(AiRouteRequest request)
    {
        var developerInstructions = """
You are an intent router for an internal IT Asset Management API.
Choose exactly one function tool call.
Never answer the user directly.
Never summarize manuals or PDFs.
Return exactly one tool call with a payload object plus routing metadata.
Use one intent from:
- QueryMyAssets
- QueryAssetStatus
- SearchManual
- TroubleshootDevice
- CreateMaintenanceDraft
- ListAssets
- ListAssetModels
- CreateAllocationRequest
- ListPendingRequests
- ApproveAllocationRequest
- RejectAllocationRequest
- GeneralHelp
- NeedClarification
Rules:
- Use the latest user message together with the recent conversation history to infer references like "thiết bị đó", "manual đó", "toàn bộ", "cái đầu tiên", or "của tôi".
- Do not ask the user to restate the same objective if it can be inferred from recent history.
- Prefer the most specific executable tool. Only ask a clarifying question if a required detail is still missing after using requestAssetId and conversation history.
- If the user asks which devices are assigned to them, choose get_my_assets.
- If the user asks about status, allocation, availability, warranty, or maintenance state of one asset, choose get_asset_status.
- If the user asks to list or search assets, available devices, company assets, department assets, or requestable stock, choose list_assets.
- If the user asks to list or search asset models or catalog models, choose list_asset_models.
- If the user asks for manual, guide, PDF, support document, or troubleshooting document, choose search_manual_sources.
- If the user asks to create a repair, support, troubleshooting, or maintenance request, choose create_maintenance_draft.
- If the user asks to request/borrow/be allocated a device for work, choose create_allocation_request. Prefer this tool even if the exact asset is not specified; infer model/category/manufacturer and let the backend select an available asset.
- If the user asks to review pending requests or what needs approval, choose list_pending_requests.
- If the user asks to approve a pending asset request, choose approve_allocation_request. Vietnamese phrases like "duyệt request", "approve request", "duyệt request theo mã ngắn", "duyệt request của <person>", or "duyệt cái này" are approval requests, not list requests.
- If the latest message approves a request without an explicit ID, infer requestId or searchText from the most recent pending-request list in conversation history. If that list shows exactly one pending request, use its short ID as searchText.
- If the user asks to reject/deny a pending asset request, choose reject_allocation_request. Infer the request from recent pending-request history when possible, but include a rejection reason when the user provided one.
- create_maintenance_draft must set requiresConfirmation to true.
- If the request is vague, the asset cannot be identified, or the user is asking what you can do, choose ask_clarifying_question.
- For general capability questions, use intent GeneralHelp with ask_clarifying_question.
- If the request is outside the supported tools, use intent GeneralHelp with ask_clarifying_question and clearly explain the current capability boundary. Do not keep narrowing scope for an unsupported task.
- Do not invent asset IDs.
- Do not invent request IDs.
- If request assetId is present, you may pass it through in payload.assetId when relevant.
- Respect authenticatedUserRole. Employee users should primarily use assigned assets, available assets, manuals, maintenance drafts, and allocation requests. Manager/AdminIT users may also list pending requests and approve/reject them.
""";

        var historyText = request.History.Count == 0
            ? "(none)"
            : string.Join(
                Environment.NewLine,
                request.History.Select(turn => $"- {turn.Role}: {turn.Content}"));

        var requestContext = $"""
Current request context:
- conversationId: {request.ConversationId}
- requestAssetId: {(request.AssetId?.ToString() ?? "null")}
- authenticatedUserId: {request.UserId}
- authenticatedUserRole: {request.UserRole}
Recent conversation history:
{historyText}
Latest user message:
{request.Message}
""";

        return
        [
            ResponseItem.CreateDeveloperMessageItem(developerInstructions),
            ResponseItem.CreateUserMessageItem(requestContext)
        ];
    }

    private static IEnumerable<FunctionTool> BuildTools()
    {
        yield return CreateTool(
            AiToolNames.GetMyAssets,
            "Use when the user asks which devices are assigned to them.",
            """
{
  "type": "object",
  "additionalProperties": false,
  "properties": {
    "intent": { "type": "string", "enum": ["QueryMyAssets"] },
    "confidence": { "type": "number", "minimum": 0, "maximum": 1 },
    "requiresConfirmation": { "type": "boolean" },
    "reason": { "type": "string" },
    "payload": {
      "type": "object",
      "additionalProperties": false,
      "properties": {
        "scope": { "type": ["string", "null"], "enum": ["assigned_to_me", null] }
      },
      "required": ["scope"]
    }
  },
  "required": ["intent", "confidence", "requiresConfirmation", "reason", "payload"]
}
""");

        yield return CreateTool(
            AiToolNames.GetAssetStatus,
            "Use when the user asks about one asset status, allocation status, warranty, maintenance state, or availability.",
            """
{
  "type": "object",
  "additionalProperties": false,
  "properties": {
    "intent": { "type": "string", "enum": ["QueryAssetStatus"] },
    "confidence": { "type": "number", "minimum": 0, "maximum": 1 },
    "requiresConfirmation": { "type": "boolean" },
    "reason": { "type": "string" },
    "payload": {
      "type": "object",
      "additionalProperties": false,
      "properties": {
        "assetId": { "type": ["string", "null"], "format": "uuid" },
        "assetCode": { "type": ["string", "null"] },
        "searchText": { "type": ["string", "null"] },
        "focus": { "type": ["string", "null"], "enum": ["status", "allocation", "warranty", "maintenance", "availability", null] }
      },
      "required": ["assetId", "assetCode", "searchText", "focus"]
    }
  },
  "required": ["intent", "confidence", "requiresConfirmation", "reason", "payload"]
}
""");

        yield return CreateTool(
            AiToolNames.SearchManualSources,
            "Use when the user asks for a device manual, guide, PDF, troubleshooting document, or support document. Only return metadata and URLs.",
            """
{
  "type": "object",
  "additionalProperties": false,
  "properties": {
    "intent": { "type": "string", "enum": ["SearchManual"] },
    "confidence": { "type": "number", "minimum": 0, "maximum": 1 },
    "requiresConfirmation": { "type": "boolean" },
    "reason": { "type": "string" },
    "payload": {
      "type": "object",
      "additionalProperties": false,
      "properties": {
        "assetId": { "type": ["string", "null"], "format": "uuid" },
        "assetCode": { "type": ["string", "null"] },
        "manufacturer": { "type": ["string", "null"] },
        "modelQuery": { "type": ["string", "null"] },
        "query": { "type": ["string", "null"] }
      },
      "required": ["assetId", "assetCode", "manufacturer", "modelQuery", "query"]
    }
  },
  "required": ["intent", "confidence", "requiresConfirmation", "reason", "payload"]
}
""");

        yield return CreateTool(
            AiToolNames.CreateMaintenanceDraft,
            "Use when the user wants to create a maintenance, repair, support, or troubleshooting request. This only creates a draft.",
            """
{
  "type": "object",
  "additionalProperties": false,
  "properties": {
    "intent": { "type": "string", "enum": ["CreateMaintenanceDraft", "TroubleshootDevice"] },
    "confidence": { "type": "number", "minimum": 0, "maximum": 1 },
    "requiresConfirmation": { "type": "boolean", "const": true },
    "reason": { "type": "string" },
    "payload": {
      "type": "object",
      "additionalProperties": false,
      "properties": {
        "assetId": { "type": ["string", "null"], "format": "uuid" },
        "assetCode": { "type": ["string", "null"] },
        "issueDescription": { "type": ["string", "null"] },
        "issueCategory": { "type": ["string", "null"] }
      },
      "required": ["assetId", "assetCode", "issueDescription", "issueCategory"]
    }
  },
  "required": ["intent", "confidence", "requiresConfirmation", "reason", "payload"]
}
""");

        yield return CreateTool(
            AiToolNames.ListAssets,
            "Use when the user asks to list or search assets, available devices, company inventory, department assets, or requestable stock.",
            """
{
  "type": "object",
  "additionalProperties": false,
  "properties": {
    "intent": { "type": "string", "enum": ["ListAssets"] },
    "confidence": { "type": "number", "minimum": 0, "maximum": 1 },
    "requiresConfirmation": { "type": "boolean" },
    "reason": { "type": "string" },
    "payload": {
      "type": "object",
      "additionalProperties": false,
      "properties": {
        "scope": { "type": ["string", "null"], "enum": ["assigned_to_me", "available", "department", "all", null] },
        "searchText": { "type": ["string", "null"] },
        "manufacturer": { "type": ["string", "null"] },
        "modelQuery": { "type": ["string", "null"] },
        "status": { "type": ["string", "null"], "enum": ["InStock", "LockedTemp", "Allocated", "Maintenance", "Retired", "Lost", "Disposed", null] },
        "category": { "type": ["string", "null"], "enum": ["Laptop", "Monitor", "Phone", "Tablet", "Peripheral", "Printer", "NetworkDevice", "Other", null] },
        "pageSize": { "type": ["integer", "null"], "minimum": 1, "maximum": 10 }
      },
      "required": ["scope", "searchText", "manufacturer", "modelQuery", "status", "category", "pageSize"]
    }
  },
  "required": ["intent", "confidence", "requiresConfirmation", "reason", "payload"]
}
""");

        yield return CreateTool(
            AiToolNames.ListAssetModels,
            "Use when the user asks to list or search asset models, model catalog entries, or company device models.",
            """
{
  "type": "object",
  "additionalProperties": false,
  "properties": {
    "intent": { "type": "string", "enum": ["ListAssetModels"] },
    "confidence": { "type": "number", "minimum": 0, "maximum": 1 },
    "requiresConfirmation": { "type": "boolean" },
    "reason": { "type": "string" },
    "payload": {
      "type": "object",
      "additionalProperties": false,
      "properties": {
        "scope": { "type": ["string", "null"], "enum": ["available", "department", "all", null] },
        "searchText": { "type": ["string", "null"] },
        "manufacturer": { "type": ["string", "null"] },
        "category": { "type": ["string", "null"], "enum": ["Laptop", "Monitor", "Phone", "Tablet", "Peripheral", "Printer", "NetworkDevice", "Other", null] },
        "availableOnly": { "type": ["boolean", "null"] },
        "pageSize": { "type": ["integer", "null"], "minimum": 1, "maximum": 10 }
      },
      "required": ["scope", "searchText", "manufacturer", "category", "availableOnly", "pageSize"]
    }
  },
  "required": ["intent", "confidence", "requiresConfirmation", "reason", "payload"]
}
""");

        yield return CreateTool(
            AiToolNames.CreateAllocationRequest,
            "Use when the user wants to request, borrow, or be allocated a device for work. The backend will resolve the model and choose an available asset.",
            """
{
  "type": "object",
  "additionalProperties": false,
  "properties": {
    "intent": { "type": "string", "enum": ["CreateAllocationRequest"] },
    "confidence": { "type": "number", "minimum": 0, "maximum": 1 },
    "requiresConfirmation": { "type": "boolean" },
    "reason": { "type": "string" },
    "payload": {
      "type": "object",
      "additionalProperties": false,
      "properties": {
        "assetId": { "type": ["string", "null"], "format": "uuid" },
        "assetCode": { "type": ["string", "null"] },
        "modelQuery": { "type": ["string", "null"] },
        "manufacturer": { "type": ["string", "null"] },
        "category": { "type": ["string", "null"], "enum": ["Laptop", "Monitor", "Phone", "Tablet", "Peripheral", "Printer", "NetworkDevice", "Other", null] },
        "reason": { "type": ["string", "null"] },
        "expectedDurationMonths": { "type": ["integer", "null"], "minimum": 1, "maximum": 60 },
        "desiredUse": { "type": ["string", "null"] }
      },
      "required": ["assetId", "assetCode", "modelQuery", "manufacturer", "category", "reason", "expectedDurationMonths", "desiredUse"]
    }
  },
  "required": ["intent", "confidence", "requiresConfirmation", "reason", "payload"]
}
""");

        yield return CreateTool(
            AiToolNames.ListPendingRequests,
            "Use when a manager or admin asks to see pending allocation requests awaiting approval.",
            """
{
  "type": "object",
  "additionalProperties": false,
  "properties": {
    "intent": { "type": "string", "enum": ["ListPendingRequests"] },
    "confidence": { "type": "number", "minimum": 0, "maximum": 1 },
    "requiresConfirmation": { "type": "boolean" },
    "reason": { "type": "string" },
    "payload": {
      "type": "object",
      "additionalProperties": false,
      "properties": {
        "searchText": { "type": ["string", "null"] },
        "pageSize": { "type": ["integer", "null"], "minimum": 1, "maximum": 10 }
      },
      "required": ["searchText", "pageSize"]
    }
  },
  "required": ["intent", "confidence", "requiresConfirmation", "reason", "payload"]
}
""");

        yield return CreateTool(
            AiToolNames.ApproveAllocationRequest,
            "Use when a manager or admin wants to approve a pending allocation request. If the latest message says to approve a listed request, infer requestId or searchText from recent conversation history.",
            """
{
  "type": "object",
  "additionalProperties": false,
  "properties": {
    "intent": { "type": "string", "enum": ["ApproveAllocationRequest"] },
    "confidence": { "type": "number", "minimum": 0, "maximum": 1 },
    "requiresConfirmation": { "type": "boolean" },
    "reason": { "type": "string" },
    "payload": {
      "type": "object",
      "additionalProperties": false,
      "properties": {
        "requestId": { "type": ["string", "null"], "format": "uuid" },
        "searchText": { "type": ["string", "null"] }
      },
      "required": ["requestId", "searchText"]
    }
  },
  "required": ["intent", "confidence", "requiresConfirmation", "reason", "payload"]
}
""");

        yield return CreateTool(
            AiToolNames.RejectAllocationRequest,
            "Use when a manager or admin explicitly wants to reject a pending allocation request and can provide a reason.",
            """
{
  "type": "object",
  "additionalProperties": false,
  "properties": {
    "intent": { "type": "string", "enum": ["RejectAllocationRequest"] },
    "confidence": { "type": "number", "minimum": 0, "maximum": 1 },
    "requiresConfirmation": { "type": "boolean" },
    "reason": { "type": "string" },
    "payload": {
      "type": "object",
      "additionalProperties": false,
      "properties": {
        "requestId": { "type": ["string", "null"], "format": "uuid" },
        "searchText": { "type": ["string", "null"] },
        "reason": { "type": ["string", "null"] }
      },
      "required": ["requestId", "searchText", "reason"]
    }
  },
  "required": ["intent", "confidence", "requiresConfirmation", "reason", "payload"]
}
""");

        yield return CreateTool(
            AiToolNames.AskClarifyingQuestion,
            "Use when the user message is too vague, the asset cannot be identified, the user asks what help is available, or the request is outside currently supported tools. For unsupported requests, use intent GeneralHelp and explain the limitation plus supported alternatives instead of asking another narrowing question.",
            """
{
  "type": "object",
  "additionalProperties": false,
  "properties": {
    "intent": { "type": "string", "enum": ["NeedClarification", "GeneralHelp"] },
    "confidence": { "type": "number", "minimum": 0, "maximum": 1 },
    "requiresConfirmation": { "type": "boolean" },
    "reason": { "type": "string" },
    "payload": {
      "type": "object",
      "additionalProperties": false,
      "properties": {
        "question": { "type": ["string", "null"] },
        "missingField": { "type": ["string", "null"] },
        "candidateAssets": {
          "type": ["array", "null"],
          "items": { "type": "string" }
        }
      },
      "required": ["question", "missingField", "candidateAssets"]
    }
  },
  "required": ["intent", "confidence", "requiresConfirmation", "reason", "payload"]
}
""");
    }

    private static FunctionTool CreateTool(string name, string description, string schemaJson) =>
        ResponseTool.CreateFunctionTool(
            functionName: name,
            functionDescription: description,
            functionParameters: BinaryData.FromBytes(Encoding.UTF8.GetBytes(schemaJson)),
            strictModeEnabled: true);

    private static AiRouteDecision ParseDecision(FunctionCallResponseItem functionCall)
    {
        using var document = JsonDocument.Parse(functionCall.FunctionArguments);
        var root = document.RootElement;

        var intent = GetRequiredString(root, "intent");
        var confidence = root.TryGetProperty("confidence", out var confidenceElement) &&
                         confidenceElement.ValueKind == JsonValueKind.Number &&
                         confidenceElement.TryGetDouble(out var parsedConfidence)
            ? parsedConfidence
            : 0;
        var requiresConfirmation = root.TryGetProperty("requiresConfirmation", out var confirmationElement) &&
                                   confirmationElement.ValueKind is JsonValueKind.True or JsonValueKind.False &&
                                   confirmationElement.GetBoolean();
        var reason = root.TryGetProperty("reason", out var reasonElement) &&
                     reasonElement.ValueKind == JsonValueKind.String
            ? reasonElement.GetString() ?? string.Empty
            : string.Empty;
        var payload = root.TryGetProperty("payload", out var payloadElement)
            ? payloadElement.Clone()
            : AiJson.ToElement(new { });

        ValidateIntentForTool(functionCall.FunctionName, intent);

        return new AiRouteDecision(
            intent,
            functionCall.FunctionName,
            payload,
            confidence,
            requiresConfirmation,
            reason);
    }

    private static string GetRequiredString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
            throw new InvalidOperationException($"OpenAI routing response is missing '{propertyName}'.");

        return property.GetString() ?? throw new InvalidOperationException($"OpenAI routing response has empty '{propertyName}'.");
    }

    private static void ValidateIntentForTool(string toolName, string intent)
    {
        string[] allowed = toolName switch
        {
            AiToolNames.GetMyAssets => [AiIntents.QueryMyAssets],
            AiToolNames.GetAssetStatus => [AiIntents.QueryAssetStatus],
            AiToolNames.SearchManualSources => [AiIntents.SearchManual],
            AiToolNames.CreateMaintenanceDraft => [AiIntents.CreateMaintenanceDraft, AiIntents.TroubleshootDevice],
            AiToolNames.ListAssets => [AiIntents.ListAssets],
            AiToolNames.ListAssetModels => [AiIntents.ListAssetModels],
            AiToolNames.CreateAllocationRequest => [AiIntents.CreateAllocationRequest],
            AiToolNames.ListPendingRequests => [AiIntents.ListPendingRequests],
            AiToolNames.ApproveAllocationRequest => [AiIntents.ApproveAllocationRequest],
            AiToolNames.RejectAllocationRequest => [AiIntents.RejectAllocationRequest],
            AiToolNames.AskClarifyingQuestion => [AiIntents.NeedClarification, AiIntents.GeneralHelp],
            _ => Array.Empty<string>()
        };

        if (!allowed.Contains(intent, StringComparer.Ordinal))
            throw new InvalidOperationException($"Intent '{intent}' is not allowed for tool '{toolName}'.");
    }
}
#pragma warning restore OPENAI001
