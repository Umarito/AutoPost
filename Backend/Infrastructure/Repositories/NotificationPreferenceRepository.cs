using Application.Abstractions.Repositories;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="INotificationPreferenceRepository"/>.
///
/// <para><b>How it works:</b>
/// Uses the unique composite index (UserId, WorkspaceId, EventType) for efficient lookups.
/// The notification dispatcher calls <c>GetByUserWorkspaceAndEventAsync</c> before sending
/// each notification to check if the user has opted out of a specific channel.</para>
///
/// <para><b>Purpose:</b>
/// Manages per-user notification channel preferences, enabling fine-grained control
/// over which events trigger in-app, email, or push notifications.</para>
/// </summary>
public class NotificationPreferenceRepository(ApplicationDbContext db) : INotificationPreferenceRepository
{
    /// <summary>
    /// Lists all notification preferences for a user within a workspace.
    /// Used to render the notification settings page.
    /// AsNoTracking — settings page loads are read-only.
    /// </summary>
    public async Task<IReadOnlyList<NotificationPreference>> GetByUserAndWorkspaceAsync(
        Guid userId, Guid workspaceId, CancellationToken ct = default)
        => await db.NotificationPreferences.AsNoTracking()
            .Where(np => np.UserId == userId && np.WorkspaceId == workspaceId)
            .ToListAsync(ct);

    /// <summary>
    /// Adds a new preference to the change tracker. Actual INSERT on SaveChangesAsync.
    /// </summary>
    public async Task<NotificationPreference> AddAsync(NotificationPreference preference, CancellationToken ct = default)
    {
        await db.NotificationPreferences.AddAsync(preference, ct);
        return preference;
    }

    /// <summary>
    /// Marks the preference as Modified for channel toggle updates.
    /// </summary>
    public void Update(NotificationPreference preference)
        => db.NotificationPreferences.Update(preference);

    /// <summary>
    /// Looks up a specific preference using the unique composite index.
    /// Called by the notification dispatcher before sending a notification:
    /// if null is returned, default channel settings are used.
    /// AsNoTracking — the dispatcher only reads channel flags.
    /// </summary>
    public async Task<NotificationPreference?> GetByUserWorkspaceAndEventAsync(
        Guid userId, Guid workspaceId, NotificationEventType eventType, CancellationToken ct = default)
        => await db.NotificationPreferences.AsNoTracking()
            .FirstOrDefaultAsync(np =>
                np.UserId == userId &&
                np.WorkspaceId == workspaceId &&
                np.EventType == eventType, ct);
}
