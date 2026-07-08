using AssetMgmt.Application.Common;
using AssetMgmt.Domain.Entities;
using AssetMgmt.Domain.Enums;
using AssetMgmt.Domain.Exceptions;
using AssetMgmt.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AssetMgmt.Application.Inventory;

public class InventoryScanService
{
    private readonly AppDbContext _db;
    private readonly DataScopeService _scope;

    public InventoryScanService(AppDbContext db, DataScopeService scope)
    {
        _db = db;
        _scope = scope;
    }

    public async Task<InventoryScanDto> CreateAsync(CreateInventoryScanRequest req, CancellationToken ct)
    {
        var departmentId = req.DepartmentId;
        if (_scope.IsManager)
        {
            var departments = await _scope.GetDepartmentIdsAsync(ct);
            departmentId ??= departments.FirstOrDefault();
            if (departmentId == Guid.Empty || !departments.Contains(departmentId.Value))
                throw new DomainException("A managed department is required for this inventory scan.");
        }
        if (departmentId is not null && !await _db.Departments.AnyAsync(d => d.Id == departmentId, ct))
            throw new DomainException("Department not found.");

        var now = DateTime.UtcNow;
        var scan = new InventoryScan
        {
            DepartmentId = departmentId,
            Status = InventoryScanStatus.Open,
            StartedAt = now,
            CreatedAt = now,
            CreatedBy = _scope.UserId
        };
        _db.InventoryScans.Add(scan);
        await _db.SaveChangesAsync(ct);
        return await GetAsync(scan.Id, ct);
    }

    public async Task<InventoryScanDto> AddItemAsync(Guid scanId, string assetCode, CancellationToken ct)
    {
        var scan = await LoadAuthorizedScanAsync(scanId, ct);
        if (scan.Status != InventoryScanStatus.Open) throw new DomainException("Inventory scan is closed.");
        var normalized = assetCode.Trim();
        if (normalized.Length == 0) throw new DomainException("Asset code is required.");
        if (scan.Items.Any(i => i.AssetCode == normalized)) return Map(scan);

        var asset = await _db.AssetInstances.AsNoTracking().Include(a => a.CurrentHolder)
            .SingleOrDefaultAsync(a => a.AssetCode == normalized, ct);
        var expected = asset is not null &&
            (scan.DepartmentId is null || asset.CurrentHolder?.DepartmentId == scan.DepartmentId);
        scan.Items.Add(new InventoryScanItem
        {
            // Do not disclose or persist the internal ID of an asset outside the
            // scan's department. The code is enough to report it as unexpected.
            AssetInstanceId = expected ? asset!.Id : null,
            AssetCode = normalized,
            Result = expected ? InventoryScanResult.Found : InventoryScanResult.Unexpected,
            ScannedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
        return await GetAsync(scan.Id, ct);
    }

    public async Task<InventoryScanDto> CloseAsync(Guid scanId, CancellationToken ct)
    {
        var scan = await LoadAuthorizedScanAsync(scanId, ct);
        if (scan.Status == InventoryScanStatus.Closed) return Map(scan);
        var scannedIds = scan.Items.Where(i => i.AssetInstanceId != null).Select(i => i.AssetInstanceId!.Value).ToList();
        var expected = _db.AssetInstances.AsNoTracking()
            .Where(a => a.Status != AssetStatus.Disposed && a.Status != AssetStatus.Lost);
        if (scan.DepartmentId is not null)
            expected = expected.Where(a => a.CurrentHolder != null && a.CurrentHolder.DepartmentId == scan.DepartmentId);
        var missing = await expected.Where(a => !scannedIds.Contains(a.Id))
            .Select(a => new { a.Id, a.AssetCode }).ToListAsync(ct);
        foreach (var asset in missing)
            scan.Items.Add(new InventoryScanItem
            {
                AssetInstanceId = asset.Id,
                AssetCode = asset.AssetCode,
                Result = InventoryScanResult.Missing,
                ScannedAt = DateTime.UtcNow
            });
        scan.Status = InventoryScanStatus.Closed;
        scan.ClosedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return await GetAsync(scan.Id, ct);
    }

    private async Task<InventoryScan> LoadAuthorizedScanAsync(Guid id, CancellationToken ct)
    {
        var scan = await _db.InventoryScans.Include(s => s.Department).Include(s => s.Items)
            .SingleOrDefaultAsync(s => s.Id == id, ct) ?? throw new DomainException("Inventory scan not found.");
        if (_scope.IsManager) await _scope.EnsureDepartmentAccessAsync(scan.DepartmentId, ct);
        return scan;
    }

    private async Task<InventoryScanDto> GetAsync(Guid id, CancellationToken ct) => Map(await LoadAuthorizedScanAsync(id, ct));
    private static InventoryScanDto Map(InventoryScan scan)
    {
        var items = scan.Items.OrderByDescending(i => i.ScannedAt)
            .Select(i => new InventoryScanItemDto(i.Id, i.AssetInstanceId, i.AssetCode, i.Result, i.ScannedAt)).ToList();
        return new InventoryScanDto(scan.Id, scan.DepartmentId, scan.Department?.Name, scan.Status,
            scan.StartedAt, scan.ClosedAt, items.Count(i => i.Result == InventoryScanResult.Found),
            items.Count(i => i.Result == InventoryScanResult.Missing),
            items.Count(i => i.Result == InventoryScanResult.Unexpected), items);
    }
}
