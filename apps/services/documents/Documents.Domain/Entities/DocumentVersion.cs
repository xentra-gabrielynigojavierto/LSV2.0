using Documents.Domain.Enums;

namespace Documents.Domain.Entities;

public sealed class DocumentVersion
{
    public Guid   Id               { get; set; }
    public Guid   DocumentId       { get; set; }
    public Guid   TenantId         { get; set; }
    public int    VersionNumber     { get; set; }
    public string MimeType         { get; set; } = string.Empty;
    public long   FileSizeBytes    { get; set; }

    // Storage — never exposed in API responses
    public string StorageKey       { get; set; } = string.Empty;
    public string StorageBucket    { get; set; } = string.Empty;
    public string? Checksum        { get; set; }

    // Scan
    public ScanStatus ScanStatus        { get; set; } = ScanStatus.Pending;
    public DateTime?  ScanCompletedAt   { get; set; }
    public int?       ScanDurationMs    { get; set; }
    public List<string> ScanThreats     { get; set; } = new();
    public string?    ScanEngineVersion { get; set; }

    // Metadata
    public string?  Label      { get; set; }
    public bool     IsDeleted  { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid?    DeletedBy  { get; set; }

    // Audit
    public DateTime UploadedAt { get; set; }
    public Guid     UploadedBy { get; set; }

    // Navigation
    public Document Document { get; set; } = null!;
}
