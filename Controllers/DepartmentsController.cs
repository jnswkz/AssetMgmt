using AssetMgmt.Application.Common;
using AssetMgmt.Application.Departments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetMgmt.Controllers;

[ApiController]
[Route("api/departments")]
[Authorize(Policy = "RequireManager")]
public class DepartmentsController : ControllerBase
{
    private readonly DepartmentService _service;

    public DepartmentsController(DepartmentService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<DepartmentListItem>>> List(
        [FromQuery] bool? isActive, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await _service.ListAsync(isActive, search, new PageQuery(page, pageSize), ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DepartmentDto>> Get(Guid id, CancellationToken ct)
        => Ok(await _service.GetByIdAsync(id, ct));

    [HttpPost]
    [Authorize(Policy = "RequireAdminIT")]
    public async Task<ActionResult<DepartmentDto>> Create(CreateDepartmentRequest req, CancellationToken ct)
    {
        var created = await _service.CreateAsync(req, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "RequireAdminIT")]
    public async Task<ActionResult<DepartmentDto>> Update(Guid id, UpdateDepartmentRequest req, CancellationToken ct)
        => Ok(await _service.UpdateAsync(id, req, ct));

    [HttpPost("{id:guid}/manager")]
    [Authorize(Policy = "RequireAdminIT")]
    public async Task<ActionResult<DepartmentDto>> AssignManager(Guid id, AssignManagerRequest req, CancellationToken ct)
        => Ok(await _service.AssignManagerAsync(id, req.ManagerId, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "RequireAdminIT")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.SoftDeleteAsync(id, ct);
        return NoContent();
    }
}
