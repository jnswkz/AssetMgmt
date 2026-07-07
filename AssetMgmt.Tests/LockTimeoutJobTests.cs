using AssetMgmt.Domain.Entities;
using AssetMgmt.Domain.Enums;
using AssetMgmt.Infrastructure.Jobs;
using AssetMgmt.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

namespace AssetMgmt.Tests;

public class LockTimeoutJobTests
{
    [Fact]
    public async Task ExpiredRequest_ReleasesOnlyItsOwnAssetLock()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
        var user = User();
        var model = new AssetModel { Id = Guid.NewGuid(), Name = "Laptop", Category = AssetCategory.Laptop };
        var token = Guid.NewGuid().ToString("N");
        var asset = new AssetInstance
        {
            Id = Guid.NewGuid(), AssetCode = "IT-LAP-0001", Serial = "S1", Model = model,
            Status = AssetStatus.LockedTemp, CurrentHolderId = user.Id, LockHolderUserId = user.Id,
            LockToken = token, LockExpiresAt = DateTime.UtcNow.AddMinutes(-1)
        };
        db.AddRange(user, model, asset);
        db.AllocationRequests.Add(new AllocationRequest
        {
            Id = Guid.NewGuid(), RequesterId = user.Id, AssetInstanceId = asset.Id,
            IdempotencyKey = "request-1", Status = RequestStatus.Pending, LockToken = token,
            LockExpiresAt = DateTime.UtcNow.AddMinutes(-1), HandoverDueAt = DateTime.UtcNow.AddMinutes(-1)
        });
        await db.SaveChangesAsync();

        await new LockTimeoutJob(db, NullLogger<LockTimeoutJob>.Instance).RunAsync();

        Assert.Equal(AssetStatus.InStock, asset.Status);
        Assert.Null(asset.CurrentHolderId);
        Assert.Null(asset.LockToken);
        Assert.Equal(RequestStatus.Expired, (await db.AllocationRequests.SingleAsync()).Status);
    }

    private static User User() => new()
    {
        Id = Guid.NewGuid(), UserName = "employee", NormalizedUserName = "EMPLOYEE",
        Email = "e@example.test", NormalizedEmail = "E@EXAMPLE.TEST", PasswordHash = "x",
        EmployeeCode = "E001", FullName = "Employee"
    };
}
