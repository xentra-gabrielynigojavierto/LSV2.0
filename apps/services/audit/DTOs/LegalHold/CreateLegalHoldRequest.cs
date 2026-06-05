using System.ComponentModel.DataAnnotations;

namespace PlatformAuditEventService.DTOs.LegalHold;

/// <summary>
/// Request body for placing a new legal hold on an audit event record.
///
/// Route: POST /audit/records/{auditId}/legal-holds
/// </summary>
public sealed class CreateLegalHoldRequest
{
    /// <summary>
    /// Canonical legal authority reference.
    /// Examples: "litigation-hold-2026-001", "HIPAA-audit-2026", "subpoena-case-12345".
    /// </summary>
    [Required]
    [MaxLength(512)]
    public required string LegalAuthority { get; init; }

    /// <summary>
    /// Optional free-text notes about the hold.
    /// </summary>
    [MaxLength(2000)]
    public string? Notes { get; init; }
}
