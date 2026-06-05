namespace Flow.Application.DTOs;

/// <summary>LS-FLOW-MERGE-P3 — request to start a product-correlated workflow instance.</summary>
public class CreateProductWorkflowRequest
{
    /// <summary>Source product entity type, e.g. "lien_case", "referral".</summary>
    public string SourceEntityType { get; set; } = string.Empty;

    /// <summary>Product-side entity id (string).</summary>
    public string SourceEntityId { get; set; } = string.Empty;

    /// <summary>Workflow template to instantiate (must belong to the route's product).</summary>
    public Guid WorkflowDefinitionId { get; set; }

    /// <summary>Title for the resulting initial Flow task.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Optional description.</summary>
    public string? Description { get; set; }

    /// <summary>Free-form correlation key (external case number, etc.).</summary>
    public string? CorrelationKey { get; set; }

    /// <summary>Initial assignee — user id.</summary>
    public string? AssignedToUserId { get; set; }

    /// <summary>Initial assignee — role key.</summary>
    public string? AssignedToRoleKey { get; set; }

    /// <summary>Initial assignee — organisation id.</summary>
    public string? AssignedToOrgId { get; set; }

    /// <summary>Optional due date for the initial Flow task.</summary>
    public DateTime? DueDate { get; set; }
}

public class ProductWorkflowResponse
{
    public Guid Id { get; set; }
    public string ProductKey { get; set; } = string.Empty;
    public string SourceEntityType { get; set; } = string.Empty;
    public string SourceEntityId { get; set; } = string.Empty;
    public Guid WorkflowDefinitionId { get; set; }

    /// <summary>LS-FLOW-MERGE-P4 — canonical workflow instance id.</summary>
    public Guid? WorkflowInstanceId { get; set; }

    /// <summary>
    /// LEGACY (Phase-3) — initial-task id retained for back-compat. New
    /// consumers should rely on <see cref="WorkflowInstanceId"/>.
    /// </summary>
    public Guid? WorkflowInstanceTaskId { get; set; }

    public string? CorrelationKey { get; set; }
    public string Status { get; set; } = "Active";
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
