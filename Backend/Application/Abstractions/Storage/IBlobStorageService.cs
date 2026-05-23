namespace Application.Abstractions.Storage;

/// <summary>
/// Abstracts binary object storage used for videos, thumbnails and other media artifacts.
///
/// <para>
/// The platform stores large media files outside the relational database. This
/// contract allows the Application layer to request uploads and deletions
/// without coupling to Azure Blob, S3 or any other specific provider.
/// </para>
/// </summary>
public interface IBlobStorageService
{
    /// <summary>
    /// Uploads a blob into the specified logical container.
    /// </summary>
    /// <param name="containerName">The logical container or bucket segment.</param>
    /// <param name="blobName">The storage key to assign to the blob.</param>
    /// <param name="content">The stream containing the payload to upload.</param>
    /// <param name="contentType">The MIME content type stored alongside the blob.</param>
    /// <param name="ct">The cancellation token for the upload operation.</param>
    /// <returns>The provider-agnostic upload result.</returns>
    Task<BlobUploadResult> UploadAsync(
        string containerName,
        string blobName,
        Stream content,
        string contentType,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes a blob from the specified logical container if it exists.
    /// </summary>
    /// <param name="containerName">The logical container or bucket segment.</param>
    /// <param name="blobName">The storage key of the blob to delete.</param>
    /// <param name="ct">The cancellation token for the delete operation.</param>
    Task DeleteAsync(string containerName, string blobName, CancellationToken ct = default);

    /// <summary>
    /// Checks whether a blob currently exists in the specified container.
    /// </summary>
    /// <param name="containerName">The logical container or bucket segment.</param>
    /// <param name="blobName">The storage key of the blob to inspect.</param>
    /// <param name="ct">The cancellation token for the existence check.</param>
    /// <returns><c>true</c> when the blob exists; otherwise <c>false</c>.</returns>
    Task<bool> ExistsAsync(string containerName, string blobName, CancellationToken ct = default);

    /// <summary>
    /// Opens an existing blob for streaming reads.
    /// </summary>
    /// <param name="containerName">The logical container or bucket segment.</param>
    /// <param name="blobName">The storage key of the blob to read.</param>
    /// <param name="ct">The cancellation token for the read operation.</param>
    /// <returns>A readable stream positioned at the start of the blob payload.</returns>
    Task<Stream> OpenReadAsync(string containerName, string blobName, CancellationToken ct = default);

    /// <summary>
    /// Builds a public or canonical URI for a blob reference.
    /// </summary>
    /// <param name="containerName">The logical container or bucket segment.</param>
    /// <param name="blobName">The storage key of the blob.</param>
    /// <returns>The absolute URI representing the blob.</returns>
    Uri GetBlobUri(string containerName, string blobName);
}
