using BuildingBlocks.Domain;
using Liens.Domain.Enums;

namespace Liens.Domain.Entities;

public class LienWorkflowConfig : AuditableEntity
{
    public Guid   Id                    { get; private set; }
    public Guid   TenantId              { get; private set; }
    public string ProductCode           { get; private set; } = string.Empty;

    public string WorkflowName          { get; private set; } = string.Empty;
    public int    Version               { get; private set; } = 1;
    public bool   IsActive              { get; private set; } = true;

    public DateTime LastUpdatedAt       { get; private set; }
    public Guid?  LastUpdatedByUserId   { get; private set; }
    public string? LastUpdatedByName    { get; private set; }
    public string LastUpdatedSource     { get; private set; } = WorkflowUpdateSources.TenantProductSettings;

    public List<LienWorkflowStage>      Stages      { get; private set; } = [];
    public List<LienWorkflowTransition> Transitions { get; private set; } = [];

    private LienWorkflowConfig() { }

    public static LienWorkflowConfig Create(
        Guid tenantId,
        string productCode,
        string workflowName,
        string updateSource,
        Guid createdByUserId,
        string? updatedByName = null)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        ArgumentException.ThrowIfNullOrWhiteSpace(productCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowName);
        if (!WorkflowUpdateSources.All.Contains(updateSource))
            throw new ArgumentException($"Invalid updateSource: '{updateSource}'.", nameof(updateSource));

        var now = DateTime.UtcNow;
        return new LienWorkflowConfig
        {
            Id                  = Guid.NewGuid(),
            TenantId            = tenantId,
            ProductCode         = productCode.Trim(),
            WorkflowName        = workflowName.Trim(),
            Version             = 1,
            IsActive            = true,
            LastUpdatedAt       = now,
            LastUpdatedByUserId = createdByUserId,
            LastUpdatedByName   = updatedByName,
            LastUpdatedSource   = updateSource,
            CreatedByUserId     = createdByUserId,
            UpdatedByUserId     = createdByUserId,
            CreatedAtUtc        = now,
            UpdatedAtUtc        = now,
        };
    }

    public void Update(
        string workflowName,
        bool isActive,
        string updateSource,
        Guid updatedByUserId,
        string? updatedByName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowName);
        if (!WorkflowUpdateSources.All.Contains(updateSource))
            throw new ArgumentException($"Invalid updateSource: '{updateSource}'.", nameof(updateSource));

        WorkflowName        = workflowName.Trim();
        IsActive            = isActive;
        LastUpdatedSource   = updateSource;
        LastUpdatedByUserId = updatedByUserId;
        LastUpdatedByName   = updatedByName;
        LastUpdatedAt       = DateTime.UtcNow;
        Version             += 1;
        UpdatedByUserId     = updatedByUserId;
        UpdatedAtUtc        = DateTime.UtcNow;
    }

    public LienWorkflowStage AddStage(
        string stageName,
        int order,
        Guid createdByUserId,
        string? description = null,
        string? defaultOwnerRole = null,
        string? slaMetadata = null)
    {
        var stage = LienWorkflowStage.Create(Id, stageName, order, createdByUserId, description, defaultOwnerRole, slaMetadata);
        Stages.Add(stage);
        Version      += 1;
        LastUpdatedAt = DateTime.UtcNow;
        UpdatedAtUtc  = DateTime.UtcNow;
        return stage;
    }

    /// <summary>Bump version when transitions are modified externally (e.g. batch save).</summary>
    public void BumpVersion(Guid updatedByUserId, string? updatedByName = null)
    {
        Version             += 1;
        LastUpdatedAt        = DateTime.UtcNow;
        LastUpdatedByUserId  = updatedByUserId;
        LastUpdatedByName    = updatedByName ?? LastUpdatedByName;
        UpdatedByUserId      = updatedByUserId;
        UpdatedAtUtc         = DateTime.UtcNow;
    }
}
