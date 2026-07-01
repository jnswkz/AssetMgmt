using AssetMgmt.Application.Auth;
using Microsoft.EntityFrameworkCore;

namespace AssetMgmt.Infrastructure.Persistence;

/// <summary>
/// Startup seeding that the DB-first init script defers to the app. The SQL
/// seed inserts users with a literal placeholder password hash
/// (<see cref="PlaceholderHash"/>); this replaces those with a real BCrypt hash
/// for a known default password so the demo accounts can actually log in.
///
/// Idempotent: only rows still holding the placeholder are touched, so an
/// account whose password was later changed is never overwritten.
/// </summary>
public class DbSeeder
{
    public const string PlaceholderHash = "PLACEHOLDER_HASH_WILL_BE_REPLACED_BY_APP";
    private const string DefaultPassword = "Password123!";

    private readonly AppDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly IConfiguration _config;
    private readonly ILogger<DbSeeder> _logger;

    public DbSeeder(
        AppDbContext db, IPasswordHasher hasher, IConfiguration config, ILogger<DbSeeder> logger)
    {
        _db = db;
        _hasher = hasher;
        _config = config;
        _logger = logger;
    }

    public async Task SeedPasswordsAsync(CancellationToken ct = default)
    {
        var pending = await _db.Users
            .Where(u => u.PasswordHash == PlaceholderHash)
            .ToListAsync(ct);

        if (pending.Count == 0)
            return;

        var password = _config["SEED_DEFAULT_PASSWORD"] ?? DefaultPassword;
        var hash = _hasher.Hash(password);

        foreach (var user in pending)
            user.PasswordHash = hash;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "DbSeeder: activated {Count} seeded account(s) with the default password. Users: {Users}",
            pending.Count, string.Join(", ", pending.Select(u => u.UserName)));
    }
}
