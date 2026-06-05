namespace PlatformAuditEventService.Services.Archival;

/// <summary>
/// Immutable descriptor for a single archival operation.
///
/// Passed to <see cref="IArchivalProvider.ArchiveAsync"/> so the provider can
/// embed meaningful metadata into the archive file name or object key.
///
/// The context identifies what time window is being archived, which tenant/category
/// the records belong to, and who/what initiated the job.
/// </summary>
public sealed class ArchivalContext
{
    /// <summary>
    /// Unique identifier for this archival job.
    /// Used in file names and log correlation.
    /// </summary>
    public required string ArchiveJobId { get; init; }

    /// <summary>
    /// Start of the retention window being archived (inclusive, RecordedAtUtc).
    /// </summary>
    public required DateTimeOffset WindowFrom { get; init; }

    /// <summary>
    /// End of the retention window being archived (exclusive, RecordedAtUtc).
    /// </summary>
    public required DateTimeOffset WindowTo { get; init; }

    /// <summary>
    /// Tenant scoped to this archival operation. Null for platform-wide archival.
    /// </summary>
    public string? TenantId { get; init; }

    /// <summary>
    /// Event category scoped to this archival operation. Null for all categories.
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    /// Actor or service that initiated this archival job.
    /// Example: "RetentionPolicyJob", "compliance-operator@example.com".
    /// </summary>
    public required string InitiatedBy { get; init; }

    /// <summary>When the archival job was initiated.</summary>
    public required DateTimeOffset InitiatedAtUtc { get; init; }
}
