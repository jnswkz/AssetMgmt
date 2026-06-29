using AssetMgmt.Application.Allocations;
using AssetMgmt.Application.Common;
using AssetMgmt.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetMgmt.Controllers;

[ApiController]
[Route("api/disposals")]
[Authorize(Policy = "RequireManager")]
public class DisposalsController : ControllerBase
{
    private readonly AssetLifecycleService _lifecycle;

    public DisposalsController(AssetLifecycleService lifecycle)
    {
        _lifecycle = lifecycle;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<DisposalDto>>> List(
        [FromQuery] DisposalType? type,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await _lifecycle.ListDisposalsAsync(type, new PageQuery(page, pageSize), ct));
}
