using AssetMgmt.Application.Auth;
using AssetMgmt.Application.Common;
using AssetMgmt.Domain.Entities;
using AssetMgmt.Domain.Enums;
using AssetMgmt.Domain.Exceptions;
using AssetMgmt.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AssetMgmt.Application.Allocations;

public class AssetLifecycleService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly DataScopeService _scope;

    public AssetLifecycleService(AppDbContext db, ICurrentUser currentUser, DataScopeService scope)
    {
        _db = db;
        _currentUser = currentUser;
        _scope = scope;
    }

    private Guid CurrentUserId =>
        _currentUser.Id ?? throw new DomainException("Not authenticated.");

    // ---------- Return (reclaim) ----------

    public Task ReturnAsync(Guid assetId, ReturnAssetDto dto, CancellationToken ct) =>
        _db.ExecuteWithRetryStrategyAsync(() => ReturnCoreAsync(assetId, dto, ct));

    private async Task ReturnCoreAsync(Guid assetId, ReturnAssetDto dto, CancellationToken ct)
    {
        var actor = CurrentUserId;
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var asset = await GetAssetAsync(assetId, ct);
        if (asset.Status != AssetStatus.Allocated)
            throw new DomainException("Only allocated assets can be returned.");

        var holderId = asset.CurrentHolderId
            ?? throw new DomainException("Allocated asset has no current holder.");

        var today = DateTime.UtcNow.Date;

        asset.Status = AssetStatus.InStock;
        asset.CurrentHolderId = null;
        asset.UpdatedBy = actor;

        _db.Allocations.Add(new Allocation
        {
            AssetInstanceId = asset.Id,
            UserId = holderId,
            EventType = AllocationEventType.Returned,
            StartDate = today,
            EndDate = today,
            Notes = dto.Notes,
            CreatedBy = actor
        });

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    // ---------- Transfer ----------

    public Task TransferAsync(Guid assetId, TransferAssetDto dto, CancellationToken ct) =>
        _db.ExecuteWithRetryStrategyAsync(() => TransferCoreAsync(assetId, dto, ct));

    private async Task TransferCoreAsync(Guid assetId, TransferAssetDto dto, CancellationToken ct)
    {
        var actor = CurrentUserId;
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var asset = await GetAssetAsync(assetId, ct);
        if (asset.Status != AssetStatus.Allocated)
            throw new DomainException("Only allocated assets can be transferred.");

        var fromUserId = asset.CurrentHolderId
            ?? throw new DomainException("Allocated asset has no current holder.");

        if (dto.ToUserId == fromUserId)
            throw new DomainException("Cannot transfer an asset to its current holder.");

        var toUser = await _db.Users.FirstOrDefaultAsync(u => u.Id == dto.ToUserId, ct)
            ?? throw new DomainException("Target user not found.");
        if (_scope.IsManager) await _scope.EnsureDepartmentAccessAsync(toUser.DepartmentId, ct);
        if (!toUser.IsActive)
            throw new DomainException("Target user is not active.");

        asset.CurrentHolderId = dto.ToUserId; // stays Allocated
        asset.UpdatedBy = actor;

        _db.Allocations.Add(new Allocation
        {
            AssetInstanceId = asset.Id,
            UserId = dto.ToUserId,
            EventType = AllocationEventType.Transferred,
            StartDate = DateTime.UtcNow.Date,
            FromUserId = fromUserId,
            ToUserId = dto.ToUserId,
            Notes = dto.Notes,
            CreatedBy = actor
        });

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    // ---------- Maintenance ----------

    public Task<MaintenanceRecordDto> StartMaintenanceAsync(Guid assetId, StartMaintenanceDto dto, CancellationToken ct) =>
        _db.ExecuteWithRetryStrategyAsync(() => StartMaintenanceCoreAsync(assetId, dto, ct));

    private async Task<MaintenanceRecordDto> StartMaintenanceCoreAsync(Guid assetId, StartMaintenanceDto dto, CancellationToken ct)
    {
        var actor = CurrentUserId;
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var asset = await GetAssetAsync(assetId, ct);
        if (asset.Status is not (AssetStatus.InStock or AssetStatus.Allocated))
            throw new DomainException("Only in-stock or allocated assets can enter maintenance.");

        var today = DateTime.UtcNow.Date;

        // If currently allocated, close the allocation (asset leaves the user).
        if (asset.Status == AssetStatus.Allocated && asset.CurrentHolderId is Guid holderId)
        {
            _db.Allocations.Add(new Allocation
            {
                AssetInstanceId = asset.Id,
                UserId = holderId,
                EventType = AllocationEventType.Returned,
                StartDate = today,
                EndDate = today,
                Notes = "Returned for maintenance.",
                CreatedBy = actor
            });
        }

        // CK_asset_instances_holder_status requires a non-null holder for Maintenance.
        // Use the acting user as a custodial pointer (not an allocation).
        asset.Status = AssetStatus.Maintenance;
        asset.CurrentHolderId = actor;
        asset.UpdatedBy = actor;

        var record = new MaintenanceRecord
        {
            AssetInstanceId = asset.Id,
            MaintenanceType = dto.Type,
            Description = dto.Description,
            Vendor = dto.Vendor,
            Cost = dto.Cost ?? 0m,
            StartDate = today,
            Status = MaintenanceStatus.InProgress,
            CreatedBy = actor
        };
        _db.MaintenanceRecords.Add(record);

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await GetMaintenanceRecordAsync(record.Id, ct);
    }

    public Task<MaintenanceRecordDto> CompleteMaintenanceAsync(
        Guid assetId, Guid recordId, CompleteMaintenanceDto dto, CancellationToken ct) =>
        _db.ExecuteWithRetryStrategyAsync(() => CompleteMaintenanceCoreAsync(assetId, recordId, dto, ct));

    private async Task<MaintenanceRecordDto> CompleteMaintenanceCoreAsync(
        Guid assetId, Guid recordId, CompleteMaintenanceDto dto, CancellationToken ct)
    {
        var actor = CurrentUserId;
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var record = await _db.MaintenanceRecords
            .FirstOrDefaultAsync(r => r.Id == recordId && r.AssetInstanceId == assetId, ct)
            ?? throw new DomainException("Maintenance record not found.");
        if (record.Status != MaintenanceStatus.InProgress)
            throw new DomainException("Only in-progress maintenance can be completed.");

        var asset = await GetAssetAsync(assetId, ct);

        record.Status = MaintenanceStatus.Completed;
        record.EndDate = DateTime.UtcNow.Date;
        if (dto.Cost is not null) record.Cost = dto.Cost.Value;
        if (dto.Notes is not null) record.Notes = dto.Notes;

        asset.Status = AssetStatus.InStock;
        asset.CurrentHolderId = null;
        asset.UpdatedBy = actor;

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await GetMaintenanceRecordAsync(record.Id, ct);
    }

    public async Task<PagedResult<MaintenanceRecordDto>> ListMaintenanceAsync(
        Guid assetId, PageQuery page, CancellationToken ct)
    {
        await GetAssetAsync(assetId, ct);
        var query = _db.MaintenanceRecords.AsNoTracking()
            .Where(r => r.AssetInstanceId == assetId);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(r => r.StartDate)
            .Skip(page.Skip).Take(page.NormalizedPageSize)
            .Select(MapMaintenance())
            .ToListAsync(ct);

        return new PagedResult<MaintenanceRecordDto>(items, total, page.NormalizedPage, page.NormalizedPageSize);
    }

    // ---------- Dispose / Sell ----------

    public Task<DisposalDto> DisposeAsync(Guid assetId, DisposeAssetDto dto, CancellationToken ct) =>
        _db.ExecuteWithRetryStrategyAsync(() => DisposeCoreAsync(assetId, dto, ct));

    private async Task<DisposalDto> DisposeCoreAsync(Guid assetId, DisposeAssetDto dto, CancellationToken ct)
    {
        var actor = CurrentUserId;
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var asset = await GetAssetAsync(assetId, ct);
        if (asset.Status is AssetStatus.Disposed or AssetStatus.Retired or AssetStatus.Lost)
            throw new DomainException("Asset has already been disposed.");

        if (dto.Type == DisposalType.Sold)
        {
            if (dto.SoldToUserId is null)
                throw new DomainException("A buyer is required when selling an asset.");
            if (dto.SalePrice is null || dto.SalePrice < 0)
                throw new DomainException("A non-negative sale price is required when selling an asset.");

            var buyer = await _db.Users.FirstOrDefaultAsync(u => u.Id == dto.SoldToUserId, ct)
                ?? throw new DomainException("Buyer not found.");
            if (_scope.IsManager) await _scope.EnsureDepartmentAccessAsync(buyer.DepartmentId, ct);
            if (!buyer.IsActive)
                throw new DomainException("Buyer is not active.");
        }

        var today = DateTime.UtcNow.Date;

        // If currently held, close the allocation history.
        if (asset.Status == AssetStatus.Allocated && asset.CurrentHolderId is Guid holderId)
        {
            _db.Allocations.Add(new Allocation
            {
                AssetInstanceId = asset.Id,
                UserId = holderId,
                EventType = AllocationEventType.Returned,
                StartDate = today,
                EndDate = today,
                Notes = "Closed on disposal.",
                CreatedBy = actor
            });
        }

        asset.Status = AssetStatus.Disposed;
        asset.CurrentHolderId = null;
        asset.LockToken = null;
        asset.LockExpiresAt = null;
        asset.LockHolderUserId = null;
        asset.UpdatedBy = actor;

        var disposal = new AssetDisposal
        {
            AssetInstanceId = asset.Id,
            DisposalType = dto.Type,
            SoldToUserId = dto.Type == DisposalType.Sold ? dto.SoldToUserId : null,
            SalePrice = dto.Type == DisposalType.Sold ? dto.SalePrice : null,
            Reason = dto.Reason,
            DisposedAt = today,
            CreatedBy = actor
        };
        _db.AssetDisposals.Add(disposal);

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await GetDisposalAsync(disposal.Id, ct);
    }

    public async Task<PagedResult<DisposalDto>> ListDisposalsAsync(
        DisposalType? type, PageQuery page, CancellationToken ct)
    {
        var query = _db.AssetDisposals.AsNoTracking();
        if (_scope.IsManager)
        {
            var departments = await _scope.GetDepartmentIdsAsync(ct);
            query = query.Where(d => _db.Allocations.Any(a =>
                a.AssetInstanceId == d.AssetInstanceId && a.User.DepartmentId != null &&
                departments.Contains(a.User.DepartmentId.Value)));
        }
        if (type is not null)
            query = query.Where(d => d.DisposalType == type);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(d => d.DisposedAt)
            .Skip(page.Skip).Take(page.NormalizedPageSize)
            .Select(MapDisposal())
            .ToListAsync(ct);

        return new PagedResult<DisposalDto>(items, total, page.NormalizedPage, page.NormalizedPageSize);
    }

    // ---------- helpers ----------

    private async Task<AssetInstance> GetAssetAsync(Guid id, CancellationToken ct)
    {
        var asset = await _db.AssetInstances.Include(a => a.CurrentHolder)
            .FirstOrDefaultAsync(a => a.Id == id, ct) ?? throw new DomainException("Asset not found.");
        if (_scope.IsManager && asset.CurrentHolderId is not null)
            await _scope.EnsureDepartmentAccessAsync(asset.CurrentHolder?.DepartmentId, ct);
        return asset;
    }

    private async Task<MaintenanceRecordDto> GetMaintenanceRecordAsync(Guid id, CancellationToken ct) =>
        await _db.MaintenanceRecords.AsNoTracking()
            .Where(r => r.Id == id).Select(MapMaintenance()).SingleAsync(ct);

    private async Task<DisposalDto> GetDisposalAsync(Guid id, CancellationToken ct) =>
        await _db.AssetDisposals.AsNoTracking()
            .Where(d => d.Id == id).Select(MapDisposal()).SingleAsync(ct);

    private static System.Linq.Expressions.Expression<Func<MaintenanceRecord, MaintenanceRecordDto>> MapMaintenance() =>
        r => new MaintenanceRecordDto(
            r.Id, r.AssetInstanceId, r.AssetInstance.AssetCode, r.MaintenanceType, r.Description,
            r.Cost, r.Vendor, r.StartDate, r.EndDate, r.Status, r.Notes, r.CreatedAt);

    private static System.Linq.Expressions.Expression<Func<AssetDisposal, DisposalDto>> MapDisposal() =>
        d => new DisposalDto(
            d.Id, d.AssetInstanceId, d.AssetInstance.AssetCode, d.DisposalType,
            d.SoldToUserId, d.SoldToUser != null ? d.SoldToUser.FullName : null,
            d.SalePrice, d.Reason, d.DisposedAt, d.CreatedAt);
}
