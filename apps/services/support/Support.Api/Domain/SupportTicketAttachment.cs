namespace Support.Api.Domain;

public class SupportTicketAttachment
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public string TenantId { get; set; } = default!;
    public string DocumentId { get; set; } = default!;
    public string FileName { get; set; } = default!;
    public string? ContentType { get; set; }
    public long? FileSizeBytes { get; set; }
    public string? UploadedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SupportTicketProductRef
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public string TenantId { get; set; } = default!;
    public string ProductCode { get; set; } = default!;
    public string EntityType { get; set; } = default!;
    public string EntityId { get; set; } = default!;
    public string? DisplayLabel { get; set; }
    public string? MetadataJson { get; set; }
    public string? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
}
