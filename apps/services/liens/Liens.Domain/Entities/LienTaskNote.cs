namespace Liens.Domain.Entities;

public class LienTaskNote
{
    public Guid   Id              { get; private set; }
    public Guid   TaskId          { get; private set; }
    public Guid   TenantId        { get; private set; }

    public string Content         { get; private set; } = string.Empty;
    public Guid   CreatedByUserId { get; private set; }
    public string CreatedByName   { get; private set; } = string.Empty;

    public bool   IsEdited        { get; private set; }
    public bool   IsDeleted       { get; private set; }

    public DateTime CreatedAtUtc  { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    private LienTaskNote() { }

    public static LienTaskNote Create(
        Guid   taskId,
        Guid   tenantId,
        string content,
        Guid   createdByUserId,
        string createdByName)
    {
        if (taskId == Guid.Empty)     throw new ArgumentException("TaskId is required.",     nameof(taskId));
        if (tenantId == Guid.Empty)   throw new ArgumentException("TenantId is required.",   nameof(tenantId));
        if (createdByUserId == Guid.Empty)
            throw new ArgumentException("CreatedByUserId is required.", nameof(createdByUserId));
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        return new LienTaskNote
        {
            Id              = Guid.NewGuid(),
            TaskId          = taskId,
            TenantId        = tenantId,
            Content         = content.Trim(),
            CreatedByUserId = createdByUserId,
            CreatedByName   = createdByName.Trim(),
            IsEdited        = false,
            IsDeleted       = false,
            CreatedAtUtc    = DateTime.UtcNow,
            UpdatedAtUtc    = null,
        };
    }

    public void Edit(string newContent, Guid editorUserId)
    {
        if (editorUserId == Guid.Empty)
            throw new ArgumentException("EditorUserId is required.", nameof(editorUserId));
        ArgumentException.ThrowIfNullOrWhiteSpace(newContent);

        Content      = newContent.Trim();
        IsEdited     = true;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SoftDelete()
    {
        IsDeleted    = true;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
