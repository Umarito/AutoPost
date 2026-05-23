namespace Application.Common;

/// <summary>
/// Provides access to the current authenticated user, workspace and session context.
/// </summary>
public interface ICurrentUserContext
{
    /// <summary>
    /// Gets the authenticated application user identifier extracted from JWT claims.
    /// </summary>
    Guid UserId { get; }

    /// <summary>
    /// Gets the current workspace identifier extracted from JWT claims.
    /// </summary>
    Guid WorkspaceId { get; }

    /// <summary>
    /// Gets the current refresh-token backed session identifier extracted from the <c>sid</c> claim when available.
    /// </summary>
    Guid? SessionId { get; }

    /// <summary>
    /// Gets the current workspace role extracted from JWT claims.
    /// </summary>
    string Role { get; }
}
