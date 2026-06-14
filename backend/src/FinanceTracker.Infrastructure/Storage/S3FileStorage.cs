using Amazon.S3;
using Amazon.S3.Model;
using FinanceTracker.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Infrastructure.Storage;

/// <summary>
/// <see cref="IFileStorage"/> backed by the AWS S3 SDK. The same client speaks
/// to MinIO, AWS S3, or any S3-compatible store — the provider is selected only
/// by <see cref="S3StorageOptions.Endpoint"/>, never by the Application layer.
/// </summary>
internal sealed class S3FileStorage(IAmazonS3 client, IOptions<S3StorageOptions> options) : IFileStorage
{
    private readonly string _bucket = options.Value.BucketName;

    public async Task UploadAsync(
        string objectKey,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var request = new PutObjectRequest
        {
            BucketName = _bucket,
            Key = objectKey,
            InputStream = content,
            ContentType = contentType,
            // The SDK buffers to compute a checksum unless we disable it for
            // forward-only streams; AutoCloseStream stays false so the caller
            // keeps ownership of the stream.
            AutoCloseStream = false,
        };

        await client.PutObjectAsync(request, cancellationToken);
    }

    public async Task<Stream> OpenReadAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        var response = await client.GetObjectAsync(_bucket, objectKey, cancellationToken);
        return response.ResponseStream;
    }

    public async Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        try
        {
            await client.GetObjectMetadataAsync(_bucket, objectKey, cancellationToken);
            return true;
        }
        catch (AmazonS3Exception e) when (e.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default) =>
        client.DeleteObjectAsync(_bucket, objectKey, cancellationToken);
}
