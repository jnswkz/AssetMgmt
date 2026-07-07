using AssetMgmt.Application.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetMgmt.Controllers;

[ApiController]
[Route("api/inventory-scans")]
[Authorize(Policy = "RequireManager")]
public class InventoryScansController : ControllerBase
{
    private readonly InventoryScanService _service;
    public InventoryScansController(InventoryScanService service) => _service = service;

    [HttpPost]
    public async Task<ActionResult<InventoryScanDto>> Create(CreateInventoryScanRequest body, CancellationToken ct)
    {
        var scan = await _service.CreateAsync(body, ct);
        return Created($"/api/inventory-scans/{scan.Id}", scan);
    }

    [HttpPost("{id:guid}/items")]
    public async Task<ActionResult<InventoryScanDto>> AddItem(
        Guid id, AddInventoryScanItemRequest body, CancellationToken ct)
        => Ok(await _service.AddItemAsync(id, body.AssetCode, ct));

    [HttpPost("{id:guid}/close")]
    public async Task<ActionResult<InventoryScanDto>> Close(Guid id, CancellationToken ct)
        => Ok(await _service.CloseAsync(id, ct));
}
