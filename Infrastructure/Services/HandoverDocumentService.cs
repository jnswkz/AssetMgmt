using System.Security.Cryptography;
using AssetMgmt.Application.Handover;
using AssetMgmt.Domain.Entities;
using AssetMgmt.Domain.Exceptions;
using AssetMgmt.Infrastructure.Documents;
using AssetMgmt.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;

namespace AssetMgmt.Infrastructure.Services;

public class HandoverDocumentService : IHandoverDocumentService
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public HandoverDocumentService(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    public async Task<HandoverResult> GenerateForAllocationAsync(
        Guid allocationId, Guid generatedBy, CancellationToken ct)
    {
        // Pull everything the template needs in one projection.
        var data = await _db.Allocations.AsNoTracking()
            .Where(a => a.Id == allocationId)
            .Select(a => new
            {
                a.Id,
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
            DocumentNumber: documentNumber,
            GeneratedAt: now,
            AssetCode: data.AssetCode,
            ModelName: data.ModelName,
            Serial: data.Serial,
            Location: data.Location,
            AcquisitionCost: data.AcquisitionCost,
            EmployeeName: data.EmployeeName,
            EmployeeCode: data.EmployeeCode,
            EmployeeDepartment: data.Department,
            ApproverName: data.ApproverName ?? "IT",
            HandoverDate: data.StartDate,
            Notes: data.Notes);

        var bytes = new HandoverPdfDocument(model).GeneratePdf();

        // Persist under wwwroot/handovers/<number>.pdf
        var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        var dir = Path.Combine(webRoot, "handovers");
        Directory.CreateDirectory(dir);

        var fileName = $"{documentNumber}.pdf";
        var absolutePath = Path.Combine(dir, fileName);
        await File.WriteAllBytesAsync(absolutePath, bytes, ct);

        var webPath = $"/handovers/{fileName}";
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        var doc = new HandoverDocument
        {
            DocumentNumber = documentNumber,
            AllocationId = data.Id,
            FilePath = webPath,
            FileSizeBytes = bytes.LongLength,
            FileHashSha256 = hash,
            GeneratedBy = generatedBy
        };
        _db.HandoverDocuments.Add(doc);
        await _db.SaveChangesAsync(ct);

        return new HandoverResult(doc.Id, documentNumber, webPath);
    }

    public async Task<HandoverResult?> GetForRequestAsync(Guid requestId, CancellationToken ct)
    {
        return await _db.HandoverDocuments.AsNoTracking()
            .Where(d => d.Allocation.AllocationRequestId == requestId)
            .OrderByDescending(d => d.GeneratedAt)
            .Select(d => new HandoverResult(d.Id, d.DocumentNumber, d.FilePath))
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Produces the next number in the BB-YYYY-NNNN series for the given year.
    /// Adequate for the single-instance MVP; a sequence/table would be needed
    /// for true concurrency safety.
    /// </summary>
    private async Task<string> NextDocumentNumberAsync(int year, CancellationToken ct)
    {
        var prefix = $"BB-{year}-";
        var countThisYear = await _db.HandoverDocuments
            .CountAsync(d => d.DocumentNumber.StartsWith(prefix), ct);
        return $"{prefix}{(countThisYear + 1):D4}";
    }
}
