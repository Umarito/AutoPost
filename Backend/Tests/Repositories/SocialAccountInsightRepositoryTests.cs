using Infrastructure.Repositories;
using Domain.Entities;
using Domain.Enums;
using Xunit;

namespace Tests.Repositories;

/// <summary>
/// Tests for the <see cref="SocialAccountInsightRepository"/>.
/// </summary>
public class SocialAccountInsightRepositoryTests
{
    /// <summary>
    /// Validates AddAsync correctly saves.
    ///
    /// <list type="bullet">
    /// <item><b>Arrange:</b> Un-tracked Insight.</item>
    /// <item><b>Act:</b> AddAsync and SaveChanges.</item>
    /// <item><b>Assert:</b> Exists in database.</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task AddAsync_PersistsNewInsight()
    {
        using var ctx = TestAppDbContextFactory.CreateContext("InsightRepo_Add");
        var repo = new SocialAccountInsightRepository(ctx);
        
        var insight = new SocialAccountInsight();
        ctx.Entry(insight).Property("Id").CurrentValue = Guid.NewGuid();

        await repo.AddAsync(insight);
        await ctx.SaveChangesAsync();

        Assert.NotNull(await ctx.SocialAccountInsights.FindAsync(insight.Id));
    }
}
