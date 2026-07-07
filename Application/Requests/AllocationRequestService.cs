using AssetMgmt.Application.Auth;
using AssetMgmt.Application.Common;
using AssetMgmt.Application.Handover;
using AssetMgmt.Domain.Entities;
using AssetMgmt.Domain.Enums;
using AssetMgmt.Domain.Exceptions;
using AssetMgmt.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AssetMgmt.Application.Requests;

public class AllocationRequestService
{
    private static readonly TimeSpan LockTtl = TimeSpan.FromHours(24);

    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IHandoverDocumentService _handover;
    private readonly DataScopeService _scope;

    public AllocationRequestService(
        AppDbContext db, ICurrentUser currentUser, IHandoverDocumentService handover,
        DataScopeService scope)
    {
        _db = db;
        _currentUser = currentUser;
        _handover = handover;
        _scope = scope;
    }

    private Guid CurrentUserId =>
        _currentUser.Id ?? throw new DomainException("Not authenticated.");

    // ---------- Day 4: create + temp lock ----------

    public async Task<AllocationRequestDto> CreateAsync(CreateRequestDto req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.IdempotencyKey))
            throw new DomainException("IdempotencyKey is required.");

        var requesterId = CurrentUserId;

        // Idempotency is requester-scoped so a guessed key cannot expose another user's request.
        var existing = await _db.AllocationRequests
            .FirstOrDefaultAsync(r => r.IdempotencyKey == req.IdempotencyKey && r.RequesterId == requesterId, ct);
        if (existing is not null)
            return await GetByIdAsync(existing.Id, ct);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var asset = await _db.AssetInstances.AsNoTracking()
                .Select(a => new { a.Id, a.Status, a.LockExpiresAt, a.RowVersion })
                .FirstOrDefaultAsync(a => a.Id == req.AssetInstanceId, ct)
                ?? throw new DomainException("Asset not found.");

            if (asset.Status != AssetStatus.InStock)
                throw new DomainException("Asset is not available for request.");

            var now = DateTime.UtcNow;
            var lockToken = Guid.NewGuid().ToString("N");

            var lockExpiresAt = now.Add(LockTtl);
            var locked = await _db.AssetInstances
                .Where(a => a.Id == asset.Id && a.Status == AssetStatus.InStock &&
                            (a.LockExpiresAt == null || a.LockExpiresAt <= now) &&
                            a.RowVersion == asset.RowVersion)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(a => a.Status, AssetStatus.LockedTemp)
                    .SetProperty(a => a.CurrentHolderId, requesterId)
                    .SetProperty(a => a.LockToken, lockToken)
                    .SetProperty(a => a.LockExpiresAt, lockExpiresAt)
                    .SetProperty(a => a.LockHolderUserId, requesterId)
                    .SetProperty(a => a.UpdatedBy, requesterId), ct);
            if (locked != 1)
                throw new ConflictException("Asset was just reserved or changed. Refresh and try another available asset.");

            var request = new AllocationRequest
            {
                RequesterId = requesterId,
                AssetInstanceId = asset.Id,
                Status = RequestStatus.Pending,
                Reason = req.Reason,
                ExpectedDurationMonths = req.ExpectedDurationMonths,
                IdempotencyKey = req.IdempotencyKey,
                LockToken = lockToken,
                LockExpiresAt = lockExpiresAt,
                HandoverDueAt = lockExpiresAt,
                CreatedAt = now,
                UpdatedAt = now
            };
            _db.AllocationRequests.Add(request);

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return await GetByIdAsync(request.Id, ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync(ct);
            throw new ConflictException("Asset was just reserved or changed. Refresh and try another available asset.");
        }
        catch (DbUpdateException)
        {
            await tx.RollbackAsync(ct);
            _db.ChangeTracker.Clear();

            var request = await _db.AllocationRequests.AsNoTracking()
                .FirstOrDefaultAsync(r => r.IdempotencyKey == req.IdempotencyKey && r.RequesterId == requesterId, ct);
            if (request is not null)
                return await GetByIdAsync(request.Id, ct);

            throw;
        }
    }

    public async Task<PagedResult<RequestListItem>> ListPendingAsync(PageQuery page, CancellationToken ct)
    {
        var query = _db.AllocationRequests.AsNoTracking()
            .Where(r => r.Status == RequestStatus.Pending);

        if (_scope.IsManager)
        {
            var departments = await _scope.GetDepartmentIdsAsync(ct);
            query = query.Where(r => r.Requester.DepartmentId != null &&
                                     departments.Contains(r.Requester.DepartmentId.Value));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(r => r.CreatedAt)
            .Skip(page.Skip).Take(page.NormalizedPageSize)
            .Select(MapListItem())
            .ToListAsync(ct);

        return new PagedResult<RequestListItem>(items, total, page.NormalizedPage, page.NormalizedPageSize);
    }

    public async Task<PagedResult<RequestListItem>> ListMineAsync(PageQuery page, CancellationToken ct)
    {
        var requesterId = CurrentUserId;
        var query = _db.AllocationRequests.AsNoTracking()
            .Where(r => r.RequesterId == requesterId);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip(page.Skip).Take(page.NormalizedPageSize)
            .Select(MapListItem())
            .ToListAsync(ct);

        return new PagedResult<RequestListItem>(items, total, page.NormalizedPage, page.NormalizedPageSize);
    }

    public async Task<AllocationRequestDto> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var r = await _db.AllocationRequests.AsNoTracking()
            .Include(x => x.Requester)
            .Include(x => x.Approver)
            .Include(x => x.AssetInstance).ThenInclude(a => a.Model)
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new DomainException("Request not found.");

        if (r.RequesterId != CurrentUserId && !_scope.IsAdmin)
        {
            if (!_scope.IsManager)
                throw new DomainException("Request not found.");
            await _scope.EnsureDepartmentAccessAsync(r.Requester.DepartmentId, ct);
        }

        return new AllocationRequestDto(
            r.Id, r.RequesterId, r.Requester.FullName,
            r.AssetInstanceId, r.AssetInstance.AssetCode, r.AssetInstance.Model.Name,
            r.Status, r.Reason, r.ExpectedDurationMonths,
            r.ApproverId, r.Approver?.FullName, r.ApprovedAt, r.RejectedReason,
            r.LockExpiresAt, r.HandoverDueAt, r.CreatedAt, r.UpdatedAt);
    }

    // ---------- Day 5: approve / reject ----------

    public async Task<AllocationRequestDto> ApproveAsync(Guid id, CancellationToken ct)
    {
        var approverId = CurrentUserId;
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var request = await _db.AllocationRequests.FirstOrDefaultAsync(r => r.Id == id, ct)
                ?? throw new DomainException("Request not found.");
            await EnsureManagerCanActAsync(request.RequesterId, ct);
            if (request.Status != RequestStatus.Pending)
                throw new DomainException("Only pending requests can be approved.");

            var asset = await _db.AssetInstances.FirstOrDefaultAsync(a => a.Id == request.AssetInstanceId, ct)
                ?? throw new DomainException("Asset not found.");

            var now = DateTime.UtcNow;
            EnsureRequestStillOwnsAssetLock(request, asset, now);

            request.Status = RequestStatus.Approved;
            request.ApproverId = approverId;
            request.ApprovedAt = now;
            request.LockToken = null;
            request.LockExpiresAt = null;

            asset.Status = AssetStatus.Allocated;
            asset.CurrentHolderId = request.RequesterId;
            asset.LockToken = null;
            asset.LockExpiresAt = null;
            asset.LockHolderUserId = null;
            asset.UpdatedBy = approverId;

            var allocation = new Allocation
            {
                AssetInstanceId = asset.Id,
                UserId = request.RequesterId,
                EventType = AllocationEventType.Allocated,
                StartDate = now,
                ExpectedReturnAt = request.ExpectedDurationMonths is { } months ? now.AddMonths(months) : null,
                AllocationRequestId = request.Id,
                CreatedBy = approverId
            };
            _db.Allocations.Add(allocation);

            await _db.SaveChangesAsync(ct);

            // Generate the handover record (Biên bản bàn giao) for this allocation.
            // Shares this DbContext/transaction, so it commits atomically below.
            await _handover.GenerateForAllocationAsync(allocation.Id, approverId, ct);

            await tx.CommitAsync(ct);

            return await GetByIdAsync(request.Id, ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync(ct);
            throw new ConflictException("Request or asset was changed by another action. Refresh and try again.");
        }
    }

    public async Task<AllocationRequestDto> RejectAsync(Guid id, string reason, CancellationToken ct)
    {
        var approverId = CurrentUserId;
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var request = await _db.AllocationRequests.FirstOrDefaultAsync(r => r.Id == id, ct)
                ?? throw new DomainException("Request not found.");
            await EnsureManagerCanActAsync(request.RequesterId, ct);
            if (request.Status != RequestStatus.Pending)
                throw new DomainException("Only pending requests can be rejected.");

            var asset = await _db.AssetInstances.FirstOrDefaultAsync(a => a.Id == request.AssetInstanceId, ct)
                ?? throw new DomainException("Asset not found.");

            EnsureRequestStillOwnsAssetLock(request, asset, DateTime.UtcNow);

            request.Status = RequestStatus.Rejected;
            request.RejectedReason = reason;
            request.ApproverId = approverId;
            request.LockToken = null;
            request.LockExpiresAt = null;

            // Release the temp lock: back to InStock (holder must be null per check constraint).
            asset.Status = AssetStatus.InStock;
            asset.CurrentHolderId = null;
            asset.LockToken = null;
            asset.LockExpiresAt = null;
            asset.LockHolderUserId = null;
            asset.UpdatedBy = approverId;

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return await GetByIdAsync(request.Id, ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync(ct);
            throw new ConflictException("Request or asset was changed by another action. Refresh and try again.");
        }
    }

    private static void EnsureRequestStillOwnsAssetLock(
        AllocationRequest request,
        AssetInstance asset,
        DateTime now)
    {
        if (request.LockExpiresAt is not null && request.LockExpiresAt <= now)
            throw new ConflictException("The temporary asset lock has expired. Refresh the request list.");

        if (asset.Status != AssetStatus.LockedTemp ||
            asset.LockToken != request.LockToken ||
            asset.LockHolderUserId != request.RequesterId ||
            asset.CurrentHolderId != request.RequesterId)
            throw new ConflictException("Asset is no longer reserved for this request. Refresh the request list.");
    }

    private static System.Linq.Expressions.Expression<Func<AllocationRequest, RequestListItem>> MapListItem() =>
        r => new RequestListItem(
            r.Id, r.RequesterId, r.Requester.FullName,
            r.AssetInstanceId, r.AssetInstance.AssetCode, r.AssetInstance.Model.Name,
            r.Status, r.ExpectedDurationMonths, r.LockExpiresAt, r.HandoverDueAt, r.CreatedAt);

    private async Task EnsureManagerCanActAsync(Guid requesterId, CancellationToken ct)
    {
        if (_scope.IsAdmin) return;
        var departmentId = await _db.Users.AsNoTracking()
            .Where(u => u.Id == requesterId)
            .Select(u => u.DepartmentId)
            .SingleAsync(ct);
        await _scope.EnsureDepartmentAccessAsync(departmentId, ct);
    }
}
