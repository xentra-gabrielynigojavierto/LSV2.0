namespace PlatformAuditEventService.DTOs.Integrity;

/// <summary>
/// Query parameters for <c>GET /audit/integrity/checkpoints</c>.
/// All fields are optional — omitting a field returns all matching records.
/// </summary>
public sealed class CheckpointListQuery
{
    /// <summary>
    /// Filter by checkpoint type label (e.g. "hourly", "daily", "manual").
    /// When null or empty, all types are returned.
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Filter to checkpoints created at or after this timestamp (inclusive).
    /// Based on <c>IntegrityCheckpoint.CreatedAtUtc</c>.
    /// </summary>
    public DateTimeOffset? From { get; set; }

    /// <summary>
    /// Filter to checkpoints created at or before this timestamp (inclusive).
    /// Based on <c>IntegrityCheckpoint.CreatedAtUtc</c>.
    /// </summary>
    public DateTimeOffset? To { get; set; }

    /// <summary>Page number (1-indexed). Defaults to 1.</summary>
    public int Page { get; set; } = 1;

    /// <summary>Records per page. Capped at 200 by the service. Defaults to 20.</summary>
    public int PageSize { get; set; } = 20;
}
