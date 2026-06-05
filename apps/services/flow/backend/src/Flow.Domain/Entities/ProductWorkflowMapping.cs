using Flow.Domain.Common;

namespace Flow.Domain.Entities;

/// <summary>
/// LS-FLOW-MERGE-P3 — explicit correlation between a product-side entity and
/// a Flow workflow. Owned by Flow (single source of truth for the link),
/// referenced by product services via API only.
///
/// One mapping row links one product entity to one workflow definition AND
/// (optionally) the resulting Flow workflow instance — represented today by
/// a TaskItem (the existing instance grain). When the Phase-4 dedicated
/// workflow-instance entity lands, <see cref="WorkflowInstanceTaskId"/> will
/// be replaced by a workflow-instance id.
/// </summary>
public class ProductWorkflowMapping : AuditableEntity
{
    /// <summary>Product key (validated against <see cref="ProductKeys"/>).</summary>
    public string ProductKey { get; set; } = ProductKeys.FlowGeneric;

    /// <summary>Caller-supplied entity type, e.g. "lien_case", "referral", "fund_application".</summary>
    public string SourceEntityType { get; set; } = string.Empty;

    /// <summary>Product-side entity id (string for flexibility — Guid, int, or composite).</summary>
    public string SourceEntityId { get; set; } = string.Empty;

    /// <summary>Linked workflow template.</summary>
    public Guid WorkflowDefinitionId { get; set; }

    /// <summary>
    /// LS-FLOW-MERGE-P4 — canonical workflow-instance pointer. Phase-4 moved
    /// the instance grain from <see cref="TaskItem"/> to <see cref="WorkflowInstance"/>.
    /// New rows always populate this; legacy rows may be null until backfilled.
    /// </summary>
    public Guid? WorkflowInstanceId { get; set; }

    /// <summary>
    /// LEGACY (Phase-3) — id of the initial Flow TaskItem that the mapping
    /// originally pointed at. Retained read-only for back-compat with
    /// existing API consumers and for debugging. Prefer
    /// <see cref="WorkflowInstanceId"/> for all new logic.
    /// </summary>
    public Guid? WorkflowInstanceTaskId { get; set; }

    /// <summary>Free-form correlation key (e.g. external case number).</summary>
    public string? CorrelationKey { get; set; }

    /// <summary>Lifecycle status — one of "Active", "Completed", "Cancelled".</summary>
    public string Status { get; set; } = "Active";

    public FlowDefinition? WorkflowDefinition { get; set; }
    public WorkflowInstance? WorkflowInstance { get; set; }
    public TaskItem? WorkflowInstanceTask { get; set; }
}
