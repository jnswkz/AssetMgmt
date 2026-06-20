using System.Security.Claims;
using AssetMgmt.Application.Auth;
using Microsoft.AspNetCore.Http;

namespace AssetMgmt.Infrastructure.Services;

public class CurrentUser : ICurrentUser
{
    private readonly ClaimsPrincipal? _principal;

    public CurrentUser(IHttpContextAccessor accessor)
    {
        _principal = accessor.HttpContext?.User;
    }

    public bool IsAuthenticated => _principal?.Identity?.IsAuthenticated ?? false;

    public Guid? Id =>
        Guid.TryParse(_principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;

    public string? UserName => _principal?.FindFirst(ClaimTypes.Name)?.Value;

    public string? Role => _principal?.FindFirst(ClaimTypes.Role)?.Value;
}
