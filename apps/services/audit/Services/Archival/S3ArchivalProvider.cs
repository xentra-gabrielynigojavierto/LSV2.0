using Microsoft.Extensions.Options;
using PlatformAuditEventService.Configuration;
using PlatformAuditEventService.Entities;

namespace PlatformAuditEventService.Services.Archival;

/// <summary>
/// AWS S3 archival provider stub.
///
/// To activate this provider:
///   1. Add the AWSSDK.S3 NuGet package:
///      dotnet add package AWSSDK.S3
///   2. Configure Archival:S3BucketName, Archival:S3KeyPrefix, and Archival:AwsRegion.
///   3. Ensure the service role has s3:PutObject, s3:GetObject on the target bucket.
///   4. Replace the NotSupportedException body with the real S3 upload logic.
///   5. Register this class in Program.cs instead of NoOpArchivalProvider or LocalArchivalProvider:
///      builder.Services.AddSingleton&lt;IArchivalProvider, S3ArchivalProvider&gt;();
///
/// Recommended implementation pattern:
///   - Instantiate AmazonS3Client using the ambient IAM role credentials (no keys in code).
///   - Stream records as NDJSON using TransferUtility.UploadAsync for multi-part uploads.
///   - Use S3 Object Lock (WORM) on the target bucket for HIPAA tamper-resistance.
///   - Key pattern: {KeyPrefix}/{tenantId}/{yyyyMMdd}/{archiveJobId}.ndjson
///
/// Security notes (HIPAA):
///   - Enable S3 server-side encryption (SSE-S3 or SSE-KMS).
///   - Enable S3 access logging on the target bucket.
///   - Use a dedicated archival bucket separate from application data.
///   - Apply a bucket policy that denies DeleteObject to all principals.
/// </summary>
public sealed class S3ArchivalProvider : IArchivalProvider
{
    private readonly ArchivalOptions                _opts;
    private readonly ILogger<S3ArchivalProvider>   _logger;

    public S3ArchivalProvider(
        IOptions<ArchivalOptions>      opts,
        ILogger<S3ArchivalProvider>    logger)
    {
        _opts   = opts.Value;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_opts.S3BucketName))
        {
            throw new InvalidOperationException(
                "S3ArchivalProvider requires Archival:S3BucketName to be configured. " +
                "Set Archival__S3BucketName environment variable.");
        }
    }

    /// <inheritdoc/>
    public string ProviderName => "S3";

    /// <inheritdoc/>
    public Task<ArchivalResult> ArchiveAsync(
        IAsyncEnumerable<AuditEventRecord> records,
        ArchivalContext                     context,
        CancellationToken                   ct = default)
    {
        _logger.LogError(
            "S3ArchivalProvider: S3 upload is not yet implemented. " +
            "Add AWSSDK.S3 package and implement the upload logic in this class. " +
            "See class-level documentation for instructions. " +
            "JobId={JobId} Bucket={Bucket}",
            context.ArchiveJobId, _opts.S3BucketName);

        throw new NotSupportedException(
            "S3ArchivalProvider: S3 upload is not implemented. " +
            "Add AWSSDK.S3 NuGet package and implement ArchiveAsync. " +
            "See Services/Archival/S3ArchivalProvider.cs for instructions.");
    }
}
