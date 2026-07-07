using AssetMgmt.Application.Common;
using AssetMgmt.Domain.Enums;
using AssetMgmt.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AssetMgmt.Application.Reports;

public class ReportService
{
    private const int DefaultIdleMonths = 3;

    private static readonly AssetStatus[] EndOfLifeStatuses =
        { AssetStatus.Retired, AssetStatus.Lost, AssetStatus.Disposed };

    private readonly AppDbContext _db;
    private readonly DataScopeService _scope;

    public ReportService(AppDbContext db, DataScopeService scope)
    {
        _db = db;
        _scope = scope;
    }

    // ---------- Dashboard KPIs ----------

    public async Task<DashboardStatsDto> GetDashboardAsync(CancellationToken ct)
    {
        // One grouped scan for status counts, one for category.
        var assets = await ScopedAssetsAsync(ct);
        var byStatus = await assets
            .GroupBy(a => a.Status)
            .Select(g => new StatusCount(g.Key, g.Count()))
            .ToListAsync(ct);

        var byCategory = await assets
            .GroupBy(a => a.Model.Category)
            .Select(g => new CategoryCount(g.Key, g.Count()))
            .ToListAsync(ct);

        var totalCost = await assets
            .SumAsync(a => (decimal?)a.AcquisitionCost, ct) ?? 0m;

        var requests = _db.AllocationRequests.AsNoTracking();
        if (_scope.IsManager)
        {
            var departments = await _scope.GetDepartmentIdsAsync(ct);
            requests = requests.Where(r => r.Requester.DepartmentId != null && departments.Contains(r.Requester.DepartmentId.Value));
        }
        var pending = await requests.CountAsync(r => r.Status == RequestStatus.Pending, ct);

        int CountOf(AssetStatus s) => byStatus.FirstOrDefault(x => x.Status == s)?.Count ?? 0;

        return new DashboardStatsDto(
            TotalAssets: byStatus.Sum(x => x.Count),
            InStock: CountOf(AssetStatus.InStock),
            Allocated: CountOf(AssetStatus.Allocated),
            LockedTemp: CountOf(AssetStatus.LockedTemp),
            Maintenance: CountOf(AssetStatus.Maintenance),
            EndOfLife: byStatus.Where(x => EndOfLifeStatuses.Contains(x.Status)).Sum(x => x.Count),
            PendingRequests: pending,
            TotalAcquisitionCost: totalCost,
            ByStatus: byStatus.OrderBy(x => x.Status).ToList(),
            ByCategory: byCategory.OrderBy(x => x.Category).ToList());
    }

    // ---------- Idle-asset (warehouse optimization) report ----------

    /// <summary>
    /// In-stock assets whose last allocation activity (or acquisition, if never
    /// allocated) is older than <paramref name="idleMonths"/> months. Surfaces
    /// capital sitting unused in the warehouse.
    /// </summary>
    public async Task<PagedResult<IdleAssetItem>> GetIdleAssetsAsync(
        int? idleMonths, PageQuery page, CancellationToken ct)
    {
        var months = idleMonths is > 0 ? idleMonths.Value : DefaultIdleMonths;
        var now = DateTime.UtcNow;
        var cutoff = now.AddMonths(-months).Date;

        // Correlated subquery for the last allocation activity per asset (null if never allocated).
        var scopedAssets = await ScopedAssetsAsync(ct);
        var query = scopedAssets
            .Where(a => a.Status == AssetStatus.InStock)
            .Select(a => new
            {
                Asset = a,
                LastAt = _db.Allocations
                    .Where(al => al.AssetInstanceId == a.Id)
                    .Max(al => (DateTime?)al.CreatedAt)
            })
            // Idle if never touched since acquisition, or last activity precedes the cutoff.
            .Where(x => (x.LastAt == null && x.Asset.AcquisitionDate < cutoff)
                     || (x.LastAt != null && x.LastAt < cutoff));

        var total = await query.CountAsync(ct);

        var rows = await query
            .OrderBy(x => x.LastAt ?? x.Asset.AcquisitionDate)
            .Skip(page.Skip).Take(page.NormalizedPageSize)
            .Select(x => new
            {
                x.Asset.Id,
                x.Asset.AssetCode,
                ModelName = x.Asset.Model.Name,
                x.Asset.Model.Category,
                x.Asset.Status,
                x.Asset.Location,
                x.Asset.AcquisitionCost,
                x.Asset.AcquisitionDate,
                x.LastAt
            })
            .ToListAsync(ct);

        var items = rows.Select(x =>
        {
            var reference = x.LastAt ?? x.AcquisitionDate;
            var idle = (int)((now.Date - reference.Date).TotalDays / 30);
            return new IdleAssetItem(
                x.Id, x.AssetCode, x.ModelName, x.Category, x.Status, x.Location,
                x.AcquisitionCost, x.AcquisitionDate, x.LastAt, idle);
        }).ToList();

        return new PagedResult<IdleAssetItem>(items, total, page.NormalizedPage, page.NormalizedPageSize);
    }

    public async Task<IReadOnlyList<AssetMatrixItem>> GetAssetMatrixAsync(
        Guid? departmentId, AssetStatus? status, CancellationToken ct)
    {
        if (departmentId is not null) await _scope.EnsureDepartmentAccessAsync(departmentId, ct);
        var query = await ScopedAssetsAsync(ct);
        if (departmentId is not null) query = query.Where(a => a.CurrentHolder != null && a.CurrentHolder.DepartmentId == departmentId);
        if (status is not null) query = query.Where(a => a.Status == status);
        return await query.OrderBy(a => a.Location).ThenBy(a => a.AssetCode)
            .Select(a => new AssetMatrixItem(a.Id, a.AssetCode, a.Model.Name, a.Model.Category,
                a.Status, a.Location, a.CurrentHolderId,
                a.CurrentHolder == null ? null : a.CurrentHolder.FullName,
                a.CurrentHolder == null ? null : a.CurrentHolder.DepartmentId,
                a.CurrentHolder == null || a.CurrentHolder.Department == null ? null : a.CurrentHolder.Department.Name))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AllocationTimelineItem>> GetAllocationTimelineAsync(
        DateTime? from, DateTime? to, Guid? departmentId, CancellationToken ct)
    {
        if (departmentId is not null) await _scope.EnsureDepartmentAccessAsync(departmentId, ct);
        var query = _db.Allocations.AsNoTracking();
        if (_scope.IsManager)
        {
            var departments = await _scope.GetDepartmentIdsAsync(ct);
            query = query.Where(a => a.User.DepartmentId != null && departments.Contains(a.User.DepartmentId.Value));
        }
        if (departmentId is not null) query = query.Where(a => a.User.DepartmentId == departmentId);
        if (from is not null) query = query.Where(a => a.StartDate >= from.Value.Date);
        if (to is not null) query = query.Where(a => a.StartDate < to.Value.Date.AddDays(1));
        return await query.OrderByDescending(a => a.StartDate).Take(500)
            .Select(a => new AllocationTimelineItem(a.Id, a.AssetInstanceId, a.AssetInstance.AssetCode,
                a.AssetInstance.Model.Name, a.UserId, a.User.FullName, a.User.DepartmentId,
                a.User.Department == null ? null : a.User.Department.Name, a.EventType,
                a.StartDate, a.EndDate, a.ExpectedReturnAt))
            .ToListAsync(ct);
    }

    private async Task<IQueryable<Domain.Entities.AssetInstance>> ScopedAssetsAsync(CancellationToken ct)
    {
        var query = _db.AssetInstances.AsNoTracking();
        if (_scope.IsManager)
        {
            var departments = await _scope.GetDepartmentIdsAsync(ct);
            query = query.Where(a => a.CurrentHolderId == null ||
                (a.CurrentHolder != null && a.CurrentHolder.DepartmentId != null && departments.Contains(a.CurrentHolder.DepartmentId.Value)));
        }
        return query;
    }
}
