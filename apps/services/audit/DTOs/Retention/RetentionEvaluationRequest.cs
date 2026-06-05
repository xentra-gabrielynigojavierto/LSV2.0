namespace PlatformAuditEventService.DTOs.Retention;

/// <summary>
/// Parameters for a retention policy evaluation scan.
///
/// Evaluation is always a dry-run in v1 — it classifies records and reports
/// counts by storage tier without modifying or deleting any data.
///
/// Scoping: pass <see cref="TenantId"/> or <see cref="Category"/> to limit the
/// scan to a specific slice. Pass null for both to evaluate across all records.
///
/// Performance: <see cref="SampleLimit"/> caps how many records are pulled
/// from the database for tier classification. Total record counts are always
/// fetched via a fast aggregate query regardless of the sample limit.
/// </summary>
public sealed class RetentionEvaluationRequest
{
    /// <summary>
    /// Scope the evaluation to a specific tenant.
    /// Null = evaluate across all tenants (PlatformAdmin context).
    /// </summary>
    public string? TenantId { get; init; }

    /// <summary>
    /// Scope the evaluation to a specific event category.
    /// Must match a value from <see cref="Enums.EventCategory"/> when provided.
    /// Null = evaluate all categories.
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    /// Maximum number of records to pull for tier classification.
    /// Records are sampled oldest-first (ascending <c>RecordedAtUtc</c>) to
    /// prioritize identifying expired records.
    ///
    /// Default: 5000. Set to 0 to skip sampling and return only total counts.
    /// </summary>
    public int SampleLimit { get; init; } = 5_000;
}
