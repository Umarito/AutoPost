using Infrastructure.Repositories;
using Domain.Entities;
using Domain.Enums;
using Xunit;

namespace Tests.Repositories;

/// <summary>
/// Tests for the <see cref="NotificationPreferenceRepository"/>.
/// </summary>
public class NotificationPreferenceRepositoryTests
{
    /// <summary>
    /// Validates GetByUserWorkspaceAndEventAsync returns the specific preference.
    ///
    /// <list type="bullet">
    /// <item><b>Arrange:</b> Seeds a preference.</item>
    /// <item><b>Act:</b> Queries by user, workspace, and event type.</item>
    /// <item><b>Assert:</b> Asserts the preference is returned correctly.</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task GetByUserWorkspaceAndEventAsync_ReturnsPreference()
    {
        using var ctx = TestAppDbContextFactory.CreateContext("NotifRepo_GetByEvent");
        
        var pref = new NotificationPreference();
        ctx.Entry(pref).Property("Id").CurrentValue = Guid.NewGuid();
        ctx.Entry(pref).Property("UserId").CurrentValue = Guid.NewGuid();
        ctx.Entry(pref).Property("WorkspaceId").CurrentValue = Guid.NewGuid();
        ctx.Entry(pref).Property("EventType").CurrentValue = NotificationEventType.PostFailed;
        
        ctx.NotificationPreferences.Add(pref);
        await ctx.SaveChangesAsync();

        var repo = new NotificationPreferenceRepository(ctx);
        var result = await repo.GetByUserWorkspaceAndEventAsync(pref.UserId, pref.WorkspaceId, NotificationEventType.PostFailed);

        Assert.NotNull(result);
        Assert.Equal(pref.Id, result.Id);
    }

    /// <summary>
    /// Validates AddAsync correctly saves.
    ///
    /// <list type="bullet">
    /// <item><b>Arrange:</b> Un-tracked NotificationPreference.</item>
    /// <item><b>Act:</b> AddAsync and SaveChanges.</item>
    /// <item><b>Assert:</b> Exists in database.</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task AddAsync_PersistsNewPreference()
    {
        using var ctx = TestAppDbContextFactory.CreateContext("NotifRepo_Add");
        var repo = new NotificationPreferenceRepository(ctx);
        
        var pref = new NotificationPreference();
        ctx.Entry(pref).Property("Id").CurrentValue = Guid.NewGuid();

        await repo.AddAsync(pref);
        await ctx.SaveChangesAsync();

        Assert.NotNull(await ctx.NotificationPreferences.FindAsync(pref.Id));
    }
}
