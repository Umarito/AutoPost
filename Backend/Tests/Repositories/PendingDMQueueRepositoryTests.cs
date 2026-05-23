using Infrastructure.Repositories;
using Domain.Entities;
using Domain.Enums;
using Xunit;

namespace Tests.Repositories;

/// <summary>
/// Tests for the <see cref="PendingDMQueueRepository"/>.
/// </summary>
public class PendingDMQueueRepositoryTests
{
    /// <summary>
    /// Validates AddAsync correctly saves.
    ///
    /// <list type="bullet">
    /// <item><b>Arrange:</b> Un-tracked PendingDMQueue.</item>
    /// <item><b>Act:</b> AddAsync and SaveChanges.</item>
    /// <item><b>Assert:</b> Exists in database.</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task AddAsync_PersistsNewPendingDM()
    {
        using var ctx = TestAppDbContextFactory.CreateContext("PendingDMRepo_Add");
        var repo = new PendingDMQueueRepository(ctx);
        
        var dm = new PendingDMQueue();
        ctx.Entry(dm).Property("Id").CurrentValue = Guid.NewGuid();
        ctx.Entry(dm).Property("ExternalUserId").CurrentValue = "ext-user";
        ctx.Entry(dm).Property("ResolvedMessageText").CurrentValue = "reply text";

        await repo.AddAsync(dm);
        await ctx.SaveChangesAsync();

        Assert.NotNull(await ctx.PendingDMQueueEntries.FindAsync(dm.Id));
    }
}
