using PlatformAuditEventService.Entities;

namespace PlatformAuditEventService.Services.Archival;

/// <summary>
/// Abstraction over the cold-storage backend used when archiving audit records
/// before deletion from the primary (hot) database.
///
/// v1 ships with <see cref="NoOpArchivalProvider"/> (Strategy=NoOp), which logs
/// what would be archived without writing any data. This is the correct default
/// for an enterprise system where archival infrastructure is configured separately
/// from the application itself.
///
/// Future implementations follow the same pattern as <see cref="Export.IExportStorageProvider"/>:
///
///   Strategy=LocalCopy   → write to a local directory structure.
///   Strategy=S3          → upload to AWS S3; return the object key.
///   Strategy=AzureBlob   → upload to Azure Blob Storage; return the blob URI.
///
/// Registration:
///   Register exactly one <c>IArchivalProvider</c> in Program.cs, selected by
///   <c>Archival:Strategy</c>. The <see cref="RetentionService"/> depends only
///   on this interface — swapping the provider requires no service-layer changes.
///
/// Legal hold compatibility (future):
///   Before calling <see cref="ArchiveAsync"/>, the retention pipeline must filter
///   out records that are on legal hold. The <c>IArchivalProvider</c> contract
///   does not enforce hold checks — that is the caller's responsibility.
/// </summary>
public interface IArchivalProvider
{
    /// <summary>Short label identifying the backing store. Used in log messages and results.</summary>
    string ProviderName { get; }

    /// <summary>
    /// Archive the provided records to cold storage.
    ///
    /// Implementations must consume <paramref name="records"/> fully (or until
    /// <paramref name="ct"/> is cancelled) and report the exact count of records
    /// processed vs. archived.
    ///
    /// Callers are responsible for:
    ///   1. Filtering out legal-hold records before streaming.
    ///   2. Committing deletion only after this method returns with
    ///      <see cref="ArchivalResult.IsSuccess"/> = true.
    ///
    /// The provider must NOT modify or delete records from the primary database.
    /// </summary>
    Task<ArchivalResult> ArchiveAsync(
        IAsyncEnumerable<AuditEventRecord> records,
        ArchivalContext                     context,
        CancellationToken                   ct = default);
}
