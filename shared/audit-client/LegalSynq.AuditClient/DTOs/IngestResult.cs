namespace LegalSynq.AuditClient.DTOs;

public sealed record IngestResult(
    bool    Accepted,
    string? AuditId,
    string? RejectionReason,
    int     StatusCode);
