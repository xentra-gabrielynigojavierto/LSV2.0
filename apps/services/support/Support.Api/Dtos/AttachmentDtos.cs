using Support.Api.Domain;

namespace Support.Api.Dtos;

public class CreateTicketAttachmentRequest
{
    public string DocumentId { get; set; } = default!;
    public string FileName { get; set; } = default!;
    public string? ContentType { get; set; }
    public long? FileSizeBytes { get; set; }
    public string? UploadedByUserId { get; set; }
}

public class TicketAttachmentResponse
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public string DocumentId { get; set; } = default!;
    public string FileName { get; set; } = default!;
    public string? ContentType { get; set; }
    public long? FileSizeBytes { get; set; }
    public string? UploadedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }

    public static TicketAttachmentResponse From(SupportTicketAttachment a) => new()
    {
        Id = a.Id,
        TicketId = a.TicketId,
        DocumentId = a.DocumentId,
        FileName = a.FileName,
        ContentType = a.ContentType,
        FileSizeBytes = a.FileSizeBytes,
        UploadedByUserId = a.UploadedByUserId,
        CreatedAt = a.CreatedAt,
    };
}
