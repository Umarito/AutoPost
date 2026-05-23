namespace Application.Abstractions.Notifications;

/// <summary>
/// Normalized transactional email payload.
/// </summary>
/// <param name="ToEmail">Destination email address.</param>
/// <param name="Subject">Localized subject line visible to the recipient.</param>
/// <param name="HtmlBody">HTML body content sent through the configured email provider.</param>
/// <param name="TextBody">Optional plain-text fallback body.</param>
public sealed record EmailMessage(
    string ToEmail,
    string Subject,
    string HtmlBody,
    string? TextBody = null);

/// <summary>
/// Normalized push notification payload.
/// </summary>
/// <param name="UserId">Application user that should receive the notification.</param>
/// <param name="Title">Short notification title.</param>
/// <param name="Body">Main notification text shown to the user.</param>
/// <param name="ActionUrl">Optional URL that the client should open when the user taps the notification.</param>
public sealed record PushNotificationMessage(
    Guid UserId,
    string Title,
    string Body,
    string? ActionUrl = null);
