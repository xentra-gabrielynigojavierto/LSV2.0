using BuildingBlocks.Domain;

namespace CareConnect.Domain;

public class ReferralAttachment : AuditableEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ReferralId { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long FileSizeBytes { get; private set; }
    public string? ExternalDocumentId { get; private set; }
    public string? ExternalStorageProvider { get; private set; }
    public string Status { get; private set; } = AttachmentStatus.Pending;
    public string? Notes { get; private set; }

    public Referral? Referral { get; private set; }

    private ReferralAttachment() { }

    public static ReferralAttachment Create(
        Guid tenantId,
        Guid referralId,
        string fileName,
        string contentType,
        long fileSizeBytes,
        string? externalDocumentId,
        string? externalStorageProvider,
        string status,
        string? notes,
        Guid? createdByUserId)
    {
        return new ReferralAttachment
        {
            Id                       = Guid.NewGuid(),
            TenantId                 = tenantId,
            ReferralId               = referralId,
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
