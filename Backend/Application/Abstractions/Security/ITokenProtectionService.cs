namespace Application.Abstractions.Security;

/// <summary>
/// Protects and restores sensitive token material using the hosting platform's data protection system.
///
/// <para>
/// OAuth access and refresh tokens for external social platforms must never be
/// stored in plaintext. This abstraction provides purpose-scoped protection so
/// Infrastructure can encrypt secrets before persistence and decrypt them only
/// when outbound platform calls need them.
/// </para>
/// </summary>
public interface ITokenProtectionService
{
    /// <summary>
    /// Protects a sensitive token value for a specific purpose.
    /// </summary>
    /// <param name="plaintext">The raw token value to protect.</param>
    /// <param name="purpose">The logical purpose namespace used to isolate protected payloads.</param>
    /// <returns>The protected token payload safe for persistence.</returns>
    string Protect(string plaintext, string purpose);

    /// <summary>
    /// Restores a previously protected token value for a specific purpose.
    /// </summary>
    /// <param name="protectedValue">The protected token payload retrieved from storage.</param>
    /// <param name="purpose">The logical purpose namespace originally used during protection.</param>
    /// <returns>The original plaintext token.</returns>
    string Unprotect(string protectedValue, string purpose);
}
