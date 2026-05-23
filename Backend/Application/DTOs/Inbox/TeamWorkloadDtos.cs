namespace Application.DTOs.Inbox;

/// <summary>
/// Represents inbox workload assigned to one team member.
///
/// <para><b>Role in the system:</b>
/// This DTO supports routing and workforce balancing screens by showing how many
/// conversations are currently assigned to each member and how many remain unread.</para>
/// </summary>
/// <param name="UserId">Team member user identifier.</param>
/// <param name="DisplayName">Display name of the team member.</param>
/// <param name="OpenConversationCount">Number of currently open conversations assigned to the member.</param>
/// <param name="UnreadConversationCount">Number of assigned conversations that still contain unread messages.</param>
public record TeamWorkloadDto(
    Guid UserId,
    string DisplayName,
    int OpenConversationCount,
    int UnreadConversationCount);
