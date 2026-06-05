namespace PlatformAuditEventService.Services.Export;

/// <summary>
/// Abstraction over the physical file storage layer used by the export pipeline.
///
/// v1 ships with <see cref="LocalExportStorageProvider"/> (Provider=Local).
/// Future implementations replace this contract without changing callers:
///
///   Provider=S3          → upload to AWS S3; return the S3 object key or pre-signed URL.
///   Provider=AzureBlob   → upload to Azure Blob Storage; return the blob URI.
///   Provider=GCS         → upload to Google Cloud Storage; return the object URL.
///
/// Implementations are responsible for:
///   - Determining the output file name (using ExportId + Format + timestamp).
///   - Creating destination directories / buckets as needed.
///   - Writing the content via the <c>writeContent</c> callback passed to <see cref="WriteAsync"/>.
///   - Returning a stable reference (path or URL) that the service persists in
///     <c>AuditExportJob.FilePath</c> and surfaces in <c>ExportStatusResponse.DownloadUrl</c>.
/// </summary>
public interface IExportStorageProvider
{
    /// <summary>Short label identifying the backing store. Used in log messages.</summary>
    string ProviderName { get; }

    /// <summary>
    /// Create the output file and invoke <paramref name="writeContent"/> to fill it.
    ///
    /// The provider opens (or creates) the destination stream and passes it to the
    /// callback. The callback is responsible for writing all bytes; the provider
    /// flushes and closes the stream when the callback completes.
    ///
    /// Returns a stable reference to the written file:
    ///   Local   → absolute or relative file-system path.
    ///   S3      → "{bucket}/{key}" or a pre-signed URL depending on options.
    ///   Azure   → blob URI or SAS URL.
    ///
    /// The returned value is stored in <c>AuditExportJob.FilePath</c> and echoed
    /// back in the API response as <c>DownloadUrl</c>.
    /// </summary>
    Task<string> WriteAsync(
        Guid              exportId,
        string            format,
        Func<Stream, Task> writeContent,
        CancellationToken ct = default);
}
