using Microsoft.AspNetCore.Identity;

namespace Domain.Entities;

/// <summary>
/// Represents a human user account inside the AutoPost platform.
/// </summary>
/// <remarks>
/// The entity intentionally inherits from <see cref="IdentityUser{TKey}"/> because the
/// project keeps ASP.NET Core Identity inside the Domain layer for the current MVP.
/// Identity-managed fields such as <see cref="IdentityUser{TKey}.Email"/>,
/// <see cref="IdentityUser{TKey}.UserName"/> and password hashes remain owned by Identity,
/// while this type stores business-specific profile and lifecycle information.
/// </remarks>
public class ApplicationUser : IdentityUser<Guid>
{
    /// <summary>
    /// Gets the display name shown across the product UI.
    /// </summary>
    public string DisplayName { get; private set; } = default!;

    /// <summary>
    /// Gets the optional avatar URL shown in collaborative screens.
    /// </summary>
    public string? AvatarUrl { get; private set; }

    /// <summary>
    /// Gets the preferred IANA time zone identifier used for display and scheduling UX.
    /// </summary>
    public string TimeZoneId { get; private set; } = "UTC";

    /// <summary>
    /// Gets the preferred UI locale of the user.
    /// </summary>
    public string Locale { get; private set; } = "en";

    /// <summary>
    /// Gets the UTC timestamp when the account was registered.
    /// </summary>
    public DateTime RegisteredAt { get; private set; }

    /// <summary>
    /// Gets the UTC timestamp of the last successful login, if any.
    /// </summary>
    public DateTime? LastLoginAt { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the account is business-active inside AutoPost.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Gets workspace memberships linked to the user.
    /// </summary>
    public IReadOnlyCollection<WorkspaceMember> WorkspaceMemberships { get; private set; } = new List<WorkspaceMember>();

    /// <summary>
    /// Gets refresh token records representing user sessions across devices.
    /// </summary>
    public IReadOnlyCollection<RefreshToken> RefreshTokens { get; private set; } = new List<RefreshToken>();

    /// <summary>
    /// Gets the user-specific notification channel preferences.
    /// </summary>
    public IReadOnlyCollection<NotificationPreference> NotificationPreferences { get; private set; } = new List<NotificationPreference>();

    /// <summary>
    /// Creates a new domain user profile prepared for ASP.NET Core Identity persistence.
    /// </summary>
    /// <param name="email">Unique email address used as the primary login identifier.</param>
    /// <param name="displayName">Display name visible to teammates and UI consumers.</param>
    /// <param name="registeredAtUtc">UTC registration timestamp.</param>
    /// <returns>A fully initialized <see cref="ApplicationUser"/> instance.</returns>
    public static ApplicationUser Create(string email, string displayName, DateTime registeredAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        var normalizedEmail = email.Trim();

        return new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            UserName = normalizedEmail,
            DisplayName = displayName.Trim(),
            TimeZoneId = "UTC",
            Locale = "en",
            RegisteredAt = registeredAtUtc,
            IsActive = true,
            EmailConfirmed = false
        };
    }

    /// <summary>
    /// Updates editable profile fields using patch semantics.
    /// </summary>
    /// <param name="displayName">New display name, or <c>null</c> to keep the current value.</param>
    /// <param name="avatarUrl">New avatar URL, or <c>null</c> to keep the current value.</param>
    /// <param name="timeZoneId">New IANA time zone identifier, or <c>null</c> to keep the current value.</param>
    /// <param name="locale">New UI locale, or <c>null</c> to keep the current value.</param>
    public void UpdateProfile(
        string? displayName,
        string? avatarUrl,
        string? timeZoneId,
        string? locale)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            DisplayName = displayName.Trim();
        }

        if (avatarUrl is not null)
        {
            AvatarUrl = string.IsNullOrWhiteSpace(avatarUrl) ? null : avatarUrl.Trim();
        }

        if (!string.IsNullOrWhiteSpace(timeZoneId))
        {
            TimeZoneId = timeZoneId.Trim();
        }

        if (!string.IsNullOrWhiteSpace(locale))
        {
            Locale = locale.Trim();
        }
    }

    /// <summary>
    /// Marks the user as having successfully completed a login flow.
    /// </summary>
    /// <param name="loggedInAtUtc">UTC timestamp of the successful login.</param>
    public void MarkSuccessfulLogin(DateTime loggedInAtUtc)
    {
        LastLoginAt = loggedInAtUtc;
        IsActive = true;
    }

    /// <summary>
    /// Deactivates the business account without deleting the identity record.
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
    }

    /// <summary>
    /// Reactivates the business account.
    /// </summary>
    public void Activate()
    {
        IsActive = true;
    }
}
