using AssetMgmt.Application.Handover;

namespace AssetMgmt.Application.Handover;

public interface IHandoverDocumentService
{
    /// <summary>
    /// Renders a handover PDF for the given (already-persisted) allocation, writes it
    /// under wwwroot/handovers, and inserts a handover_documents row linked to it.
    /// Runs on the caller's DbContext/transaction — does not commit.
    /// Returns the created document's number and web-relative path.
    /// </summary>
    Task<HandoverResult> GenerateForAllocationAsync(Guid allocationId, Guid generatedBy, CancellationToken ct);

    /// <summary>
    /// Returns the handover document produced for a given allocation request
    /// (via its Allocated event), or null if none exists yet.
    /// </summary>
    Task<HandoverResult?> GetForRequestAsync(Guid requestId, CancellationToken ct);
}

public record HandoverResult(Guid Id, string DocumentNumber, string FilePath);
