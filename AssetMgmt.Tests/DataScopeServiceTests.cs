using AssetMgmt.Application.Auth;
using AssetMgmt.Application.Common;
using AssetMgmt.Domain.Entities;
using AssetMgmt.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AssetMgmt.Tests;

public class DataScopeServiceTests
{
    [Fact]
    public async Task ManagerScope_ContainsOwnAndManagedDepartmentsOnly()
    {
        await using var db = CreateDb();
        var managerId = Guid.NewGuid();
        var ownId = Guid.NewGuid();
        var managedId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        db.Departments.AddRange(
            Department(ownId, "OWN"), Department(managedId, "MNG", managerId), Department(otherId, "OTH"));
        db.Users.Add(User(managerId, ownId));
        await db.SaveChangesAsync();

        var scope = new DataScopeService(db, new TestCurrentUser(managerId, "Manager"));
        var ids = await scope.GetDepartmentIdsAsync(default);

        Assert.Contains(ownId, ids);
        Assert.Contains(managedId, ids);
        Assert.DoesNotContain(otherId, ids);
    }

    [Fact]
    public async Task AdminScope_IsUnrestricted()
    {
        await using var db = CreateDb();
        var scope = new DataScopeService(db, new TestCurrentUser(Guid.NewGuid(), "AdminIT"));
        Assert.Empty(await scope.GetDepartmentIdsAsync(default));
    }

    private static AppDbContext CreateDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static Department Department(Guid id, string code, Guid? managerId = null) => new()
    {
        Id = id, Code = code, Name = code, ManagerId = managerId, IsActive = true
    };

    private static User User(Guid id, Guid departmentId) => new()
    {
        Id = id, UserName = "manager", NormalizedUserName = "MANAGER", Email = "m@example.test",
        NormalizedEmail = "M@EXAMPLE.TEST", PasswordHash = "x", EmployeeCode = "M001",
        FullName = "Manager", DepartmentId = departmentId
    };

    private sealed class TestCurrentUser(Guid id, string role) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid? Id => id;
        public string? UserName => "test";
        public string? Role => role;
    }
}
