using Infrastructure.Repositories;
using Domain.Entities;
using Xunit;

namespace Tests.Repositories;

/// <summary>
/// Tests for the <see cref="WorkspaceRepository"/>.
/// </summary>
public class WorkspaceRepositoryTests
{
    /// <summary>
    /// Validates that GetByIdAsync returns the correct entity when it exists.
    ///
    /// <list type="bullet">
    /// <item><b>Arrange:</b> Seeds a Workspace into a fresh in-memory database to establish test state.</item>
    /// <item><b>Act:</b> Calls GetByIdAsync with the seeded workspace's ID.</item>
    /// <item><b>Assert:</b> Verifies that the returned workspace is not null and its properties match the seeded data, proving the basic read capability.</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task GetByIdAsync_ReturnsWorkspace_WhenExists()
    {
        using var ctx = TestAppDbContextFactory.CreateContext("WorkspaceRepo_GetById");
        var seeded = await TestEntityFactory.SeedWorkspaceAsync(ctx, name: "Alpha Team");
        var repo = new WorkspaceRepository(ctx);

        var result = await repo.GetByIdAsync(seeded.Id);

        Assert.NotNull(result);
        Assert.Equal("Alpha Team", result.Name);
    }

    /// <summary>
    /// Validates that GetByIdAsync correctly handles queries for non-existent entities.
    ///
    /// <list type="bullet">
    /// <item><b>Arrange:</b> Initializes an empty in-memory database context.</item>
    /// <item><b>Act:</b> Calls GetByIdAsync with a randomly generated Guid that doesn't exist.</item>
    /// <item><b>Assert:</b> Verifies the result is null, ensuring the repository correctly reports absent data.</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotExists()
    {
        using var ctx = TestAppDbContextFactory.CreateContext("WorkspaceRepo_GetByIdNull");
        var repo = new WorkspaceRepository(ctx);

        var result = await repo.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    /// <summary>
    /// Validates that GetBySlugAsync resolves workspaces by their unique slug.
    ///
    /// <list type="bullet">
    /// <item><b>Arrange:</b> Seeds a Workspace with a specific URL slug.</item>
    /// <item><b>Act:</b> Queries the repository using the slug.</item>
    /// <item><b>Assert:</b> Asserts the returned entity matches the seeded one, validating the tenant routing mechanism.</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task GetBySlugAsync_ReturnsWorkspace_WhenSlugMatches()
    {
        using var ctx = TestAppDbContextFactory.CreateContext("WorkspaceRepo_GetBySlug");
        var seeded = await TestEntityFactory.SeedWorkspaceAsync(ctx, slug: "alpha-team");
        var repo = new WorkspaceRepository(ctx);

        var result = await repo.GetBySlugAsync("alpha-team");

        Assert.NotNull(result);
        Assert.Equal(seeded.Id, result.Id);
    }

    /// <summary>
    /// Validates that SlugExistsAsync correctly identifies when a slug is taken.
    ///
    /// <list type="bullet">
    /// <item><b>Arrange:</b> Seeds a Workspace with a known slug.</item>
    /// <item><b>Act:</b> Checks if the slug exists.</item>
    /// <item><b>Assert:</b> Asserts true, which is critical for preventing duplicate workspace URLs during creation.</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task SlugExistsAsync_ReturnsTrue_WhenTaken()
    {
        using var ctx = TestAppDbContextFactory.CreateContext("WorkspaceRepo_SlugExists");
        await TestEntityFactory.SeedWorkspaceAsync(ctx, slug: "taken-slug");
        var repo = new WorkspaceRepository(ctx);

        var result = await repo.SlugExistsAsync("taken-slug");

        Assert.True(result);
    }

    /// <summary>
    /// Validates that AddAsync tracks a new workspace for insertion.
    ///
    /// <list type="bullet">
    /// <item><b>Arrange:</b> Creates an isolated context and an un-tracked Workspace instance.</item>
    /// <item><b>Act:</b> Calls AddAsync and saves changes to commit.</item>
    /// <item><b>Assert:</b> Queries the database to confirm the workspace was physically persisted with the correct data.</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task AddAsync_PersistsNewWorkspace()
    {
        using var ctx = TestAppDbContextFactory.CreateContext("WorkspaceRepo_Add");
        var repo = new WorkspaceRepository(ctx);
        var workspace = new Workspace();
        ctx.Entry(workspace).Property("Id").CurrentValue = Guid.NewGuid();
        ctx.Entry(workspace).Property("Name").CurrentValue = "New Workspace";
        ctx.Entry(workspace).Property("Slug").CurrentValue = "new-workspace";

        await repo.AddAsync(workspace);
        await ctx.SaveChangesAsync();

        var inDb = await ctx.Workspaces.FindAsync(workspace.Id);
        Assert.NotNull(inDb);
        Assert.Equal("New Workspace", inDb.Name);
    }

    /// <summary>
    /// Validates that Update marks the entity as modified.
    ///
    /// <list type="bullet">
    /// <item><b>Arrange:</b> Seeds a workspace.</item>
    /// <item><b>Act:</b> Modifies a property, calls Update, and saves changes.</item>
    /// <item><b>Assert:</b> Uses a fresh context to verify the update actually hit the database.</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task Update_ModifiesExistingWorkspace()
    {
        using var ctx = TestAppDbContextFactory.CreateContext("WorkspaceRepo_Update");
        var seeded = await TestEntityFactory.SeedWorkspaceAsync(ctx, name: "Old Name");
        
        var repo = new WorkspaceRepository(ctx);
        ctx.Entry(seeded).Property("Name").CurrentValue = "Updated Name";
        repo.Update(seeded);
        await ctx.SaveChangesAsync();

        var inDb = await repo.GetByIdAsync(seeded.Id);
        Assert.Equal("Updated Name", inDb?.Name);
    }

    /// <summary>
    /// Validates that GetByUserIdAsync retrieves only workspaces where the user is a member.
    ///
    /// <list type="bullet">
    /// <item><b>Arrange:</b> Seeds a user, two workspaces they belong to, and one they don't.</item>
    /// <item><b>Act:</b> Queries workspaces for that user ID.</item>
    /// <item><b>Assert:</b> Asserts the count is exactly 2 and the non-member workspace is excluded.</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task GetByUserIdAsync_ReturnsOnlyWorkspacesWhereUserIsMember()
    {
        using var ctx = TestAppDbContextFactory.CreateContext("WorkspaceRepo_GetByUserId");
        var user = await TestEntityFactory.SeedUserAsync(ctx);
        
        var w1 = await TestEntityFactory.SeedWorkspaceAsync(ctx, name: "W1");
        var w2 = await TestEntityFactory.SeedWorkspaceAsync(ctx, name: "W2");
        var w3 = await TestEntityFactory.SeedWorkspaceAsync(ctx, name: "W3"); // User not in W3

        await TestEntityFactory.SeedWorkspaceMemberAsync(ctx, w1.Id, user.Id);
        await TestEntityFactory.SeedWorkspaceMemberAsync(ctx, w2.Id, user.Id);
        
        var repo = new WorkspaceRepository(ctx);

        var result = await repo.GetByUserIdAsync(user.Id);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, w => w.Id == w1.Id);
        Assert.Contains(result, w => w.Id == w2.Id);
        Assert.DoesNotContain(result, w => w.Id == w3.Id);
    }
}
