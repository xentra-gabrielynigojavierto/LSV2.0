using Documents.Domain.Enums;

namespace Documents.Domain.Entities;

public sealed class Document
{
    public Guid   Id               { get; set; }
    public Guid   TenantId         { get; set; }
    public string ProductId        { get; set; } = string.Empty;
    public string ReferenceId      { get; set; } = string.Empty;
    public string ReferenceType    { get; set; } = string.Empty;
    public Guid   DocumentTypeId   { get; set; }
    public string Title            { get; set; } = string.Empty;
    public string? Description     { get; set; }
    public DocumentStatus Status   { get; set; } = DocumentStatus.Draft;
    public string MimeType         { get; set; } = string.Empty;
    public long   FileSizeBytes    { get; set; }

    // Storage — never exposed in API responses
    public string StorageKey       { get; set; } = string.Empty;
    public string StorageBucket    { get; set; } = string.Empty;
    public string? Checksum        { get; set; }

    // Versioning
    public Guid?  CurrentVersionId { get; set; }
    public int    VersionCount     { get; set; }

    // Scan
    public ScanStatus   ScanStatus        { get; set; } = ScanStatus.Pending;
    public DateTime?    ScanCompletedAt   { get; set; }
    public int?         ScanDurationMs    { get; set; }
    public List<string> ScanThreats       { get; set; } = new();
    public string?      ScanEngineVersion { get; set; }

    // Published logo tracking — true only when explicitly registered via the logo workflow
    public bool IsPublishedAsLogo { get; set; }

    // Soft delete
    public bool     IsDeleted  { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid?    DeletedBy  { get; set; }

    // Compliance
    public DateTime? RetainUntil { get; set; }
    public DateTime? LegalHoldAt { get; set; }

    // Audit
    public DateTime CreatedAt  { get; set; }
    public Guid     CreatedBy  { get; set; }
    public DateTime UpdatedAt  { get; set; }
    public Guid     UpdatedBy  { get; set; }

    // Navigation
    public ICollection<DocumentVersion> Versions { get; set; } = new List<DocumentVersion>();
    public ICollection<DocumentAudit>   Audits   { get; set; } = new List<DocumentAudit>();

    public bool IsOnLegalHold => LegalHoldAt.HasValue;

    public static Document Create(
        Guid   tenantId,
        string productId,
        string referenceId,
        string referenceType,
        Guid   documentTypeId,
        string title,
        string? description,
        string mimeType,
        long   fileSizeBytes,
        string storageKey,
        string storageBucket,
        string? checksum,
        Guid   createdBy)
    {
        var now = DateTime.UtcNow;
        return new Document
        {
            Id             = Guid.NewGuid(),
            TenantId       = tenantId,
            ProductId      = productId,
            ReferenceId    = referenceId,
            ReferenceType  = referenceType,
            DocumentTypeId = documentTypeId,
            Title          = title,
            Description    = description,
            Status         = DocumentStatus.Draft,
            MimeType       = mimeType,
            FileSizeBytes  = fileSizeBytes,
            StorageKey     = storageKey,
            StorageBucket  = storageBucket,
            Checksum       = checksum,
            ScanStatus     = ScanStatus.Pending,
            IsDeleted      = false,
            CreatedAt      = now,
            CreatedBy      = createdBy,
            UpdatedAt      = now,
            UpdatedBy      = createdBy,
        };
    }
}
