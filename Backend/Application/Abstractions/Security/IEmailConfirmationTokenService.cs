namespace Application.Abstractions.Security;

/// <summary>
/// Generates and validates email confirmation tokens used during account verification flows.
/// </summary>
public interface IEmailConfirmationTokenService
{
    /// <summary>
    /// Generates a confirmation token for a user account.
    /// </summary>
    /// <param name="userId">Application user that must confirm ownership of the email address.</param>
    /// <param name="email">Email address being confirmed.</param>
    /// <param name="expiresAtUtc">UTC expiration timestamp for the token.</param>
    /// <returns>Opaque confirmation token suitable for inclusion in an email link.</returns>
    string Generate(Guid userId, string email, DateTime expiresAtUtc);

    /// <summary>
    /// Validates and decodes a confirmation token.
    /// </summary>
    /// <param name="token">Opaque token presented by the client.</param>
    /// <returns>Decoded confirmation payload if the token is valid and unexpired.</returns>
    EmailConfirmationTokenPayload Validate(string token);
}
