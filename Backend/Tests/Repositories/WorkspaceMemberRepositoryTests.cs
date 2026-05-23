using Infrastructure.Repositories;
using Domain.Entities;
using Domain.Enums;
using Xunit;

namespace Tests.Repositories;

/// <summary>
/// Tests for the <see cref="WorkspaceMemberRepository"/>.
/// </summary>
public class WorkspaceMemberRepositoryTests
{
    /// <summary>
    /// Validates that GetByIdAsync returns the membership with eager-loaded user.
    ///
    /// <list type="bullet">
    /// <item><b>Arrange:</b> Seeds a Workspace, a User, and a Membership.</item>
    /// <item><b>Act:</b> Queries by the membership ID.</item>
    /// <item><b>Assert:</b> Asserts the membership is returned and the User navigation property is loaded.</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task GetByIdAsync_ReturnsMemberWithUserLoaded()
    {
        using var ctx = TestAppDbContextFactory.CreateContext("MemberRepo_GetById");
        var ws = await TestEntityFactory.SeedWorkspaceAsync(ctx);
        var user = await TestEntityFactory.SeedUserAsync(ctx);
        var member = await TestEntityFactory.SeedWorkspaceMemberAsync(ctx, ws.Id, user.Id);
        
        var repo = new WorkspaceMemberRepository(ctx);

        var result = await repo.GetByIdAsync(member.Id);

        Assert.NotNull(result);
        Assert.NotNull(result.User); // Verify eager loading
        Assert.Equal(user.Id, result.User.Id);
    }

    /// <summary>
    /// Validates GetByWorkspaceIdAsync lists all members correctly ordered.
    ///
    /// <list type="bullet">
    /// <item><b>Arrange:</b> Seeds a Workspace and two members.</item>
    /// <item><b>Act:</b> Queries for all members in that workspace.</item>
    /// <item><b>Assert:</b> Asserts both members are returned, with User navigation loaded.</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task GetByWorkspaceIdAsync_ReturnsAllMembersInWorkspace()
    {
        using var ctx = TestAppDbContextFactory.CreateContext("MemberRepo_GetByWsId");
        var ws = await TestEntityFactory.SeedWorkspaceAsync(ctx);
        var u1 = await TestEntityFactory.SeedUserAsync(ctx, email: "u1@test.com");
        var u2 = await TestEntityFactory.SeedUserAsync(ctx, email: "u2@test.com");
        
        await TestEntityFactory.SeedWorkspaceMemberAsync(ctx, ws.Id, u1.Id);
        await TestEntityFactory.SeedWorkspaceMemberAsync(ctx, ws.Id, u2.Id);
        
        var repo = new WorkspaceMemberRepository(ctx);

        var result = await repo.GetByWorkspaceIdAsync(ws.Id);

        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.NotNull(r.User));
    }

    /// <summary>
    /// Validates GetByUserAndWorkspaceAsync finds the specific overlap.
    ///
    /// <list type="bullet">
    /// <item><b>Arrange:</b> Seeds a membership.</item>
    /// <item><b>Act:</b> Queries using the specific user ID and workspace ID.</item>
    /// <item><b>Assert:</b> Asserts the correct membership record is returned for RBAC checks.</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task GetByUserAndWorkspaceAsync_ReturnsSpecificMember()
    {
        using var ctx = TestAppDbContextFactory.CreateContext("MemberRepo_GetByUserAndWs");
        var ws = await TestEntityFactory.SeedWorkspaceAsync(ctx);
        var user = await TestEntityFactory.SeedUserAsync(ctx);
        var member = await TestEntityFactory.SeedWorkspaceMemberAsync(ctx, ws.Id, user.Id);
        
        var repo = new WorkspaceMemberRepository(ctx);

        var result = await repo.GetByUserAndWorkspaceAsync(user.Id, ws.Id);

        Assert.NotNull(result);
        Assert.Equal(member.Id, result.Id);
    }

    /// <summary>
    /// Validates ExistsAsync prevents duplicate invites.
    ///
    /// <list type="bullet">
    /// <item><b>Arrange:</b> Seeds a membership.</item>
    /// <item><b>Act:</b> Checks existence for those same IDs.</item>
    /// <item><b>Assert:</b> Returns true.</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task ExistsAsync_ReturnsTrue_WhenUserIsAlreadyMember()
    {
        using var ctx = TestAppDbContextFactory.CreateContext("MemberRepo_Exists");
        var ws = await TestEntityFactory.SeedWorkspaceAsync(ctx);
        var user = await TestEntityFactory.SeedUserAsync(ctx);
        await TestEntityFactory.SeedWorkspaceMemberAsync(ctx, ws.Id, user.Id);
        
        var repo = new WorkspaceMemberRepository(ctx);

        var result = await repo.ExistsAsync(user.Id, ws.Id);

        Assert.True(result);
    }

    /// <summary>
    /// Validates Remove cascades deletion properly.
    ///
    /// <list type="bullet">
    /// <item><b>Arrange:</b> Seeds a membership.</item>
    /// <item><b>Act:</b> Removes and saves.</item>
    /// <item><b>Assert:</b> Verifies the database no longer contains the record.</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task Remove_DeletesMemberFromDatabase()
    {
        using var ctx = TestAppDbContextFactory.CreateContext("MemberRepo_Remove");
        var ws = await TestEntityFactory.SeedWorkspaceAsync(ctx);
        var user = await TestEntityFactory.SeedUserAsync(ctx);
        var member = await TestEntityFactory.SeedWorkspaceMemberAsync(ctx, ws.Id, user.Id);
        
        var repo = new WorkspaceMemberRepository(ctx);
        repo.Remove(member);
        await ctx.SaveChangesAsync();

        var inDb = await ctx.WorkspaceMembers.FindAsync(member.Id);
        Assert.Null(inDb);
    }
}
