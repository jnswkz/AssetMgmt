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

    public ReportService(AppDbContext db)
    {
        _db = db;
    }

    // ---------- Dashboard KPIs ----------

    public async Task<DashboardStatsDto> GetDashboardAsync(CancellationToken ct)
    {
        // One grouped scan for status counts, one for category.
        var byStatus = await _db.AssetInstances.AsNoTracking()
            .GroupBy(a => a.Status)
            .Select(g => new StatusCount(g.Key, g.Count()))
            .ToListAsync(ct);

        var byCategory = await _db.AssetInstances.AsNoTracking()
            .GroupBy(a => a.Model.Category)
            .Select(g => new CategoryCount(g.Key, g.Count()))
            .ToListAsync(ct);

        var totalCost = await _db.AssetInstances.AsNoTracking()
            .SumAsync(a => (decimal?)a.AcquisitionCost, ct) ?? 0m;

        var pending = await _db.AllocationRequests.AsNoTracking()
            .CountAsync(r => r.Status == RequestStatus.Pending, ct);

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
        var query = _db.AssetInstances.AsNoTracking()
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
}
