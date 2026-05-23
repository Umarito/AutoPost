using Infrastructure.Repositories;
using Domain.Entities;
using Domain.Enums;
using Xunit;

namespace Tests.Repositories;

/// <summary>
/// Tests for the <see cref="PostRepository"/>.
/// </summary>
public class PostRepositoryTests
{
    /// <summary>
    /// Validates GetByIdWithTargetsAsync eagerly loads targets and their nested accounts.
    ///
    /// <list type="bullet">
    /// <item><b>Arrange:</b> Seeds a Workspace, Account, Post, and PostTarget.</item>
    /// <item><b>Act:</b> Calls GetByIdWithTargetsAsync.</item>
    /// <item><b>Assert:</b> Asserts the targets and nested social accounts are not null.</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task GetByIdWithTargetsAsync_EagerLoadsTargetsAndAccounts()
    {
        using var ctx = TestAppDbContextFactory.CreateContext("PostRepo_GetWithTargets");
        var ws = await TestEntityFactory.SeedWorkspaceAsync(ctx);
        var u = await TestEntityFactory.SeedUserAsync(ctx);
        var acc = await TestEntityFactory.SeedSocialAccountAsync(ctx, ws.Id);
        var post = await TestEntityFactory.SeedPostAsync(ctx, ws.Id, u.Id);
        await TestEntityFactory.SeedPostTargetAsync(ctx, post.Id, acc.Id);

        var repo = new PostRepository(ctx);
        var result = await repo.GetByIdWithTargetsAsync(post.Id);

        Assert.NotNull(result);
        Assert.NotEmpty(result.Targets);
        Assert.All(result.Targets, t => Assert.NotNull(t.SocialAccount));
    }

    /// <summary>
    /// Validates GetByWorkspaceIdAsync filters correctly by parameters.
    ///
    /// <list type="bullet">
    /// <item><b>Arrange:</b> Seeds multiple posts with different statuses and platforms.</item>
    /// <item><b>Act:</b> Queries by a specific status.</item>
    /// <item><b>Assert:</b> Asserts only the matching posts are returned.</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task GetByWorkspaceIdAsync_FiltersCorrectly()
    {
        using var ctx = TestAppDbContextFactory.CreateContext("PostRepo_Filters");
        var ws = await TestEntityFactory.SeedWorkspaceAsync(ctx);
        var u = await TestEntityFactory.SeedUserAsync(ctx);
        
        await TestEntityFactory.SeedPostAsync(ctx, ws.Id, u.Id, status: PostStatus.Draft);
        await TestEntityFactory.SeedPostAsync(ctx, ws.Id, u.Id, status: PostStatus.Scheduled);

        var repo = new PostRepository(ctx);
        
        // Filter by Scheduled
        var scheduledPosts = await repo.GetByWorkspaceIdAsync(ws.Id, status: PostStatus.Scheduled);
        
        Assert.Single(scheduledPosts);
        Assert.Equal(PostStatus.Scheduled, scheduledPosts[0].Status);
    }

    /// <summary>
    /// Validates GetScheduledPostsDueAsync only picks up due posts.
    ///
    /// <list type="bullet">
    /// <item><b>Arrange:</b> Seeds an overdue scheduled post, a future scheduled post, and a draft.</item>
    /// <item><b>Act:</b> Calls GetScheduledPostsDueAsync with current time.</item>
    /// <item><b>Assert:</b> Asserts only the overdue scheduled post is returned.</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task GetScheduledPostsDueAsync_ReturnsOnlyOverdueScheduledPosts()
    {
        using var ctx = TestAppDbContextFactory.CreateContext("PostRepo_ScheduledDue");
        var ws = await TestEntityFactory.SeedWorkspaceAsync(ctx);
        var u = await TestEntityFactory.SeedUserAsync(ctx);
        
        var now = DateTime.UtcNow;

        var overdue = await TestEntityFactory.SeedPostAsync(ctx, ws.Id, u.Id, status: PostStatus.Scheduled);
        ctx.Entry(overdue).Reference("Schedule").TargetEntry!.Property("ScheduledAt").CurrentValue = now.AddHours(-1);
        
        var future = await TestEntityFactory.SeedPostAsync(ctx, ws.Id, u.Id, status: PostStatus.Scheduled);
        ctx.Entry(future).Reference("Schedule").TargetEntry!.Property("ScheduledAt").CurrentValue = now.AddHours(1);

        var draft = await TestEntityFactory.SeedPostAsync(ctx, ws.Id, u.Id, status: PostStatus.Draft);
        ctx.Entry(draft).Reference("Schedule").TargetEntry!.Property("ScheduledAt").CurrentValue = now.AddHours(-1);
        await ctx.SaveChangesAsync();

        var repo = new PostRepository(ctx);
        var result = await repo.GetScheduledPostsDueAsync(now);

        Assert.Single(result);
        Assert.Equal(overdue.Id, result[0].Id);
    }

    /// <summary>
    /// Validates AddAsync correctly saves post.
    ///
    /// <list type="bullet">
    /// <item><b>Arrange:</b> Un-tracked post.</item>
    /// <item><b>Act:</b> AddAsync and SaveChanges.</item>
    /// <item><b>Assert:</b> Exists in database.</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task AddAsync_PersistsNewPost()
    {
        using var ctx = TestAppDbContextFactory.CreateContext("PostRepo_Add");
        var repo = new PostRepository(ctx);
        
        var post = new Post();
        ctx.Entry(post).Property("Id").CurrentValue = Guid.NewGuid();
        ctx.Entry(post).Property("WorkspaceId").CurrentValue = Guid.NewGuid();
        ctx.Entry(post).Property("CreatedByUserId").CurrentValue = Guid.NewGuid();

        await repo.AddAsync(post);
        await ctx.SaveChangesAsync();

        Assert.NotNull(await ctx.Posts.FindAsync(post.Id));
    }
}
