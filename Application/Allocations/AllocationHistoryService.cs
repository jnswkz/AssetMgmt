using AssetMgmt.Application.Auth;
using AssetMgmt.Application.Common;
using AssetMgmt.Domain.Enums;
using AssetMgmt.Domain.Exceptions;
using AssetMgmt.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AssetMgmt.Application.Allocations;

public class AllocationHistoryService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public AllocationHistoryService(AppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<AllocationHistoryItem>> ListAsync(
        Guid? assetId, Guid? userId, PageQuery page, CancellationToken ct)
    {
        var query = _db.Allocations.AsNoTracking();

        if (assetId is not null)
            query = query.Where(a => a.AssetInstanceId == assetId);
        if (userId is not null)
            query = query.Where(a => a.UserId == userId);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip(page.Skip).Take(page.NormalizedPageSize)
            .Select(a => new AllocationHistoryItem(
                a.Id, a.AssetInstanceId, a.AssetInstance.AssetCode, a.AssetInstance.Model.Name,
                a.UserId, a.User.FullName, a.EventType, a.StartDate, a.EndDate,
                a.AllocationRequestId, a.Notes, a.CreatedAt))
            .ToListAsync(ct);

        return new PagedResult<AllocationHistoryItem>(items, total, page.NormalizedPage, page.NormalizedPageSize);
    }

    /// <summary>Assets currently held by the signed-in user (Allocated and held by them).</summary>
    public async Task<IReadOnlyList<MyAssetItem>> GetMyAssetsAsync(CancellationToken ct)
    {
        var userId = _currentUser.Id ?? throw new DomainException("Not authenticated.");

        return await _db.AssetInstances.AsNoTracking()
            .Where(a => a.CurrentHolderId == userId && a.Status == AssetStatus.Allocated)
            .OrderBy(a => a.AssetCode)
            .Select(a => new MyAssetItem(
                a.Id, a.AssetCode, a.Model.Name, a.Status, a.Location,
                _db.Allocations
                    .Where(al => al.AssetInstanceId == a.Id && al.UserId == userId
                        && al.EventType == AllocationEventType.Allocated)
                    .OrderByDescending(al => al.CreatedAt)
                    .Select(al => al.StartDate)
                    .FirstOrDefault()))
            .ToListAsync(ct);
    }
}
