using AssetMgmt.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
    public async Task<ActionResult<TokenResponse>> Login(LoginRequest req, CancellationToken ct)
    {
        var tokens = await _auth.LoginAsync(req, ct);
        return Ok(tokens);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<TokenResponse>> Refresh(RefreshRequest req, CancellationToken ct)
    {
        var tokens = await _auth.RefreshAsync(req, ct);
        return Ok(tokens);
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
