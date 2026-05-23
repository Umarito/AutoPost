using System.ComponentModel.DataAnnotations;

namespace WebApi.Options;

/// <summary>
/// Defines real-time SignalR transport settings and endpoint paths.
/// </summary>
public sealed class SignalROptions
{
    /// <summary>
    /// Gets the configuration section name used to bind these options.
    /// </summary>
    public const string SectionName = "SignalR";

    /// <summary>
    /// Gets or sets the endpoint path used by the notification hub.
    /// </summary>
    [Required]
    public string NotificationHubPath { get; set; } = "/hubs/notifications";

    /// <summary>
    /// Gets or sets the server keep-alive interval in seconds.
    /// </summary>
    [Range(1, 300)]
    public int KeepAliveSeconds { get; set; } = 15;

    /// <summary>
    /// Gets or sets the client timeout interval in seconds.
    /// </summary>
    [Range(1, 300)]
    public int ClientTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets the maximum inbound message size in bytes.
    /// </summary>
    [Range(1024, 1048576)]
    public long MaximumReceiveMessageSize { get; set; } = 32 * 1024;

    /// <summary>
    /// Gets or sets the Redis channel prefix used by the SignalR backplane.
    /// </summary>
    [Required]
    public string ChannelPrefix { get; set; } = "AutoPostSignalR";
}
