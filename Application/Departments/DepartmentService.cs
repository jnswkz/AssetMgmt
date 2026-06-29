using AssetMgmt.Application.Common;
using AssetMgmt.Domain.Entities;
using AssetMgmt.Domain.Enums;
using AssetMgmt.Domain.Exceptions;
using AssetMgmt.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AssetMgmt.Application.Departments;

public class DepartmentService
{
    private readonly AppDbContext _db;

    public DepartmentService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<DepartmentListItem>> ListAsync(
        bool? isActive, string? search, PageQuery page, CancellationToken ct)
    {
        var query = _db.Departments.AsNoTracking();

        if (isActive is not null) query = query.Where(d => d.IsActive == isActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(d =>
                EF.Functions.Like(d.Name, $"%{term}%") || EF.Functions.Like(d.Code, $"%{term}%"));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(d => d.Code)
            .Skip(page.Skip).Take(page.NormalizedPageSize)
            .Select(d => new DepartmentListItem(
                d.Id, d.Code, d.Name, d.ManagerId, d.Manager != null ? d.Manager.FullName : null,
                d.IsActive, d.Users.Count(u => u.DeletedAt == null)))
            .ToListAsync(ct);

        return new PagedResult<DepartmentListItem>(items, total, page.NormalizedPage, page.NormalizedPageSize);
    }

    public async Task<DepartmentDto> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var d = await _db.Departments.AsNoTracking()
            .Include(x => x.Manager)
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new DomainException("Department not found.");
        return Map(d);
    }

    public async Task<DepartmentDto> CreateAsync(CreateDepartmentRequest req, CancellationToken ct)
    {
        var code = req.Code.Trim();
        if (await _db.Departments.AnyAsync(d => d.Code == code, ct))
            throw new DomainException("Department code is already in use.");

        if (req.ManagerId is not null)
            await EnsureValidManagerAsync(req.ManagerId.Value, ct);

        var dept = new Department
        {
            Code = code,
            Name = req.Name.Trim(),
            ManagerId = req.ManagerId,
            IsActive = true
        };

        _db.Departments.Add(dept);
        await _db.SaveChangesAsync(ct);
        return await GetByIdAsync(dept.Id, ct);
    }

    public async Task<DepartmentDto> UpdateAsync(Guid id, UpdateDepartmentRequest req, CancellationToken ct)
    {
        var dept = await _db.Departments.FirstOrDefaultAsync(d => d.Id == id, ct)
            ?? throw new DomainException("Department not found.");

        if (req.ManagerId is not null)
            await EnsureValidManagerAsync(req.ManagerId.Value, ct);

        dept.Name = req.Name.Trim();
        dept.ManagerId = req.ManagerId;
        dept.IsActive = req.IsActive;
        dept.UpdatedAt = DateTime.UtcNow; // no DB trigger on ref.departments

        await _db.SaveChangesAsync(ct);
        return await GetByIdAsync(dept.Id, ct);
    }

    public async Task<DepartmentDto> AssignManagerAsync(Guid id, Guid managerId, CancellationToken ct)
    {
        var dept = await _db.Departments.FirstOrDefaultAsync(d => d.Id == id, ct)
            ?? throw new DomainException("Department not found.");

        await EnsureValidManagerAsync(managerId, ct);

        dept.ManagerId = managerId;
        dept.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return await GetByIdAsync(dept.Id, ct);
    }

    public async Task SoftDeleteAsync(Guid id, CancellationToken ct)
    {
        var dept = await _db.Departments.FirstOrDefaultAsync(d => d.Id == id, ct)
            ?? throw new DomainException("Department not found.");

        if (await _db.Users.AnyAsync(u => u.DepartmentId == id, ct))
            throw new DomainException("Cannot delete a department that still has assigned users.");

        dept.DeletedAt = DateTime.UtcNow;
        dept.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private async Task EnsureValidManagerAsync(Guid managerId, CancellationToken ct)
    {
        var manager = await _db.Users.FirstOrDefaultAsync(u => u.Id == managerId, ct)
            ?? throw new DomainException("Manager not found.");
        if (!manager.IsActive)
            throw new DomainException("Manager is not active.");
        if (manager.Role is not (UserRole.Manager or UserRole.AdminIT))
            throw new DomainException("Assigned manager must have the Manager or AdminIT role.");
    }

    private static DepartmentDto Map(Department d) => new(
        d.Id, d.Code, d.Name, d.ManagerId, d.Manager?.FullName, d.IsActive, d.CreatedAt, d.UpdatedAt);
}
