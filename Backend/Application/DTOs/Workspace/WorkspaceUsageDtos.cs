namespace Application.DTOs.Workspace;

/// <summary>
/// Represents current plan usage for a workspace.
///
/// <para><b>Role in the system:</b>
/// The workspace settings and billing screens use this DTO to show how many
/// members, social accounts and monthly posts are already consumed versus the
/// limits granted by the active subscription plan.</para>
/// </summary>
/// <param name="WorkspaceId">Workspace the usage snapshot belongs to.</param>
/// <param name="Plan">Human-readable plan name.</param>
/// <param name="SocialAccountsUsed">Current number of connected social accounts.</param>
/// <param name="SocialAccountsLimit">Maximum allowed social accounts for the plan.</param>
/// <param name="MembersUsed">Current number of workspace members.</param>
/// <param name="MembersLimit">Maximum allowed members for the plan.</param>
/// <param name="PostsUsedThisMonth">Current number of posts created or published in the current billing month.</param>
/// <param name="PostsLimitPerMonth">Maximum number of monthly posts allowed by the plan.</param>
public record WorkspacePlanUsageDto(
    Guid WorkspaceId,
    string Plan,
    int SocialAccountsUsed,
    int SocialAccountsLimit,
    int MembersUsed,
    int MembersLimit,
    int PostsUsedThisMonth,
    int PostsLimitPerMonth);
