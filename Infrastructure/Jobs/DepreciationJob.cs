using AssetMgmt.Domain.Entities;
using AssetMgmt.Domain.Enums;
using AssetMgmt.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AssetMgmt.Infrastructure.Jobs;

/// <summary>
/// Recurring job (monthly, 1st of the month) that posts one depreciation
/// ledger row per in-service asset that has a <see cref="DepreciationPolicy"/>.
/// Idempotent: the unique (asset, period) constraint plus an existence check
/// mean re-running for the same period is a no-op.
/// </summary>
public class DepreciationJob
{
    private readonly AppDbContext _db;
    private readonly ILogger<DepreciationJob> _logger;

    // Statuses that end an asset's depreciable life.
    private static readonly AssetStatus[] EndOfLife =
        { AssetStatus.Disposed, AssetStatus.Retired, AssetStatus.Lost };

    public DepreciationJob(AppDbContext db, ILogger<DepreciationJob> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>Posts depreciation for the current calendar month (UTC).</summary>
    public Task RunAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        return PostForPeriodAsync(new DateTime(now.Year, now.Month, 1), ct);
    }

    /// <summary>
    /// Posts depreciation for <paramref name="period"/> (must be the first day of a month).
    /// Exposed so the run can be triggered manually for a specific period.
    /// </summary>
    public async Task PostForPeriodAsync(DateTime period, CancellationToken ct = default)
    {
        period = period.Date;
        if (period.Day != 1)
            period = new DateTime(period.Year, period.Month, 1);

        // Assets still in service, with a policy, that already existed in this period.
        var assets = await _db.AssetInstances
            .Include(a => a.Model).ThenInclude(m => m.DepreciationPolicy)
            .Where(a => !EndOfLife.Contains(a.Status)
                        && a.Model.DepreciationPolicy != null
                        && a.AcquisitionDate <= period)
            .ToListAsync(ct);

        if (assets.Count == 0)
            return;

        // Assets already posted for this period — skip them (idempotency).
        var assetIds = assets.Select(a => a.Id).ToList();
        var alreadyPosted = await _db.DepreciationLedger
            .Where(l => l.PeriodDate == period && assetIds.Contains(l.AssetInstanceId))
            .Select(l => l.AssetInstanceId)
            .ToListAsync(ct);
        var postedSet = alreadyPosted.ToHashSet();

        var posted = 0;
        foreach (var asset in assets)
        {
            if (postedSet.Contains(asset.Id))
                continue;

            var policy = asset.Model.DepreciationPolicy!;

            // Respect the policy's effective window.
            if (period < policy.EffectiveFrom.Date) continue;
            if (policy.EffectiveTo is { } end && period > end.Date) continue;

            var entry = await BuildEntryAsync(asset, policy, period, ct);
            if (entry is null)
                continue; // fully depreciated / nothing to post

            _db.DepreciationLedger.Add(entry);
            posted++;
        }

        if (posted > 0)
            await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "DepreciationJob: posted {PostedCount} ledger row(s) for period {Period:yyyy-MM}.",
            posted, period);
    }

    private async Task<DepreciationLedger?> BuildEntryAsync(
        AssetInstance asset, DepreciationPolicy policy, DateTime period, CancellationToken ct)
    {
        var salvage = Round(asset.AcquisitionCost * (policy.SalvageValuePercent / 100m));

        // Carry forward from the most recent prior period, else start at cost.
        var prior = await _db.DepreciationLedger.AsNoTracking()
            .Where(l => l.AssetInstanceId == asset.Id && l.PeriodDate < period)
            .OrderByDescending(l => l.PeriodDate)
            .FirstOrDefaultAsync(ct);

        var opening = prior?.ClosingBookValue ?? asset.AcquisitionCost;
        var accumulated = prior?.AccumulatedDepreciation ?? 0m;

        // Already at (or below) salvage — nothing left to depreciate.
        if (opening <= salvage)
            return null;

        var periodDepreciation = policy.Method switch
        {
            DepreciationMethod.StraightLine =>
                Round((asset.AcquisitionCost - salvage) / policy.UsefulLifeMonths),
            DepreciationMethod.DecliningBalance when policy.AnnualDeclineRate is { } rate =>
                Round(opening * (rate / 12m)),
            _ => 0m
        };

        if (periodDepreciation <= 0m)
            return null;

        // Never depreciate below salvage value.
        periodDepreciation = Math.Min(periodDepreciation, opening - salvage);

        return new DepreciationLedger
        {
            AssetInstanceId = asset.Id,
            PeriodDate = period,
            OpeningBookValue = opening,
            PeriodDepreciation = periodDepreciation,
            AccumulatedDepreciation = accumulated + periodDepreciation,
            ClosingBookValue = opening - periodDepreciation,
            PolicyId = policy.Id,
            PostedBy = null // system job
        };
    }

    private static decimal Round(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
