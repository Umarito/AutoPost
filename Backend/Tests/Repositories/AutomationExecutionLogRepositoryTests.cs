using Infrastructure.Repositories;
using Domain.Entities;
using Domain.Enums;
using Xunit;

namespace Tests.Repositories;

/// <summary>
/// Tests for the <see cref="AutomationExecutionLogRepository"/>.
/// </summary>
public class AutomationExecutionLogRepositoryTests
{
    /// <summary>
    /// Validates AddAsync correctly saves.
    ///
    /// <list type="bullet">
    /// <item><b>Arrange:</b> Un-tracked Log.</item>
    /// <item><b>Act:</b> AddAsync and SaveChanges.</item>
    /// <item><b>Assert:</b> Exists in database.</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task AddAsync_PersistsNewLog()
    {
        using var ctx = TestAppDbContextFactory.CreateContext("AutoLogRepo_Add");
        var repo = new AutomationExecutionLogRepository(ctx);
        
        var log = new AutomationExecutionLog();
        ctx.Entry(log).Property("Id").CurrentValue = Guid.NewGuid();
        ctx.Entry(log).Property("ExternalTriggerEventId").CurrentValue = "ext-event-1";
        ctx.Entry(log).Property("TriggerExternalUserId").CurrentValue = "ext-user-1";

        await repo.AddAsync(log);
        await ctx.SaveChangesAsync();

        Assert.NotNull(await ctx.AutomationExecutionLogs.FindAsync(log.Id));
    }
}
