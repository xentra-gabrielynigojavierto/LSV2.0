namespace PlatformAuditEventService.DTOs.Ingest;

/// <summary>
/// Per-item result within a <see cref="BatchIngestResponse"/>.
/// Callers should inspect Accepted to determine per-item outcome.
/// </summary>
public sealed class IngestItemResult
{
    /// <summary>
    /// Zero-based position of this item in the original Events list.
    /// Use to correlate results back to submitted items.
    /// </summary>
    public int Index { get; init; }

    /// <summary>
    /// Echo of the EventType from the submitted item.
    /// Aids correlation when results are inspected without the original request.
    /// </summary>
    public string? EventType { get; init; }

    /// <summary>
    /// Echo of the IdempotencyKey from the submitted item, if provided.
    /// </summary>
    public string? IdempotencyKey { get; init; }

    /// <summary>
    /// True if the item passed validation and was successfully persisted.
    /// False if validation failed, a duplicate was detected, or persistence failed.
    /// </summary>
    public bool Accepted { get; init; }

    /// <summary>
    /// Platform-assigned AuditId for the persisted record.
    /// Null when Accepted is false.
    /// Callers should store this for later retrieval or correlation.
    /// </summary>
    public Guid? AuditId { get; init; }

    /// <summary>
    /// Machine-readable rejection code when Accepted is false.
    /// Values: "ValidationFailed" | "DuplicateIdempotencyKey" | "PersistenceError" | "Skipped"
    /// </summary>
    public string? RejectionReason { get; init; }

    /// <summary>
    /// Human-readable field-level validation errors when RejectionReason is "ValidationFailed".
    /// Empty for accepted items or non-validation rejections.
    /// </summary>
    public IReadOnlyList<string> ValidationErrors { get; init; } = [];
}
