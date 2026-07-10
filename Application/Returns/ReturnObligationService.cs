using AssetMgmt.Application.Auth;
using AssetMgmt.Application.Common;
using AssetMgmt.Domain.Entities;
using AssetMgmt.Domain.Enums;
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

    private Guid CurrentUserId =>
        _currentUser.Id ?? throw new DomainException("Not authenticated.");

    public Task<ReturnObligationDto> ResolveAsync(Guid id, string? notes, CancellationToken ct) =>
        _db.ExecuteWithRetryStrategyAsync(() => ResolveCoreAsync(id, notes, ct));

    private async Task<ReturnObligationDto> ResolveCoreAsync(Guid id, string? notes, CancellationToken ct)
    {
        var actor = CurrentUserId;
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var obligation = await _db.ReturnObligations
            .Include(o => o.User)
            .Include(o => o.AssetInstance)
            .FirstOrDefaultAsync(o => o.Id == id, ct)
            ?? throw new DomainException("Return obligation not found.");
        if (obligation.ResolvedAt is not null)
            throw new DomainException("Return obligation is already resolved.");
        if (_scope.IsManager)
            await _scope.EnsureDepartmentAccessAsync(obligation.User.DepartmentId, ct);

        var now = DateTime.UtcNow;
        var asset = obligation.AssetInstance;
        if (asset.Status == AssetStatus.Allocated && asset.CurrentHolderId == obligation.UserId)
        {
            asset.Status = AssetStatus.InStock;
            asset.CurrentHolderId = null;
            asset.UpdatedBy = actor;

            _db.Allocations.Add(new Allocation
            {
                AssetInstanceId = asset.Id,
                UserId = obligation.UserId,
                EventType = AllocationEventType.Returned,
                StartDate = now.Date,
                EndDate = now.Date,
                Notes = notes?.Trim() ?? "Returned during offboarding.",
                CreatedBy = actor
            });
        }
        else if (asset.Status == AssetStatus.Allocated && asset.CurrentHolderId != obligation.UserId)
        {
            throw new DomainException("Asset is allocated to another user.");
        }

        obligation.ResolvedAt = now;
        obligation.ResolvedBy = actor;
        obligation.ResolutionNotes = notes?.Trim();
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return (await ListAsync(true, ct)).Single(o => o.Id == id);
    }
}
