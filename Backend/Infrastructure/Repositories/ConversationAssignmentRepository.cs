using Application.Abstractions.Repositories;
using Application.DTOs.Inbox;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IConversationAssignmentRepository"/>.
///
/// <para><b>How it works:</b>
/// ConversationAssignment has a one-to-one relationship with InboxConversation enforced
/// by a unique index on ConversationId. To re-assign, the caller must Remove the old
/// assignment and Add a new one — this ensures the unique constraint is respected.</para>
///
/// <para><b>Purpose:</b>
/// Distributes inbox workload by assigning conversations to specific team members.</para>
/// </summary>
public class ConversationAssignmentRepository(ApplicationDbContext db) : IConversationAssignmentRepository
{
    /// <summary>
    /// Gets the current assignment for a conversation, with the assigned user's profile loaded.
    /// Returns null if the conversation is unassigned.
    /// AsNoTracking — assignment lookup is read-only (modifications use Remove + Add pattern).
    /// </summary>
    public async Task<ConversationAssignment?> GetByConversationIdAsync(Guid conversationId, CancellationToken ct = default)
        => await db.ConversationAssignments.AsNoTracking()
            .Include(a => a.AssignedTo)
            .Include(a => a.AssignedBy)
            .FirstOrDefaultAsync(a => a.ConversationId == conversationId, ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<TeamWorkloadDto>> GetWorkloadByWorkspaceIdAsync(Guid workspaceId, CancellationToken ct = default)
        => await db.ConversationAssignments.AsNoTracking()
            .Where(assignment => assignment.Conversation.WorkspaceId == workspaceId)
            .GroupBy(
                assignment => new
                {
                    assignment.AssignedToUserId,
                    assignment.AssignedTo.DisplayName
                })
            .Select(group => new TeamWorkloadDto(
                group.Key.AssignedToUserId,
                group.Key.DisplayName,
                group.Count(assignment => assignment.Conversation.Status == ConversationStatus.Open),
                group.Count(assignment => assignment.Conversation.UnreadCount > 0)))
            .OrderByDescending(item => item.OpenConversationCount)
            .ThenBy(item => item.DisplayName)
            .ToListAsync(ct);

    /// <summary>
    /// Adds a new assignment to the change tracker. Actual INSERT on SaveChangesAsync.
    /// </summary>
    public async Task<ConversationAssignment> AddAsync(ConversationAssignment assignment, CancellationToken ct = default)
    {
        await db.ConversationAssignments.AddAsync(assignment, ct);
        return assignment;
    }

    /// <summary>
    /// Marks the assignment for deletion. EF Core generates a DELETE on SaveChangesAsync.
    /// Called before re-assigning to a different user or when explicitly un-assigning.
    /// </summary>
    public void Remove(ConversationAssignment assignment)
        => db.ConversationAssignments.Remove(assignment);
}
