using AssetMgmt.Application.Auth;
using AssetMgmt.Application.Common;
using AssetMgmt.Application.Handover;
using AssetMgmt.Application.Requests;
using AssetMgmt.Domain.Entities;
using AssetMgmt.Domain.Enums;
using AssetMgmt.Domain.Exceptions;
using AssetMgmt.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AssetMgmt.Tests;

public class AllocationRequestAuthorizationTests
{
    [Fact]
    public async Task EmployeeCannotReadAnotherEmployeesRequest()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var owner = User("owner");
        var attacker = User("attacker");
        var model = new AssetModel
        {
            Id = Guid.NewGuid(), Name = "Laptop", Category = AssetCategory.Laptop
        };
        var asset = new AssetInstance
        {
            Id = Guid.NewGuid(), AssetCode = "IT-LAP-1", Serial = "S1", Model = model
        };
        var request = new AllocationRequest
        {
            Id = Guid.NewGuid(), Requester = owner, RequesterId = owner.Id,
            AssetInstance = asset, AssetInstanceId = asset.Id,
            IdempotencyKey = "owner-key", HandoverDueAt = DateTime.UtcNow.AddDays(1)
        };
        db.AddRange(owner, attacker, model, asset, request);
        await db.SaveChangesAsync();
        var current = new TestCurrentUser(attacker.Id);
        var service = new AllocationRequestService(
            db, current, new NoopHandoverService(), new DataScopeService(db, current));

        await Assert.ThrowsAsync<DomainException>(() => service.GetByIdAsync(request.Id, default));
    }

    private static User User(string name) => new()
    {
        Id = Guid.NewGuid(), UserName = name, NormalizedUserName = name.ToUpperInvariant(),
        Email = $"{name}@example.test", NormalizedEmail = $"{name}@example.test".ToUpperInvariant(),
        PasswordHash = "x", SecurityStamp = Guid.NewGuid().ToString("N"),
        EmployeeCode = name, FullName = name, IsActive = true
    };

    private sealed class TestCurrentUser(Guid id) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid? Id => id;
        public string? UserName => "employee";
        public string? Role => "Employee";
    }

    private sealed class NoopHandoverService : IHandoverDocumentService
    {
        public Task<HandoverResult> GenerateForAllocationAsync(Guid allocationId, Guid generatedBy, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task<HandoverResult?> GetForRequestAsync(Guid requestId, CancellationToken ct) =>
            Task.FromResult<HandoverResult?>(null);
        public Task<HandoverFileResult?> GetFileForRequestAsync(Guid requestId, CancellationToken ct) =>
            Task.FromResult<HandoverFileResult?>(null);
        public Task<HandoverFileResult?> GetFileForAllocationAsync(Guid allocationId, CancellationToken ct) =>
            Task.FromResult<HandoverFileResult?>(null);
        public Task MigrateLegacyFilesAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
