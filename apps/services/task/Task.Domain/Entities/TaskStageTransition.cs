using BuildingBlocks.Domain;

namespace Task.Domain.Entities;

/// <summary>
/// TASK-MIG-04 — Minimal task-board stage transition rule.
///
/// Records which stage-to-stage moves are allowed for a given tenant / product.
/// This is intentionally NOT a workflow or orchestration model:
///   - No conditions, rules or branching logic
///   - No automation hooks or event types
///   - Uniqueness key: (TenantId, SourceProductCode, FromStageId, ToStageId)
///
/// Semantic: "open-move mode" — when no rows exist for a tenant/product, any move is allowed.
///           "strict mode"    — when at least one active row exists, only listed moves are allowed.
/// </summary>
public class TaskStageTransition : AuditableEntity
{
    public Guid   Id                { get; private set; }
    public Guid   TenantId          { get; private set; }
    public string SourceProductCode { get; private set; } = string.Empty;
    public Guid   FromStageId       { get; private set; }
    public Guid   ToStageId         { get; private set; }
    public bool   IsActive          { get; private set; } = true;
    public int    SortOrder         { get; private set; }

    private TaskStageTransition() { }

    public static TaskStageTransition Create(
        Guid   tenantId,
        string sourceProductCode,
        Guid   fromStageId,
        Guid   toStageId,
        Guid   createdByUserId,
        int    sortOrder = 0,
        Guid?  id        = null)
    {
        if (tenantId       == Guid.Empty) throw new ArgumentException("TenantId is required.",       nameof(tenantId));
        if (fromStageId    == Guid.Empty) throw new ArgumentException("FromStageId is required.",    nameof(fromStageId));
        if (toStageId      == Guid.Empty) throw new ArgumentException("ToStageId is required.",      nameof(toStageId));
        if (fromStageId    == toStageId)  throw new ArgumentException("Self-transition is not allowed.");
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceProductCode);

        var now = DateTime.UtcNow;
        return new TaskStageTransition
        {
            Id                = id ?? Guid.NewGuid(),
            TenantId          = tenantId,
            SourceProductCode = sourceProductCode.Trim().ToUpperInvariant(),
            FromStageId       = fromStageId,
            ToStageId         = toStageId,
            IsActive          = true,
            SortOrder         = sortOrder,
            CreatedByUserId   = createdByUserId,
            UpdatedByUserId   = createdByUserId,
            CreatedAtUtc      = now,
            UpdatedAtUtc      = now,
        };
    }

    public void Update(bool isActive, int sortOrder, Guid updatedByUserId)
    {
        IsActive        = isActive;
        SortOrder       = sortOrder;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;
    }
}
