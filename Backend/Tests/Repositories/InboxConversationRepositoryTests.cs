using Infrastructure.Repositories;
using Domain.Entities;
using Domain.Enums;
using Xunit;

namespace Tests.Repositories;

/// <summary>
/// Tests for the <see cref="InboxConversationRepository"/>.
/// </summary>
public class InboxConversationRepositoryTests
{
    /// <summary>
    /// Validates GetByIdWithMessagesAsync eager loads messages.
    ///
    /// <list type="bullet">
    /// <item><b>Arrange:</b> Seeds a conversation and a message.</item>
    /// <item><b>Act:</b> Calls GetByIdWithMessagesAsync.</item>
    /// <item><b>Assert:</b> Verifies conversation and its Messages collection are loaded.</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task GetByIdWithMessagesAsync_ReturnsConversationWithMessages()
    {
        using var ctx = TestAppDbContextFactory.CreateContext("InboxRepo_GetWithMsgs");
        var ws = await TestEntityFactory.SeedWorkspaceAsync(ctx);
        var acc = await TestEntityFactory.SeedSocialAccountAsync(ctx, ws.Id);
        var conv = await TestEntityFactory.SeedInboxConversationAsync(ctx, ws.Id, acc.Id);
        await TestEntityFactory.SeedInboxMessageAsync(ctx, conv.Id);

        var repo = new InboxConversationRepository(ctx);
        var result = await repo.GetByIdWithMessagesAsync(conv.Id);

        Assert.NotNull(result);
        Assert.NotEmpty(result.Messages);
    }

    /// <summary>
    /// Validates GetByExternalIdAsync matches the right conversation.
    ///
    /// <list type="bullet">
    /// <item><b>Arrange:</b> Seeds an inbox conversation with an external ID.</item>
    /// <item><b>Act:</b> Queries by that external ID.</item>
    /// <item><b>Assert:</b> Asserts it finds the correct conversation.</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task GetByExternalIdAsync_ReturnsCorrectConversation()
    {
        using var ctx = TestAppDbContextFactory.CreateContext("InboxRepo_GetExtId");
        var ws = await TestEntityFactory.SeedWorkspaceAsync(ctx);
        var acc = await TestEntityFactory.SeedSocialAccountAsync(ctx, ws.Id);
        var conv = await TestEntityFactory.SeedInboxConversationAsync(ctx, ws.Id, acc.Id, externalId: "match-me");

        var repo = new InboxConversationRepository(ctx);
        var result = await repo.GetByExternalIdAsync(acc.Id, "match-me");

        Assert.NotNull(result);
        Assert.Equal(conv.Id, result.Id);
    }

    /// <summary>
    /// Validates AddAsync correctly saves.
    ///
    /// <list type="bullet">
    /// <item><b>Arrange:</b> Un-tracked Conversation.</item>
    /// <item><b>Act:</b> AddAsync and SaveChanges.</item>
    /// <item><b>Assert:</b> Exists in database.</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task AddAsync_PersistsNewConversation()
    {
        using var ctx = TestAppDbContextFactory.CreateContext("InboxRepo_Add");
        var repo = new InboxConversationRepository(ctx);
        
        var conv = new InboxConversation();
        ctx.Entry(conv).Property("Id").CurrentValue = Guid.NewGuid();
        ctx.Entry(conv).Property("WorkspaceId").CurrentValue = Guid.NewGuid();
        ctx.Entry(conv).Property("ExternalConversationId").CurrentValue = "ext-conv-1";
        ctx.Entry(conv).Property("ExternalUserId").CurrentValue = "ext-user-1";

        await repo.AddAsync(conv);
        await ctx.SaveChangesAsync();

        Assert.NotNull(await ctx.InboxConversations.FindAsync(conv.Id));
    }
}
