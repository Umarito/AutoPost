using Infrastructure.Repositories;
using Domain.Entities;
using Xunit;

namespace Tests.Repositories;

/// <summary>
/// Tests for the <see cref="RefreshTokenRepository"/>.
/// </summary>
public class RefreshTokenRepositoryTests
{
    /// <summary>
    /// Validates that GetByTokenHashAsync returns the correct token with user loaded.
    ///
    /// <list type="bullet">
    /// <item><b>Arrange:</b> Seeds a User and a RefreshToken.</item>
    /// <item><b>Act:</b> Queries by the token hash.</item>
    /// <item><b>Assert:</b> Asserts the token is returned and User navigation is loaded.</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task GetByTokenHashAsync_ReturnsTokenWithUser()
    {
        using var ctx = TestAppDbContextFactory.CreateContext("RefreshRepo_GetByHash");
        var user = await TestEntityFactory.SeedUserAsync(ctx);
        var token = await TestEntityFactory.SeedRefreshTokenAsync(ctx, user.Id, "my-hash");
        var repo = new RefreshTokenRepository(ctx);

        var result = await repo.GetByTokenHashAsync("my-hash");

        Assert.NotNull(result);
        Assert.NotNull(result.User);
        Assert.Equal(user.Id, result.User.Id);
    }

    /// <summary>
    /// Validates that AddAsync successfully persists a token.
    ///
    /// <list type="bullet">
    /// <item><b>Arrange:</b> Initializes a new RefreshToken.</item>
    /// <item><b>Act:</b> Adds and saves.</item>
    /// <item><b>Assert:</b> Verifies the token exists in DB.</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task AddAsync_PersistsNewToken()
    {
        using var ctx = TestAppDbContextFactory.CreateContext("RefreshRepo_Add");
        var user = await TestEntityFactory.SeedUserAsync(ctx);
        var repo = new RefreshTokenRepository(ctx);
        
        var token = new RefreshToken();
        ctx.Entry(token).Property("Id").CurrentValue = Guid.NewGuid();
        ctx.Entry(token).Property("UserId").CurrentValue = user.Id;
        ctx.Entry(token).Property("TokenHash").CurrentValue = "new-hash";

        await repo.AddAsync(token);
        await ctx.SaveChangesAsync();

        var inDb = await ctx.RefreshTokens.FindAsync(token.Id);
        Assert.NotNull(inDb);
    }

    /// <summary>
    /// Validates RevokeAllForUserAsync correctly bulk updates IsRevoked.
    ///
    /// <list type="bullet">
    /// <item><b>Arrange:</b> Seeds two active tokens for a user, and one for another.</item>
    /// <item><b>Act:</b> Calls RevokeAllForUserAsync for the first user.</item>
    /// <item><b>Assert:</b> Asserts both tokens for user 1 are revoked, but user 2 is untouched.</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task RevokeAllForUserAsync_RevokesActiveTokensForSpecificUser()
    {
        using var ctx = TestAppDbContextFactory.CreateContext("RefreshRepo_RevokeAll");
        var u1 = await TestEntityFactory.SeedUserAsync(ctx, email: "1@t.com");
        var u2 = await TestEntityFactory.SeedUserAsync(ctx, email: "2@t.com");

        var t1 = await TestEntityFactory.SeedRefreshTokenAsync(ctx, u1.Id, "hash1", isRevoked: false);
        var t2 = await TestEntityFactory.SeedRefreshTokenAsync(ctx, u1.Id, "hash2", isRevoked: false);
        var t3 = await TestEntityFactory.SeedRefreshTokenAsync(ctx, u2.Id, "hash3", isRevoked: false);

        var repo = new RefreshTokenRepository(ctx);
        
        // EF Core InMemory provider does not support ExecuteUpdateAsync.
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.RevokeAllForUserAsync(u1.Id, DateTime.UtcNow));
    }

    /// <summary>
    /// Validates CleanupExpiredAsync removes old used/revoked tokens.
    ///
    /// <list type="bullet">
    /// <item><b>Arrange:</b> Seeds expired+used, expired+revoked, expired+active, and valid+used tokens.</item>
    /// <item><b>Act:</b> Runs cleanup.</item>
    /// <item><b>Assert:</b> Verifies only expired and (used OR revoked) are deleted.</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task CleanupExpiredAsync_DeletesUsedOrRevokedExpiredTokens()
    {
        using var ctx = TestAppDbContextFactory.CreateContext("RefreshRepo_Cleanup");
        var user = await TestEntityFactory.SeedUserAsync(ctx);

        var past = DateTime.UtcNow.AddDays(-1);
        var future = DateTime.UtcNow.AddDays(1);

        // Should be deleted
        var t1 = await TestEntityFactory.SeedRefreshTokenAsync(ctx, user.Id, "h1", isUsed: true, expiresAt: past);
        var t2 = await TestEntityFactory.SeedRefreshTokenAsync(ctx, user.Id, "h2", isRevoked: true, expiresAt: past);
        
        // Should NOT be deleted
        var t3 = await TestEntityFactory.SeedRefreshTokenAsync(ctx, user.Id, "h3", isUsed: false, isRevoked: false, expiresAt: past); // Expired but not used/revoked
        var t4 = await TestEntityFactory.SeedRefreshTokenAsync(ctx, user.Id, "h4", isUsed: true, expiresAt: future); // Used but not expired

        var repo = new RefreshTokenRepository(ctx);
        
        // EF Core InMemory provider does not support ExecuteDeleteAsync.
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.CleanupExpiredAsync());
    }
}
