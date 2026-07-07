using AssetMgmt.Application.Common;
using AssetMgmt.Application.Reports;
using AssetMgmt.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetMgmt.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Policy = "RequireManager")]
public class ReportsController : ControllerBase
{
    private readonly ReportService _service;

    public ReportsController(ReportService service)
    {
        _service = service;
    }

    /// <summary>Aggregate KPIs for the dashboard (counts by status/category, book value, pending requests).</summary>
    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardStatsDto>> Dashboard(CancellationToken ct)
        => Ok(await _service.GetDashboardAsync(ct));

    /// <summary>In-stock assets idle beyond the threshold (default 3 months).</summary>
    [HttpGet("idle-assets")]
    public async Task<ActionResult<PagedResult<IdleAssetItem>>> IdleAssets(
        [FromQuery] int? idleMonths,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await _service.GetIdleAssetsAsync(idleMonths, new PageQuery(page, pageSize), ct));

    [HttpGet("asset-matrix")]
    public async Task<ActionResult<IReadOnlyList<AssetMatrixItem>>> AssetMatrix(
        [FromQuery] Guid? departmentId, [FromQuery] AssetStatus? status, CancellationToken ct)
        => Ok(await _service.GetAssetMatrixAsync(departmentId, status, ct));

    [HttpGet("allocation-timeline")]
    public async Task<ActionResult<IReadOnlyList<AllocationTimelineItem>>> AllocationTimeline(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] Guid? departmentId, CancellationToken ct)
        => Ok(await _service.GetAllocationTimelineAsync(from, to, departmentId, ct));
}
