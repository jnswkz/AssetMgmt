using AssetMgmt.Domain.Entities;

namespace AssetMgmt.Application.Auth;

public interface ITokenService
{
    (string token, DateTime expiresAt) CreateAccessToken(User user);
    string CreateRefreshToken(User user);

    /// <summary>Validates a refresh token and returns the user id, or null if invalid.</summary>
    Guid? ValidateRefreshToken(string refreshToken);
}
