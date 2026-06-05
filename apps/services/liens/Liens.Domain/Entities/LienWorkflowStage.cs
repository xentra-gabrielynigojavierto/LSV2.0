using BuildingBlocks.Domain;

namespace Liens.Domain.Entities;

public class LienWorkflowStage : AuditableEntity
{
    public Guid   Id               { get; private set; }
    public Guid   WorkflowConfigId { get; private set; }

    public string StageName        { get; private set; } = string.Empty;
    public int    StageOrder       { get; private set; }
    public string? Description     { get; private set; }
    public bool   IsActive         { get; private set; } = true;

    public string? DefaultOwnerRole { get; private set; }
    public string? SlaMetadata      { get; private set; }

    private LienWorkflowStage() { }

    public static LienWorkflowStage Create(
        Guid workflowConfigId,
        string stageName,
        int order,
        Guid createdByUserId,
        string? description = null,
        string? defaultOwnerRole = null,
        string? slaMetadata = null)
    {
        if (workflowConfigId == Guid.Empty) throw new ArgumentException("WorkflowConfigId is required.", nameof(workflowConfigId));
        ArgumentException.ThrowIfNullOrWhiteSpace(stageName);

        var now = DateTime.UtcNow;
        return new LienWorkflowStage
        {
            Id               = Guid.NewGuid(),
            WorkflowConfigId = workflowConfigId,
            StageName        = stageName.Trim(),
            StageOrder       = order,
            Description      = description?.Trim(),
            IsActive         = true,
            DefaultOwnerRole = defaultOwnerRole?.Trim(),
            SlaMetadata      = slaMetadata,
            CreatedByUserId  = createdByUserId,
            UpdatedByUserId  = createdByUserId,
            CreatedAtUtc     = now,
            UpdatedAtUtc     = now,
        };
    }

    public void Update(
        string stageName,
        int order,
        bool isActive,
        Guid updatedByUserId,
        string? description = null,
        string? defaultOwnerRole = null,
        string? slaMetadata = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stageName);

        StageName        = stageName.Trim();
        StageOrder       = order;
        IsActive         = isActive;
        Description      = description?.Trim();
        DefaultOwnerRole = defaultOwnerRole?.Trim();
        SlaMetadata      = slaMetadata;
        UpdatedByUserId  = updatedByUserId;
        UpdatedAtUtc     = DateTime.UtcNow;
    }

    public void Deactivate(Guid updatedByUserId)
    {
        IsActive        = false;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;
    }
}
