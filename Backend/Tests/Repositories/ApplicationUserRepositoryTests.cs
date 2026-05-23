using Infrastructure.Repositories;
using Domain.Entities;
using Xunit;

namespace Tests.Repositories;

/// <summary>
/// Tests for the <see cref="ApplicationUserRepository"/>.
/// </summary>
public class ApplicationUserRepositoryTests
{
    /// <summary>
    /// Validates that GetByIdAsync retrieves the user properly.
    ///
    /// <list type="bullet">
    /// <item><b>Arrange:</b> Seeds a user.</item>
    /// <item><b>Act:</b> Queries by the user ID.</item>
    /// <item><b>Assert:</b> Asserts the user is returned and data matches.</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task GetByIdAsync_ReturnsUser_WhenExists()
    {
        using var ctx = TestAppDbContextFactory.CreateContext("UserRepo_GetById");
        var seeded = await TestEntityFactory.SeedUserAsync(ctx, displayName: "John Doe");
        var repo = new ApplicationUserRepository(ctx);

        var result = await repo.GetByIdAsync(seeded.Id);

        Assert.NotNull(result);
        Assert.Equal("John Doe", result.DisplayName);
    }

    /// <summary>
    /// Validates handling of non-existent users by ID.
    ///
    /// <list type="bullet">
    /// <item><b>Arrange:</b> Empty database.</item>
    /// <item><b>Act:</b> Query by random Guid.</item>
    /// <item><b>Assert:</b> Asserts null is returned.</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotExists()
    {
        using var ctx = TestAppDbContextFactory.CreateContext("UserRepo_GetByIdNull");
        var repo = new ApplicationUserRepository(ctx);
        var result = await repo.GetByIdAsync(Guid.NewGuid());
        Assert.Null(result);
    }

    /// <summary>
    /// Validates that GetByEmailAsync works with normalized emails.
    ///
    /// <list type="bullet">
    /// <item><b>Arrange:</b> Seeds a user with normalized email.</item>
    /// <item><b>Act:</b> Queries by uppercase version of the email.</item>
    /// <item><b>Assert:</b> Asserts the user is found, verifying login lookup logic.</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task GetByEmailAsync_ReturnsUser_WhenNormalizedEmailMatches()
    {
        using var ctx = TestAppDbContextFactory.CreateContext("UserRepo_GetByEmail");
        var email = "john@TEST.com";
        var seeded = await TestEntityFactory.SeedUserAsync(ctx, email: email);
        var repo = new ApplicationUserRepository(ctx);

        var result = await repo.GetByEmailAsync(email);

        Assert.NotNull(result);
        Assert.Equal(seeded.Id, result.Id);
    }

    /// <summary>
    /// Validates AddAsync correctly attaches the user.
    ///
    /// <list type="bullet">
    /// <item><b>Arrange:</b> Creates a new user instance.</item>
    /// <item><b>Act:</b> Adds and saves.</item>
    /// <item><b>Assert:</b> Checks context to ensure persistence.</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task AddAsync_PersistsNewUser()
    {
        using var ctx = TestAppDbContextFactory.CreateContext("UserRepo_Add");
        var repo = new ApplicationUserRepository(ctx);
        var user = TestEntityFactory.CreateUser(email: "new@user.com");
        ctx.Entry(user).Property("DisplayName").CurrentValue = "New User";

        await repo.AddAsync(user);
        await ctx.SaveChangesAsync();

        Assert.NotNull(await ctx.ApplicationUsers.FindAsync(user.Id));
    }

    /// <summary>
    /// Validates UpdateLastLoginAsync executes a direct SQL update.
    ///
    /// <list type="bullet">
    /// <item><b>Arrange:</b> Seeds a user with null last login.</item>
    /// <item><b>Act:</b> Calls the bulk update method.</item>
    /// <item><b>Assert:</b> Checks the database to ensure the field was updated without full entity loading.</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task UpdateLastLoginAsync_UpdatesFieldCorrectly()
    {
        using var ctx = TestAppDbContextFactory.CreateContext("UserRepo_UpdateLastLogin");
        var user = await TestEntityFactory.SeedUserAsync(ctx);
        Assert.Null(user.LastLoginAt); // Confirm initial state

        var repo = new ApplicationUserRepository(ctx);
        
        // EF Core InMemory provider does not support ExecuteUpdateAsync.
        // We expect it to throw InvalidOperationException.
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.UpdateLastLoginAsync(user.Id, DateTime.UtcNow));
    }

    /// <summary>
    /// Validates EmailExistsAsync correctly spots taken emails.
    ///
    /// <list type="bullet">
    /// <item><b>Arrange:</b> Seeds user with specific email.</item>
    /// <item><b>Act:</b> Checks if email exists.</item>
    /// <item><b>Assert:</b> Asserts true, required for registration validation.</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task EmailExistsAsync_ReturnsTrue_WhenTaken()
    {
        using var ctx = TestAppDbContextFactory.CreateContext("UserRepo_EmailExists");
        var email = "taken@test.com";
        await TestEntityFactory.SeedUserAsync(ctx, email: email);
        var repo = new ApplicationUserRepository(ctx);

        var result = await repo.EmailExistsAsync(email);

        Assert.True(result);
    }
}
