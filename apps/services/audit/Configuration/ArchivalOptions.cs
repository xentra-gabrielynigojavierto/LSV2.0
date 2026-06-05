using PlatformAuditEventService.Enums;

namespace PlatformAuditEventService.Configuration;

/// <summary>
/// Configuration for the audit record archival pipeline.
/// Bound from the "Archival" section in appsettings.
/// Environment variable override prefix: Archival__
///
/// Archival is the step that moves or copies records from the primary (hot)
/// database to long-term cold storage before deletion from the primary store.
///
/// Strategy guide:
///   None      — skip archival; records are deleted directly (not recommended for HIPAA).
///   NoOp      — archival pipeline is wired but takes no action (safe default for dev).
///   LocalCopy — write to a local directory (dev / single-node deployments).
///   S3        — upload to AWS S3 (production recommended for cloud deployments).
///   AzureBlob — upload to Azure Blob Storage (production for Azure-hosted deployments).
///
/// Extension:
///   Implement <see cref="Services.Archival.IArchivalProvider"/> and register it
///   conditionally in Program.cs based on <see cref="Strategy"/>. No other code changes
///   are required — the <see cref="Services.RetentionService"/> uses the abstraction.
/// </summary>
public sealed class ArchivalOptions
{
    public const string SectionName = "Archival";

    // ── Strategy ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Which archival backend to use.
    /// Default: NoOp (logs what would be archived; writes nothing).
    /// </summary>
    public ArchivalStrategy Strategy { get; set; } = ArchivalStrategy.NoOp;

    // ── Batch control ─────────────────────────────────────────────────────────

    /// <summary>
    /// Number of records to archive per batch.
    /// Smaller batches reduce memory pressure and allow progress checkpointing.
    /// </summary>
    public int BatchSize { get; set; } = 10_000;

    // ── Local filesystem ──────────────────────────────────────────────────────

    /// <summary>
    /// Root directory for archival output when Strategy = LocalCopy.
    /// Default: "archive" (relative to working directory).
    /// </summary>
    public string LocalOutputPath { get; set; } = "archive";

    /// <summary>File name prefix for archived output files.</summary>
    public string FileNamePrefix { get; set; } = "audit-archive";

    // ── AWS S3 ────────────────────────────────────────────────────────────────

    /// <summary>S3 bucket name when Strategy = S3.</summary>
    public string? S3BucketName { get; set; }

    /// <summary>S3 key prefix (folder path) for archived files.</summary>
    public string? S3KeyPrefix { get; set; }

    /// <summary>AWS region for S3 provider. Example: "us-east-1".</summary>
    public string? AwsRegion { get; set; }

    // ── Azure Blob Storage ────────────────────────────────────────────────────

    /// <summary>
    /// Azure Blob Storage connection string when Strategy = AzureBlob.
    /// Inject via environment variable — never hardcode in appsettings.
    /// </summary>
    public string? AzureBlobConnectionString { get; set; }

    /// <summary>Azure Blob container name when Strategy = AzureBlob.</summary>
    public string? AzureContainerName { get; set; }
}
