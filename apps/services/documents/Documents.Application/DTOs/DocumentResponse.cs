using Documents.Domain.Entities;

namespace Documents.Application.DTOs;

public sealed class DocumentResponse
{
    public Guid     Id               { get; init; }
    public Guid     TenantId         { get; init; }
    public string   ProductId        { get; init; } = string.Empty;
    public string   ReferenceId      { get; init; } = string.Empty;
    public string   ReferenceType    { get; init; } = string.Empty;
    public Guid     DocumentTypeId   { get; init; }
    public string   Title            { get; init; } = string.Empty;
    public string?  Description      { get; init; }
    public string   Status           { get; init; } = string.Empty;
    public string   MimeType         { get; init; } = string.Empty;
    public long     FileSizeBytes    { get; init; }
    public Guid?    CurrentVersionId { get; init; }
    public int      VersionCount     { get; init; }
    public string   ScanStatus       { get; init; } = string.Empty;
    public DateTime? ScanCompletedAt { get; init; }
    public List<string> ScanThreats  { get; init; } = new();
    public bool     IsDeleted        { get; init; }
    public DateTime? DeletedAt       { get; init; }
    public Guid?    DeletedBy        { get; init; }
    public DateTime? RetainUntil     { get; init; }
    public DateTime? LegalHoldAt     { get; init; }
    public DateTime CreatedAt        { get; init; }
    public Guid     CreatedBy        { get; init; }
    public DateTime UpdatedAt        { get; init; }
    public Guid     UpdatedBy        { get; init; }

    // storageKey, storageBucket, and checksum are intentionally omitted — never exposed to clients

    public static DocumentResponse From(Document doc) => new()
    {
        Id               = doc.Id,
        TenantId         = doc.TenantId,
        ProductId        = doc.ProductId,
        ReferenceId      = doc.ReferenceId,
        ReferenceType    = doc.ReferenceType,
        DocumentTypeId   = doc.DocumentTypeId,
        Title            = doc.Title,
        Description      = doc.Description,
        Status           = doc.Status.ToString().ToUpperInvariant(),
        MimeType         = doc.MimeType,
        FileSizeBytes    = doc.FileSizeBytes,
        CurrentVersionId = doc.CurrentVersionId,
        VersionCount     = doc.VersionCount,
        ScanStatus       = doc.ScanStatus.ToString().ToUpperInvariant(),
        ScanCompletedAt  = doc.ScanCompletedAt,
        ScanThreats      = doc.ScanThreats,
        IsDeleted        = doc.IsDeleted,
        DeletedAt        = doc.DeletedAt,
        DeletedBy        = doc.DeletedBy,
        RetainUntil      = doc.RetainUntil,
        LegalHoldAt      = doc.LegalHoldAt,
        CreatedAt        = doc.CreatedAt,
        CreatedBy        = doc.CreatedBy,
        UpdatedAt        = doc.UpdatedAt,
        UpdatedBy        = doc.UpdatedBy,
    };
}

public sealed class DocumentListResponse
{
    public IReadOnlyList<DocumentResponse> Data   { get; init; } = Array.Empty<DocumentResponse>();
    public int                             Total  { get; init; }
    public int                             Limit  { get; init; }
    public int                             Offset { get; init; }
}
