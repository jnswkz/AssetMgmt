using AssetMgmt.Application.Agents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AssetMgmt.Controllers;

[ApiController]
[Route("api/ai")]
[Authorize(Policy = "RequireEmployee")]
[EnableRateLimiting("Ai")]
public class AiController : ControllerBase
{
    private readonly AiAskService _service;
    private readonly AiPendingActionService _pendingActions;

    public AiController(AiAskService service, AiPendingActionService pendingActions)
    {
        _service = service;
        _pendingActions = pendingActions;
    }

    [HttpPost("ask")]
    public async Task<ActionResult<AiAskResponse>> Ask(AiAskRequest request, CancellationToken ct)
        => Ok(await _service.AskAsync(request, ct));

    [HttpPost("actions/{id:guid}/confirm")]
    public async Task<ActionResult<AiAskResponse>> Confirm(Guid id, CancellationToken ct)
        => Ok(await _pendingActions.ConfirmAsync(id, ct));

    [HttpDelete("actions/{id:guid}")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        await _pendingActions.CancelAsync(id, ct);
        return NoContent();
    }
}
