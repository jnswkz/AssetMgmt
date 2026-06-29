using AssetMgmt.Application.Allocations;
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
    private readonly AssetLifecycleService _lifecycle;

    public AssetInstancesController(AssetInstanceService service, AssetLifecycleService lifecycle)
    {
        _service = service;
        _lifecycle = lifecycle;
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

    // ---------- Lifecycle (Manager/AdminIT) ----------

    [HttpPost("{id:guid}/return")]
    [Authorize(Policy = "RequireManager")]
    public async Task<IActionResult> Return(Guid id, ReturnAssetDto body, CancellationToken ct)
    {
        await _lifecycle.ReturnAsync(id, body, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/transfer")]
    [Authorize(Policy = "RequireManager")]
    public async Task<IActionResult> Transfer(Guid id, TransferAssetDto body, CancellationToken ct)
    {
        await _lifecycle.TransferAsync(id, body, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/maintenance")]
    [Authorize(Policy = "RequireManager")]
    public async Task<ActionResult<MaintenanceRecordDto>> StartMaintenance(Guid id, StartMaintenanceDto body, CancellationToken ct)
        => Ok(await _lifecycle.StartMaintenanceAsync(id, body, ct));

    [HttpPost("{id:guid}/maintenance/{recordId:guid}/complete")]
    [Authorize(Policy = "RequireManager")]
    public async Task<ActionResult<MaintenanceRecordDto>> CompleteMaintenance(
        Guid id, Guid recordId, CompleteMaintenanceDto body, CancellationToken ct)
        => Ok(await _lifecycle.CompleteMaintenanceAsync(id, recordId, body, ct));

    [HttpGet("{id:guid}/maintenance")]
    [Authorize(Policy = "RequireManager")]
    public async Task<ActionResult<PagedResult<MaintenanceRecordDto>>> MaintenanceHistory(
        Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await _lifecycle.ListMaintenanceAsync(id, new PageQuery(page, pageSize), ct));

    [HttpPost("{id:guid}/dispose")]
    [Authorize(Policy = "RequireManager")]
    public async Task<ActionResult<DisposalDto>> Dispose(Guid id, DisposeAssetDto body, CancellationToken ct)
        => Ok(await _lifecycle.DisposeAsync(id, body, ct));
}
