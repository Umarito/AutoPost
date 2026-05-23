using Infrastructure.Repositories;
using Domain.Entities;
using Domain.Enums;
using Xunit;

namespace Tests.Repositories;

/// <summary>
/// Tests for the <see cref="InboxMessageRepository"/>.
/// </summary>
public class InboxMessageRepositoryTests
{
    /// <summary>
    /// Validates AddAsync correctly saves.
    ///
    /// <list type="bullet">
    /// <item><b>Arrange:</b> Un-tracked Message.</item>
    /// <item><b>Act:</b> AddAsync and SaveChanges.</item>
    /// <item><b>Assert:</b> Exists in database.</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task AddAsync_PersistsNewMessage()
    {
        using var ctx = TestAppDbContextFactory.CreateContext("InboxMsgRepo_Add");
        var repo = new InboxMessageRepository(ctx);
        
        var msg = new InboxMessage();
        ctx.Entry(msg).Property("Id").CurrentValue = Guid.NewGuid();
        ctx.Entry(msg).Property("ExternalMessageId").CurrentValue = "ext-msg-123";

        await repo.AddAsync(msg);
        await ctx.SaveChangesAsync();

        Assert.NotNull(await ctx.InboxMessages.FindAsync(msg.Id));
    }
}
