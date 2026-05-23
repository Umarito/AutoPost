using Infrastructure.Repositories;
using Domain.Entities;
using Domain.Enums;
using Xunit;

namespace Tests.Repositories;

/// <summary>
/// Tests for the <see cref="ConversationAssignmentRepository"/>.
/// </summary>
public class ConversationAssignmentRepositoryTests
{
    /// <summary>
    /// Validates AddAsync correctly saves.
    ///
    /// <list type="bullet">
    /// <item><b>Arrange:</b> Un-tracked Assignment.</item>
    /// <item><b>Act:</b> AddAsync and SaveChanges.</item>
    /// <item><b>Assert:</b> Exists in database.</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task AddAsync_PersistsNewAssignment()
    {
        using var ctx = TestAppDbContextFactory.CreateContext("ConvAssignRepo_Add");
        var repo = new ConversationAssignmentRepository(ctx);
        
        var assignment = new ConversationAssignment();
        ctx.Entry(assignment).Property("Id").CurrentValue = Guid.NewGuid();
        ctx.Entry(assignment).Property("AssignedToUserId").CurrentValue = Guid.NewGuid();
        ctx.Entry(assignment).Property("ConversationId").CurrentValue = Guid.NewGuid();

        await repo.AddAsync(assignment);
        await ctx.SaveChangesAsync();

        Assert.NotNull(await ctx.ConversationAssignments.FindAsync(assignment.Id));
    }
}
