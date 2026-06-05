namespace PlatformAuditEventService.DTOs.LegalHold;

/// <summary>
/// API response shape for a legal hold record.
/// </summary>
public sealed class LegalHoldResponse
{
    public Guid   HoldId          { get; init; }
    public Guid   AuditId         { get; init; }
    public string HeldByUserId    { get; init; } = string.Empty;
    public DateTimeOffset HeldAtUtc { get; init; }
    public DateTimeOffset? ReleasedAtUtc  { get; init; }
    public string? ReleasedByUserId       { get; init; }
    public string LegalAuthority          { get; init; } = string.Empty;
    public string? Notes                  { get; init; }

    /// <summary>True when ReleasedAtUtc is null.</summary>
    public bool IsActive => ReleasedAtUtc is null;
}
