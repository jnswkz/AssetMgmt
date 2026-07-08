using System.Text.Json;
using AssetMgmt.Application.Agents;
using AssetMgmt.Application.Auth;
using AssetMgmt.Domain.Exceptions;
using AssetMgmt.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AssetMgmt.Tests;

public class AiPendingActionTests
{
    [Fact]
    public async Task MutatingTool_IsNotExecutedUntilExplicitConfirmation_AndConfirmIsIdempotent()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        db.Users.Add(TestData.User(userId));
        await db.SaveChangesAsync();
        var handler = new CountingCreateHandler();
        var service = new AiPendingActionService(
            db, new TestCurrentUser(userId), [handler]);
        var conversationId = Guid.NewGuid();
        var decision = new AiRouteDecision(
            AiIntents.CreateAllocationRequest,
            AiToolNames.CreateAllocationRequest,
            AiJson.ToElement(new CreateAllocationRequestPayload(
                null, "IT-LAP-0001", null, null, null, "Work", 12, null)),
            .99,
            false,
            "model output");

        var staged = await service.StageAsync(userId, conversationId, "create it", decision, default);

        Assert.Equal(0, handler.ExecutionCount);
        Assert.Equal("Pending", staged.Pending.Status);
        Assert.Single(await db.AiPendingActions.ToListAsync());

        var first = await service.ConfirmAsync(staged.Pending.Id, default);
        var second = await service.ConfirmAsync(staged.Pending.Id, default);

        Assert.Equal(1, handler.ExecutionCount);
        Assert.Equal(first.Answer, second.Answer);
    }

    [Fact]
    public async Task PendingAction_IsHiddenFromDifferentUser()
    {
        await using var db = CreateDb();
        var owner = Guid.NewGuid();
        var other = Guid.NewGuid();
        db.Users.AddRange(TestData.User(owner), TestData.User(other));
        await db.SaveChangesAsync();
        var handler = new CountingCreateHandler();
        var ownerService = new AiPendingActionService(db, new TestCurrentUser(owner), [handler]);
        var decision = new AiRouteDecision(
            AiIntents.CreateAllocationRequest,
            AiToolNames.CreateAllocationRequest,
            AiJson.ToElement(new CreateAllocationRequestPayload(null, "A1", null, null, null, null, null, null)),
            1, false, "test");
        var staged = await ownerService.StageAsync(owner, Guid.NewGuid(), "create", decision, default);
        var otherService = new AiPendingActionService(db, new TestCurrentUser(other), [handler]);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            otherService.ConfirmAsync(staged.Pending.Id, default));
        Assert.Equal(0, handler.ExecutionCount);
    }

    private static AppDbContext CreateDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private sealed class TestCurrentUser(Guid id) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid? Id => id;
        public string? UserName => "test";
        public string? Role => "Employee";
    }

    private sealed class CountingCreateHandler : IAiToolHandler
    {
        public int ExecutionCount { get; private set; }
        public string ToolName => AiToolNames.CreateAllocationRequest;

        public Task<AiToolExecutionResult> ExecuteAsync(AiToolExecutionContext context, CancellationToken ct)
        {
            ExecutionCount++;
            return Task.FromResult(new AiToolExecutionResult(
                AiIntents.CreateAllocationRequest,
                ToolName,
                context.Decision.Arguments.Clone(),
                "created",
                [],
                [],
                true,
                false));
        }
    }

    private static class TestData
    {
        public static Domain.Entities.User User(Guid id) => new()
        {
            Id = id,
            UserName = id.ToString("N"),
            NormalizedUserName = id.ToString("N").ToUpperInvariant(),
            Email = $"{id:N}@example.test",
            NormalizedEmail = $"{id:N}@EXAMPLE.TEST".ToUpperInvariant(),
            PasswordHash = "x",
            SecurityStamp = Guid.NewGuid().ToString("N"),
            EmployeeCode = id.ToString("N")[..8],
            FullName = "Test User",
            IsActive = true
        };
    }
}
