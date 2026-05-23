using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Options;

/// <summary>
/// Defines Azure Blob Storage settings for media persistence.
/// </summary>
public sealed class AzureBlobStorageOptions
{
    /// <summary>
    /// Gets the configuration section name used to bind these options.
    /// </summary>
    public const string SectionName = "AzureBlobStorage";

    /// <summary>
    /// Gets or sets the Azure Blob Storage connection string.
    /// </summary>
    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the container name used for source video files.
    /// </summary>
    [Required]
    public string VideosContainerName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the container name used for generated thumbnails.
    /// </summary>
    [Required]
    public string ThumbnailsContainerName { get; set; } = string.Empty;
}
