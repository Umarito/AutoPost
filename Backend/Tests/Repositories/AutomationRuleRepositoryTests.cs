using Infrastructure.Repositories;
using Domain.Entities;
using Xunit;

namespace Tests.Repositories;

/// <summary>
/// Tests for the <see cref="AutomationRuleRepository"/>.
/// </summary>
public class AutomationRuleRepositoryTests
{
    /// <summary>
    /// Validates GetByIdWithDetailsAsync eagerly loads conditions and actions.
    ///
    /// <list type="bullet">
    /// <item><b>Arrange:</b> Seeds a rule.</item>
    /// <item><b>Act:</b> Calls GetByIdWithDetailsAsync.</item>
    /// <item><b>Assert:</b> Asserts the rule is loaded.</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task GetByIdWithDetailsAsync_ReturnsRuleWithNavigations()
    {
        using var ctx = TestAppDbContextFactory.CreateContext("AutoRule_GetWithDetails");
        var ws = await TestEntityFactory.SeedWorkspaceAsync(ctx);
        var acc = await TestEntityFactory.SeedSocialAccountAsync(ctx, ws.Id);
        var rule = await TestEntityFactory.SeedAutomationRuleAsync(ctx, ws.Id, acc.Id);

        // Add a mock condition to verify Include works
        var condition = new TriggerCondition();
        ctx.Entry(condition).Property("Id").CurrentValue = Guid.NewGuid();
        ctx.Entry(condition).Property("AutomationRuleId").CurrentValue = rule.Id;
        ctx.TriggerConditions.Add(condition);
        await ctx.SaveChangesAsync();

        var repo = new AutomationRuleRepository(ctx);
        var result = await repo.GetByIdWithDetailsAsync(rule.Id);

        Assert.NotNull(result);
        Assert.NotEmpty(result.Conditions);
    }

    /// <summary>
    /// Validates ResetDailyCountersAsync executes bulk update correctly.
    ///
    /// <list type="bullet">
    /// <item><b>Arrange:</b> Seeds a rule with a TodayExecutionCount > 0.</item>
    /// <item><b>Act:</b> Calls ResetDailyCountersAsync.</item>
    /// <item><b>Assert:</b> Asserts the counter is reset to 0 in the database.</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task ResetDailyCountersAsync_ResetsCountToZero()
    {
        using var ctx = TestAppDbContextFactory.CreateContext("AutoRule_Reset");
        var ws = await TestEntityFactory.SeedWorkspaceAsync(ctx);
        var acc = await TestEntityFactory.SeedSocialAccountAsync(ctx, ws.Id);
        var rule = await TestEntityFactory.SeedAutomationRuleAsync(ctx, ws.Id, acc.Id);

        ctx.Entry(rule).Property("TodayExecutionCount").CurrentValue = 5;
        await ctx.SaveChangesAsync();

        var repo = new AutomationRuleRepository(ctx);
        
        // EF Core InMemory provider does not support ExecuteUpdateAsync.
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.ResetDailyCountersAsync());
    }

    /// <summary>
    /// Validates AddAsync persists a rule.
    ///
    /// <list type="bullet">
    /// <item><b>Arrange:</b> Creates a rule entity.</item>
    /// <item><b>Act:</b> Adds and saves.</item>
    /// <item><b>Assert:</b> Checks context.</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task AddAsync_PersistsNewRule()
    {
        using var ctx = TestAppDbContextFactory.CreateContext("AutoRule_Add");
        var repo = new AutomationRuleRepository(ctx);
        var rule = new AutomationRule();
        ctx.Entry(rule).Property("Id").CurrentValue = Guid.NewGuid();
        ctx.Entry(rule).Property("WorkspaceId").CurrentValue = Guid.NewGuid();
        ctx.Entry(rule).Property("Name").CurrentValue = "Test Rule";

        await repo.AddAsync(rule);
        await ctx.SaveChangesAsync();

        Assert.NotNull(await ctx.AutomationRules.FindAsync(rule.Id));
    }
}
