using Liens.Domain.Enums;

namespace Liens.Domain.Entities;

public class LienCaseNote
{
    public Guid   Id              { get; private set; }
    public Guid   CaseId          { get; private set; }
    public Guid   TenantId        { get; private set; }

    public string Content         { get; private set; } = string.Empty;
    public string Category        { get; private set; } = CaseNoteCategory.General;

    public bool   IsPinned        { get; private set; }

    public Guid   CreatedByUserId { get; private set; }
    public string CreatedByName   { get; private set; } = string.Empty;

    public bool   IsEdited        { get; private set; }
    public bool   IsDeleted       { get; private set; }

    public DateTime  CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    private LienCaseNote() { }

    public static LienCaseNote Create(
        Guid   caseId,
        Guid   tenantId,
        string content,
        string category,
        Guid   createdByUserId,
        string createdByName)
    {
        if (caseId == Guid.Empty)         throw new ArgumentException("CaseId is required.",         nameof(caseId));
        if (tenantId == Guid.Empty)       throw new ArgumentException("TenantId is required.",       nameof(tenantId));
        if (createdByUserId == Guid.Empty) throw new ArgumentException("CreatedByUserId is required.", nameof(createdByUserId));
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        var resolvedCategory = CaseNoteCategory.All.Contains(category) ? category : CaseNoteCategory.General;

        return new LienCaseNote
        {
            Id              = Guid.NewGuid(),
            CaseId          = caseId,
            TenantId        = tenantId,
            Content         = content.Trim(),
            Category        = resolvedCategory,
            IsPinned        = false,
            CreatedByUserId = createdByUserId,
            CreatedByName   = createdByName.Trim(),
            IsEdited        = false,
            IsDeleted       = false,
            CreatedAtUtc    = DateTime.UtcNow,
            UpdatedAtUtc    = null,
        };
    }

    public void Edit(string newContent, string? newCategory, Guid editorUserId)
    {
        if (editorUserId == Guid.Empty)
            throw new ArgumentException("EditorUserId is required.", nameof(editorUserId));
        ArgumentException.ThrowIfNullOrWhiteSpace(newContent);

        Content  = newContent.Trim();
        IsEdited = true;

        if (newCategory != null && CaseNoteCategory.All.Contains(newCategory))
            Category = newCategory;

        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Pin()
    {
        IsPinned     = true;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Unpin()
    {
        IsPinned     = false;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SoftDelete()
    {
        IsDeleted    = true;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
