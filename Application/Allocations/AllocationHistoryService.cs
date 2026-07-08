using AssetMgmt.Application.Auth;
using AssetMgmt.Application.Common;
using AssetMgmt.Application.Handover;
using AssetMgmt.Domain.Enums;
using AssetMgmt.Domain.Exceptions;
using AssetMgmt.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace AssetMgmt.Application.Allocations;

public class AllocationHistoryService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IHandoverDocumentService _handover;
    private readonly DataScopeService _scope;

    public AllocationHistoryService(
        AppDbContext db,
        ICurrentUser currentUser,
        IHandoverDocumentService handover,
        DataScopeService scope)
    {
        _db = db;
        _currentUser = currentUser;
        _handover = handover;
        _scope = scope;
    }

    public async Task<PagedResult<AllocationHistoryItem>> ListAsync(
        Guid? assetId, Guid? userId, PageQuery page, CancellationToken ct)
    {
        var query = _db.Allocations.AsNoTracking();

        if (_scope.IsManager)
        {
            var departments = await _scope.GetDepartmentIdsAsync(ct);
            query = query.Where(a => a.User.DepartmentId != null &&
                                     departments.Contains(a.User.DepartmentId.Value));
        }

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
                a.UserId, a.User.FullName, a.EventType, a.StartDate, a.EndDate, a.ExpectedReturnAt,
                a.AllocationRequestId, a.Notes, a.CreatedAt))
            .ToListAsync(ct);

        return new PagedResult<AllocationHistoryItem>(items, total, page.NormalizedPage, page.NormalizedPageSize);
    }

    /// <summary>Assets currently held by the signed-in user (Allocated and held by them).</summary>
    public async Task<IReadOnlyList<MyAssetItem>> GetMyAssetsAsync(CancellationToken ct)
    {
        var userId = _currentUser.Id ?? throw new DomainException("Not authenticated.");

        var assets = await _db.AssetInstances.AsNoTracking()
            .Where(a => a.CurrentHolderId == userId && a.Status == AssetStatus.Allocated)
            .OrderBy(a => a.AssetCode)
            .Select(a => new
            {
                a.Id,
                a.AssetCode,
                ModelName = a.Model.Name,
                a.Status,
                a.Location
            })
            .ToListAsync(ct);

        var assetIds = assets.Select(a => a.Id).ToList();
        var allocationRows = await _db.Allocations.AsNoTracking()
            .Where(al => assetIds.Contains(al.AssetInstanceId) && al.UserId == userId
                && al.EventType == AllocationEventType.Allocated)
            .OrderByDescending(al => al.CreatedAt)
            .Select(al => new
            {
                al.Id,
                al.AssetInstanceId,
                al.StartDate,
                al.ExpectedReturnAt,
                al.AllocationRequestId
            })
            .ToListAsync(ct);

        var latestAllocations = allocationRows
            .GroupBy(al => al.AssetInstanceId)
            .ToDictionary(group => group.Key, group => group.First());
        var allocationIds = latestAllocations.Values.Select(al => al.Id).ToList();

        var handoverRows = await _db.HandoverDocuments.AsNoTracking()
            .Where(d => allocationIds.Contains(d.AllocationId))
            .OrderByDescending(d => d.GeneratedAt)
            .Select(d => new
            {
                d.AllocationId,
                d.DocumentNumber,
                d.FilePath
            })
            .ToListAsync(ct);

        var latestHandovers = handoverRows
            .GroupBy(d => d.AllocationId)
            .ToDictionary(group => group.Key, group => group.First());

        return assets.Select(a =>
        {
            latestAllocations.TryGetValue(a.Id, out var allocation);
            var handover = allocation is not null && latestHandovers.TryGetValue(allocation.Id, out var found)
                ? found
                : null;

            return new MyAssetItem(
                a.Id,
                a.AssetCode,
                a.ModelName,
                a.Status,
                a.Location,
                allocation?.StartDate ?? default,
                allocation?.ExpectedReturnAt,
                allocation?.AllocationRequestId,
                handover?.DocumentNumber,
                handover is not null);
        }).ToList();
    }

    public async Task<HandoverFileResult?> GetMyAssetHandoverAsync(Guid assetId, CancellationToken ct)
    {
        var userId = _currentUser.Id ?? throw new DomainException("Not authenticated.");

        var allocation = await _db.Allocations.AsNoTracking()
            .Where(a =>
                a.AssetInstanceId == assetId &&
                a.UserId == userId &&
                a.EventType == AllocationEventType.Allocated &&
                a.AssetInstance.CurrentHolderId == userId &&
                a.AssetInstance.Status == AssetStatus.Allocated)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new
            {
                a.Id,
                a.CreatedBy
            })
            .FirstOrDefaultAsync(ct);

        if (allocation is null)
            return null;

        var handover = await _handover.GetFileForAllocationAsync(allocation.Id, ct);

        if (handover is not null)
            return handover;

        await _handover.GenerateForAllocationAsync(allocation.Id, allocation.CreatedBy, ct);
        return await _handover.GetFileForAllocationAsync(allocation.Id, ct);
    }

}
