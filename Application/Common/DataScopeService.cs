using AssetMgmt.Application.Auth;
using AssetMgmt.Domain.Exceptions;
using AssetMgmt.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AssetMgmt.Application.Common;

public class DataScopeService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public DataScopeService(AppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public bool IsAdmin => string.Equals(_currentUser.Role, "AdminIT", StringComparison.Ordinal);
    public bool IsManager => string.Equals(_currentUser.Role, "Manager", StringComparison.Ordinal);
    public Guid UserId => _currentUser.Id ?? throw new DomainException("Not authenticated.");

    public async Task<IReadOnlySet<Guid>> GetDepartmentIdsAsync(CancellationToken ct)
    {
        if (IsAdmin)
            return new HashSet<Guid>();

        var userId = UserId;
        var ids = await _db.Departments.AsNoTracking()
            .Where(d => d.ManagerId == userId)
            .Select(d => d.Id)
            .ToListAsync(ct);

        var ownDepartmentId = await _db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.DepartmentId)
            .SingleAsync(ct);
        if (ownDepartmentId is { } id)
            ids.Add(id);

        return ids.ToHashSet();
    }

    public async Task EnsureDepartmentAccessAsync(Guid? departmentId, CancellationToken ct)
    {
        if (IsAdmin)
            return;
        var allowed = await GetDepartmentIdsAsync(ct);
        if (departmentId is null || !allowed.Contains(departmentId.Value))
            throw new DomainException("The requested record is outside your department scope.");
    }
}
