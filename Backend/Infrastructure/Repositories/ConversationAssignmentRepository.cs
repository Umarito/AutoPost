using Application.Abstractions.Repositories;
using Application.DTOs.Inbox;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IConversationAssignmentRepository"/> targeting the ConversationAssignments table.
/// </summary>
/// <remarks>
/// <para><b>Business &amp; Technical Justification:</b>
/// Implements data persistence logic to handle support ticket and inbox thread assignments. Essential to meet Stage 4 TRD goals of assigning tasks to support members.</para>
/// <para><b>Execution, Process &amp; Relationships:</b>
/// Interfaces directly with <see cref="ApplicationDbContext"/>, executing SQL queries and tracking entities to reflect support queue mutations.</para>
/// <para><b>Project Impact &amp; Indispensability:</b>
/// Forms the core execution logic for workload distribution and agent assignments. Cannot be removed without breaking the ability to delegate chats.</para>
/// </remarks>
public class ConversationAssignmentRepository(ApplicationDbContext db) : IConversationAssignmentRepository
{
    /// <summary>
    /// Gets the current assignment for a conversation, with the assigned user's profile loaded.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Crucial for verification of existing assignments before deleting or replacing them.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Queries the ConversationAssignments table with INNER JOINs on AssignedTo and AssignedBy. This entity is loaded as tracked so that it can be removed from the DbContext.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Essential to avoid foreign key or uniqueness constraint errors on DB updates.</para>
    /// </remarks>
    public async Task<ConversationAssignment?> GetByConversationIdAsync(Guid conversationId, CancellationToken ct = default)
        => await db.ConversationAssignments
            .Include(a => a.AssignedTo)
            .Include(a => a.AssignedBy)
            .FirstOrDefaultAsync(a => a.ConversationId == conversationId, ct);

    /// <summary>
    /// Compiles an aggregate load workload report across all agents.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Drives the workload widget on the dashboard, identifying active vs overloaded agents.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Uses AsNoTracking. Groups the rows in SQL by AssignedToUserId, executing COUNT aggregates on the matching conversations.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Prevents performance degradation by computing workload statistics directly in PostgreSQL rather than in-memory.</para>
    /// </remarks>
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
    /// Adds a new assignment to the change tracker.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Registers the fact that an agent has taken charge of a conversation.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Adds a new <see cref="ConversationAssignment"/> instance to the change tracker in the Added state.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Necessary for tracking who is handling the conversation; without it, assignments cannot be stored.</para>
    /// </remarks>
    public async Task<ConversationAssignment> AddAsync(ConversationAssignment assignment, CancellationToken ct = default)
    {
        await db.ConversationAssignments.AddAsync(assignment, ct);
        return assignment;
    }

    /// <summary>
    /// Marks the assignment for deletion.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Allows unassigning or shifting conversation ownership.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Marks the tracked <see cref="ConversationAssignment"/> entity for deletion from the change tracker.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Ensures clean cleanup of old assignments, avoiding unique index conflicts.</para>
    /// </remarks>
    public void Remove(ConversationAssignment assignment)
        => db.ConversationAssignments.Remove(assignment);
}
