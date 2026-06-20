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
            new(TokenTypeClaim, AccessType)
        };
        return (Write(claims, expires), expires);
    }

    public string CreateRefreshToken(User user)
    {
        var expires = DateTime.UtcNow.AddDays(_opts.RefreshTokenDays);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            // Bind the refresh token to the user's current security stamp so it can be
            // invalidated by rotating the stamp (e.g. on password change).
            new("stamp", user.SecurityStamp ?? string.Empty),
            new(TokenTypeClaim, RefreshType)
        };
        return Write(claims, expires);
    }

    public Guid? ValidateRefreshToken(string refreshToken)
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
            return Guid.TryParse(sub, out var id) ? id : null;
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
