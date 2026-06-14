using Amazon.S3;
using Amazon.S3.Util;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Infrastructure.Storage;

/// <summary>
/// Readiness probe for the S3/MinIO object store (T1.1.10). Confirms the backing
/// store is reachable and the <c>finance-files</c> bucket is present, reusing the
/// already-configured <see cref="IAmazonS3"/> client (no extra dependency).
/// </summary>
internal sealed class MinioHealthCheck(IAmazonS3 client, IOptions<S3StorageOptions> options) : IHealthCheck
{
    private readonly string _bucket = options.Value.BucketName;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            bool exists = await AmazonS3Util.DoesS3BucketExistV2Async(client, _bucket);
            return exists
                ? HealthCheckResult.Healthy($"Bucket '{_bucket}' is reachable.")
                : new HealthCheckResult(context.Registration.FailureStatus, $"Bucket '{_bucket}' not found.");
        }
        catch (Exception e)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, "S3/MinIO is unreachable.", e);
        }
    }
}
