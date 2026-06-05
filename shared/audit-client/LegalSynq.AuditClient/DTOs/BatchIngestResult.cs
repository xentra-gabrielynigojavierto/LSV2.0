namespace LegalSynq.AuditClient.DTOs;

public sealed record BatchIngestResult(
    int                    Submitted,
    int                    Accepted,
    int                    Rejected,
    IReadOnlyList<IngestResult> Results);
