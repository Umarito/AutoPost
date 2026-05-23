using Infrastructure.Repositories;
using Domain.Entities;
using Domain.Enums;
using Xunit;

namespace Tests.Repositories;

/// <summary>
/// Tests for the <see cref="WebhookEventRepository"/>.
/// </summary>
public class WebhookEventRepositoryTests
{
    /// <summary>
    /// Validates AddAsync correctly saves.
    ///
    /// <list type="bullet">
    /// <item><b>Arrange:</b> Un-tracked Event.</item>
    /// <item><b>Act:</b> AddAsync and SaveChanges.</item>
    /// <item><b>Assert:</b> Exists in database.</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task AddAsync_PersistsNewEvent()
    {
        using var ctx = TestAppDbContextFactory.CreateContext("WebhookRepo_Add");
        var repo = new WebhookEventRepository(ctx);
        
        var evt = new WebhookEvent();
        ctx.Entry(evt).Property("Id").CurrentValue = Guid.NewGuid();
        ctx.Entry(evt).Property("IsVerified").CurrentValue = true;
        ctx.Entry(evt).Property("EventType").CurrentValue = "comment.created";
        ctx.Entry(evt).Property("RawPayload").CurrentValue = "{}";

        await repo.AddAsync(evt);
        await ctx.SaveChangesAsync();

        Assert.NotNull(await ctx.WebhookEvents.FindAsync(evt.Id));
    }
}
