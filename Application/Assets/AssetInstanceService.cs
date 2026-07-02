using AssetMgmt.Application.Auth;
using AssetMgmt.Application.Common;
using AssetMgmt.Domain.Entities;
using AssetMgmt.Domain.Enums;
using AssetMgmt.Domain.Exceptions;
using AssetMgmt.Infrastructure.Persistence;
using AssetMgmt.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace AssetMgmt.Application.Assets;

public class AssetInstanceService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IQrCodeService _qr;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AssetInstanceService(
        AppDbContext db,
        ICurrentUser currentUser,
        IQrCodeService qr,
        IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _currentUser = currentUser;
        _qr = qr;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<PagedResult<AssetInstanceListItem>> ListAsync(
        AssetStatus? status, Guid? modelId, string? search, PageQuery page, CancellationToken ct)
    {
        var query = _db.AssetInstances.AsNoTracking();

        if (status is not null)
            query = query.Where(a => a.Status == status);
        if (modelId is not null)
            query = query.Where(a => a.ModelId == modelId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(a =>
                EF.Functions.Like(a.AssetCode, $"%{term}%") ||
                EF.Functions.Like(a.Serial, $"%{term}%"));
        }

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderBy(a => a.AssetCode)
            .Skip(page.Skip).Take(page.NormalizedPageSize)
            .Select(a => new
            {
                a.Id,
                a.AssetCode,
                a.Serial,
                a.ModelId,
                ModelName = a.Model.Name,
                a.Status,
                a.CurrentHolderId,
                CurrentHolderName = a.CurrentHolder != null ? a.CurrentHolder.FullName : null,
                a.Location,
                a.QrCodePath
            })
            .ToListAsync(ct);

        var items = rows
            .Select(a => new AssetInstanceListItem(
                a.Id, a.AssetCode, a.Serial, a.ModelId, a.ModelName, a.Status,
                a.CurrentHolderId, a.CurrentHolderName, a.Location, a.QrCodePath, ToPublicUrl(a.QrCodePath)))
            .ToList();

        return new PagedResult<AssetInstanceListItem>(items, total, page.NormalizedPage, page.NormalizedPageSize);
    }

    public async Task<AssetInstanceDto> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var a = await _db.AssetInstances.AsNoTracking()
            .Include(x => x.Model)
            .Include(x => x.CurrentHolder)
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new DomainException("Asset not found.");
        return Map(a);
    }

    public async Task<AssetInstanceDto> CreateAsync(CreateAssetInstanceRequest req, CancellationToken ct)
    {
        var model = await _db.AssetModels.FirstOrDefaultAsync(m => m.Id == req.ModelId, ct)
            ?? throw new DomainException("Asset model not found.");

        if (await _db.AssetInstances.AnyAsync(a => a.Serial == req.Serial, ct))
            throw new DomainException("An asset with this serial already exists.");

        // Reuse the DB function so codes match seed-data format and numbering is consistent.
        var categoryName = model.Category.ToString();
        var assetCode = await _db.Database
            .SqlQuery<string>($"SELECT asset.fn_generate_asset_code({categoryName}) AS Value")
            .SingleAsync(ct);

        var instance = new AssetInstance
        {
            AssetCode = assetCode,
            Serial = req.Serial.Trim(),
            ModelId = req.ModelId,
            Status = AssetStatus.InStock,
            CurrentHolderId = null,
            AcquisitionCost = req.AcquisitionCost,
            AcquisitionDate = req.AcquisitionDate,
            SalvageValue = req.SalvageValue ?? 0m,
            Location = req.Location?.Trim(),
            WarrantyExpiresAt = req.WarrantyExpiresAt,
            Notes = req.Notes,
            CreatedBy = _currentUser.Id,
            UpdatedBy = _currentUser.Id
        };

        _db.AssetInstances.Add(instance);
        await _db.SaveChangesAsync(ct);

        instance.QrCodePath = await _qr.GenerateForAssetAsync(instance.AssetCode, ct);
        await _db.SaveChangesAsync(ct);

        return await GetByIdAsync(instance.Id, ct);
    }

    public async Task<AssetInstanceDto> UpdateAsync(Guid id, UpdateAssetInstanceRequest req, CancellationToken ct)
    {
        var a = await _db.AssetInstances.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new DomainException("Asset not found.");

        if (!string.Equals(a.Serial, req.Serial.Trim(), StringComparison.Ordinal) &&
            await _db.AssetInstances.AnyAsync(x => x.Serial == req.Serial && x.Id != id, ct))
            throw new DomainException("An asset with this serial already exists.");

        a.Serial = req.Serial.Trim();
        a.AcquisitionCost = req.AcquisitionCost;
        a.AcquisitionDate = req.AcquisitionDate;
        a.SalvageValue = req.SalvageValue;
        a.Location = req.Location?.Trim();
        a.WarrantyExpiresAt = req.WarrantyExpiresAt;
        a.Notes = req.Notes;
        a.UpdatedBy = _currentUser.Id;

        await _db.SaveChangesAsync(ct);
        return await GetByIdAsync(a.Id, ct);
    }

    public async Task SoftDeleteAsync(Guid id, CancellationToken ct)
    {
        var a = await _db.AssetInstances.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new DomainException("Asset not found.");

        if (a.Status is AssetStatus.Allocated or AssetStatus.LockedTemp)
            throw new DomainException("Cannot delete an asset that is allocated or locked.");

        a.DeletedAt = DateTime.UtcNow;
        a.UpdatedBy = _currentUser.Id;
        await _db.SaveChangesAsync(ct);
    }

    private AssetInstanceDto Map(AssetInstance a) => new(
        a.Id, a.AssetCode, a.Serial, a.ModelId, a.Model?.Name ?? string.Empty, a.Status,
        a.CurrentHolderId, a.CurrentHolder?.FullName,
        a.AcquisitionCost, a.AcquisitionDate, a.SalvageValue, a.Location,
        a.WarrantyExpiresAt, a.QrCodePath, ToPublicUrl(a.QrCodePath), a.Notes, a.Version, a.CreatedAt, a.UpdatedAt);

    private string? ToPublicUrl(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        if (Uri.TryCreate(path, UriKind.Absolute, out _))
            return path;

        var request = _httpContextAccessor.HttpContext?.Request;
        if (request is null)
            return path;

        var normalizedPath = path.StartsWith('/') ? path : $"/{path}";
        return $"{request.Scheme}://{request.Host}{request.PathBase}{normalizedPath}";
    }
}
