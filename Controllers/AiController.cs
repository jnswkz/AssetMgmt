using AssetMgmt.Application.Agents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetMgmt.Controllers;

[ApiController]
[Route("api/ai")]
[Authorize(Policy = "RequireEmployee")]
public class AiController : ControllerBase
{
    private readonly AiAskService _service;

    public AiController(AiAskService service)
    {
        _service = service;
    }

    [HttpPost("ask")]
    public async Task<ActionResult<AiAskResponse>> Ask(AiAskRequest request, CancellationToken ct)
        => Ok(await _service.AskAsync(request, ct));
}
