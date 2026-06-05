using BuildingBlocks.Domain;

namespace CareConnect.Domain;

public class AppointmentAttachment : AuditableEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid AppointmentId { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long FileSizeBytes { get; private set; }
    public string? ExternalDocumentId { get; private set; }
    public string? ExternalStorageProvider { get; private set; }
    public string Status { get; private set; } = AttachmentStatus.Pending;
    public string? Notes { get; private set; }

    public Appointment? Appointment { get; private set; }

    private AppointmentAttachment() { }

    public static AppointmentAttachment Create(
        Guid tenantId,
        Guid appointmentId,
        string fileName,
        string contentType,
        long fileSizeBytes,
        string? externalDocumentId,
        string? externalStorageProvider,
        string status,
        string? notes,
        Guid? createdByUserId)
    {
        return new AppointmentAttachment
        {
            Id                       = Guid.NewGuid(),
            TenantId                 = tenantId,
            AppointmentId            = appointmentId,
            FileName                 = fileName.Trim(),
            ContentType              = contentType.Trim(),
            FileSizeBytes            = fileSizeBytes,
            ExternalDocumentId       = externalDocumentId?.Trim(),
            ExternalStorageProvider  = externalStorageProvider?.Trim(),
            Status                   = status,
            Notes                    = notes?.Trim(),
            CreatedByUserId          = createdByUserId,
            UpdatedByUserId          = createdByUserId,
            CreatedAtUtc             = DateTime.UtcNow,
            UpdatedAtUtc             = DateTime.UtcNow
        };
    }
}
