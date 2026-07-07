using AssetMgmt.Application.Common;
using AssetMgmt.Application.Handover;
using AssetMgmt.Application.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetMgmt.Controllers;

[ApiController]
[Route("api/requests")]
public class AllocationRequestsController : ControllerBase
{
    private readonly AllocationRequestService _service;
    private readonly IHandoverDocumentService _handover;

    public AllocationRequestsController(
        AllocationRequestService service, IHandoverDocumentService handover)
    {
        _service = service;
        _handover = handover;
    }

    [HttpPost]
    [Authorize(Policy = "RequireEmployee")]
    public async Task<ActionResult<AllocationRequestDto>> Create(CreateRequestDto req, CancellationToken ct)
    {
        var created = await _service.CreateAsync(req, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpGet("pending")]
    [Authorize(Policy = "RequireManager")]
    public async Task<ActionResult<PagedResult<RequestListItem>>> Pending(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await _service.ListPendingAsync(new PageQuery(page, pageSize), ct));

    [HttpGet("mine")]
    [Authorize(Policy = "RequireEmployee")]
    public async Task<ActionResult<PagedResult<RequestListItem>>> Mine(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await _service.ListMineAsync(new PageQuery(page, pageSize), ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "RequireEmployee")]
    public async Task<ActionResult<AllocationRequestDto>> Get(Guid id, CancellationToken ct)
        => Ok(await _service.GetByIdAsync(id, ct));

    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = "RequireManager")]
    public async Task<ActionResult<AllocationRequestDto>> Approve(Guid id, CancellationToken ct)
        => Ok(await _service.ApproveAsync(id, ct));

    [HttpPost("{id:guid}/reject")]
    [Authorize(Policy = "RequireManager")]
    public async Task<ActionResult<AllocationRequestDto>> Reject(Guid id, RejectRequestDto body, CancellationToken ct)
        => Ok(await _service.RejectAsync(id, body.Reason, ct));

    [HttpGet("{id:guid}/handover")]
    [Authorize(Policy = "RequireEmployee")]
    public async Task<ActionResult<HandoverResult>> GetHandover(Guid id, CancellationToken ct)
    {
        await _service.GetByIdAsync(id, ct);
        var result = await _handover.GetForRequestAsync(id, ct);
        return result is not null ? Ok(result) : NotFound();
    }
}
