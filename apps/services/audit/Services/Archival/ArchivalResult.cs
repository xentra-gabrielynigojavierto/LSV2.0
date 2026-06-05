namespace PlatformAuditEventService.Services.Archival;

/// <summary>
/// Result of a single <see cref="IArchivalProvider.ArchiveAsync"/> operation.
///
/// Returned regardless of success or failure. Callers should check
/// <see cref="IsSuccess"/> before treating <see cref="DestinationReference"/>
/// as a valid archive location.
/// </summary>
public sealed class ArchivalResult
{
    /// <summary>Number of records passed to (and processed by) the archival provider.</summary>
    public required long RecordsProcessed { get; init; }

    /// <summary>
    /// Number of records successfully archived.
    /// May differ from <see cref="RecordsProcessed"/> when partial failures occur.
    /// </summary>
    public required long RecordsArchived { get; init; }

    /// <summary>
    /// Stable reference to the archive destination.
    /// Local   → absolute file path.
    /// S3      → "{bucket}/{key}" or pre-signed URL.
    /// Azure   → blob URI.
    /// NoOp    → null (no write performed).
    /// </summary>
    public string? DestinationReference { get; init; }

    /// <summary>True when the archival completed without errors.</summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// Human-readable error description when <see cref="IsSuccess"/> is false.
    /// Null for successful operations.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// The provider's own name, echoed back for log correlation.
    /// Example: "NoOp", "Local", "S3", "AzureBlob".
    /// </summary>
    public required string ProviderName { get; init; }

    /// <summary>When the archival operation completed.</summary>
    public required DateTimeOffset CompletedAtUtc { get; init; }
}
