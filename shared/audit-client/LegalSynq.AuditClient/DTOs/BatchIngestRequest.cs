namespace LegalSynq.AuditClient.DTOs;

public sealed class BatchIngestRequest
{
    public List<IngestAuditEventRequest> Events { get; set; } = [];
}
