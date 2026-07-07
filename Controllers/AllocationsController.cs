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
    private readonly IWebHostEnvironment _env;

    public AllocationsController(AllocationHistoryService service, IWebHostEnvironment env)
    {
        _service = service;
        _env = env;
    }

    [HttpGet("allocations/history")]
    [Authorize(Policy = "RequireManager")]
    public async Task<ActionResult<PagedResult<AllocationHistoryItem>>> History(
        [FromQuery] Guid? assetId, [FromQuery] Guid? userId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await _service.ListAsync(assetId, userId, new PageQuery(page, pageSize), ct));

    [HttpGet("assets/{assetId:guid}/history")]
    [Authorize(Policy = "RequireManager")]
    public async Task<ActionResult<PagedResult<AllocationHistoryItem>>> AssetHistory(
        Guid assetId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await _service.ListAsync(assetId, null, new PageQuery(page, pageSize), ct));

    [HttpGet("me/assets")]
    [Authorize(Policy = "RequireEmployee")]
    public async Task<ActionResult<IReadOnlyList<MyAssetItem>>> MyAssets(CancellationToken ct)
        => Ok(await _service.GetMyAssetsAsync(ct));

    [HttpGet("me/assets/{assetId:guid}/handover")]
    [Authorize(Policy = "RequireEmployee")]
    public async Task<IActionResult> MyAssetHandover(Guid assetId, CancellationToken ct)
    {
        var handover = await _service.GetMyAssetHandoverAsync(assetId, ct);
        if (handover is null)
            return NotFound();

        var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        var fullPath = ResolveWwwrootPath(webRoot, handover.FilePath);
        if (!System.IO.File.Exists(fullPath))
            return NotFound();

        return PhysicalFile(
            fullPath,
            "application/pdf",
            $"{handover.DocumentNumber}.pdf");
    }

    private static string ResolveWwwrootPath(string webRoot, string webPath)
    {
        var relativePath = webPath
            .TrimStart('/', '\\')
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);

        var root = Path.GetFullPath(webRoot);
        var fullPath = Path.GetFullPath(Path.Combine(root, relativePath));
        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootWithSeparator, StringComparison.Ordinal))
            throw new InvalidOperationException("Invalid file path.");

        return fullPath;
    }
}
