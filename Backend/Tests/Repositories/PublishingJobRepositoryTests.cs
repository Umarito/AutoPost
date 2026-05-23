using Infrastructure.Repositories;
using Domain.Entities;
using Domain.Enums;
using Xunit;

namespace Tests.Repositories;

/// <summary>
/// Tests for the <see cref="PublishingJobRepository"/>.
/// </summary>
public class PublishingJobRepositoryTests
{
    /// <summary>
    /// Validates AddAsync correctly saves.
    ///
    /// <list type="bullet">
    /// <item><b>Arrange:</b> Un-tracked Job.</item>
    /// <item><b>Act:</b> AddAsync and SaveChanges.</item>
    /// <item><b>Assert:</b> Exists in database.</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task AddAsync_PersistsNewJob()
    {
        using var ctx = TestAppDbContextFactory.CreateContext("PublishJobRepo_Add");
        var repo = new PublishingJobRepository(ctx);
        
        var job = new PublishingJob();
        ctx.Entry(job).Property("Id").CurrentValue = Guid.NewGuid();

        await repo.AddAsync(job);
        await ctx.SaveChangesAsync();

        Assert.NotNull(await ctx.PublishingJobs.FindAsync(job.Id));
    }
}
