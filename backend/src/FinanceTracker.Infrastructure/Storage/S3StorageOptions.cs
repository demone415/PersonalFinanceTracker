using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Infrastructure.Storage;

/// <summary>
/// Binds the <c>S3</c> configuration section. Defaults target the local MinIO
/// container; in production these come from environment / secret manager
/// (see ARCHITECTURE.md §11.10).
/// </summary>
public sealed class S3StorageOptions
{
    public const string SectionName = "S3";

    /// <summary>S3 service endpoint (e.g. <c>http://minio:9000</c> for MinIO, empty for real AWS).</summary>
    [Required]
    public string Endpoint { get; set; } = string.Empty;

    [Required]
    public string AccessKey { get; set; } = string.Empty;

    [Required]
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Private bucket holding import/export files.</summary>
    [Required]
    public string BucketName { get; set; } = "finance-files";

    /// <summary>
    /// MinIO and most self-hosted S3 stores require path-style addressing
    /// (<c>host/bucket/key</c>) rather than virtual-hosted (<c>bucket.host/key</c>).
    /// </summary>
    public bool ForcePathStyle { get; set; } = true;
}
