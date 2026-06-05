using BuildingBlocks.Domain;
using Liens.Domain.Enums;

namespace Liens.Domain.Entities;

/// <summary>
/// LS-LIENS-FLOW-006 — Per-tenant task creation governance settings for Synq Liens.
/// Controls whether assignee, case linkage, and workflow stage are required on task creation.
/// One record per tenant/product; created on first access with safe defaults.
/// </summary>
public class LienTaskGovernanceSettings : AuditableEntity
{
    public Guid   Id          { get; private set; }
    public Guid   TenantId    { get; private set; }
    public string ProductCode { get; private set; } = string.Empty;

    public bool RequireAssigneeOnCreate      { get; private set; } = true;
    public bool RequireCaseLinkOnCreate      { get; private set; } = true;
    public bool AllowMultipleAssignees       { get; private set; } = false;
    public bool RequireWorkflowStageOnCreate { get; private set; } = true;

    public string DefaultStartStageMode  { get; private set; } = StartStageMode.FirstActiveStage;
    public Guid?  ExplicitStartStageId   { get; private set; }

    public int      Version             { get; private set; } = 1;
    public DateTime LastUpdatedAt       { get; private set; }
    public Guid?    LastUpdatedByUserId { get; private set; }
    public string?  LastUpdatedByName   { get; private set; }
    public string   LastUpdatedSource   { get; private set; } = WorkflowUpdateSources.TenantProductSettings;

    private LienTaskGovernanceSettings() { }

    public static LienTaskGovernanceSettings CreateDefault(
        Guid   tenantId,
        string productCode,
        string updateSource,
        Guid   createdByUserId,
        string? createdByName = null)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(tenantId));
        ArgumentException.ThrowIfNullOrWhiteSpace(productCode);
        if (!WorkflowUpdateSources.All.Contains(updateSource))
            throw new ArgumentException($"Invalid updateSource: '{updateSource}'.", nameof(updateSource));

        var now = DateTime.UtcNow;
        return new LienTaskGovernanceSettings
        {
            Id                       = Guid.NewGuid(),
            TenantId                 = tenantId,
            ProductCode              = productCode.Trim(),
            RequireAssigneeOnCreate  = true,
            RequireCaseLinkOnCreate  = true,
            AllowMultipleAssignees   = false,
            RequireWorkflowStageOnCreate = true,
            DefaultStartStageMode    = StartStageMode.FirstActiveStage,
            ExplicitStartStageId     = null,
            Version                  = 1,
            LastUpdatedAt            = now,
            LastUpdatedByUserId      = createdByUserId,
            LastUpdatedByName        = createdByName,
            LastUpdatedSource        = updateSource,
            CreatedByUserId          = createdByUserId,
            UpdatedByUserId          = createdByUserId,
            CreatedAtUtc             = now,
            UpdatedAtUtc             = now,
        };
    }

    public void Update(
        bool   requireAssigneeOnCreate,
        bool   requireCaseLinkOnCreate,
        bool   allowMultipleAssignees,
        bool   requireWorkflowStageOnCreate,
        string defaultStartStageMode,
        Guid?  explicitStartStageId,
        string updateSource,
        Guid   updatedByUserId,
        string? updatedByName = null)
    {
        if (!StartStageMode.All.Contains(defaultStartStageMode))
            throw new ArgumentException($"Invalid defaultStartStageMode: '{defaultStartStageMode}'.", nameof(defaultStartStageMode));
        if (!WorkflowUpdateSources.All.Contains(updateSource))
            throw new ArgumentException($"Invalid updateSource: '{updateSource}'.", nameof(updateSource));

        RequireAssigneeOnCreate      = requireAssigneeOnCreate;
        RequireCaseLinkOnCreate      = requireCaseLinkOnCreate;
        AllowMultipleAssignees       = allowMultipleAssignees;
        RequireWorkflowStageOnCreate = requireWorkflowStageOnCreate;
        DefaultStartStageMode        = defaultStartStageMode;
        ExplicitStartStageId         = defaultStartStageMode == StartStageMode.ExplicitStage
                                       ? explicitStartStageId
                                       : null;
        Version             += 1;
        LastUpdatedAt        = DateTime.UtcNow;
        LastUpdatedByUserId  = updatedByUserId;
        LastUpdatedByName    = updatedByName;
        LastUpdatedSource    = updateSource;
        UpdatedByUserId      = updatedByUserId;
        UpdatedAtUtc         = DateTime.UtcNow;
    }
}
