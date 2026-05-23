using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Inbox;

// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║  CONVERSATION ASSIGNMENT DTOs — Inbox Workload Distribution                ║
// ║  TRD Stage 4: Inbox & Automation                                           ║
// ║  Endpoint: POST /api/inbox/conversations/{id}/assign                       ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

/// <summary>
/// Payload for assigning a conversation to a team member.
///
/// <para><b>What it does:</b>
/// Assigns (or re-assigns) a conversation to a specific team member. If the conversation
/// already has an assignee, the old assignment is replaced. If AssigneeUserId is null,
/// the conversation is un-assigned (returned to the unassigned pool).</para>
///
/// <para><b>Authorization:</b>
/// Only Owners, Admins, and the current assignee can reassign conversations.</para>
///
/// <para><b>TRD API:</b> POST /api/inbox/conversations/{id}/assign</para>
/// </summary>
/// <param name="AssigneeUserId">The user Id to assign the conversation to, or null to un-assign.</param>
/// <param name="Note">Optional note explaining the assignment (e.g., "VIP client, handle with care").</param>
public record AssignConversationRequest(
    Guid? AssigneeUserId,
    [MaxLength(500)] string? Note
);

/// <summary>
/// Assignment details returned after assigning a conversation.
///
/// <para><b>What it contains:</b>
/// Who assigned the conversation, to whom, when, and any notes.
/// Displayed in the conversation header as "Assigned to {Name} by {AssignerName}".</para>
/// </summary>
/// <param name="ConversationId">The conversation that was assigned.</param>
/// <param name="AssignedToName">Display name of the assigned team member.</param>
/// <param name="AssignedByName">Display name of whoever made the assignment, or null for auto-assignment.</param>
/// <param name="AssignedAt">UTC timestamp when the assignment was made.</param>
/// <param name="Note">Optional assignment note.</param>
public record ConversationAssignmentDto(
    Guid ConversationId,
    string AssignedToName,
    string? AssignedByName,
    DateTime AssignedAt,
    string? Note
);
