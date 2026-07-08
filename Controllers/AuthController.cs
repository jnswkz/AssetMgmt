using AssetMgmt.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AssetMgmt.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _auth;
    private readonly ICurrentUser _currentUser;

    public AuthController(AuthService auth, ICurrentUser currentUser)
    {
        _auth = auth;
        _currentUser = currentUser;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("Login")]
    public async Task<ActionResult<TokenResponse>> Login(LoginRequest req, CancellationToken ct)
    {
        var tokens = await _auth.LoginAsync(req, ct);
        Response.Headers.CacheControl = "no-store";
        return Ok(tokens);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("Refresh")]
    public async Task<ActionResult<TokenResponse>> Refresh(RefreshRequest req, CancellationToken ct)
    {
        var tokens = await _auth.RefreshAsync(req, ct);
        Response.Headers.CacheControl = "no-store";
        return Ok(tokens);
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    [EnableRateLimiting("Refresh")]
    public async Task<IActionResult> Logout(LogoutRequest req, CancellationToken ct)
    {
        await _auth.LogoutAsync(req.RefreshToken, ct);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<MeResponse>> Me(CancellationToken ct)
    {
        var id = _currentUser.Id;
        if (id is null) return Unauthorized();
        var me = await _auth.GetMeAsync(id.Value, ct);
        return Ok(me);
    }
}
