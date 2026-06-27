using AssetMgmt.Application.Auth;
using AssetMgmt.Application.Common;
using AssetMgmt.Domain.Entities;
using AssetMgmt.Domain.Enums;
using AssetMgmt.Domain.Exceptions;
using AssetMgmt.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AssetMgmt.Application.Assets;

public class AssetModelService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public AssetModelService(AppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<AssetModelListItem>> ListAsync(
        AssetCategory? category, string? search, PageQuery page, CancellationToken ct)
    {
        var query = _db.AssetModels.AsNoTracking();

        if (category is not null)
            query = query.Where(m => m.Category == category);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(m =>
                EF.Functions.Like(m.Name, $"%{term}%") ||
                (m.Manufacturer != null && EF.Functions.Like(m.Manufacturer, $"%{term}%")));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(m => m.Name)
            .Skip(page.Skip).Take(page.NormalizedPageSize)
            .Select(m => new AssetModelListItem(
                m.Id, m.Name, m.Category, m.Manufacturer, m.ModelNumber,
                m.DefaultUsefulLifeMonths, m.Instances.Count(i => i.DeletedAt == null)))
            .ToListAsync(ct);

        return new PagedResult<AssetModelListItem>(items, total, page.NormalizedPage, page.NormalizedPageSize);
    }

    public async Task<AssetModelDto> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var m = await _db.AssetModels.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new DomainException("Asset model not found.");
        return Map(m);
    }

    public async Task<AssetModelDto> CreateAsync(CreateAssetModelRequest req, CancellationToken ct)
    {
        var model = new AssetModel
        {
            Name = req.Name.Trim(),
            Category = req.Category,
            Manufacturer = req.Manufacturer?.Trim(),
            ModelNumber = req.ModelNumber?.Trim(),
            Specs = req.Specs,
            DefaultUsefulLifeMonths = req.DefaultUsefulLifeMonths ?? 36,
            DefaultDepreciationMethod = req.DefaultDepreciationMethod ?? DepreciationMethod.StraightLine,
            ImageUrl = req.ImageUrl,
            CreatedBy = _currentUser.Id,
            UpdatedBy = _currentUser.Id
        };

        _db.AssetModels.Add(model);
        await _db.SaveChangesAsync(ct);
        return Map(model);
    }

    public async Task<AssetModelDto> UpdateAsync(Guid id, UpdateAssetModelRequest req, CancellationToken ct)
    {
        var model = await _db.AssetModels.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new DomainException("Asset model not found.");

        model.Name = req.Name.Trim();
        model.Category = req.Category;
        model.Manufacturer = req.Manufacturer?.Trim();
        model.ModelNumber = req.ModelNumber?.Trim();
        model.Specs = req.Specs;
        model.DefaultUsefulLifeMonths = req.DefaultUsefulLifeMonths;
        model.DefaultDepreciationMethod = req.DefaultDepreciationMethod;
        model.ImageUrl = req.ImageUrl;
        model.UpdatedBy = _currentUser.Id;

        await _db.SaveChangesAsync(ct);
        return Map(model);
    }

    public async Task SoftDeleteAsync(Guid id, CancellationToken ct)
    {
        var model = await _db.AssetModels
            .Include(m => m.Instances)
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new DomainException("Asset model not found.");

        if (model.Instances.Any(i => i.DeletedAt == null))
            throw new DomainException("Cannot delete a model that still has asset instances.");

        model.DeletedAt = DateTime.UtcNow;
        model.UpdatedBy = _currentUser.Id;
        await _db.SaveChangesAsync(ct);
    }

    private static AssetModelDto Map(AssetModel m) => new(
        m.Id, m.Name, m.Category, m.Manufacturer, m.ModelNumber, m.Specs,
        m.DefaultUsefulLifeMonths, m.DefaultDepreciationMethod, m.ImageUrl,
        m.CreatedAt, m.UpdatedAt);
}
