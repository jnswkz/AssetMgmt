using AssetMgmt.Application.Assets;
using AssetMgmt.Application.Common;
using AssetMgmt.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetMgmt.Controllers;

[ApiController]
[Route("api/asset-models")]
[Authorize(Policy = "RequireManager")]
public class AssetModelsController : ControllerBase
{
    private readonly AssetModelService _service;

    public AssetModelsController(AssetModelService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<AssetModelListItem>>> List(
        [FromQuery] AssetCategory? category, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await _service.ListAsync(category, search, new PageQuery(page, pageSize), ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AssetModelDto>> Get(Guid id, CancellationToken ct)
        => Ok(await _service.GetByIdAsync(id, ct));

    [HttpPost]
    [Authorize(Policy = "RequireAdminIT")]
    public async Task<ActionResult<AssetModelDto>> Create(CreateAssetModelRequest req, CancellationToken ct)
    {
        var created = await _service.CreateAsync(req, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "RequireAdminIT")]
    public async Task<ActionResult<AssetModelDto>> Update(Guid id, UpdateAssetModelRequest req, CancellationToken ct)
        => Ok(await _service.UpdateAsync(id, req, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "RequireAdminIT")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.SoftDeleteAsync(id, ct);
        return NoContent();
    }
}
