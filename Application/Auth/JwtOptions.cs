namespace AssetMgmt.Application.Auth;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "AssetMgmt";
    public string Audience { get; set; } = "AssetMgmtClient";
    public int AccessTokenMinutes { get; set; } = 10;
    public int RefreshTokenDays { get; set; } = 7;

    // Bound from environment (JWT_SECRET), not appsettings.
    public string Secret { get; set; } = null!;
}
