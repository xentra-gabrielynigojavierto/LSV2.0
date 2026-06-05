using BuildingBlocks.Domain;

namespace Task.Domain.Entities;

public class TaskNote : AuditableEntity
{
    public Guid   Id                { get; private set; }
    public Guid   TaskId            { get; private set; }
    public Guid   TenantId          { get; private set; }
    public string Note              { get; private set; } = string.Empty;

    /// <summary>Display name of the note author. Populated by consumer products (e.g. Liens) that track user display names.</summary>
    public string? AuthorName       { get; private set; }

    /// <summary>Whether the note has been edited since creation.</summary>
    public bool   IsEdited          { get; private set; }

    /// <summary>Whether the note has been soft-deleted.</summary>
    public bool   IsDeleted         { get; private set; }

    private TaskNote() { }

    public static TaskNote Create(
        Guid    taskId,
        Guid    tenantId,
        string  note,
        Guid    createdByUserId,
        string? authorName = null)
    {
        if (taskId == Guid.Empty)          throw new ArgumentException("TaskId is required.", nameof(taskId));
        if (tenantId == Guid.Empty)        throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (createdByUserId == Guid.Empty) throw new ArgumentException("CreatedByUserId is required.", nameof(createdByUserId));
        ArgumentException.ThrowIfNullOrWhiteSpace(note);

        var now = DateTime.UtcNow;
        return new TaskNote
        {
            Id              = Guid.NewGuid(),
            TaskId          = taskId,
            TenantId        = tenantId,
            Note            = note.Trim(),
            AuthorName      = authorName?.Trim(),
            IsEdited        = false,
            IsDeleted       = false,
            CreatedByUserId = createdByUserId,
            UpdatedByUserId = createdByUserId,
            CreatedAtUtc    = now,
            UpdatedAtUtc    = now,
        };
    }

    public void Edit(string newContent, Guid editorUserId)
    {
        if (editorUserId == Guid.Empty)
            throw new ArgumentException("EditorUserId is required.", nameof(editorUserId));
        ArgumentException.ThrowIfNullOrWhiteSpace(newContent);
        Note            = newContent.Trim();
        IsEdited        = true;
        UpdatedByUserId = editorUserId;
        UpdatedAtUtc    = DateTime.UtcNow;
    }

    public void SoftDelete(Guid deletedByUserId)
    {
        IsDeleted       = true;
        UpdatedByUserId = deletedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;
    }
}
