using Domain.Entities;
using Application.DTOs.Inbox;

namespace Application.Abstractions.Repositories;

/// <summary>
/// Defines the data-access contract for the <see cref="ConversationAssignment"/> entity.
///
/// <para><b>Role in the system:</b>
/// ConversationAssignment implements a one-to-one relationship with InboxConversation — each
/// conversation can have at most one active assignee (team member). Assignments distribute
/// the support workload: an admin or automation rule assigns conversations to specific team
/// members so they know which threads they are responsible for.</para>
///
/// <para><b>TRD reference:</b>
/// Stage 4 — Inbox. Endpoint: POST /api/inbox/conversations/{id}/assign.</para>
/// </summary>
public interface IConversationAssignmentRepository
{
    /// <summary>
    /// Gets the current assignment for a conversation, including the assigned user's profile.
    /// Returns <c>null</c> if the conversation is unassigned.
    /// Result is not tracked (read-only).
    /// </summary>
    Task<ConversationAssignment?> GetByConversationIdAsync(Guid conversationId, CancellationToken ct = default);

    /// <summary>
    /// Aggregates per-user inbox workload inside one workspace.
    /// </summary>
    Task<IReadOnlyList<TeamWorkloadDto>> GetWorkloadByWorkspaceIdAsync(Guid workspaceId, CancellationToken ct = default);

    /// <summary>
    /// Creates a new assignment. If the conversation already has an assignment,
    /// the caller must remove the old one first (one-to-one constraint enforced by unique index).
    /// </summary>
    Task<ConversationAssignment> AddAsync(ConversationAssignment assignment, CancellationToken ct = default);

    /// <summary>
    /// Removes the current assignment (un-assigns the conversation).
    /// Called when re-assigning to a different user (remove old + add new)
    /// or when explicitly un-assigning a conversation.
    /// </summary>
    void Remove(ConversationAssignment assignment);
}
