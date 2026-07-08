using AssetMgmt.Application.Auth;
using AssetMgmt.Domain.Entities;
using AssetMgmt.Domain.Exceptions;
using AssetMgmt.Infrastructure.Persistence;
using AssetMgmt.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AssetMgmt.Tests;

public class AuthSecurityTests
{
    [Fact]
    public async Task RefreshToken_IsRotated_AndReplayRevokesFamilyAndStamp()
    {
        await using var db = CreateDb();
        var user = User();
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var auth = CreateService(db);

        var login = await auth.LoginAsync(new LoginRequest(user.UserName, "correct"), default);
        var refreshed = await auth.RefreshAsync(new RefreshRequest(login.RefreshToken), default);

        Assert.NotEqual(login.RefreshToken, refreshed.RefreshToken);
        Assert.Equal(2, await db.RefreshSessions.CountAsync());
        var stampBeforeReplay = user.SecurityStamp;

        await Assert.ThrowsAsync<DomainException>(() =>
            auth.RefreshAsync(new RefreshRequest(login.RefreshToken), default));

        Assert.NotEqual(stampBeforeReplay, user.SecurityStamp);
        Assert.All(await db.RefreshSessions.ToListAsync(), session => Assert.NotNull(session.RevokedAt));
    }

    [Fact]
    public async Task FiveInvalidPasswords_LockAccountForFifteenMinutes()
    {
        await using var db = CreateDb();
        var user = User();
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var auth = CreateService(db);

        for (var i = 0; i < 5; i++)
            await Assert.ThrowsAsync<DomainException>(() =>
                auth.LoginAsync(new LoginRequest(user.UserName, "wrong"), default));

        Assert.NotNull(user.LockoutEnd);
        Assert.True(user.LockoutEnd > DateTimeOffset.UtcNow.AddMinutes(14));
        await Assert.ThrowsAsync<DomainException>(() =>
            auth.LoginAsync(new LoginRequest(user.UserName, "correct"), default));
    }

    [Fact]
    public async Task SecurityStampChange_InvalidatesRefreshToken()
    {
        await using var db = CreateDb();
        var user = User();
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var auth = CreateService(db);
        var login = await auth.LoginAsync(new LoginRequest(user.UserName, "correct"), default);

        user.SecurityStamp = Guid.NewGuid().ToString("N");
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<DomainException>(() =>
            auth.RefreshAsync(new RefreshRequest(login.RefreshToken), default));
    }

    private static AuthService CreateService(AppDbContext db)
    {
        var tokenService = new JwtTokenService(Options.Create(new JwtOptions
        {
            Issuer = "tests",
            Audience = "tests",
            Secret = new string('s', 64),
            AccessTokenMinutes = 10,
            RefreshTokenDays = 7
        }));
        return new AuthService(db, new PlainPasswordHasher(), tokenService, new HttpContextAccessor());
    }

    private static AppDbContext CreateDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static User User() => new()
    {
        Id = Guid.NewGuid(),
        UserName = "employee",
        NormalizedUserName = "EMPLOYEE",
        Email = "employee@example.test",
        NormalizedEmail = "EMPLOYEE@EXAMPLE.TEST",
        PasswordHash = "correct",
        SecurityStamp = Guid.NewGuid().ToString("N"),
        EmployeeCode = "E001",
        FullName = "Employee",
        IsActive = true,
        LockoutEnabled = true
    };

    private sealed class PlainPasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => password;
        public bool Verify(string password, string hash) => password == hash;
    }
}
