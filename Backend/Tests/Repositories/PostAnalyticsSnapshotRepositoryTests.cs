using Infrastructure.Repositories;
using Domain.Entities;
using Domain.Enums;
using Xunit;

namespace Tests.Repositories;

/// <summary>
/// Tests for the <see cref="PostAnalyticsSnapshotRepository"/>.
/// </summary>
public class PostAnalyticsSnapshotRepositoryTests
{
    /// <summary>
    /// Validates AddAsync correctly saves.
    ///
    /// <list type="bullet">
    /// <item><b>Arrange:</b> Un-tracked Snapshot.</item>
    /// <item><b>Act:</b> AddAsync and SaveChanges.</item>
    /// <item><b>Assert:</b> Exists in database.</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task AddAsync_PersistsNewSnapshot()
    {
        using var ctx = TestAppDbContextFactory.CreateContext("AnalyticsRepo_Add");
        var repo = new PostAnalyticsSnapshotRepository(ctx);
        
        var snapshot = new PostAnalyticsSnapshot();
        ctx.Entry(snapshot).Property("Id").CurrentValue = Guid.NewGuid();

        await repo.AddAsync(snapshot);
        await ctx.SaveChangesAsync();

        Assert.NotNull(await ctx.PostAnalyticsSnapshots.FindAsync(snapshot.Id));
    }
}
