using AssetMgmt.Application.Common;
using AssetMgmt.Application.Users;
using AssetMgmt.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetMgmt.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Policy = "RequireManager")]
public class UsersController : ControllerBase
{
    private readonly UserAdminService _service;

    public UsersController(UserAdminService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<UserListItem>>> List(
        [FromQuery] UserRole? role, [FromQuery] Guid? departmentId, [FromQuery] bool? isActive,
        [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await _service.ListAsync(role, departmentId, isActive, search, new PageQuery(page, pageSize), ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserDto>> Get(Guid id, CancellationToken ct)
        => Ok(await _service.GetByIdAsync(id, ct));

    [HttpPost]
    [Authorize(Policy = "RequireAdminIT")]
    public async Task<ActionResult<UserDto>> Create(CreateUserRequest req, CancellationToken ct)
    {
        var created = await _service.CreateAsync(req, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "RequireAdminIT")]
    public async Task<ActionResult<UserDto>> Update(Guid id, UpdateUserRequest req, CancellationToken ct)
        => Ok(await _service.UpdateAsync(id, req, ct));

    [HttpPost("{id:guid}/reset-password")]
    [Authorize(Policy = "RequireAdminIT")]
    public async Task<IActionResult> ResetPassword(Guid id, ResetPasswordRequest req, CancellationToken ct)
    {
        await _service.ResetPasswordAsync(id, req.NewPassword, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/offboard")]
    [Authorize(Policy = "RequireAdminIT")]
    public async Task<ActionResult<UserDto>> Offboard(Guid id, CancellationToken ct)
        => Ok(await _service.OffboardAsync(id, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "RequireAdminIT")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeactivateAsync(id, ct);
        return NoContent();
    }
}
