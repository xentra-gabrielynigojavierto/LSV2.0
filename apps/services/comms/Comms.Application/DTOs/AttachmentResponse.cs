namespace Comms.Application.DTOs;

public record AttachmentResponse(
    Guid Id,
    Guid MessageId,
    Guid DocumentId,
    string FileName,
    string ContentType,
    long? FileSizeBytes,
    bool IsActive,
    DateTime CreatedAtUtc,
    Guid? CreatedByUserId);
