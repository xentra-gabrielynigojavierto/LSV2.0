using BuildingBlocks.Domain;
using Liens.Domain.Enums;

namespace Liens.Domain.Entities;

public class ServicingItem : AuditableEntity
{
    public Guid Id               { get; private set; }
    public Guid TenantId         { get; private set; }
    public Guid OrgId            { get; private set; }

    public string TaskNumber     { get; private set; } = string.Empty;
    public string TaskType       { get; private set; } = string.Empty;
    public string Description    { get; private set; } = string.Empty;

    public string Status         { get; private set; } = ServicingStatus.Pending;
    public string Priority       { get; private set; } = ServicingPriority.Normal;

    public string AssignedTo     { get; private set; } = string.Empty;
    public Guid? AssignedToUserId { get; private set; }

    public Guid? CaseId          { get; private set; }
    public Guid? LienId          { get; private set; }

    public DateOnly? DueDate     { get; private set; }
    public string? Notes         { get; private set; }
    public string? Resolution    { get; private set; }

    public DateTime? StartedAtUtc   { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public DateTime? EscalatedAtUtc { get; private set; }

    private ServicingItem() { }

    public static ServicingItem Create(
        Guid tenantId,
        Guid orgId,
        string taskNumber,
        string taskType,
        string description,
        string assignedTo,
        Guid createdByUserId,
        string? priority = null,
        Guid? caseId = null,
        Guid? lienId = null,
        DateOnly? dueDate = null,
        string? notes = null,
        Guid? assignedToUserId = null)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (orgId == Guid.Empty) throw new ArgumentException("OrgId is required.", nameof(orgId));
        if (createdByUserId == Guid.Empty) throw new ArgumentException("CreatedByUserId is required.", nameof(createdByUserId));
        ArgumentException.ThrowIfNullOrWhiteSpace(taskNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(taskType);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentException.ThrowIfNullOrWhiteSpace(assignedTo);

        var resolvedPriority = priority ?? ServicingPriority.Normal;
        if (!ServicingPriority.All.Contains(resolvedPriority))
            throw new ArgumentException($"Invalid priority: '{resolvedPriority}'.");

        var now = DateTime.UtcNow;
        return new ServicingItem
        {
            Id               = Guid.NewGuid(),
            TenantId         = tenantId,
            OrgId            = orgId,
            TaskNumber       = taskNumber.Trim(),
            TaskType         = taskType.Trim(),
            Description      = description.Trim(),
            Status           = ServicingStatus.Pending,
            Priority         = resolvedPriority,
            AssignedTo       = assignedTo.Trim(),
            AssignedToUserId = assignedToUserId,
            CaseId           = caseId,
            LienId           = lienId,
            DueDate          = dueDate,
            Notes            = notes?.Trim(),
            CreatedByUserId  = createdByUserId,
            UpdatedByUserId  = createdByUserId,
            CreatedAtUtc     = now,
            UpdatedAtUtc     = now,
        };
    }

    public void Update(
        string taskType,
        string description,
        string assignedTo,
        Guid updatedByUserId,
        string? priority = null,
        Guid? caseId = null,
        Guid? lienId = null,
        DateOnly? dueDate = null,
        string? notes = null,
        Guid? assignedToUserId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskType);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentException.ThrowIfNullOrWhiteSpace(assignedTo);

        if (priority is not null && !ServicingPriority.All.Contains(priority))
            throw new ArgumentException($"Invalid priority: '{priority}'.");

        TaskType         = taskType.Trim();
        Description      = description.Trim();
        AssignedTo       = assignedTo.Trim();
        AssignedToUserId = assignedToUserId;
        if (priority is not null) Priority = priority;
        CaseId           = caseId;
        LienId           = lienId;
        DueDate          = dueDate;
        Notes            = notes?.Trim();
        UpdatedByUserId  = updatedByUserId;
        UpdatedAtUtc     = DateTime.UtcNow;
    }

    public void TransitionStatus(string newStatus, Guid updatedByUserId, string? resolution = null)
    {
        if (!ServicingStatus.All.Contains(newStatus))
            throw new ArgumentException($"Invalid servicing status: '{newStatus}'.");

        var now = DateTime.UtcNow;

        if (newStatus == ServicingStatus.InProgress && StartedAtUtc is null)
            StartedAtUtc = now;

        if (newStatus == ServicingStatus.Completed)
        {
            CompletedAtUtc = now;
            Resolution = resolution?.Trim() ?? Resolution;
        }

        if (newStatus == ServicingStatus.Escalated)
        {
            EscalatedAtUtc = now;
            Priority = ServicingPriority.Urgent;
        }

        Status          = newStatus;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = now;
    }

    public void Reassign(string assignedTo, Guid updatedByUserId, Guid? assignedToUserId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assignedTo);

        AssignedTo       = assignedTo.Trim();
        AssignedToUserId = assignedToUserId;
        UpdatedByUserId  = updatedByUserId;
        UpdatedAtUtc     = DateTime.UtcNow;
    }
}
