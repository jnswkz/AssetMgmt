using System.Security.Cryptography;
using System.Text;
using AssetMgmt.Domain.Entities;
using AssetMgmt.Domain.Exceptions;
using AssetMgmt.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AssetMgmt.Application.Auth;

public class AuthService
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private readonly AppDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly ITokenService _tokens;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuthService(
        AppDbContext db,
        IPasswordHasher hasher,
        ITokenService tokens,
        IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _hasher = hasher;
        _tokens = tokens;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<TokenResponse> LoginAsync(LoginRequest req, CancellationToken ct)
    {
        var normalized = req.UserName.Trim().ToUpperInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.NormalizedUserName == normalized, ct);
        var now = DateTimeOffset.UtcNow;

        var isLocked = user?.LockoutEnabled == true && user.LockoutEnd > now;
        var valid = user is not null && user.IsActive && !isLocked &&
                    _hasher.Verify(req.Password, user.PasswordHash);
        if (!valid)
        {
            if (user is not null && user.IsActive && !isLocked)
            {
                user.AccessFailedCount++;
                if (user.LockoutEnabled && user.AccessFailedCount >= MaxFailedAttempts)
                {
                    user.LockoutEnd = now.Add(LockoutDuration);
                    user.AccessFailedCount = 0;
                }
                await _db.SaveChangesAsync(ct);
            }
            throw new DomainException("Invalid username or password.");
        }

        user!.AccessFailedCount = 0;
        user.LockoutEnd = null;
        user.LastLoginAt = DateTime.UtcNow;
        user.SecurityStamp ??= Guid.NewGuid().ToString("N");
        return await IssueNewFamilyAsync(user, ct);
    }

    public Task<TokenResponse> RefreshAsync(RefreshRequest req, CancellationToken ct) =>
        _db.ExecuteWithRetryStrategyAsync(() => RefreshCoreAsync(req, ct));

    private async Task<TokenResponse> RefreshCoreAsync(RefreshRequest req, CancellationToken ct)
    {
        var validated = _tokens.ValidateRefreshToken(req.RefreshToken)
            ?? throw InvalidRefresh();
        var jtiHash = HashJti(validated.Jti);

        await using var transaction = await BeginTransactionIfSupportedAsync(ct);
        var session = await _db.RefreshSessions
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.UserId == validated.UserId && x.TokenJtiHash == jtiHash, ct)
            ?? throw InvalidRefresh();

        if (session.UsedAt is not null || session.RevokedAt is not null)
        {
            await RevokeCompromisedFamilyAsync(session, ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
            throw InvalidRefresh();
        }

        var now = DateTime.UtcNow;
        var user = session.User;
        if (session.ExpiresAt <= now || validated.ExpiresAt <= now || !user.IsActive ||
            !StampMatches(user.SecurityStamp, validated.SecurityStamp))
        {
            session.RevokedAt = now;
            await _db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
            throw InvalidRefresh();
        }

        var replacement = _tokens.CreateRefreshToken(user);
        var replacementSession = CreateSession(
            user.Id, session.FamilyId, replacement, CurrentIpAddress());
        session.UsedAt = now;
        session.ReplacedById = replacementSession.Id;
        _db.RefreshSessions.Add(replacementSession);
        await _db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);

        var (access, accessExpires) = _tokens.CreateAccessToken(user);
        return new TokenResponse(access, replacement.Token, accessExpires);
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken ct)
    {
        var validated = _tokens.ValidateRefreshToken(refreshToken);
        if (validated is null) return;

        var hash = HashJti(validated.Jti);
        var session = await _db.RefreshSessions
            .FirstOrDefaultAsync(x => x.UserId == validated.UserId && x.TokenJtiHash == hash, ct);
        if (session is null || session.RevokedAt is not null) return;

        session.RevokedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<MeResponse> GetMeAsync(Guid userId, CancellationToken ct)
    {
        var user = await _db.Users
            .Include(u => u.Department)
            .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive, ct)
            ?? throw new DomainException("User not found.");

        return new MeResponse(
            user.Id, user.UserName, user.Email, user.FullName, user.EmployeeCode,
            user.Role.ToString(), user.DepartmentId, user.Department?.Name);
    }

    private async Task<TokenResponse> IssueNewFamilyAsync(User user, CancellationToken ct)
    {
        var refresh = _tokens.CreateRefreshToken(user);
        _db.RefreshSessions.Add(CreateSession(
            user.Id, Guid.NewGuid(), refresh, CurrentIpAddress()));
        await _db.SaveChangesAsync(ct);

        var (access, accessExpires) = _tokens.CreateAccessToken(user);
        return new TokenResponse(access, refresh.Token, accessExpires);
    }

    private async Task RevokeCompromisedFamilyAsync(RefreshSession reused, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var family = await _db.RefreshSessions
            .Where(x => x.UserId == reused.UserId && x.FamilyId == reused.FamilyId && x.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var session in family) session.RevokedAt = now;

        reused.User.SecurityStamp = Guid.NewGuid().ToString("N");
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = reused.UserId,
            Action = "AUTH.RefreshTokenReplay",
            EntityType = "refresh_session",
            EntityId = reused.Id,
            IpAddress = CurrentIpAddress(),
            Severity = "Critical",
            Result = "Blocked"
        });
        await _db.SaveChangesAsync(ct);
    }

    private static RefreshSession CreateSession(
        Guid userId, Guid familyId, RefreshTokenResult refresh, string? ipAddress) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        FamilyId = familyId,
        TokenJtiHash = HashJti(refresh.Jti),
        ExpiresAt = refresh.ExpiresAt,
        CreatedByIp = ipAddress,
        CreatedAt = DateTime.UtcNow
    };

    private async Task<IDbContextTransaction?> BeginTransactionIfSupportedAsync(CancellationToken ct) =>
        _db.Database.IsRelational() ? await _db.Database.BeginTransactionAsync(ct) : null;

    private string? CurrentIpAddress() =>
        _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    private static string HashJti(string jti) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(jti))).ToLowerInvariant();

    private static bool StampMatches(string? actual, string expected)
    {
        if (string.IsNullOrWhiteSpace(actual)) return false;
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(actual), Encoding.UTF8.GetBytes(expected));
    }

    private static DomainException InvalidRefresh() =>
        new("Invalid or expired refresh token.");
}
