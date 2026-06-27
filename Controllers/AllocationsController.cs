using AssetMgmt.Application.Allocations;
using AssetMgmt.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetMgmt.Controllers;

[ApiController]
[Route("api")]
public class AllocationsController : ControllerBase
{
    private readonly AllocationHistoryService _service;

    public AllocationsController(AllocationHistoryService service)
    {
        _service = service;
    }

    [HttpGet("allocations/history")]
    [Authorize(Policy = "RequireManager")]
    public async Task<ActionResult<PagedResult<AllocationHistoryItem>>> History(
        [FromQuery] Guid? assetId, [FromQuery] Guid? userId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await _service.ListAsync(assetId, userId, new PageQuery(page, pageSize), ct));

    [HttpGet("assets/{assetId:guid}/history")]
    [Authorize(Policy = "RequireEmployee")]
    public async Task<ActionResult<PagedResult<AllocationHistoryItem>>> AssetHistory(
        Guid assetId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await _service.ListAsync(assetId, null, new PageQuery(page, pageSize), ct));

    [HttpGet("me/assets")]
    [Authorize(Policy = "RequireEmployee")]
    public async Task<ActionResult<IReadOnlyList<MyAssetItem>>> MyAssets(CancellationToken ct)
        => Ok(await _service.GetMyAssetsAsync(ct));
}
