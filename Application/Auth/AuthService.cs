using AssetMgmt.Domain.Exceptions;
using AssetMgmt.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AssetMgmt.Application.Auth;

public class AuthService
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly ITokenService _tokens;

    public AuthService(AppDbContext db, IPasswordHasher hasher, ITokenService tokens)
    {
        _db = db;
        _hasher = hasher;
        _tokens = tokens;
    }

    public async Task<TokenResponse> LoginAsync(LoginRequest req, CancellationToken ct)
    {
        var normalized = req.UserName.Trim().ToUpperInvariant();
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.NormalizedUserName == normalized, ct);

        if (user is null || !user.IsActive || !_hasher.Verify(req.Password, user.PasswordHash))
            throw new DomainException("Invalid username or password.");

        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return BuildTokens(user);
    }

    public async Task<TokenResponse> RefreshAsync(RefreshRequest req, CancellationToken ct)
    {
        var userId = _tokens.ValidateRefreshToken(req.RefreshToken)
            ?? throw new DomainException("Invalid or expired refresh token.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null || !user.IsActive)
            throw new DomainException("Invalid or expired refresh token.");

        return BuildTokens(user);
    }

    public async Task<MeResponse> GetMeAsync(Guid userId, CancellationToken ct)
    {
        var user = await _db.Users
            .Include(u => u.Department)
            .FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new DomainException("User not found.");

        return new MeResponse(
            user.Id, user.UserName, user.Email, user.FullName, user.EmployeeCode,
            user.Role.ToString(), user.DepartmentId, user.Department?.Name);
    }

    private TokenResponse BuildTokens(Domain.Entities.User user)
    {
        var (access, expires) = _tokens.CreateAccessToken(user);
        var refresh = _tokens.CreateRefreshToken(user);
        return new TokenResponse(access, refresh, expires);
    }
}
