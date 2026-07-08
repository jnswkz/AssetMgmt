using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AssetMgmt.Application.Auth;
using AssetMgmt.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AssetMgmt.Infrastructure.Services;

public class JwtTokenService : ITokenService
{
    private const string TokenTypeClaim = "typ";
    private const string AccessType = "access";
    private const string RefreshType = "refresh";
    public const string SecurityStampClaim = "stamp";

    private readonly JwtOptions _opts;
    private readonly SymmetricSecurityKey _key;

    public JwtTokenService(IOptions<JwtOptions> opts)
    {
        _opts = opts.Value;
        _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opts.Secret));
    }

    public (string token, DateTime expiresAt) CreateAccessToken(User user)
    {
        var expires = DateTime.UtcNow.AddMinutes(_opts.AccessTokenMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("employee_code", user.EmployeeCode),
            new("full_name", user.FullName),
            new(SecurityStampClaim, user.SecurityStamp ?? string.Empty),
            new(TokenTypeClaim, AccessType)
        };
        return (Write(claims, expires), expires);
    }

    public RefreshTokenResult CreateRefreshToken(User user)
    {
        var expires = DateTime.UtcNow.AddDays(_opts.RefreshTokenDays);
        var jti = Guid.NewGuid().ToString("N");
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, jti),
            // Bind the refresh token to the user's current security stamp so it can be
            // invalidated by rotating the stamp (e.g. on password change).
            new(SecurityStampClaim, user.SecurityStamp ?? string.Empty),
            new(TokenTypeClaim, RefreshType)
        };
        return new RefreshTokenResult(Write(claims, expires), jti, expires);
    }

    public ValidatedRefreshToken? ValidateRefreshToken(string refreshToken)
    {
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        try
        {
            var principal = handler.ValidateToken(refreshToken, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _opts.Issuer,
                ValidateAudience = true,
                ValidAudience = _opts.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = _key,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            }, out _);

            if (principal.FindFirst(TokenTypeClaim)?.Value != RefreshType)
                return null;

            var sub = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            var jti = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
            var stamp = principal.FindFirst(SecurityStampClaim)?.Value;
            var exp = principal.FindFirst(JwtRegisteredClaimNames.Exp)?.Value;
            if (!Guid.TryParse(sub, out var id) || string.IsNullOrWhiteSpace(jti) ||
                string.IsNullOrWhiteSpace(stamp) || !long.TryParse(exp, out var unixSeconds))
                return null;

            return new ValidatedRefreshToken(
                id, jti, stamp, DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime);
        }
        catch
        {
            return null;
        }
    }

    private string Write(IEnumerable<Claim> claims, DateTime expires)
    {
        var creds = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _opts.Issuer,
            audience: _opts.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
