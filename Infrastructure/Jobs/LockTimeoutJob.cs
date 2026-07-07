using AssetMgmt.Domain.Enums;
using AssetMgmt.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AssetMgmt.Infrastructure.Jobs;

/// <summary>
/// Recurring job (every 5 minutes) that reclaims assets whose temporary
/// allocation lock has expired. A request is created with a 24-hour TTL
/// (see <c>AllocationRequestService</c>); if no manager approves in time we
/// expire the request and return the asset to stock so others can request it.
/// </summary>
public class LockTimeoutJob
{
    private readonly AppDbContext _db;
    private readonly ILogger<LockTimeoutJob> _logger;

    public LockTimeoutJob(AppDbContext db, ILogger<LockTimeoutJob> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        // Source of truth is the pending request: if its lock window has passed,
        // expire it and release whatever asset it is still holding.
        var expired = await _db.AllocationRequests
            .Where(r => r.Status == RequestStatus.Pending
                        && r.LockExpiresAt != null
                        && r.LockExpiresAt < now)
            .ToListAsync(ct);

        if (expired.Count == 0)
            return;

        var released = 0;
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        foreach (var request in expired)
        {
            request.Status = RequestStatus.Expired;
            request.ExpiredAt = now;

            var asset = await _db.AssetInstances
                .FirstOrDefaultAsync(a => a.Id == request.AssetInstanceId, ct);

            // Only release if the asset is still held by THIS request's lock —
            // guards against a race where it was already approved/reassigned.
            if (asset is { Status: AssetStatus.LockedTemp } && asset.LockToken == request.LockToken)
            {
                asset.Status = AssetStatus.InStock;
                asset.CurrentHolderId = null;
                asset.LockToken = null;
                asset.LockExpiresAt = null;
                asset.LockHolderUserId = null;
                released++;
            }

            request.LockToken = null;
            request.LockExpiresAt = null;
        }

        try
        {
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync(ct);
            _logger.LogInformation(
                "LockTimeoutJob: skipped expiration batch because one or more requests/assets changed concurrently.");
            return;
        }

        _logger.LogInformation(
            "LockTimeoutJob: expired {ExpiredCount} request(s), released {ReleasedCount} asset(s) back to stock.",
            expired.Count, released);
    }
}
