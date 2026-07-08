using Microsoft.EntityFrameworkCore;

namespace AssetMgmt.Infrastructure.Persistence;

public static class DbExecutionExtensions
{
    public static Task<T> ExecuteWithRetryStrategyAsync<T>(
        this DbContext db,
        Func<Task<T>> operation)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(operation);
    }

    public static Task ExecuteWithRetryStrategyAsync(
        this DbContext db,
        Func<Task> operation)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(operation);
    }
}
