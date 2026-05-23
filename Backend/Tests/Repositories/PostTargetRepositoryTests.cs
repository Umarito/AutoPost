using Infrastructure.Repositories;
using Domain.Entities;
using Domain.Enums;
using Xunit;

namespace Tests.Repositories;

/// <summary>
/// Tests for the <see cref="PostTargetRepository"/>.
/// </summary>
public class PostTargetRepositoryTests
{
    /// <summary>
    /// Validates GetByIdAsync returns the post target.
    ///
    /// <list type="bullet">
    /// <item><b>Arrange:</b> Seeds a workspace, account, post, and target.</item>
    /// <item><b>Act:</b> Calls GetByIdAsync.</item>
    /// <item><b>Assert:</b> Asserts the target is returned.</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task GetByIdAsync_ReturnsTarget()
    {
        using var ctx = TestAppDbContextFactory.CreateContext("TargetRepo_GetById");
        var ws = await TestEntityFactory.SeedWorkspaceAsync(ctx);
        var u = await TestEntityFactory.SeedUserAsync(ctx);
        var acc = await TestEntityFactory.SeedSocialAccountAsync(ctx, ws.Id);
        var post = await TestEntityFactory.SeedPostAsync(ctx, ws.Id, u.Id);
        var target = await TestEntityFactory.SeedPostTargetAsync(ctx, post.Id, acc.Id);

        var repo = new PostTargetRepository(ctx);
        var result = await repo.GetByIdAsync(target.Id);

        Assert.NotNull(result);
        Assert.Equal(target.Id, result.Id);
    }

    /// <summary>
    /// Validates AddAsync correctly saves.
    ///
    /// <list type="bullet">
    /// <item><b>Arrange:</b> Un-tracked Target.</item>
    /// <item><b>Act:</b> AddAsync and SaveChanges.</item>
    /// <item><b>Assert:</b> Exists in database.</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task AddAsync_PersistsNewTarget()
    {
        using var ctx = TestAppDbContextFactory.CreateContext("TargetRepo_Add");
        var repo = new PostTargetRepository(ctx);
        
        var target = new PostTarget();
        ctx.Entry(target).Property("Id").CurrentValue = Guid.NewGuid();
        ctx.Entry(target).Property("PostId").CurrentValue = Guid.NewGuid();

        await repo.AddAsync(target);
        await ctx.SaveChangesAsync();

        Assert.NotNull(await ctx.PostTargets.FindAsync(target.Id));
    }
}
