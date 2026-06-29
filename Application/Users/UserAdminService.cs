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

    public UserAdminService(AppDbContext db, ICurrentUser currentUser, IPasswordHasher hasher)
    {
        _db = db;
        _currentUser = currentUser;
        _hasher = hasher;
    }

    public async Task<PagedResult<UserListItem>> ListAsync(
        UserRole? role, Guid? departmentId, bool? isActive, string? search, PageQuery page, CancellationToken ct)
    {
        var query = _db.Users.AsNoTracking();

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
        return Map(u);
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 6)
            throw new DomainException("Password must be at least 6 characters.");

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

        user.Email = req.Email.Trim();
        user.NormalizedEmail = normalizedEmail;
        user.FullName = req.FullName.Trim();
        user.Role = req.Role;
        user.DepartmentId = req.DepartmentId;
        user.IsActive = req.IsActive;

        await _db.SaveChangesAsync(ct);
        return await GetByIdAsync(user.Id, ct);
    }

    public async Task ResetPasswordAsync(Guid id, string newPassword, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
            throw new DomainException("Password must be at least 6 characters.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new DomainException("User not found.");

        user.PasswordHash = _hasher.Hash(newPassword);
        // Rotate the security stamp so existing refresh tokens are invalidated.
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeactivateAsync(Guid id, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new DomainException("User not found.");

        if (id == _currentUser.Id)
            throw new DomainException("You cannot deactivate your own account.");

        user.IsActive = false;
        user.DeletedAt = DateTime.UtcNow;
        // Rotate stamp to revoke any active sessions.
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        await _db.SaveChangesAsync(ct);
    }

    private async Task EnsureDepartmentExistsAsync(Guid? departmentId, CancellationToken ct)
    {
        if (departmentId is null) return;
        if (!await _db.Departments.AnyAsync(d => d.Id == departmentId, ct))
            throw new DomainException("Department not found.");
    }

    private static UserDto Map(User u) => new(
        u.Id, u.UserName, u.Email, u.FullName, u.EmployeeCode, u.Role,
        u.DepartmentId, u.Department?.Name, u.IsActive, u.LastLoginAt, u.CreatedAt, u.UpdatedAt);
}
