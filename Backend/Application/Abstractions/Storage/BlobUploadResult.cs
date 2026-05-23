namespace Application.Abstractions.Storage;

/// <summary>
/// Describes the outcome of a blob upload operation.
///
/// <para>
/// The result provides both the canonical blob URI and the storage key so the
/// caller can persist references without depending on storage-provider SDK types.
/// </para>
/// </summary>
/// <param name="BlobName">The provider-specific blob name or key.</param>
/// <param name="BlobUri">The absolute URI of the uploaded blob.</param>
public sealed record BlobUploadResult(string BlobName, Uri BlobUri);
