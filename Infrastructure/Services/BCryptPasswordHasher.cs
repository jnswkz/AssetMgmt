using AssetMgmt.Application.Auth;

namespace AssetMgmt.Infrastructure.Services;

public class BCryptPasswordHasher : IPasswordHasher
{
    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);

    public bool Verify(string password, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch
        {
            // Placeholder/invalid hashes (e.g. unseeded users) should fail closed, not throw.
            return false;
        }
    }
}
