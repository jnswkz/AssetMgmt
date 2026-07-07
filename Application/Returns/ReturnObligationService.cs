using AssetMgmt.Application.Auth;
using AssetMgmt.Application.Common;
using AssetMgmt.Domain.Exceptions;
using AssetMgmt.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AssetMgmt.Application.Returns;

public class ReturnObligationService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly DataScopeService _scope;

    public ReturnObligationService(AppDbContext db, ICurrentUser currentUser, DataScopeService scope)
    {
        _db = db;
        _currentUser = currentUser;
        _scope = scope;
    }

    public async Task<IReadOnlyList<ReturnObligationDto>> ListAsync(bool includeResolved, CancellationToken ct)
    {
        var query = _db.ReturnObligations.AsNoTracking();
        if (!includeResolved) query = query.Where(o => o.ResolvedAt == null);
        if (_scope.IsManager)
        {
            var departments = await _scope.GetDepartmentIdsAsync(ct);
            query = query.Where(o => o.User.DepartmentId != null && departments.Contains(o.User.DepartmentId.Value));
        }

        return await query.OrderBy(o => o.ResolvedAt != null).ThenBy(o => o.DueAt)
            .Select(o => new ReturnObligationDto(
                o.Id, o.UserId, o.User.FullName, o.AssetInstanceId, o.AssetInstance.AssetCode,
                o.AssetInstance.Model.Name, o.Reason, o.DueAt, o.ResolvedAt,
                o.ResolutionNotes, o.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<ReturnObligationDto> ResolveAsync(Guid id, string? notes, CancellationToken ct)
    {
        var obligation = await _db.ReturnObligations.Include(o => o.User)
            .FirstOrDefaultAsync(o => o.Id == id, ct)
            ?? throw new DomainException("Return obligation not found.");
        if (obligation.ResolvedAt is not null)
            throw new DomainException("Return obligation is already resolved.");
        if (_scope.IsManager)
            await _scope.EnsureDepartmentAccessAsync(obligation.User.DepartmentId, ct);

        obligation.ResolvedAt = DateTime.UtcNow;
        obligation.ResolvedBy = _currentUser.Id;
        obligation.ResolutionNotes = notes?.Trim();
        await _db.SaveChangesAsync(ct);
        return (await ListAsync(true, ct)).Single(o => o.Id == id);
    }
}
