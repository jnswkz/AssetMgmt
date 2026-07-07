using AssetMgmt.Application.Common;
using AssetMgmt.Domain.Entities;
using AssetMgmt.Domain.Enums;
using AssetMgmt.Domain.Exceptions;
using AssetMgmt.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AssetMgmt.Application.Depreciation;

public class DepreciationService
{
    private readonly AppDbContext _db;
    private readonly DataScopeService _scope;

    public DepreciationService(AppDbContext db, DataScopeService scope)
    {
        _db = db;
        _scope = scope;
    }

    public async Task<DepreciationPolicyDto?> GetPolicyAsync(Guid modelId, CancellationToken ct)
    {
        if (!await _db.AssetModels.AnyAsync(m => m.Id == modelId, ct))
            throw new DomainException("Asset model not found.");
        var policy = await _db.DepreciationPolicies.AsNoTracking()
            .SingleOrDefaultAsync(p => p.AssetModelId == modelId, ct);
        return policy is null ? null : Map(policy);
    }

    public async Task<DepreciationPolicyDto> PutPolicyAsync(
        Guid modelId, PutDepreciationPolicyRequest req, CancellationToken ct)
    {
        Validate(req);
        if (!await _db.AssetModels.AnyAsync(m => m.Id == modelId, ct))
            throw new DomainException("Asset model not found.");
        var policy = await _db.DepreciationPolicies.SingleOrDefaultAsync(p => p.AssetModelId == modelId, ct);
        if (policy is null)
        {
            policy = new DepreciationPolicy { AssetModelId = modelId };
            _db.DepreciationPolicies.Add(policy);
        }
        policy.Method = req.Method;
        policy.UsefulLifeMonths = req.UsefulLifeMonths;
        policy.AnnualDeclineRate = req.AnnualDeclineRate;
        policy.SalvageValuePercent = req.SalvageValuePercent;
        policy.EffectiveFrom = req.EffectiveFrom.Date;
        policy.EffectiveTo = req.EffectiveTo?.Date;
        await _db.SaveChangesAsync(ct);
        return Map(policy);
    }

    public async Task<AssetDepreciationDto> GetAssetAsync(Guid assetId, CancellationToken ct)
    {
        IQueryable<AssetInstance> query = _db.AssetInstances.AsNoTracking()
            .Include(a => a.Model).ThenInclude(m => m.DepreciationPolicy);
        if (_scope.IsManager)
        {
            var departments = await _scope.GetDepartmentIdsAsync(ct);
            query = query.Where(a => a.CurrentHolderId == null ||
                (a.CurrentHolder != null && a.CurrentHolder.DepartmentId != null &&
                 departments.Contains(a.CurrentHolder.DepartmentId.Value)));
        }
        var asset = await query.SingleOrDefaultAsync(a => a.Id == assetId, ct)
            ?? throw new DomainException("Asset not found.");
        var policy = asset.Model.DepreciationPolicy
            ?? throw new DomainException("No depreciation policy is configured for this asset model.");
        var ledger = await _db.DepreciationLedger.AsNoTracking()
            .Where(l => l.AssetInstanceId == assetId)
            .OrderByDescending(l => l.PeriodDate)
            .FirstOrDefaultAsync(ct);
        return Calculate(asset, policy, ledger, DateTime.UtcNow.Date);
    }

    public async Task<IReadOnlyList<DepreciationAlertItem>> GetAlertsAsync(CancellationToken ct)
    {
        var query = _db.AssetInstances.AsNoTracking()
            .Where(a => a.Model.DepreciationPolicy != null &&
                        a.Status != AssetStatus.Disposed && a.Status != AssetStatus.Lost);
        if (_scope.IsManager)
        {
            var departments = await _scope.GetDepartmentIdsAsync(ct);
            query = query.Where(a => a.CurrentHolder != null && a.CurrentHolder.DepartmentId != null &&
                                     departments.Contains(a.CurrentHolder.DepartmentId.Value));
        }
        var assets = await query.Include(a => a.Model).ThenInclude(m => m.DepreciationPolicy)
            .Include(a => a.CurrentHolder).ToListAsync(ct);
        var ids = assets.Select(a => a.Id).ToList();
        var ledgerRows = await _db.DepreciationLedger.AsNoTracking()
            .Where(l => ids.Contains(l.AssetInstanceId))
            .OrderByDescending(l => l.PeriodDate).ToListAsync(ct);
        var latest = ledgerRows.GroupBy(l => l.AssetInstanceId).ToDictionary(g => g.Key, g => g.First());

        return assets.Select(a => Calculate(a, a.Model.DepreciationPolicy!, latest.GetValueOrDefault(a.Id), DateTime.UtcNow.Date))
            .Where(d => d.FullyDepreciated || d.NearEndOfLife || d.NeedsUpgrade)
            .Select(d =>
            {
                var a = assets.Single(x => x.Id == d.AssetInstanceId);
                return new DepreciationAlertItem(d.AssetInstanceId, d.AssetCode, a.Model.Name,
                    a.CurrentHolder?.FullName, a.CurrentHolder?.DepartmentId, d.BookValue,
                    d.RemainingUsefulLifeMonths, d.FullyDepreciated, d.NearEndOfLife, d.NeedsUpgrade);
            }).OrderBy(x => x.RemainingUsefulLifeMonths).ToList();
    }

    public static AssetDepreciationDto Calculate(
        AssetInstance asset, DepreciationPolicy policy, DepreciationLedger? ledger, DateTime asOf)
    {
        var salvage = Math.Max(asset.SalvageValue,
            Round(asset.AcquisitionCost * policy.SalvageValuePercent / 100m));
        var months = Math.Max(0, (asOf.Year - asset.AcquisitionDate.Year) * 12 + asOf.Month - asset.AcquisitionDate.Month);
        var remaining = Math.Max(0, policy.UsefulLifeMonths - months);
        decimal bookValue;
        decimal accumulated;
        if (ledger is not null)
        {
            bookValue = ledger.ClosingBookValue;
            accumulated = ledger.AccumulatedDepreciation;
        }
        else if (policy.Method == DepreciationMethod.StraightLine)
        {
            accumulated = Math.Min(asset.AcquisitionCost - salvage,
                Round((asset.AcquisitionCost - salvage) / policy.UsefulLifeMonths * Math.Min(months, policy.UsefulLifeMonths)));
            bookValue = asset.AcquisitionCost - accumulated;
        }
        else
        {
            var rate = policy.AnnualDeclineRate ?? 0m;
            var factor = (decimal)Math.Pow((double)(1m - rate / 12m), months);
            bookValue = Math.Max(salvage, Round(asset.AcquisitionCost * factor));
            accumulated = asset.AcquisitionCost - bookValue;
        }
        var fully = remaining == 0 || bookValue <= salvage;
        return new AssetDepreciationDto(asset.Id, asset.AssetCode, asset.AcquisitionCost, salvage,
            Round(bookValue), Round(accumulated), ledger?.PeriodDate ?? asOf, ledger is not null,
            remaining, fully, !fully && remaining <= 3, remaining == 0, Map(policy));
    }

    private static void Validate(PutDepreciationPolicyRequest req)
    {
        if (req.UsefulLifeMonths <= 0) throw new DomainException("Useful life must be greater than zero.");
        if (req.SalvageValuePercent is < 0 or > 100) throw new DomainException("Salvage percent must be between 0 and 100.");
        if (req.Method == DepreciationMethod.DecliningBalance && req.AnnualDeclineRate is not (> 0 and < 1))
            throw new DomainException("Declining balance requires an annual rate between 0 and 1.");
        if (req.EffectiveTo < req.EffectiveFrom) throw new DomainException("Effective end date cannot precede start date.");
    }

    private static DepreciationPolicyDto Map(DepreciationPolicy p) => new(
        p.Id, p.AssetModelId, p.Method, p.UsefulLifeMonths, p.AnnualDeclineRate,
        p.SalvageValuePercent, p.EffectiveFrom, p.EffectiveTo);
    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
