namespace Documents.Domain.Entities;

public sealed class DocumentAudit
{
    public Guid     Id            { get; set; }
    public Guid     TenantId      { get; set; }
    public Guid?    DocumentId    { get; set; }
    public string   Event         { get; set; } = string.Empty;
    public Guid?    ActorId       { get; set; }
    public string?  ActorEmail    { get; set; }
    public string   Outcome       { get; set; } = "SUCCESS";
    public string?  IpAddress     { get; set; }
    public string?  UserAgent     { get; set; }
    public string?  CorrelationId { get; set; }

    /// <summary>Arbitrary JSON detail payload stored as a string (maps to JSON column).</summary>
    public string?  Detail        { get; set; }

    public DateTime OccurredAt    { get; set; }

    // Navigation
    public Document? Document     { get; set; }
}
