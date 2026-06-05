using BuildingBlocks.Domain;

namespace Liens.Domain.Entities;

public class LienWorkflowTransition : AuditableEntity
{
    public Guid Id               { get; private set; }
    public Guid WorkflowConfigId { get; private set; }
    public Guid FromStageId      { get; private set; }
    public Guid ToStageId        { get; private set; }
    public bool IsActive         { get; private set; } = true;
    public int  SortOrder        { get; private set; }

    private LienWorkflowTransition() { }

    public static LienWorkflowTransition Create(
        Guid workflowConfigId,
        Guid fromStageId,
        Guid toStageId,
        Guid createdByUserId,
        int  sortOrder = 0)
    {
        if (workflowConfigId == Guid.Empty) throw new ArgumentException("WorkflowConfigId is required.", nameof(workflowConfigId));
        if (fromStageId      == Guid.Empty) throw new ArgumentException("FromStageId is required.",      nameof(fromStageId));
        if (toStageId        == Guid.Empty) throw new ArgumentException("ToStageId is required.",        nameof(toStageId));
        if (fromStageId      == toStageId)  throw new ArgumentException("A transition cannot point to the same stage (self-transition).");

        var now = DateTime.UtcNow;
        return new LienWorkflowTransition
        {
            Id               = Guid.NewGuid(),
            WorkflowConfigId = workflowConfigId,
            FromStageId      = fromStageId,
            ToStageId        = toStageId,
            IsActive         = true,
            SortOrder        = sortOrder,
            CreatedByUserId  = createdByUserId,
            UpdatedByUserId  = createdByUserId,
            CreatedAtUtc     = now,
            UpdatedAtUtc     = now,
        };
    }

    public void Deactivate(Guid updatedByUserId)
    {
        IsActive        = false;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;
    }

    public void Activate(Guid updatedByUserId)
    {
        IsActive        = true;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;
    }
}
