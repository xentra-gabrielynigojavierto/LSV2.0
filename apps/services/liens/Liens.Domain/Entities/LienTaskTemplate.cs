using BuildingBlocks.Domain;
using Liens.Domain.Enums;

namespace Liens.Domain.Entities;

public class LienTaskTemplate : AuditableEntity
{
    public Guid   Id                        { get; private set; }
    public Guid   TenantId                  { get; private set; }
    public string ProductCode               { get; private set; } = string.Empty;

    public string Name                      { get; private set; } = string.Empty;
    public string? Description              { get; private set; }

    public string DefaultTitle              { get; private set; } = string.Empty;
    public string? DefaultDescription       { get; private set; }
    public string DefaultPriority           { get; private set; } = "MEDIUM";
    public int?   DefaultDueOffsetDays      { get; private set; }
    public string? DefaultRoleId            { get; private set; }

    public string ContextType               { get; private set; } = TaskTemplateContextType.General;
    public Guid?  ApplicableWorkflowStageId { get; private set; }

    public bool   IsActive                  { get; private set; } = true;
    public int    Version                   { get; private set; } = 1;

    public DateTime LastUpdatedAt           { get; private set; }
    public Guid?   LastUpdatedByUserId      { get; private set; }
    public string? LastUpdatedByName        { get; private set; }
    public string  LastUpdatedSource        { get; private set; } = WorkflowUpdateSources.TenantProductSettings;

    private LienTaskTemplate() { }

    public static LienTaskTemplate Create(
        Guid   tenantId,
        string name,
        string defaultTitle,
        string defaultPriority,
        string contextType,
        string updateSource,
        Guid   createdByUserId,
        string? description               = null,
        string? defaultDescription        = null,
        int?    defaultDueOffsetDays      = null,
        string? defaultRoleId             = null,
        Guid?   applicableWorkflowStageId = null,
        string? updatedByName             = null)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(tenantId));
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultTitle);
        if (!TaskTemplateContextType.All.Contains(contextType))
            throw new ArgumentException($"Invalid contextType: '{contextType}'.", nameof(contextType));
        if (!WorkflowUpdateSources.All.Contains(updateSource))
            throw new ArgumentException($"Invalid updateSource: '{updateSource}'.", nameof(updateSource));

        var now = DateTime.UtcNow;
        return new LienTaskTemplate
        {
            Id                        = Guid.NewGuid(),
            TenantId                  = tenantId,
            ProductCode               = "SYNQ_LIENS",
            Name                      = name.Trim(),
            Description               = description?.Trim(),
            DefaultTitle              = defaultTitle.Trim(),
            DefaultDescription        = defaultDescription?.Trim(),
            DefaultPriority           = defaultPriority,
            DefaultDueOffsetDays      = defaultDueOffsetDays,
            DefaultRoleId             = defaultRoleId?.Trim(),
            ContextType               = contextType,
            ApplicableWorkflowStageId = applicableWorkflowStageId,
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
        string defaultTitle,
        string? defaultDescription,
        string defaultPriority,
        int?   defaultDueOffsetDays,
        string? defaultRoleId,
        string contextType,
        Guid?  applicableWorkflowStageId,
        string updateSource,
        Guid   updatedByUserId,
        int    expectedVersion,
        string? updatedByName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultTitle);
        if (!TaskTemplateContextType.All.Contains(contextType))
            throw new ArgumentException($"Invalid contextType: '{contextType}'.", nameof(contextType));
        if (!WorkflowUpdateSources.All.Contains(updateSource))
            throw new ArgumentException($"Invalid updateSource: '{updateSource}'.", nameof(updateSource));
        if (Version != expectedVersion)
            throw new InvalidOperationException($"Version conflict — expected {Version}, got {expectedVersion}.");

        Name                      = name.Trim();
        Description               = description?.Trim();
        DefaultTitle              = defaultTitle.Trim();
        DefaultDescription        = defaultDescription?.Trim();
        DefaultPriority           = defaultPriority;
        DefaultDueOffsetDays      = defaultDueOffsetDays;
        DefaultRoleId             = defaultRoleId?.Trim();
        ContextType               = contextType;
        ApplicableWorkflowStageId = applicableWorkflowStageId;
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
