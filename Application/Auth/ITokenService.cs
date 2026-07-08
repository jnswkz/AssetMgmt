using AssetMgmt.Domain.Entities;

namespace AssetMgmt.Application.Auth;

public interface ITokenService
{
    (string token, DateTime expiresAt) CreateAccessToken(User user);
    RefreshTokenResult CreateRefreshToken(User user);

    ValidatedRefreshToken? ValidateRefreshToken(string refreshToken);
}

public sealed record RefreshTokenResult(string Token, string Jti, DateTime ExpiresAt);
public sealed record ValidatedRefreshToken(Guid UserId, string Jti, string SecurityStamp, DateTime ExpiresAt);
