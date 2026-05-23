using System.Text.Json;
using Application.Abstractions.Security;
using Microsoft.AspNetCore.DataProtection;

namespace Infrastructure.Security;

/// <summary>
/// Protects workspace invitation payloads using ASP.NET Core Data Protection.
/// </summary>
public sealed class InviteTokenService : IInviteTokenService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IDataProtector _dataProtector;

    /// <summary>
    /// Initializes the invite token service.
    /// </summary>
    /// <param name="dataProtectionProvider">Root data protection provider.</param>
    public InviteTokenService(IDataProtectionProvider dataProtectionProvider)
    {
        _dataProtector = dataProtectionProvider.CreateProtector("AutoPost.InviteTokens.v1");
    }

    /// <inheritdoc />
    public string Generate(Guid workspaceId, string email, string role, DateTime expiresAtUtc)
    {
        var payload = new InviteTokenPayload(workspaceId, email, role, expiresAtUtc);
        var serialized = JsonSerializer.Serialize(payload, SerializerOptions);
        return _dataProtector.Protect(serialized);
    }

    /// <inheritdoc />
    public InviteTokenPayload Validate(string token)
    {
        try
        {
            var serialized = _dataProtector.Unprotect(token);
            var payload = JsonSerializer.Deserialize<InviteTokenPayload>(serialized, SerializerOptions)
                ?? throw new InvalidOperationException("Invitation token payload is empty.");

            if (payload.ExpiresAtUtc <= DateTime.UtcNow)
            {
                throw new InvalidOperationException("Invitation token has expired.");
            }

            return payload;
        }
        catch (Exception exception) when (exception is not InvalidOperationException)
        {
            throw new InvalidOperationException("Invitation token is invalid.", exception);
        }
    }
}
