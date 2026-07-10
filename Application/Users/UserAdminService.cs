using AssetMgmt.Application.Auth;
using AssetMgmt.Application.Common;
using AssetMgmt.Domain.Entities;
using AssetMgmt.Domain.Enums;
using AssetMgmt.Domain.Exceptions;
using AssetMgmt.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AssetMgmt.Application.Users;

public class UserAdminService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IPasswordHasher _hasher;
    private readonly DataScopeService _scope;

    public UserAdminService(AppDbContext db, ICurrentUser currentUser, IPasswordHasher hasher,
        DataScopeService scope)
    {
        _db = db;
        _currentUser = currentUser;
        _hasher = hasher;
        _scope = scope;
    }

    public async Task<PagedResult<UserListItem>> ListAsync(
        UserRole? role, Guid? departmentId, bool? isActive, string? search, PageQuery page, CancellationToken ct)
    {
        var query = _db.Users.AsNoTracking();

        if (_scope.IsManager)
        {
            var departments = await _scope.GetDepartmentIdsAsync(ct);
            query = query.Where(u => u.DepartmentId != null && departments.Contains(u.DepartmentId.Value));
        }

        if (role is not null) query = query.Where(u => u.Role == role);
        if (departmentId is not null) query = query.Where(u => u.DepartmentId == departmentId);
        if (isActive is not null) query = query.Where(u => u.IsActive == isActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(u =>
                EF.Functions.Like(u.FullName, $"%{term}%") ||
                EF.Functions.Like(u.UserName, $"%{term}%") ||
                EF.Functions.Like(u.EmployeeCode, $"%{term}%"));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(u => u.FullName)
            .Skip(page.Skip).Take(page.NormalizedPageSize)
            .Select(u => new UserListItem(
                u.Id, u.UserName, u.Email, u.FullName, u.EmployeeCode, u.Role,
                u.DepartmentId, u.Department != null ? u.Department.Name : null, u.IsActive))
            .ToListAsync(ct);

        return new PagedResult<UserListItem>(items, total, page.NormalizedPage, page.NormalizedPageSize);
    }

    public async Task<UserDto> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var u = await _db.Users.AsNoTracking()
            .Include(x => x.Department)
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new DomainException("User not found.");
        if (_scope.IsManager)
            await _scope.EnsureDepartmentAccessAsync(u.DepartmentId, ct);
        return Map(u);
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 8)
            throw new DomainException("Password must be at least 8 characters.");

        var normalizedUserName = req.UserName.Trim().ToUpperInvariant();
        var normalizedEmail = req.Email.Trim().ToUpperInvariant();
        var employeeCode = req.EmployeeCode.Trim();

        if (await _db.Users.AnyAsync(u => u.NormalizedUserName == normalizedUserName, ct))
            throw new DomainException("Username is already taken.");
        if (await _db.Users.AnyAsync(u => u.NormalizedEmail == normalizedEmail, ct))
            throw new DomainException("Email is already in use.");
        if (await _db.Users.AnyAsync(u => u.EmployeeCode == employeeCode, ct))
            throw new DomainException("Employee code is already in use.");

        await EnsureDepartmentExistsAsync(req.DepartmentId, ct);

        var user = new User
        {
            UserName = req.UserName.Trim(),
            NormalizedUserName = normalizedUserName,
            Email = req.Email.Trim(),
            NormalizedEmail = normalizedEmail,
            EmployeeCode = employeeCode,
            FullName = req.FullName.Trim(),
            Role = req.Role,
            DepartmentId = req.DepartmentId,
            IsActive = true,
            PasswordHash = _hasher.Hash(req.Password),
            SecurityStamp = Guid.NewGuid().ToString("N")
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);
        return await GetByIdAsync(user.Id, ct);
    }

    public async Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest req, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new DomainException("User not found.");

        var normalizedEmail = req.Email.Trim().ToUpperInvariant();
        if (!string.Equals(user.NormalizedEmail, normalizedEmail, StringComparison.Ordinal) &&
            await _db.Users.AnyAsync(u => u.NormalizedEmail == normalizedEmail && u.Id != id, ct))
            throw new DomainException("Email is already in use.");

        await EnsureDepartmentExistsAsync(req.DepartmentId, ct);

        var departmentChanged = user.DepartmentId != req.DepartmentId;
        var deactivated = user.IsActive && !req.IsActive;
        if (deactivated && id == _currentUser.Id)
            throw new DomainException("You cannot deactivate your own account.");
        var securityContextChanged = departmentChanged || user.Role != req.Role || deactivated;

        user.Email = req.Email.Trim();
        user.NormalizedEmail = normalizedEmail;
        user.FullName = req.FullName.Trim();
        user.Role = req.Role;
        user.DepartmentId = req.DepartmentId;
        user.IsActive = req.IsActive;

        if (securityContextChanged)
        {
            user.SecurityStamp = Guid.NewGuid().ToString("N");
            await RevokeRefreshSessionsAsync(user.Id, ct);
        }

        if (deactivated)
            await CompleteOffboardingAsync(user.Id, ct);
        else if (departmentChanged)
            await CreateReturnObligationsAsync(user.Id,
                ReturnObligationReason.DepartmentChanged, ct);

        await _db.SaveChangesAsync(ct);
        return await GetByIdAsync(user.Id, ct);
    }

    public async Task ResetPasswordAsync(Guid id, string newPassword, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
            throw new DomainException("Password must be at least 8 characters.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new DomainException("User not found.");

        user.PasswordHash = _hasher.Hash(newPassword);
        // Rotate the security stamp so existing refresh tokens are invalidated.
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        await RevokeRefreshSessionsAsync(user.Id, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<UserDto> OffboardAsync(Guid id, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new DomainException("User not found.");

        if (id == _currentUser.Id)
            throw new DomainException("You cannot deactivate your own account.");

        user.IsActive = false;
        // Rotate stamp to revoke any active sessions.
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        await RevokeRefreshSessionsAsync(user.Id, ct);
        await CompleteOffboardingAsync(user.Id, ct);
        await _db.SaveChangesAsync(ct);
        return await GetByIdAsync(user.Id, ct);
    }

    public async Task DeactivateAsync(Guid id, CancellationToken ct)
    {
        await OffboardAsync(id, ct);
    }

    private async Task EnsureDepartmentExistsAsync(Guid? departmentId, CancellationToken ct)
    {
        if (departmentId is null) return;
        if (!await _db.Departments.AnyAsync(d => d.Id == departmentId, ct))
            throw new DomainException("Department not found.");
    }

    private async Task CreateReturnObligationsAsync(
        Guid userId, ReturnObligationReason reason, CancellationToken ct)
    {
        var openAssetIds = await _db.ReturnObligations
            .Where(o => o.UserId == userId && o.ResolvedAt == null)
            .Select(o => o.AssetInstanceId)
            .ToListAsync(ct);
        var heldAssetIds = await _db.AssetInstances
            .Where(a => a.CurrentHolderId == userId && a.Status == AssetStatus.Allocated &&
                        !openAssetIds.Contains(a.Id))
            .Select(a => a.Id)
            .ToListAsync(ct);
        var dueAt = DateTime.UtcNow.AddDays(3);
        foreach (var assetId in heldAssetIds)
            _db.ReturnObligations.Add(new ReturnObligation
            {
                UserId = userId,
                AssetInstanceId = assetId,
                Reason = reason,
                DueAt = dueAt
            });
    }

    private async Task CompleteOffboardingAsync(Guid userId, CancellationToken ct)
    {
        var actor = _currentUser.Id;
        var now = DateTime.UtcNow;
        var pendingRequests = await _db.AllocationRequests
            .Include(r => r.AssetInstance)
            .Where(r => r.RequesterId == userId &&
                        (r.Status == RequestStatus.Pending || r.Status == RequestStatus.Locked))
            .ToListAsync(ct);

        foreach (var request in pendingRequests)
        {
            request.Status = RequestStatus.Cancelled;
            request.CancelledAt = now;
            request.CancellationReason = "Requester offboarded.";
            request.LockToken = null;
            request.LockExpiresAt = null;
            request.UpdatedAt = now;

            var asset = request.AssetInstance;
            if (asset.Status == AssetStatus.LockedTemp &&
                asset.LockHolderUserId == userId &&
                asset.CurrentHolderId == userId)
            {
                asset.Status = AssetStatus.InStock;
                asset.CurrentHolderId = null;
                asset.LockToken = null;
                asset.LockExpiresAt = null;
                asset.LockHolderUserId = null;
                asset.UpdatedBy = actor;
            }
        }

        await CreateReturnObligationsAsync(userId, ReturnObligationReason.UserDeactivated, ct);
    }

    private async Task RevokeRefreshSessionsAsync(Guid userId, CancellationToken ct)
    {
        var sessions = await _db.RefreshSessions
            .Where(x => x.UserId == userId && x.RevokedAt == null)
            .ToListAsync(ct);
        var now = DateTime.UtcNow;
        foreach (var session in sessions) session.RevokedAt = now;
    }

    private static UserDto Map(User u) => new(
        u.Id, u.UserName, u.Email, u.FullName, u.EmployeeCode, u.Role,
        u.DepartmentId, u.Department?.Name, u.IsActive, u.LastLoginAt, u.CreatedAt, u.UpdatedAt);
}
