using Infrastructure.Repositories;
using Domain.Entities;
using Domain.Enums;
using Xunit;

namespace Tests.Repositories;

/// <summary>
/// Tests for the <see cref="VideoRepository"/>.
/// </summary>
public class VideoRepositoryTests
{
    /// <summary>
    /// Validates GetByIdAsync returns the video.
    ///
    /// <list type="bullet">
    /// <item><b>Arrange:</b> Seeds a workspace, user, and a video.</item>
    /// <item><b>Act:</b> Calls GetByIdAsync.</item>
    /// <item><b>Assert:</b> Asserts the video is returned.</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task GetByIdAsync_ReturnsVideo()
    {
        using var ctx = TestAppDbContextFactory.CreateContext("VideoRepo_GetById");
        var ws = await TestEntityFactory.SeedWorkspaceAsync(ctx);
        var u = await TestEntityFactory.SeedUserAsync(ctx);
        var video = await TestEntityFactory.SeedVideoAsync(ctx, ws.Id, u.Id);

        var repo = new VideoRepository(ctx);
        var result = await repo.GetByIdAsync(video.Id);

        Assert.NotNull(result);
        Assert.Equal(video.Id, result.Id);
    }

    /// <summary>
    /// Validates AddAsync correctly saves.
    ///
    /// <list type="bullet">
    /// <item><b>Arrange:</b> Un-tracked Video.</item>
    /// <item><b>Act:</b> AddAsync and SaveChanges.</item>
    /// <item><b>Assert:</b> Exists in database.</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task AddAsync_PersistsNewVideo()
    {
        using var ctx = TestAppDbContextFactory.CreateContext("VideoRepo_Add");
        var repo = new VideoRepository(ctx);
        
        var video = new Video();
        ctx.Entry(video).Property("Id").CurrentValue = Guid.NewGuid();
        ctx.Entry(video).Property("WorkspaceId").CurrentValue = Guid.NewGuid();
        ctx.Entry(video).Property("ContentType").CurrentValue = "video/mp4";
        ctx.Entry(video).Property("OriginalFileName").CurrentValue = "test.mp4";
        ctx.Entry(video).Property("StorageKey").CurrentValue = "videos/test.mp4";

        await repo.AddAsync(video);
        await ctx.SaveChangesAsync();

        Assert.NotNull(await ctx.Videos.FindAsync(video.Id));
    }
}
