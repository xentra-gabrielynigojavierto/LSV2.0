namespace PlatformAuditEventService.Enums;

/// <summary>
/// Determines how the archival pipeline stores records before they are
/// removed from the primary (hot) database.
///
/// Configured via <c>Archival:Strategy</c> in appsettings.
/// The registered <see cref="Services.Archival.IArchivalProvider"/> must match
/// the chosen strategy; mismatches are caught at startup.
///
/// Current support:
///   None, NoOp — v1 (foundation only; no actual writes)
///
/// Planned:
///   LocalCopy, S3, AzureBlob — same pattern as <see cref="Services.Export.IExportStorageProvider"/>
/// </summary>
public enum ArchivalStrategy
{
    /// <summary>
    /// No archival. Records eligible for deletion are purged directly.
    /// Use only when regulatory requirements do not mandate long-term cold storage.
    /// </summary>
    None = 0,

    /// <summary>
    /// Archival is enabled but performed by a no-op provider.
    /// Used during development and as a safe default when archival infra is not yet configured.
    /// Logs what would be archived without writing any data.
    /// </summary>
    NoOp = 1,

    /// <summary>Local filesystem archival — mirrors the Local export provider pattern.</summary>
    LocalCopy = 2,

    /// <summary>Archive to AWS S3. Requires Archival:S3BucketName and AWS credentials.</summary>
    S3 = 3,

    /// <summary>Archive to Azure Blob Storage. Requires Archival:AzureBlobConnectionString.</summary>
    AzureBlob = 4,
}
