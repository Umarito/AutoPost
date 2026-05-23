namespace Application.DTOs.Auth;

/// <summary>
/// Represents one active or historical user session backed by a refresh token.
/// </summary>
/// <param name="Id">The refresh token record identifier that represents this session.</param>
/// <param name="CreatedAt">UTC timestamp when the session was created.</param>
/// <param name="ExpiresAt">UTC timestamp when the refresh token expires.</param>
/// <param name="RevokedAt">UTC timestamp when the session was revoked, or <c>null</c> if still active.</param>
/// <param name="DeviceInfo">Optional user-friendly device description captured when the session was issued.</param>
/// <param name="IpAddress">Optional originating IP address captured for audit and suspicious-activity analysis.</param>
/// <param name="IsCurrent">Whether this session corresponds to the device currently making the request.</param>
public record UserSessionDto(
    Guid Id,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    DateTime? RevokedAt,
    string? DeviceInfo,
    string? IpAddress,
    bool IsCurrent);
