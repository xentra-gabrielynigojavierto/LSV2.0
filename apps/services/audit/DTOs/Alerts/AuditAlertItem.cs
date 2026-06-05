namespace PlatformAuditEventService.DTOs.Alerts;

/// <summary>
/// Represents a single alert record in an API response.
///
/// Maps directly to <see cref="PlatformAuditEventService.Entities.AuditAlert"/>.
/// </summary>
public sealed class AuditAlertItem
{
    public Guid   AlertId             { get; set; }
    public string RuleKey             { get; set; } = string.Empty;
    public string ScopeType           { get; set; } = string.Empty;
    public string? TenantId           { get; set; }
    public string Severity            { get; set; } = string.Empty;
    public string Status              { get; set; } = string.Empty;
    public string Title               { get; set; } = string.Empty;
    public string Description         { get; set; } = string.Empty;
    public string? DrillDownPath      { get; set; }
    public string? ContextJson        { get; set; }

    public DateTimeOffset FirstDetectedAtUtc { get; set; }
    public DateTimeOffset LastDetectedAtUtc  { get; set; }
    public int            DetectionCount     { get; set; }

    public DateTimeOffset? AcknowledgedAtUtc { get; set; }
    public string?         AcknowledgedBy    { get; set; }
    public DateTimeOffset? ResolvedAtUtc     { get; set; }
    public string?         ResolvedBy        { get; set; }
}
