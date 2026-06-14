using Amazon.S3;
using Amazon.S3.Util;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Infrastructure.Storage;

/// <summary>
/// Ensures the private <c>finance-files</c> bucket exists at startup, so the
/// app is self-contained against a fresh MinIO/S3 instance (T1.1.9). Idempotent:
/// a missing storage backend is logged but does not crash the host — the
/// readiness probe (T1.1.10) reflects MinIO availability instead.
/// </summary>
internal sealed class S3BucketInitializer(
    IAmazonS3 client,
    IOptions<S3StorageOptions> options,
    ILogger<S3BucketInitializer> logger) : IHostedService
{
    private readonly string _bucket = options.Value.BucketName;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (await AmazonS3Util.DoesS3BucketExistV2Async(client, _bucket))
            {
                logger.LogInformation("Object storage bucket {Bucket} already exists.", _bucket);
                return;
            }

            await client.PutBucketAsync(_bucket, cancellationToken);
            logger.LogInformation("Created object storage bucket {Bucket}.", _bucket);
        }
        catch (Exception e)
        {
            // Do not block startup if MinIO is briefly unavailable; readiness
            // will stay red until the dependency is reachable.
            logger.LogWarning(e, "Could not ensure object storage bucket {Bucket} at startup.", _bucket);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
