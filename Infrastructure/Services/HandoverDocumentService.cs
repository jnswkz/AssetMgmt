using System.Security.Cryptography;
using AssetMgmt.Application.Handover;
using AssetMgmt.Domain.Entities;
using AssetMgmt.Domain.Exceptions;
using AssetMgmt.Infrastructure.Documents;
using AssetMgmt.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;

namespace AssetMgmt.Infrastructure.Services;

public class HandoverDocumentService : IHandoverDocumentService
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly string _storageRoot;

    public HandoverDocumentService(
        AppDbContext db,
        IWebHostEnvironment env,
        IOptions<HandoverStorageOptions> options)
    {
        _db = db;
        _env = env;
        _storageRoot = Path.GetFullPath(options.Value.HandoverRoot
            ?? Path.Combine(env.ContentRootPath, "private", "handovers"));

        var webRoot = Path.GetFullPath(env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"));
        if (string.Equals(_storageRoot, webRoot, StringComparison.OrdinalIgnoreCase) ||
            IsWithin(_storageRoot, webRoot))
            throw new InvalidOperationException("Handover storage must be outside wwwroot.");
    }

    public async Task<HandoverResult> GenerateForAllocationAsync(
        Guid allocationId, Guid generatedBy, CancellationToken ct)
    {
        var data = await _db.Allocations.AsNoTracking()
            .Where(a => a.Id == allocationId)
            .Select(a => new
            {
                a.Id,
                a.AllocationRequestId,
                a.StartDate,
                a.Notes,
                AssetCode = a.AssetInstance.AssetCode,
                ModelName = a.AssetInstance.Model.Name,
                a.AssetInstance.Serial,
                a.AssetInstance.Location,
                a.AssetInstance.AcquisitionCost,
                EmployeeName = a.User.FullName,
                a.User.EmployeeCode,
                Department = a.User.Department != null ? a.User.Department.Name : null,
                ApproverName = _db.Users.Where(u => u.Id == generatedBy)
                    .Select(u => u.FullName).FirstOrDefault()
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new DomainException("Allocation not found.");

        var now = DateTime.UtcNow;
        var documentNumber = await NextDocumentNumberAsync(now.Year, ct);
        var model = new HandoverModel(
            documentNumber, now, data.AssetCode, data.ModelName, data.Serial,
            data.Location, data.AcquisitionCost, data.EmployeeName, data.EmployeeCode,
            data.Department, data.ApproverName ?? "IT", data.StartDate, data.Notes);
        var bytes = new HandoverPdfDocument(model).GeneratePdf();

        Directory.CreateDirectory(_storageRoot);
        var storageKey = $"{Guid.NewGuid():N}.pdf";
        var absolutePath = ResolvePrivatePath(storageKey);
        await File.WriteAllBytesAsync(absolutePath, bytes, ct);

        var doc = new HandoverDocument
        {
            DocumentNumber = documentNumber,
            AllocationId = data.Id,
            FilePath = storageKey,
            FileSizeBytes = bytes.LongLength,
            FileHashSha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
            GeneratedBy = generatedBy
        };
        _db.HandoverDocuments.Add(doc);
        await _db.SaveChangesAsync(ct);

        return Map(doc, data.AllocationRequestId);
    }

    public async Task<HandoverResult?> GetForRequestAsync(Guid requestId, CancellationToken ct)
    {
        var doc = await QueryForRequest(requestId).AsNoTracking().FirstOrDefaultAsync(ct);
        return doc is null ? null : Map(doc, requestId);
    }

    public async Task<HandoverFileResult?> GetFileForRequestAsync(Guid requestId, CancellationToken ct)
    {
        var doc = await QueryForRequest(requestId).FirstOrDefaultAsync(ct);
        if (doc is null) return null;
        await EnsurePrivateStorageAsync(doc, ct);
        return new HandoverFileResult(doc.DocumentNumber, ResolvePrivatePath(doc.FilePath));
    }

    public async Task<HandoverFileResult?> GetFileForAllocationAsync(Guid allocationId, CancellationToken ct)
    {
        var doc = await _db.HandoverDocuments
            .Where(d => d.AllocationId == allocationId)
            .OrderByDescending(d => d.GeneratedAt)
            .FirstOrDefaultAsync(ct);
        if (doc is null) return null;
        await EnsurePrivateStorageAsync(doc, ct);
        return new HandoverFileResult(doc.DocumentNumber, ResolvePrivatePath(doc.FilePath));
    }

    public async Task MigrateLegacyFilesAsync(CancellationToken ct)
    {
        var legacy = await _db.HandoverDocuments
            .Where(d => d.FilePath.StartsWith("/handovers/") || d.FilePath.StartsWith("handovers/"))
            .ToListAsync(ct);
        foreach (var doc in legacy) await EnsurePrivateStorageAsync(doc, ct, save: false);
        if (legacy.Count > 0) await _db.SaveChangesAsync(ct);
    }

    private IQueryable<HandoverDocument> QueryForRequest(Guid requestId) =>
        _db.HandoverDocuments
            .Where(d => d.Allocation.AllocationRequestId == requestId)
            .OrderByDescending(d => d.GeneratedAt);

    private async Task EnsurePrivateStorageAsync(HandoverDocument doc, CancellationToken ct, bool save = true)
    {
        if (!IsLegacyPath(doc.FilePath)) return;

        var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        var legacyName = Path.GetFileName(doc.FilePath);
        var source = Path.GetFullPath(Path.Combine(webRoot, "handovers", legacyName));
        var expectedLegacyRoot = Path.GetFullPath(Path.Combine(webRoot, "handovers"));
        if (!IsWithin(source, expectedLegacyRoot))
            throw new InvalidOperationException("Invalid legacy handover path.");

        Directory.CreateDirectory(_storageRoot);
        var storageKey = $"{doc.Id:N}.pdf";
        var destination = ResolvePrivatePath(storageKey);
        if (File.Exists(source) && !File.Exists(destination))
        {
            await using var input = File.OpenRead(source);
            await using var output = File.Create(destination);
            await input.CopyToAsync(output, ct);
        }
        doc.FilePath = storageKey;
        if (save) await _db.SaveChangesAsync(ct);
    }

    private string ResolvePrivatePath(string storageKey)
    {
        if (Path.IsPathRooted(storageKey) || storageKey != Path.GetFileName(storageKey))
            throw new InvalidOperationException("Invalid handover storage key.");
        var fullPath = Path.GetFullPath(Path.Combine(_storageRoot, storageKey));
        if (!IsWithin(fullPath, _storageRoot))
            throw new InvalidOperationException("Invalid handover storage path.");
        return fullPath;
    }

    private static bool IsLegacyPath(string path) =>
        path.StartsWith("/handovers/", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("handovers/", StringComparison.OrdinalIgnoreCase);

    private static bool IsWithin(string path, string root)
    {
        var normalizedRoot = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        return path.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static HandoverResult Map(HandoverDocument doc, Guid? requestId) => new(
        doc.Id,
        doc.DocumentNumber,
        requestId is null ? string.Empty : $"/api/requests/{requestId:D}/handover/download");

    private async Task<string> NextDocumentNumberAsync(int year, CancellationToken ct)
    {
        var prefix = $"BB-{year}-";
        var countThisYear = await _db.HandoverDocuments.CountAsync(d => d.DocumentNumber.StartsWith(prefix), ct);
        return $"{prefix}{(countThisYear + 1):D4}";
    }
}
