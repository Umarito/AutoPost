using Domain.Entities;
using Application.DTOs.Inbox;

namespace Application.Abstractions.Repositories;

/// <summary>
/// Defines the data-access contract for the <see cref="ConversationAssignment"/> entity targeting the ConversationAssignments table.
/// </summary>
/// <remarks>
/// <para><b>Business &amp; Technical Justification:</b>
/// Enforces support load balancing and explicit team assignment inside the Inbox. The TRD demands that conversations can be assigned to a specific workspace member to ensure one-to-one accountability, preventing multiple agents from responding to the same conversation.</para>
/// <para><b>Execution, Process &amp; Relationships:</b>
/// Manages the state of workspace member assignments, linking <see cref="InboxConversation"/> to a specific active <see cref="WorkspaceMember"/> user profile.</para>
/// <para><b>Project Impact &amp; Indispensability:</b>
/// Essential for multi-agent support workflows. Removing or simplifying this contract breaks the responsibility mapping and compromises database integrity due to the one-to-one unique database index constraints on ConversationId.</para>
/// </remarks>
public interface IConversationAssignmentRepository
{
    /// <summary>
    /// Retrieves the current assignment record for a conversation, targeting the ConversationAssignments table.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Necessary for checking if a conversation is already assigned, and for deleting the existing assignment before re-assigning it to another agent.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Executes a SQL query with INNER/LEFT JOINs on AssignedTo (Users table) and AssignedBy (Users table). The returned entity is tracked to enable subsequent removal from the DB Context.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Prevents duplicate assignments for the same conversation thread, protecting database constraints and ensuring clean transition states.</para>
    /// </remarks>
    Task<ConversationAssignment?> GetByConversationIdAsync(Guid conversationId, CancellationToken ct = default);

    /// <summary>
    /// Compiles an aggregate load workload report across all agents, targeting the ConversationAssignments and InboxConversations tables.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Required for Stage 4 Inbox team workload metrics to balance support queues and identify agent bottlenecks.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Executes a read-only grouped SQL query using AsNoTracking. Groups assignments by AssignedToUserId, counting open and unread conversations.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Optimizes database performance by computing aggregates in a single query rather than running N+1 queries per agent, which is crucial for dashboard speed.</para>
    /// </remarks>
    Task<IReadOnlyList<TeamWorkloadDto>> GetWorkloadByWorkspaceIdAsync(Guid workspaceId, CancellationToken ct = default);

    /// <summary>
    /// Persists a new assignment record in the ConversationAssignments table.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Enforces accountability for responding to direct messages and user requests.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Adds a new <see cref="ConversationAssignment"/> instance to the change tracker in the Added state.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Essential for tracking who is handling the conversation; without it, assignments cannot be stored.</para>
    /// </remarks>
    Task<ConversationAssignment> AddAsync(ConversationAssignment assignment, CancellationToken ct = default);

    /// <summary>
    /// Removes an existing assignment record, targeting the ConversationAssignments table.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Supports the "Unassign" workflow and is required during re-assignment to free up the conversation thread.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Marks the tracked <see cref="ConversationAssignment"/> entity for deletion from the change tracker.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Prevents database unique constraint violations by cleaning up existing rows before writing new ones.</para>
    /// </remarks>
    void Remove(ConversationAssignment assignment);
}
