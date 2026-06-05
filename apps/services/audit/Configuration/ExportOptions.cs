namespace PlatformAuditEventService.Configuration;

/// <summary>
/// Audit event export configuration.
/// Bound from "Export" section in appsettings.
/// Environment variable override prefix: Export__
/// </summary>
public sealed class ExportOptions
{
    public const string SectionName = "Export";

    /// <summary>
    /// Export storage provider.
    /// Allowed values: "None" | "Local" | "S3" | "AzureBlob"
    /// "None" disables export endpoints.
    /// Environment variable: Export__Provider
    /// </summary>
    public string Provider { get; set; } = "None";

    /// <summary>
    /// Supported export formats.
    /// Allowed values: "Json", "Csv", "Ndjson"
    /// </summary>
    public List<string> SupportedFormats { get; set; } = ["Json", "Csv", "Ndjson"];

    /// <summary>
    /// Maximum number of records per export file.
    /// Exports exceeding this will be split into multiple files.
    /// </summary>
    public int MaxRecordsPerFile { get; set; } = 100_000;

    /// <summary>
    /// Local filesystem output directory when Provider = "Local".
    /// Must be writable by the service process.
    /// </summary>
    public string? LocalOutputPath { get; set; }

    /// <summary>
    /// S3 bucket name when Provider = "S3".
    /// </summary>
    public string? S3BucketName { get; set; }

    /// <summary>
    /// S3 key prefix (folder path) for exported files.
    /// </summary>
    public string? S3KeyPrefix { get; set; }

    /// <summary>
    /// AWS region for S3 provider.
    /// </summary>
    public string? AwsRegion { get; set; }

    /// <summary>
    /// Azure Blob Storage connection string when Provider = "AzureBlob".
    /// Inject via environment variable — never hardcode.
    /// </summary>
    public string? AzureBlobConnectionString { get; set; }

    /// <summary>
    /// Azure Blob container name when Provider = "AzureBlob".
    /// </summary>
    public string? AzureContainerName { get; set; }

    /// <summary>
    /// Export file name prefix. Final name: {Prefix}_{Timestamp}_{Page}.{ext}
    /// </summary>
    public string FileNamePrefix { get; set; } = "audit-export";

    // ── Background processing (ExportProcessingJob) ───────────────────────────

    /// <summary>
    /// How often the background export worker polls for pending jobs (in seconds).
    /// Default: 30.
    /// </summary>
    public int ProcessingIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum number of export jobs the background worker processes per tick.
    /// Default: 4.
    /// </summary>
    public int MaxConcurrentExports { get; set; } = 4;

    /// <summary>
    /// How long a job may remain in the Processing state before the worker considers it
    /// stalled and resets it to Pending for retry (in minutes).
    /// Default: 30.
    /// </summary>
    public int StalledJobTimeoutMinutes { get; set; } = 30;
}
