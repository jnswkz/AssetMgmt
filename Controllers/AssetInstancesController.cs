using AssetMgmt.Application.Assets;
using AssetMgmt.Application.Common;
using AssetMgmt.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetMgmt.Controllers;

[ApiController]
[Route("api/assets")]
[Authorize(Policy = "RequireEmployee")]
public class AssetInstancesController : ControllerBase
{
    private readonly AssetInstanceService _service;

    public AssetInstancesController(AssetInstanceService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<AssetInstanceListItem>>> List(
        [FromQuery] AssetStatus? status, [FromQuery] Guid? modelId, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await _service.ListAsync(status, modelId, search, new PageQuery(page, pageSize), ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AssetInstanceDto>> Get(Guid id, CancellationToken ct)
        => Ok(await _service.GetByIdAsync(id, ct));

    [HttpPost]
    [Authorize(Policy = "RequireAdminIT")]
    public async Task<ActionResult<AssetInstanceDto>> Create(CreateAssetInstanceRequest req, CancellationToken ct)
    {
        var created = await _service.CreateAsync(req, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "RequireAdminIT")]
    public async Task<ActionResult<AssetInstanceDto>> Update(Guid id, UpdateAssetInstanceRequest req, CancellationToken ct)
        => Ok(await _service.UpdateAsync(id, req, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "RequireAdminIT")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.SoftDeleteAsync(id, ct);
        return NoContent();
    }
}
