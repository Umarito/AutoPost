using Application.Abstractions.Security;
using Microsoft.AspNetCore.DataProtection;

namespace Infrastructure.Security;

/// <summary>
/// Protects and unprotects sensitive token values using ASP.NET Core Data Protection.
/// </summary>
public sealed class TokenProtectionService : ITokenProtectionService
{
    private readonly IDataProtectionProvider _dataProtectionProvider;

    /// <summary>
    /// Initializes the service with the application-wide data protection provider.
    /// </summary>
    /// <param name="dataProtectionProvider">The data protection provider used to create purpose-scoped protectors.</param>
    public TokenProtectionService(IDataProtectionProvider dataProtectionProvider)
    {
        _dataProtectionProvider = dataProtectionProvider;
    }

    /// <inheritdoc />
    public string Protect(string plaintext, string purpose)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plaintext);
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);

        var protector = _dataProtectionProvider.CreateProtector($"AutoPost.Tokens.{purpose}");
        return protector.Protect(plaintext);
    }

    /// <inheritdoc />
    public string Unprotect(string protectedValue, string purpose)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedValue);
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);

        var protector = _dataProtectionProvider.CreateProtector($"AutoPost.Tokens.{purpose}");
        return protector.Unprotect(protectedValue);
    }
}
