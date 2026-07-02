using System.Text.Json;
using AssetMgmt.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace AssetMgmt.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (DomainException ex)
        {
            await WriteProblem(context, StatusCodes.Status400BadRequest, "Bad Request", ex.Message);
        }
        catch (ConflictException ex)
        {
            await WriteProblem(context, StatusCodes.Status409Conflict, "Conflict", ex.Message);
        }
        catch (DbUpdateConcurrencyException)
        {
            await WriteProblem(context, StatusCodes.Status409Conflict,
                "Conflict", "The record was changed by another action. Refresh and try again.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            await WriteProblem(context, StatusCodes.Status500InternalServerError,
                "Internal Server Error", "An unexpected error occurred.");
        }
    }

    private static async Task WriteProblem(HttpContext context, int status, string title, string detail)
    {
        if (context.Response.HasStarted) return;
        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        var payload = JsonSerializer.Serialize(new
        {
            type = $"https://httpstatuses.io/{status}",
            title,
            status,
            detail
        });
        await context.Response.WriteAsync(payload);
    }
}
