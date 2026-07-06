using AssetMgmt.Application.Auth;
using AssetMgmt.Domain.Entities;
using AssetMgmt.Domain.Enums;
using AssetMgmt.Domain.Exceptions;
using AssetMgmt.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AssetMgmt.Application.Agents;

public class AiAssetAccessService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public AiAssetAccessService(AppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public Guid CurrentUserId =>
        _currentUser.Id ?? throw new DomainException("Not authenticated.");

    public string CurrentUserRole =>
        _currentUser.Role ?? throw new DomainException("Not authenticated.");

    public async Task<IReadOnlyList<AiOwnedAssetSummary>> GetAssignedAssetsAsync(CancellationToken ct)
    {
        var userId = CurrentUserId;

        return await _db.AssetInstances.AsNoTracking()
            .Where(a => a.CurrentHolderId == userId)
            .OrderBy(a => a.AssetCode)
            .Select(a => new AiOwnedAssetSummary(
                a.Id,
                a.AssetCode,
                a.Model.Name,
                a.Status,
                a.WarrantyExpiresAt))
            .ToListAsync(ct);
    }

    public async Task<AiAssetResolution> ResolveAccessibleAssetAsync(
        Guid? requestAssetId,
        Guid? toolAssetId,
        string? assetCode,
        string? searchText,
        CancellationToken ct)
    {
        var anyAssetId = toolAssetId ?? requestAssetId;
        if (anyAssetId is Guid exactId)
            return await ResolveByIdAsync(exactId, ct);

        if (!string.IsNullOrWhiteSpace(assetCode))
            return await ResolveByAssetCodeAsync(assetCode.Trim(), ct);

        if (!string.IsNullOrWhiteSpace(searchText))
            return await ResolveBySearchTextAsync(searchText.Trim(), ct);

        return AiAssetResolution.NeedsClarification("missing_asset_identifier");
    }

    public async Task<bool> CanAccessAssetAsync(AssetInstance asset, CancellationToken ct)
    {
        var userId = CurrentUserId;
        var role = CurrentUserRole;

        if (string.Equals(role, nameof(UserRole.AdminIT), StringComparison.Ordinal))
            return true;

        if (string.Equals(role, nameof(UserRole.Employee), StringComparison.Ordinal))
            return asset.CurrentHolderId == userId;

        if (string.Equals(role, nameof(UserRole.Manager), StringComparison.Ordinal))
        {
            if (asset.CurrentHolder?.DepartmentId is not Guid holderDepartmentId)
                return false;

            var managedDepartmentIds = await _db.Departments.AsNoTracking()
                .Where(d => d.ManagerId == userId && d.IsActive && d.DeletedAt == null)
                .Select(d => d.Id)
                .ToListAsync(ct);

            return managedDepartmentIds.Contains(holderDepartmentId);
        }

        return false;
    }

    private async Task<AiAssetResolution> ResolveByIdAsync(Guid assetId, CancellationToken ct)
    {
        var asset = await BaseAssetQuery()
            .FirstOrDefaultAsync(a => a.Id == assetId, ct);

        if (asset is null)
            return AiAssetResolution.NotFound("asset_not_found");

        return await CanAccessAssetAsync(asset, ct)
            ? AiAssetResolution.Found(asset)
            : AiAssetResolution.Forbidden();
    }

    private async Task<AiAssetResolution> ResolveByAssetCodeAsync(string assetCode, CancellationToken ct)
    {
        var asset = await BaseAssetQuery()
            .FirstOrDefaultAsync(a => a.AssetCode == assetCode, ct);

        if (asset is null)
            return AiAssetResolution.NotFound("asset_not_found");

        return await CanAccessAssetAsync(asset, ct)
            ? AiAssetResolution.Found(asset)
            : AiAssetResolution.Forbidden();
    }

    private async Task<AiAssetResolution> ResolveBySearchTextAsync(string searchText, CancellationToken ct)
    {
        var accessibleAssets = await BaseAssetQuery()
            .Where(a =>
                EF.Functions.Like(a.AssetCode, $"%{searchText}%") ||
                EF.Functions.Like(a.Serial, $"%{searchText}%") ||
                EF.Functions.Like(a.Model.Name, $"%{searchText}%") ||
                (a.Model.Manufacturer != null && EF.Functions.Like(a.Model.Manufacturer, $"%{searchText}%")) ||
                (a.Model.ModelNumber != null && EF.Functions.Like(a.Model.ModelNumber, $"%{searchText}%")))
            .OrderBy(a => a.AssetCode)
            .ToListAsync(ct);

        var allowedAssets = new List<AssetInstance>();
        foreach (var asset in accessibleAssets)
        {
            if (await CanAccessAssetAsync(asset, ct))
                allowedAssets.Add(asset);
        }

        if (allowedAssets.Count == 1)
            return AiAssetResolution.Found(allowedAssets[0]);

        if (allowedAssets.Count > 1)
        {
            var candidates = allowedAssets
                .Select(a => $"{a.AssetCode} - {a.Model.Name}")
                .Take(5)
                .ToList();
            return AiAssetResolution.Ambiguous(candidates);
        }

        return AiAssetResolution.NotFound("asset_not_found");
    }

    private IQueryable<AssetInstance> BaseAssetQuery() =>
        _db.AssetInstances.AsNoTracking()
            .Include(a => a.Model)
            .Include(a => a.CurrentHolder);
}

public sealed record AiOwnedAssetSummary(
    Guid Id,
    string AssetCode,
    string ModelName,
    AssetStatus Status,
    DateTime? WarrantyExpiresAt);

public sealed class AiAssetResolution
{
    private AiAssetResolution(
        AssetInstance? asset,
        bool isForbidden,
        string? failureCode,
        IReadOnlyList<string>? candidates)
    {
        Asset = asset;
        IsForbidden = isForbidden;
        FailureCode = failureCode;
        CandidateAssets = candidates ?? [];
    }

    public AssetInstance? Asset { get; }
    public bool HasAsset => Asset is not null;
    public bool IsForbidden { get; }
    public string? FailureCode { get; }
    public IReadOnlyList<string> CandidateAssets { get; }

    public static AiAssetResolution Found(AssetInstance asset) => new(asset, false, null, null);
    public static AiAssetResolution Forbidden() => new(null, true, "forbidden", null);
    public static AiAssetResolution NotFound(string code) => new(null, false, code, null);
    public static AiAssetResolution NeedsClarification(string code) => new(null, false, code, null);
    public static AiAssetResolution Ambiguous(IReadOnlyList<string> candidates) => new(null, false, "ambiguous_asset", candidates);
}
