using AssetMgmt.Application.Depreciation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetMgmt.Controllers;

[ApiController]
[Authorize(Policy = "RequireManager")]
public class DepreciationController : ControllerBase
{
    private readonly DepreciationService _service;
    public DepreciationController(DepreciationService service) => _service = service;

    [HttpGet("api/asset-models/{id:guid}/depreciation-policy")]
    public async Task<ActionResult<DepreciationPolicyDto>> GetPolicy(Guid id, CancellationToken ct)
    {
        var policy = await _service.GetPolicyAsync(id, ct);
        return policy is null ? NotFound() : Ok(policy);
    }

    [HttpPut("api/asset-models/{id:guid}/depreciation-policy")]
    [Authorize(Policy = "RequireAdminIT")]
    public async Task<ActionResult<DepreciationPolicyDto>> PutPolicy(
        Guid id, PutDepreciationPolicyRequest body, CancellationToken ct)
        => Ok(await _service.PutPolicyAsync(id, body, ct));

    [HttpGet("api/assets/{id:guid}/depreciation")]
    public async Task<ActionResult<AssetDepreciationDto>> Asset(Guid id, CancellationToken ct)
        => Ok(await _service.GetAssetAsync(id, ct));

    [HttpGet("api/reports/depreciation-alerts")]
    public async Task<ActionResult<IReadOnlyList<DepreciationAlertItem>>> Alerts(CancellationToken ct)
        => Ok(await _service.GetAlertsAsync(ct));
}
