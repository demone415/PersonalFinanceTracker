namespace FinanceTracker.Application.Common.Interfaces;

/// <summary>
/// Provider-agnostic object storage abstraction over the private
/// <c>finance-files</c> bucket. The Application layer depends only on this
/// contract — never on a concrete S3/MinIO client — so the backing store can be
/// swapped (MinIO ⇄ AWS S3 ⇄ any S3-compatible service) without touching
/// business logic.
/// </summary>
/// <remarks>
/// Object keys are cryptographically random opaque tokens (256-bit) and are
/// never exposed to the client; files are served only by streaming through the
/// API (see ARCHITECTURE.md §11.8). No presigned URLs.
/// </remarks>
public interface IFileStorage
{
    /// <summary>Uploads <paramref name="content"/> under <paramref name="objectKey"/>, overwriting any existing object.</summary>
    Task UploadAsync(
        string objectKey,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a readable stream for the object. The caller owns the returned
    /// stream and must dispose it. Throws if the object does not exist.
    /// </summary>
    Task<Stream> OpenReadAsync(string objectKey, CancellationToken cancellationToken = default);

    /// <summary>Returns <c>true</c> if an object with <paramref name="objectKey"/> exists.</summary>
    Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken = default);

    /// <summary>Deletes the object. Succeeds even if the object is already absent.</summary>
    Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default);
}
