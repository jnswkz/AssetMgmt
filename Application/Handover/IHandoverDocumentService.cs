namespace AssetMgmt.Application.Handover;

public interface IHandoverDocumentService
{
    Task<HandoverResult> GenerateForAllocationAsync(Guid allocationId, Guid generatedBy, CancellationToken ct);
    Task<HandoverResult?> GetForRequestAsync(Guid requestId, CancellationToken ct);
    Task<HandoverFileResult?> GetFileForRequestAsync(Guid requestId, CancellationToken ct);
    Task<HandoverFileResult?> GetFileForAllocationAsync(Guid allocationId, CancellationToken ct);
    Task MigrateLegacyFilesAsync(CancellationToken ct);
}

public record HandoverResult(Guid Id, string DocumentNumber, string DownloadUrl);
public record HandoverFileResult(string DocumentNumber, string FullPath);

public sealed class HandoverStorageOptions
{
    public const string SectionName = "Storage";
    public string? HandoverRoot { get; set; }
}
