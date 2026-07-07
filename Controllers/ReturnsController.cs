using AssetMgmt.Application.Returns;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetMgmt.Controllers;

[ApiController]
[Route("api/returns/obligations")]
[Authorize(Policy = "RequireManager")]
public class ReturnsController : ControllerBase
{
    private readonly ReturnObligationService _service;
    public ReturnsController(ReturnObligationService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ReturnObligationDto>>> List(
        [FromQuery] bool includeResolved = false, CancellationToken ct = default)
        => Ok(await _service.ListAsync(includeResolved, ct));

    [HttpPost("{id:guid}/resolve")]
    public async Task<ActionResult<ReturnObligationDto>> Resolve(
        Guid id, ResolveReturnObligationRequest body, CancellationToken ct)
        => Ok(await _service.ResolveAsync(id, body.Notes, ct));
}
