using System.ComponentModel.DataAnnotations;

namespace PlatformAuditEventService.DTOs.LegalHold;

/// <summary>
/// Request body for releasing an active legal hold.
///
/// Route: POST /audit/legal-holds/{holdId}/release
/// </summary>
public sealed class ReleaseLegalHoldRequest
{
    /// <summary>
    /// Optional notes explaining the reason for release.
    /// </summary>
    [MaxLength(2000)]
    public string? ReleaseNotes { get; init; }
}
