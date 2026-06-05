using BuildingBlocks.Domain;
using Liens.Domain.Enums;

namespace Liens.Domain.Entities;

public class LienTaskGenerationRule : AuditableEntity
{
    public Guid   Id                        { get; private set; }
    public Guid   TenantId                  { get; private set; }
    public string ProductCode               { get; private set; } = string.Empty;

    public string Name                      { get; private set; } = string.Empty;
    public string? Description              { get; private set; }

    public string EventType                 { get; private set; } = string.Empty;
    public Guid   TaskTemplateId            { get; private set; }

    public string ContextType               { get; private set; } = TaskTemplateContextType.General;
    public Guid?  ApplicableWorkflowStageId { get; private set; }

    public string DuplicatePreventionMode   { get; private set; } = Enums.DuplicatePreventionMode.Default;
    public string AssignmentMode            { get; private set; } = Enums.AssignmentMode.Default;
    public string DueDateMode               { get; private set; } = Enums.DueDateMode.Default;
    public int?   DueDateOffsetDays         { get; private set; }

    public bool   IsActive                  { get; private set; } = true;
    public int    Version                   { get; private set; } = 1;

    public DateTime LastUpdatedAt           { get; private set; }
    public Guid?   LastUpdatedByUserId      { get; private set; }
    public string? LastUpdatedByName        { get; private set; }
    public string  LastUpdatedSource        { get; private set; } = WorkflowUpdateSources.TenantProductSettings;

    private LienTaskGenerationRule() { }

    public static LienTaskGenerationRule Create(
        Guid   tenantId,
        string name,
        string eventType,
        Guid   taskTemplateId,
        string contextType,
        string updateSource,
        Guid   createdByUserId,
        string? description               = null,
        Guid?   applicableWorkflowStageId = null,
        string  duplicatePreventionMode   = Enums.DuplicatePreventionMode.Default,
        string  assignmentMode            = Enums.AssignmentMode.Default,
        string  dueDateMode               = Enums.DueDateMode.Default,
        int?    dueDateOffsetDays         = null,
        string? updatedByName             = null)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (taskTemplateId == Guid.Empty)
            throw new ArgumentException("TaskTemplateId is required.", nameof(taskTemplateId));
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!TaskGenerationEventType.All.Contains(eventType))
            throw new ArgumentException($"Invalid eventType: '{eventType}'.", nameof(eventType));
        if (!TaskTemplateContextType.All.Contains(contextType))
            throw new ArgumentException($"Invalid contextType: '{contextType}'.", nameof(contextType));
        if (!WorkflowUpdateSources.All.Contains(updateSource))
            throw new ArgumentException($"Invalid updateSource: '{updateSource}'.", nameof(updateSource));
        if (!Enums.DuplicatePreventionMode.All.Contains(duplicatePreventionMode))
            throw new ArgumentException($"Invalid duplicatePreventionMode: '{duplicatePreventionMode}'.", nameof(duplicatePreventionMode));
        if (!Enums.AssignmentMode.All.Contains(assignmentMode))
            throw new ArgumentException($"Invalid assignmentMode: '{assignmentMode}'.", nameof(assignmentMode));
        if (!Enums.DueDateMode.All.Contains(dueDateMode))
            throw new ArgumentException($"Invalid dueDateMode: '{dueDateMode}'.", nameof(dueDateMode));

        var now = DateTime.UtcNow;
        return new LienTaskGenerationRule
        {
            Id                        = Guid.NewGuid(),
            TenantId                  = tenantId,
            ProductCode               = "SYNQ_LIENS",
            Name                      = name.Trim(),
            Description               = description?.Trim(),
            EventType                 = eventType,
            TaskTemplateId            = taskTemplateId,
            ContextType               = contextType,
            ApplicableWorkflowStageId = applicableWorkflowStageId,
            DuplicatePreventionMode   = duplicatePreventionMode,
            AssignmentMode            = assignmentMode,
            DueDateMode               = dueDateMode,
            DueDateOffsetDays         = dueDateOffsetDays,
            IsActive                  = true,
            Version                   = 1,
            LastUpdatedAt             = now,
            LastUpdatedByUserId       = createdByUserId,
            LastUpdatedByName         = updatedByName,
            LastUpdatedSource         = updateSource,
            CreatedByUserId           = createdByUserId,
            UpdatedByUserId           = createdByUserId,
            CreatedAtUtc              = now,
            UpdatedAtUtc              = now,
        };
    }

    public void Update(
        string name,
        string? description,
        string eventType,
        Guid   taskTemplateId,
        string contextType,
        Guid?  applicableWorkflowStageId,
        string duplicatePreventionMode,
        string assignmentMode,
        string dueDateMode,
        int?   dueDateOffsetDays,
        string updateSource,
        Guid   updatedByUserId,
        int    expectedVersion,
        string? updatedByName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (taskTemplateId == Guid.Empty)
            throw new ArgumentException("TaskTemplateId is required.", nameof(taskTemplateId));
        if (!TaskGenerationEventType.All.Contains(eventType))
            throw new ArgumentException($"Invalid eventType: '{eventType}'.", nameof(eventType));
        if (!TaskTemplateContextType.All.Contains(contextType))
            throw new ArgumentException($"Invalid contextType: '{contextType}'.", nameof(contextType));
        if (!WorkflowUpdateSources.All.Contains(updateSource))
            throw new ArgumentException($"Invalid updateSource: '{updateSource}'.", nameof(updateSource));
        if (!Enums.DuplicatePreventionMode.All.Contains(duplicatePreventionMode))
            throw new ArgumentException($"Invalid duplicatePreventionMode.", nameof(duplicatePreventionMode));
        if (!Enums.AssignmentMode.All.Contains(assignmentMode))
            throw new ArgumentException($"Invalid assignmentMode.", nameof(assignmentMode));
        if (!Enums.DueDateMode.All.Contains(dueDateMode))
            throw new ArgumentException($"Invalid dueDateMode.", nameof(dueDateMode));
        if (Version != expectedVersion)
            throw new InvalidOperationException($"Version conflict — expected {Version}, got {expectedVersion}.");

        Name                      = name.Trim();
        Description               = description?.Trim();
        EventType                 = eventType;
        TaskTemplateId            = taskTemplateId;
        ContextType               = contextType;
        ApplicableWorkflowStageId = applicableWorkflowStageId;
        DuplicatePreventionMode   = duplicatePreventionMode;
        AssignmentMode            = assignmentMode;
        DueDateMode               = dueDateMode;
        DueDateOffsetDays         = dueDateOffsetDays;
        LastUpdatedSource         = updateSource;
        LastUpdatedByUserId       = updatedByUserId;
        LastUpdatedByName         = updatedByName;
        LastUpdatedAt             = DateTime.UtcNow;
        Version                   += 1;
        UpdatedByUserId           = updatedByUserId;
        UpdatedAtUtc              = DateTime.UtcNow;
    }

    public void Activate(Guid updatedByUserId, string updateSource, string? updatedByName = null)
    {
        if (!WorkflowUpdateSources.All.Contains(updateSource))
            throw new ArgumentException($"Invalid updateSource: '{updateSource}'.", nameof(updateSource));
        IsActive            = true;
        LastUpdatedByUserId = updatedByUserId;
        LastUpdatedByName   = updatedByName;
        LastUpdatedSource   = updateSource;
        LastUpdatedAt       = DateTime.UtcNow;
        Version             += 1;
        UpdatedByUserId     = updatedByUserId;
        UpdatedAtUtc        = DateTime.UtcNow;
    }

    public void Deactivate(Guid updatedByUserId, string updateSource, string? updatedByName = null)
    {
        if (!WorkflowUpdateSources.All.Contains(updateSource))
            throw new ArgumentException($"Invalid updateSource: '{updateSource}'.", nameof(updateSource));
        IsActive            = false;
        LastUpdatedByUserId = updatedByUserId;
        LastUpdatedByName   = updatedByName;
        LastUpdatedSource   = updateSource;
        LastUpdatedAt       = DateTime.UtcNow;
        Version             += 1;
        UpdatedByUserId     = updatedByUserId;
        UpdatedAtUtc        = DateTime.UtcNow;
    }
}
