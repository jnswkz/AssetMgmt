using System.Security.Cryptography;
using System.Text;
using AssetMgmt.Application.Requests;
using AssetMgmt.Domain.Entities;
using AssetMgmt.Domain.Enums;
using AssetMgmt.Domain.Exceptions;
using AssetMgmt.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AssetMgmt.Application.Agents;

public class AiOperationsService
{
    private readonly AppDbContext _db;
    private readonly AiAssetAccessService _assetAccess;
    private readonly AllocationRequestService _requestService;

    public AiOperationsService(
        AppDbContext db,
        AiAssetAccessService assetAccess,
        AllocationRequestService requestService)
    {
        _db = db;
        _assetAccess = assetAccess;
        _requestService = requestService;
    }

    public async Task<IReadOnlyList<AiAssetCatalogItem>> ListAssetsAsync(
        ListAssetsPayload payload,
        CancellationToken ct)
    {
        var scope = NormalizeScope(payload.Scope);
        var pageSize = ClampPageSize(payload.PageSize);
        var role = _assetAccess.CurrentUserRole;
        var managedDepartmentIds = await GetManagedDepartmentIdsAsync(ct);

        var query = _db.AssetInstances.AsNoTracking()
            .Include(a => a.Model)
            .Include(a => a.CurrentHolder)
            .Where(a => a.DeletedAt == null);

        query = ApplyScope(query, scope, role, managedDepartmentIds);

        if (TryParseAssetStatus(payload.Status, out var status))
            query = query.Where(a => a.Status == status);

        if (TryParseAssetCategory(payload.Category, out var category))
            query = query.Where(a => a.Model.Category == category);

        if (!string.IsNullOrWhiteSpace(payload.Manufacturer))
        {
            var manufacturer = payload.Manufacturer.Trim();
            query = query.Where(a => a.Model.Manufacturer != null && EF.Functions.Like(a.Model.Manufacturer, $"%{manufacturer}%"));
        }

        var searchText = BuildSearch(payload.SearchText, payload.ModelQuery);
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var term = searchText.Trim();
            query = query.Where(a =>
                EF.Functions.Like(a.AssetCode, $"%{term}%") ||
                EF.Functions.Like(a.Serial, $"%{term}%") ||
                EF.Functions.Like(a.Model.Name, $"%{term}%") ||
                (a.Model.Manufacturer != null && EF.Functions.Like(a.Model.Manufacturer, $"%{term}%")) ||
                (a.Model.ModelNumber != null && EF.Functions.Like(a.Model.ModelNumber, $"%{term}%")));
        }

        return await query
            .OrderBy(a => a.Status)
            .ThenBy(a => a.AssetCode)
            .Take(pageSize)
            .Select(a => new AiAssetCatalogItem(
                a.Id,
                a.AssetCode,
                a.Model.Name,
                a.Model.Manufacturer,
                a.Model.ModelNumber,
                a.Model.Category,
                a.Status,
                a.CurrentHolder != null ? a.CurrentHolder.FullName : null,
                a.Location))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AiAssetModelCatalogItem>> ListAssetModelsAsync(
        ListAssetModelsPayload payload,
        CancellationToken ct)
    {
        var scope = NormalizeScope(payload.Scope);
        var pageSize = ClampPageSize(payload.PageSize);
        var role = _assetAccess.CurrentUserRole;
        var managedDepartmentIds = await GetManagedDepartmentIdsAsync(ct);

        if (string.Equals(role, nameof(UserRole.Employee), StringComparison.Ordinal) &&
            !string.Equals(scope, "available", StringComparison.Ordinal) &&
            !string.Equals(scope, "assigned_to_me", StringComparison.Ordinal))
        {
            scope = "available";
        }

        if (payload.AvailableOnly == true)
            scope = "available";

        var assetQuery = _db.AssetInstances.AsNoTracking()
            .Include(a => a.Model)
            .Include(a => a.CurrentHolder)
            .Where(a => a.DeletedAt == null);

        assetQuery = ApplyScope(assetQuery, scope, role, managedDepartmentIds);

        if (TryParseAssetCategory(payload.Category, out var category))
            assetQuery = assetQuery.Where(a => a.Model.Category == category);

        if (!string.IsNullOrWhiteSpace(payload.Manufacturer))
        {
            var manufacturer = payload.Manufacturer.Trim();
            assetQuery = assetQuery.Where(a => a.Model.Manufacturer != null && EF.Functions.Like(a.Model.Manufacturer, $"%{manufacturer}%"));
        }

        var searchText = BuildSearch(payload.SearchText, null);
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var term = searchText.Trim();
            assetQuery = assetQuery.Where(a =>
                EF.Functions.Like(a.Model.Name, $"%{term}%") ||
                (a.Model.Manufacturer != null && EF.Functions.Like(a.Model.Manufacturer, $"%{term}%")) ||
                (a.Model.ModelNumber != null && EF.Functions.Like(a.Model.ModelNumber, $"%{term}%")));
        }

        return await assetQuery
            .GroupBy(a => new
            {
                a.ModelId,
                a.Model.Name,
                a.Model.Category,
                a.Model.Manufacturer,
                a.Model.ModelNumber
            })
            .OrderBy(g => g.Key.Name)
            .Take(pageSize)
            .Select(g => new AiAssetModelCatalogItem(
                g.Key.ModelId,
                g.Key.Name,
                g.Key.Category,
                g.Key.Manufacturer,
                g.Key.ModelNumber,
                g.Count(),
                g.Count(a => a.Status == AssetStatus.InStock),
                g.Count(a => a.Status == AssetStatus.Allocated),
                g.Count(a => a.Status == AssetStatus.LockedTemp)))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AiPendingRequestSummary>> ListPendingRequestsAsync(
        ListPendingRequestsPayload payload,
        CancellationToken ct)
    {
        EnsureCanManageRequests();

        var pageSize = ClampPageSize(payload.PageSize);
        var query = AccessiblePendingRequestsQuery(await GetManagedDepartmentIdsAsync(ct));

        if (!string.IsNullOrWhiteSpace(payload.SearchText))
        {
            var term = payload.SearchText.Trim();
            query = query.Where(r =>
                EF.Functions.Like(r.AssetInstance.AssetCode, $"%{term}%") ||
                EF.Functions.Like(r.AssetInstance.Model.Name, $"%{term}%") ||
                EF.Functions.Like(r.Requester.FullName, $"%{term}%") ||
                EF.Functions.Like(r.Requester.EmployeeCode, $"%{term}%"));
        }

        var rows = await query
            .OrderBy(r => r.CreatedAt)
            .Take(pageSize)
            .Select(r => new
            {
                r.Id,
                RequesterName = r.Requester.FullName,
                AssetCode = r.AssetInstance.AssetCode,
                ModelName = r.AssetInstance.Model.Name,
                r.CreatedAt,
                r.LockExpiresAt
            })
            .ToListAsync(ct);

        return rows
            .Select(r => ToPendingSummary(r.Id, r.RequesterName, r.AssetCode, r.ModelName, r.CreatedAt, r.LockExpiresAt))
            .ToList();
    }

    public async Task<AiAllocationCreateResult> CreateAllocationRequestFromNeedAsync(
        CreateAllocationRequestPayload payload,
        Guid conversationId,
        CancellationToken ct)
    {
        var category = ParseAssetCategory(payload.Category);
        if (payload.AssetId is null &&
            string.IsNullOrWhiteSpace(payload.AssetCode) &&
            string.IsNullOrWhiteSpace(payload.ModelQuery) &&
            string.IsNullOrWhiteSpace(payload.Manufacturer) &&
            category is null)
        {
            throw new DomainException("Bạn vui lòng nêu rõ loại thiết bị, model, hãng hoặc mã tài sản cần cấp.");
        }

        if (payload.AssetId is Guid assetId)
            return await CreateRequestForCandidateAsync(await FindExactAssetCandidateByIdAsync(assetId, ct), payload, conversationId, ct);

        if (!string.IsNullOrWhiteSpace(payload.AssetCode))
            return await CreateRequestForCandidateAsync(await FindExactAssetCandidateByCodeAsync(payload.AssetCode.Trim(), ct), payload, conversationId, ct);

        var candidates = await FindAvailableCandidatesAsync(payload, category, ct);
        if (candidates.Count == 0)
            throw new DomainException("Tôi chưa tìm thấy asset trống phù hợp với nhu cầu này.");

        var idempotencyKey = BuildIdempotencyKey(conversationId, payload);
        foreach (var candidate in candidates)
        {
            try
            {
                var request = await _requestService.CreateAsync(
                    new CreateRequestDto(
                        candidate.AssetId,
                        BuildRequestReason(payload),
                        payload.ExpectedDurationMonths,
                        idempotencyKey),
                    ct);

                return new AiAllocationCreateResult(request, candidate);
            }
            catch (ConflictException)
            {
                continue;
            }
            catch (DomainException ex) when (IsAssetUnavailableMessage(ex.Message))
            {
                continue;
            }
        }

        throw new ConflictException("Asset phù hợp vừa được người khác giữ trước. Vui lòng thử lại để tôi chọn asset trống khác.");
    }

    public async Task<AllocationRequestDto> ApprovePendingRequestAsync(
        ApproveAllocationRequestPayload payload,
        CancellationToken ct)
    {
        var request = await ResolvePendingRequestAsync(payload.RequestId, payload.SearchText, ct);
        return await _requestService.ApproveAsync(request.Id, ct);
    }

    public async Task<AllocationRequestDto> RejectPendingRequestAsync(
        RejectAllocationRequestPayload payload,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(payload.Reason))
            throw new DomainException("Bạn cần nêu rõ lý do từ chối request.");

        var request = await ResolvePendingRequestAsync(payload.RequestId, payload.SearchText, ct);
        return await _requestService.RejectAsync(request.Id, payload.Reason.Trim(), ct);
    }

    private async Task<AiPendingRequestSummary> ResolvePendingRequestAsync(Guid? requestId, string? searchText, CancellationToken ct)
    {
        EnsureCanManageRequests();
        var query = AccessiblePendingRequestsQuery(await GetManagedDepartmentIdsAsync(ct));

        if (requestId is Guid exactId)
        {
            var exact = await query
                .Where(r => r.Id == exactId)
                .Select(r => new
                {
                    r.Id,
                    RequesterName = r.Requester.FullName,
                    AssetCode = r.AssetInstance.AssetCode,
                    ModelName = r.AssetInstance.Model.Name,
                    r.CreatedAt,
                    r.LockExpiresAt
                })
                .FirstOrDefaultAsync(ct);

            return exact is null
                ? throw new DomainException("Không tìm thấy request chờ duyệt phù hợp.")
                : ToPendingSummary(exact.Id, exact.RequesterName, exact.AssetCode, exact.ModelName, exact.CreatedAt, exact.LockExpiresAt);
        }

        if (string.IsNullOrWhiteSpace(searchText) || IsGenericRequestReference(searchText))
            return await ResolveSinglePendingRequestAsync(query, ct);

        var term = searchText.Trim();
        if (Guid.TryParse(term, out var parsedGuid))
        {
            var guidMatch = await query
                .Where(r => r.Id == parsedGuid)
                .Select(r => new
                {
                    r.Id,
                    RequesterName = r.Requester.FullName,
                    AssetCode = r.AssetInstance.AssetCode,
                    ModelName = r.AssetInstance.Model.Name,
                    r.CreatedAt,
                    r.LockExpiresAt
                })
                .FirstOrDefaultAsync(ct);

            if (guidMatch is not null)
                return ToPendingSummary(
                    guidMatch.Id,
                    guidMatch.RequesterName,
                    guidMatch.AssetCode,
                    guidMatch.ModelName,
                    guidMatch.CreatedAt,
                    guidMatch.LockExpiresAt);
        }

        var matchRows = await query
            .Where(r =>
                EF.Functions.Like(r.AssetInstance.AssetCode, $"%{term}%") ||
                EF.Functions.Like(r.AssetInstance.Model.Name, $"%{term}%") ||
                EF.Functions.Like(r.Requester.FullName, $"%{term}%") ||
                EF.Functions.Like(r.Requester.EmployeeCode, $"%{term}%"))
            .OrderBy(r => r.CreatedAt)
            .Take(10)
            .Select(r => new
            {
                r.Id,
                RequesterName = r.Requester.FullName,
                AssetCode = r.AssetInstance.AssetCode,
                ModelName = r.AssetInstance.Model.Name,
                r.CreatedAt,
                r.LockExpiresAt
            })
            .ToListAsync(ct);

        var matches = matchRows
            .Select(r => ToPendingSummary(r.Id, r.RequesterName, r.AssetCode, r.ModelName, r.CreatedAt, r.LockExpiresAt))
            .Where(r => r.IdShort.StartsWith(term, StringComparison.OrdinalIgnoreCase) ||
                        r.RequesterName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                        r.AssetCode.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                        r.ModelName.Contains(term, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 0 && LooksLikeShortRequestId(term))
        {
            var shortIdRows = await query
                .OrderBy(r => r.CreatedAt)
                .Take(50)
                .Select(r => new
                {
                    r.Id,
                    RequesterName = r.Requester.FullName,
                    AssetCode = r.AssetInstance.AssetCode,
                    ModelName = r.AssetInstance.Model.Name,
                    r.CreatedAt,
                    r.LockExpiresAt
                })
                .ToListAsync(ct);

            matches = shortIdRows
                .Select(r => ToPendingSummary(r.Id, r.RequesterName, r.AssetCode, r.ModelName, r.CreatedAt, r.LockExpiresAt))
                .Where(r => r.IdShort.StartsWith(term, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return matches.Count switch
        {
            0 => throw new DomainException("Không tìm thấy request chờ duyệt phù hợp."),
            > 1 => throw new DomainException(
                "Tôi thấy nhiều request phù hợp: " + string.Join("; ", matches.Select(x => $"{x.IdShort} - {x.RequesterName} - {x.AssetCode} ({x.ModelName})"))),
            _ => matches[0]
        };
    }

    private static async Task<AiPendingRequestSummary> ResolveSinglePendingRequestAsync(
        IQueryable<AllocationRequest> query,
        CancellationToken ct)
    {
        var pending = await query
            .OrderBy(r => r.CreatedAt)
            .Take(2)
            .Select(r => new
            {
                r.Id,
                RequesterName = r.Requester.FullName,
                AssetCode = r.AssetInstance.AssetCode,
                ModelName = r.AssetInstance.Model.Name,
                r.CreatedAt,
                r.LockExpiresAt
            })
            .ToListAsync(ct);

        return pending.Count switch
        {
            0 => throw new DomainException("Không tìm thấy request chờ duyệt phù hợp."),
            > 1 => throw new DomainException(
                "Có nhiều request đang chờ duyệt. Vui lòng chọn mã request cụ thể: "
                + string.Join("; ", pending.Select(x => $"{x.Id.ToString("N").Substring(0, 8)} - {x.RequesterName} - {x.AssetCode} ({x.ModelName})"))),
            _ => ToPendingSummary(
                pending[0].Id,
                pending[0].RequesterName,
                pending[0].AssetCode,
                pending[0].ModelName,
                pending[0].CreatedAt,
                pending[0].LockExpiresAt)
        };
    }

    private IQueryable<AllocationRequest> AccessiblePendingRequestsQuery(IReadOnlyCollection<Guid> managedDepartmentIds)
    {
        var role = _assetAccess.CurrentUserRole;
        var query = _db.AllocationRequests.AsNoTracking()
            .Include(r => r.Requester)
            .Include(r => r.AssetInstance).ThenInclude(a => a.Model)
            .Where(r => r.Status == RequestStatus.Pending);

        if (string.Equals(role, nameof(UserRole.AdminIT), StringComparison.Ordinal))
            return query;

        return query.Where(r => r.Requester.DepartmentId != null && managedDepartmentIds.Contains(r.Requester.DepartmentId.Value));
    }

    private async Task<AiAssetRequestCandidate> FindExactAssetCandidateByIdAsync(Guid assetId, CancellationToken ct)
    {
        var asset = await _db.AssetInstances.AsNoTracking()
            .Include(a => a.Model)
            .Where(a => a.Id == assetId && a.DeletedAt == null)
            .Select(MapCandidate())
            .FirstOrDefaultAsync(ct);

        return asset ?? throw new DomainException("Không tìm thấy asset phù hợp.");
    }

    private async Task<AiAssetRequestCandidate> FindExactAssetCandidateByCodeAsync(string assetCode, CancellationToken ct)
    {
        var asset = await _db.AssetInstances.AsNoTracking()
            .Include(a => a.Model)
            .Where(a => a.AssetCode == assetCode && a.DeletedAt == null)
            .Select(MapCandidate())
            .FirstOrDefaultAsync(ct);

        return asset ?? throw new DomainException("Không tìm thấy asset phù hợp.");
    }

    private async Task<IReadOnlyList<AiAssetRequestCandidate>> FindAvailableCandidatesAsync(
        CreateAllocationRequestPayload payload,
        AssetCategory? category,
        CancellationToken ct)
    {
        var searchText = BuildSearch(payload.ModelQuery, payload.Manufacturer, payload.DesiredUse, payload.Reason);

        var query = _db.AssetInstances.AsNoTracking()
            .Include(a => a.Model)
            .Where(a => a.DeletedAt == null && a.Status == AssetStatus.InStock);

        if (category is not null)
            query = query.Where(a => a.Model.Category == category.Value);

        if (!string.IsNullOrWhiteSpace(payload.Manufacturer))
        {
            var manufacturer = payload.Manufacturer.Trim();
            query = query.Where(a => a.Model.Manufacturer != null && EF.Functions.Like(a.Model.Manufacturer, $"%{manufacturer}%"));
        }

        var raw = await query
            .Select(MapCandidate())
            .ToListAsync(ct);

        var ranked = raw
            .Select(candidate => candidate with { Score = ScoreCandidate(candidate, payload, category, searchText) })
            .Where(candidate => category is not null || !string.IsNullOrWhiteSpace(payload.ModelQuery) || !string.IsNullOrWhiteSpace(payload.Manufacturer)
                ? candidate.Score > 0
                : true)
            .OrderByDescending(candidate => candidate.Score)
            .ThenByDescending(candidate => candidate.AcquisitionDate)
            .ThenBy(candidate => candidate.AssetCode, StringComparer.Ordinal)
            .Take(10)
            .ToList();

        if (ranked.Count >= 2 &&
            ranked[0].Score > 0 &&
            ranked[0].Score == ranked[1].Score &&
            !string.IsNullOrWhiteSpace(payload.ModelQuery) &&
            string.IsNullOrWhiteSpace(payload.Manufacturer) &&
            category is null)
        {
            return ranked.Take(1).ToList();
        }

        return ranked;
    }

    private async Task<AiAllocationCreateResult> CreateRequestForCandidateAsync(
        AiAssetRequestCandidate candidate,
        CreateAllocationRequestPayload payload,
        Guid conversationId,
        CancellationToken ct)
    {
        if (candidate.Status != AssetStatus.InStock)
            throw new DomainException("Asset này hiện không còn sẵn sàng để tạo request.");

        var request = await _requestService.CreateAsync(
            new CreateRequestDto(
                candidate.AssetId,
                BuildRequestReason(payload),
                payload.ExpectedDurationMonths,
                BuildIdempotencyKey(conversationId, payload)),
            ct);

        return new AiAllocationCreateResult(request, candidate);
    }

    private IQueryable<AssetInstance> ApplyScope(
        IQueryable<AssetInstance> query,
        string scope,
        string role,
        IReadOnlyCollection<Guid> managedDepartmentIds)
    {
        if (string.Equals(role, nameof(UserRole.AdminIT), StringComparison.Ordinal))
        {
            return scope switch
            {
                "available" => query.Where(a => a.Status == AssetStatus.InStock),
                "assigned_to_me" => query.Where(a => a.CurrentHolderId == _assetAccess.CurrentUserId),
                _ => query
            };
        }

        if (string.Equals(role, nameof(UserRole.Manager), StringComparison.Ordinal))
        {
            return scope switch
            {
                "available" => query.Where(a => a.Status == AssetStatus.InStock),
                "assigned_to_me" => query.Where(a => a.CurrentHolderId == _assetAccess.CurrentUserId),
                _ => query.Where(a =>
                    a.CurrentHolder != null &&
                    a.CurrentHolder.DepartmentId != null &&
                    managedDepartmentIds.Contains(a.CurrentHolder.DepartmentId.Value))
            };
        }

        return scope switch
        {
            "assigned_to_me" => query.Where(a => a.CurrentHolderId == _assetAccess.CurrentUserId),
            _ => query.Where(a => a.Status == AssetStatus.InStock)
        };
    }

    private void EnsureCanManageRequests()
    {
        var role = _assetAccess.CurrentUserRole;
        if (!string.Equals(role, nameof(UserRole.Manager), StringComparison.Ordinal) &&
            !string.Equals(role, nameof(UserRole.AdminIT), StringComparison.Ordinal))
        {
            throw new DomainException("Bạn không có quyền duyệt request.");
        }
    }

    private async Task<IReadOnlyCollection<Guid>> GetManagedDepartmentIdsAsync(CancellationToken ct)
    {
        var role = _assetAccess.CurrentUserRole;
        if (!string.Equals(role, nameof(UserRole.Manager), StringComparison.Ordinal))
            return Array.Empty<Guid>();

        return await _db.Departments.AsNoTracking()
            .Where(d => d.ManagerId == _assetAccess.CurrentUserId && d.IsActive && d.DeletedAt == null)
            .Select(d => d.Id)
            .ToListAsync(ct);
    }

    private static string NormalizeScope(string? scope) =>
        string.IsNullOrWhiteSpace(scope) ? "available" : scope.Trim().ToLowerInvariant();

    private static int ClampPageSize(int? pageSize) =>
        pageSize is null or < 1 ? 5 : Math.Min(pageSize.Value, 10);

    private static bool TryParseAssetStatus(string? value, out AssetStatus status) =>
        Enum.TryParse(value, true, out status);

    private static bool TryParseAssetCategory(string? value, out AssetCategory category) =>
        Enum.TryParse(value, true, out category);

    private static AssetCategory? ParseAssetCategory(string? value) =>
        Enum.TryParse<AssetCategory>(value, true, out var category) ? category : null;

    private static string? BuildSearch(params string?[] parts)
    {
        var normalized = parts
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .ToList();

        return normalized.Count == 0 ? null : string.Join(' ', normalized);
    }

    private static string BuildRequestReason(CreateAllocationRequestPayload payload)
    {
        var parts = new[]
        {
            payload.Reason?.Trim(),
            payload.DesiredUse?.Trim(),
            payload.ModelQuery?.Trim()
        }.Where(x => !string.IsNullOrWhiteSpace(x));

        return string.Join(" | ", parts);
    }

    private static string BuildIdempotencyKey(Guid conversationId, CreateAllocationRequestPayload payload)
    {
        var material = string.Join('|', new[]
        {
            conversationId.ToString("N"),
            payload.AssetId?.ToString("N") ?? string.Empty,
            payload.AssetCode ?? string.Empty,
            payload.ModelQuery ?? string.Empty,
            payload.Manufacturer ?? string.Empty,
            payload.Category ?? string.Empty,
            payload.Reason ?? string.Empty,
            payload.ExpectedDurationMonths?.ToString() ?? string.Empty,
            payload.DesiredUse ?? string.Empty
        });

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
        return $"ai-{hash[..24].ToLowerInvariant()}";
    }

    private static bool IsAssetUnavailableMessage(string message) =>
        message.Contains("not available", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("không còn sẵn", StringComparison.OrdinalIgnoreCase);

    private static bool IsGenericRequestReference(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        return normalized is "request" or "pending request" or "mã ngắn" or "ma ngan" ||
               normalized.Contains("theo mã ngắn", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("theo ma ngan", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeShortRequestId(string value) =>
        value.Length is >= 6 and <= 12 &&
        value.All(Uri.IsHexDigit);

    private static AiPendingRequestSummary ToPendingSummary(
        Guid id,
        string requesterName,
        string assetCode,
        string modelName,
        DateTime createdAt,
        DateTime? lockExpiresAt) =>
        new(
            id,
            id.ToString("N").Substring(0, 8),
            requesterName,
            assetCode,
            modelName,
            createdAt,
            lockExpiresAt);

    private static int ScoreCandidate(
        AiAssetRequestCandidate candidate,
        CreateAllocationRequestPayload payload,
        AssetCategory? category,
        string? searchText)
    {
        var score = 0;

        if (category is not null && candidate.Category == category.Value)
            score += 20;

        if (!string.IsNullOrWhiteSpace(payload.Manufacturer) &&
            candidate.Manufacturer != null &&
            candidate.Manufacturer.Contains(payload.Manufacturer.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            score += 25;
        }

        if (!string.IsNullOrWhiteSpace(payload.ModelQuery))
        {
            var query = payload.ModelQuery.Trim();
            if (candidate.ModelName.Contains(query, StringComparison.OrdinalIgnoreCase))
                score += 40;
            if (!string.IsNullOrWhiteSpace(candidate.ModelNumber) &&
                candidate.ModelNumber.Contains(query, StringComparison.OrdinalIgnoreCase))
                score += 35;
        }

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            foreach (var token in searchText
                         .Split([' ', ',', '.', ';', ':', '-', '_', '/'], StringSplitOptions.RemoveEmptyEntries)
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (candidate.AssetCode.Contains(token, StringComparison.OrdinalIgnoreCase))
                    score += 10;
                if (candidate.ModelName.Contains(token, StringComparison.OrdinalIgnoreCase))
                    score += 8;
                if (!string.IsNullOrWhiteSpace(candidate.Manufacturer) &&
                    candidate.Manufacturer.Contains(token, StringComparison.OrdinalIgnoreCase))
                    score += 6;
                if (!string.IsNullOrWhiteSpace(candidate.ModelNumber) &&
                    candidate.ModelNumber.Contains(token, StringComparison.OrdinalIgnoreCase))
                    score += 6;
            }
        }

        return score;
    }

    private static System.Linq.Expressions.Expression<Func<AssetInstance, AiAssetRequestCandidate>> MapCandidate() =>
        a => new AiAssetRequestCandidate(
            a.Id,
            a.AssetCode,
            a.Model.Name,
            a.Model.Manufacturer,
            a.Model.ModelNumber,
            a.Model.Category,
            a.Status,
            a.AcquisitionDate,
            a.Location,
            0);
}

public sealed record AiAssetRequestCandidate(
    Guid AssetId,
    string AssetCode,
    string ModelName,
    string? Manufacturer,
    string? ModelNumber,
    AssetCategory Category,
    AssetStatus Status,
    DateTime AcquisitionDate,
    string? Location,
    int Score);

public sealed record AiAllocationCreateResult(
    AllocationRequestDto Request,
    AiAssetRequestCandidate SelectedAsset);
