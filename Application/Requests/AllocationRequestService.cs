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
    private static readonly TimeSpan LockTtl = TimeSpan.FromMinutes(30);

    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IHandoverDocumentService _handover;

    public AllocationRequestService(
        AppDbContext db, ICurrentUser currentUser, IHandoverDocumentService handover)
    {
        _db = db;
        _currentUser = currentUser;
        _handover = handover;
    }

    private Guid CurrentUserId =>
        _currentUser.Id ?? throw new DomainException("Not authenticated.");

    // ---------- Day 4: create + temp lock ----------

    public async Task<AllocationRequestDto> CreateAsync(CreateRequestDto req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.IdempotencyKey))
            throw new DomainException("IdempotencyKey is required.");

        var requesterId = CurrentUserId;

        // Idempotency: return the existing request if the same key was already used.
        var existing = await _db.AllocationRequests
            .FirstOrDefaultAsync(r => r.IdempotencyKey == req.IdempotencyKey, ct);
        if (existing is not null)
            return await GetByIdAsync(existing.Id, ct);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var asset = await _db.AssetInstances.FirstOrDefaultAsync(a => a.Id == req.AssetInstanceId, ct)
            ?? throw new DomainException("Asset not found.");

        if (asset.Status != AssetStatus.InStock)
            throw new DomainException("Asset is not available for request.");

        var now = DateTime.UtcNow;
        var lockToken = Guid.NewGuid().ToString("N");

        // Temp-lock the asset. CK_asset_instances_holder_status requires a holder for LockedTemp.
        asset.Status = AssetStatus.LockedTemp;
        asset.CurrentHolderId = requesterId;
        asset.LockToken = lockToken;
        asset.LockExpiresAt = now.Add(LockTtl);
        asset.LockHolderUserId = requesterId;
        asset.UpdatedBy = requesterId;

        var request = new AllocationRequest
        {
            RequesterId = requesterId,
            AssetInstanceId = asset.Id,
            Status = RequestStatus.Pending,
            Reason = req.Reason,
            ExpectedDurationMonths = req.ExpectedDurationMonths,
            IdempotencyKey = req.IdempotencyKey,
            LockToken = lockToken,
            LockExpiresAt = now.Add(LockTtl)
        };
        _db.AllocationRequests.Add(request);

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await GetByIdAsync(request.Id, ct);
    }

    public async Task<PagedResult<RequestListItem>> ListPendingAsync(PageQuery page, CancellationToken ct)
    {
        var query = _db.AllocationRequests.AsNoTracking()
            .Where(r => r.Status == RequestStatus.Pending);

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

        return new AllocationRequestDto(
            r.Id, r.RequesterId, r.Requester.FullName,
            r.AssetInstanceId, r.AssetInstance.AssetCode, r.AssetInstance.Model.Name,
            r.Status, r.Reason, r.ExpectedDurationMonths,
            r.ApproverId, r.Approver?.FullName, r.ApprovedAt, r.RejectedReason,
            r.LockExpiresAt, r.CreatedAt, r.UpdatedAt);
    }

    // ---------- Day 5: approve / reject ----------

    public async Task<AllocationRequestDto> ApproveAsync(Guid id, CancellationToken ct)
    {
        var approverId = CurrentUserId;
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var request = await _db.AllocationRequests.FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new DomainException("Request not found.");
        if (request.Status != RequestStatus.Pending)
            throw new DomainException("Only pending requests can be approved.");

        var asset = await _db.AssetInstances.FirstOrDefaultAsync(a => a.Id == request.AssetInstanceId, ct)
            ?? throw new DomainException("Asset not found.");

        var now = DateTime.UtcNow;

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

    public async Task<AllocationRequestDto> RejectAsync(Guid id, string reason, CancellationToken ct)
    {
        var approverId = CurrentUserId;
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var request = await _db.AllocationRequests.FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new DomainException("Request not found.");
        if (request.Status != RequestStatus.Pending)
            throw new DomainException("Only pending requests can be rejected.");

        var asset = await _db.AssetInstances.FirstOrDefaultAsync(a => a.Id == request.AssetInstanceId, ct)
            ?? throw new DomainException("Asset not found.");

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

    private static System.Linq.Expressions.Expression<Func<AllocationRequest, RequestListItem>> MapListItem() =>
        r => new RequestListItem(
            r.Id, r.RequesterId, r.Requester.FullName,
            r.AssetInstanceId, r.AssetInstance.AssetCode, r.AssetInstance.Model.Name,
            r.Status, r.ExpectedDurationMonths, r.LockExpiresAt, r.CreatedAt);
}
