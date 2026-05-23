using Infrastructure.Repositories;
using Domain.Entities;
using Domain.Enums;
using Xunit;

namespace Tests.Repositories;

/// <summary>
/// Tests for the <see cref="SocialAccountRepository"/>.
/// </summary>
public class SocialAccountRepositoryTests
{
    /// <summary>
    /// Validates GetByIdAsync returns the social account.
    ///
    /// <list type="bullet">
    /// <item><b>Arrange:</b> Seeds a workspace and social account.</item>
    /// <item><b>Act:</b> Calls GetByIdAsync.</item>
    /// <item><b>Assert:</b> Verifies the account is returned successfully.</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task GetByIdAsync_ReturnsSocialAccount()
    {
        using var ctx = TestAppDbContextFactory.CreateContext("SocialRepo_GetById");
        var ws = await TestEntityFactory.SeedWorkspaceAsync(ctx);
        var account = await TestEntityFactory.SeedSocialAccountAsync(ctx, ws.Id, platform: Platform.Facebook);
        var repo = new SocialAccountRepository(ctx);

        var result = await repo.GetByIdAsync(account.Id);

        Assert.NotNull(result);
        Assert.Equal(Platform.Facebook, result.Platform);
    }

    /// <summary>
    /// Validates GetByExternalIdAsync finds accounts matching external ID and workspace.
    ///
    /// <list type="bullet">
    /// <item><b>Arrange:</b> Seeds two accounts in different workspaces but same external ID.</item>
    /// <item><b>Act:</b> Queries by workspace 1 and external ID.</item>
    /// <item><b>Assert:</b> Verifies only the account in workspace 1 is returned.</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task GetByExternalIdAsync_ReturnsAccountForCorrectWorkspace()
    {
        using var ctx = TestAppDbContextFactory.CreateContext("SocialRepo_GetExtId");
        var w1 = await TestEntityFactory.SeedWorkspaceAsync(ctx, slug: "w1");
        var w2 = await TestEntityFactory.SeedWorkspaceAsync(ctx, slug: "w2");

        var a1 = await TestEntityFactory.SeedSocialAccountAsync(ctx, w1.Id, externalId: "ext-same");
        await TestEntityFactory.SeedSocialAccountAsync(ctx, w2.Id, externalId: "ext-same");

        var repo = new SocialAccountRepository(ctx);
        var result = await repo.GetByExternalIdAsync(w1.Id, Platform.Instagram, "ext-same");

        Assert.NotNull(result);
        Assert.Equal(a1.Id, result.Id);
    }

    /// <summary>
    /// Validates AddAsync persists a social account.
    ///
    /// <list type="bullet">
    /// <item><b>Arrange:</b> Prepares a SocialAccount instance.</item>
    /// <item><b>Act:</b> Calls AddAsync and saves.</item>
    /// <item><b>Assert:</b> Checks persistence in DB.</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task AddAsync_PersistsNewSocialAccount()
    {
        using var ctx = TestAppDbContextFactory.CreateContext("SocialRepo_Add");
        var ws = await TestEntityFactory.SeedWorkspaceAsync(ctx);
        var repo = new SocialAccountRepository(ctx);
        
        var account = new SocialAccount();
        ctx.Entry(account).Property("Id").CurrentValue = Guid.NewGuid();
        ctx.Entry(account).Property("WorkspaceId").CurrentValue = ws.Id;
        ctx.Entry(account).Property("Platform").CurrentValue = Platform.Facebook;
        ctx.Entry(account).Property("ExternalAccountId").CurrentValue = "ext-fb";
        ctx.Entry(account).Property("AccountDisplayName").CurrentValue = "Test FB";
        ctx.Entry(account).Property("EncryptedAccessToken").CurrentValue = "token";
        ctx.Entry(account).Property("GrantedScopes").CurrentValue = "read";

        await repo.AddAsync(account);
        await ctx.SaveChangesAsync();

        Assert.NotNull(await ctx.SocialAccounts.FindAsync(account.Id));
    }

    /// <summary>
    /// Validates Remove properly deletes the entity.
    ///
    /// <list type="bullet">
    /// <item><b>Arrange:</b> Seeds a social account.</item>
    /// <item><b>Act:</b> Calls Remove and saves.</item>
    /// <item><b>Assert:</b> Asserts it's deleted from DB.</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task Remove_DeletesFromDatabase()
    {
        using var ctx = TestAppDbContextFactory.CreateContext("SocialRepo_Remove");
        var ws = await TestEntityFactory.SeedWorkspaceAsync(ctx);
        var account = await TestEntityFactory.SeedSocialAccountAsync(ctx, ws.Id);
        
        var repo = new SocialAccountRepository(ctx);
        repo.Remove(account);
        await ctx.SaveChangesAsync();

        Assert.Null(await ctx.SocialAccounts.FindAsync(account.Id));
    }
}
