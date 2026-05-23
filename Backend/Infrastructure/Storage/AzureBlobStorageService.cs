using Application.Abstractions.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace Infrastructure.Storage;

/// <summary>
/// Stores media assets in Azure Blob Storage.
/// </summary>
public sealed class AzureBlobStorageService : IBlobStorageService
{
    private readonly BlobServiceClient _blobServiceClient;

    /// <summary>
    /// Initializes the storage service with the root Azure Blob service client.
    /// </summary>
    /// <param name="blobServiceClient">The configured Azure Blob service client.</param>
    public AzureBlobStorageService(BlobServiceClient blobServiceClient)
    {
        _blobServiceClient = blobServiceClient;
    }

    /// <inheritdoc />
    public async Task<BlobUploadResult> UploadAsync(
        string containerName,
        string blobName,
        Stream content,
        string contentType,
        CancellationToken ct = default)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        await containerClient.CreateIfNotExistsAsync(cancellationToken: ct);

        var blobClient = containerClient.GetBlobClient(blobName);
        await blobClient.UploadAsync(
            content,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = contentType
                }
            },
            ct);

        return new BlobUploadResult(blobName, blobClient.Uri);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string containerName, string blobName, CancellationToken ct = default)
    {
        var blobClient = _blobServiceClient.GetBlobContainerClient(containerName).GetBlobClient(blobName);
        await blobClient.DeleteIfExistsAsync(cancellationToken: ct);
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(string containerName, string blobName, CancellationToken ct = default)
    {
        var blobClient = _blobServiceClient.GetBlobContainerClient(containerName).GetBlobClient(blobName);
        var response = await blobClient.ExistsAsync(ct);
        return response.Value;
    }

    /// <inheritdoc />
    public async Task<Stream> OpenReadAsync(string containerName, string blobName, CancellationToken ct = default)
    {
        var blobClient = _blobServiceClient.GetBlobContainerClient(containerName).GetBlobClient(blobName);
        var response = await blobClient.OpenReadAsync(cancellationToken: ct);
        return response;
    }

    /// <inheritdoc />
    public Uri GetBlobUri(string containerName, string blobName)
        => _blobServiceClient.GetBlobContainerClient(containerName).GetBlobClient(blobName).Uri;
}
