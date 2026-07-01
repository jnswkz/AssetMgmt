using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Security.Claims;
using AssetMgmt.Domain.Entities;
using AssetMgmt.Infrastructure.Persistence;

namespace AssetMgmt.Middleware;

/// <summary>
/// Records an <see cref="AuditLog"/> row for every state-changing API call
/// (POST/PUT/PATCH/DELETE under /api). Read requests and the dashboard are
/// ignored. Runs after the request so it can capture the outcome; auditing
/// never affects the response — failures here are swallowed and logged.
///
/// Request bodies are deliberately NOT recorded (they may contain passwords).
/// </summary>
public class AuditLoggingMiddleware
{
    private static readonly HashSet<string> AuditedMethods =
        new(StringComparer.OrdinalIgnoreCase) { "POST", "PUT", "PATCH", "DELETE" };

    private readonly RequestDelegate _next;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuditLoggingMiddleware> _logger;

    public AuditLoggingMiddleware(
        RequestDelegate next, IServiceScopeFactory scopeFactory, ILogger<AuditLoggingMiddleware> logger)
    {
        _next = next;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!ShouldAudit(context))
        {
            await _next(context);
            return;
        }

        ExceptionDispatchInfo? failure = null;
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            failure = ExceptionDispatchInfo.Capture(ex);
        }

        await WriteAuditAsync(context, failure?.SourceException);

        failure?.Throw(); // preserve original stack for the exception middleware
    }

    private static bool ShouldAudit(HttpContext context)
    {
        if (!AuditedMethods.Contains(context.Request.Method))
            return false;

        var path = context.Request.Path;
        return path.StartsWithSegments("/api");
    }

    private async Task WriteAuditAsync(HttpContext context, Exception? exception)
    {
        try
        {
            var req = context.Request;
            var failed = exception is not null || context.Response.StatusCode >= 400;

            var entry = new AuditLog
            {
                UserId = TryGetUserId(context.User),
                Action = $"{req.Method} {req.Path}",
                EntityType = DeriveEntityType(req.Path),
                EntityId = TryGetEntityId(context),
                IpAddress = context.Connection.RemoteIpAddress?.ToString(),
                UserAgent = Truncate(req.Headers.UserAgent.ToString(), 500),
                CorrelationId = TryGetCorrelationId(context),
                Severity = failed ? "Warning" : "Info",
                Result = failed ? "Failed" : "Success",
                ErrorMessage = exception?.Message,
            };

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.AuditLogs.Add(entry);
            await db.SaveChangesAsync(context.RequestAborted);
        }
        catch (Exception ex)
        {
            // Auditing must never break the request path.
            _logger.LogWarning(ex, "Failed to write audit log for {Method} {Path}",
                context.Request.Method, context.Request.Path);
        }
    }

    private static Guid? TryGetUserId(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;

    /// <summary>First path segment after "api", e.g. /api/requests/{id} → "requests".</summary>
    private static string? DeriveEntityType(PathString path)
    {
        var segments = path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments is { Length: >= 2 } ? segments[1] : null;
    }

    /// <summary>First GUID-valued route parameter, if any (id, assetId, etc.).</summary>
    private static Guid? TryGetEntityId(HttpContext context)
    {
        var endpoint = context.GetRouteData();
        foreach (var value in endpoint.Values.Values)
        {
            if (Guid.TryParse(value?.ToString(), out var id))
                return id;
        }
        return null;
    }

    private static Guid? TryGetCorrelationId(HttpContext context) =>
        Guid.TryParse(Activity.Current?.TraceId.ToString(), out var id) ? id : null;

    private static string? Truncate(string? value, int max) =>
        string.IsNullOrEmpty(value) ? null : value.Length <= max ? value : value[..max];
}
