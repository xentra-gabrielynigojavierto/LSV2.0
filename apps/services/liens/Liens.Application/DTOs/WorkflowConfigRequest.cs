namespace Liens.Application.DTOs;

public sealed class CreateWorkflowConfigRequest
{
    public string WorkflowName  { get; init; } = string.Empty;
    public string UpdateSource  { get; init; } = string.Empty;
    public string? UpdatedByName { get; init; }
}

public sealed class UpdateWorkflowConfigRequest
{
    public string WorkflowName  { get; init; } = string.Empty;
    public bool   IsActive      { get; init; } = true;
    public string UpdateSource  { get; init; } = string.Empty;
    public string? UpdatedByName { get; init; }
    public int    Version       { get; init; }
}

public sealed class AddWorkflowStageRequest
{
    public string StageName         { get; init; } = string.Empty;
    public int    StageOrder        { get; init; }
    public string? Description      { get; init; }
    public string? DefaultOwnerRole { get; init; }
    public string? SlaMetadata      { get; init; }
}

public sealed class UpdateWorkflowStageRequest
{
    public string StageName         { get; init; } = string.Empty;
    public int    StageOrder        { get; init; }
    public bool   IsActive          { get; init; } = true;
    public string? Description      { get; init; }
    public string? DefaultOwnerRole { get; init; }
    public string? SlaMetadata      { get; init; }
}

public sealed class ReorderStagesRequest
{
    public List<StageOrderEntry> Stages { get; init; } = [];
}

public sealed class StageOrderEntry
{
    public Guid StageId    { get; init; }
    public int  StageOrder { get; init; }
}

// ── Transition requests (LS-LIENS-FLOW-005) ──────────────────────────────────

public sealed class AddWorkflowTransitionRequest
{
    public Guid FromStageId { get; init; }
    public Guid ToStageId   { get; init; }
    public int  SortOrder   { get; init; }
}

/// <summary>
/// Batch-save transitions: replaces all active transitions for the workflow.
/// Sends the desired full set; the service deactivates any not in the list.
/// </summary>
public sealed class SaveWorkflowTransitionsRequest
{
    public List<TransitionEntry> Transitions { get; init; } = [];
    public string UpdateSource  { get; init; } = string.Empty;
    public string? UpdatedByName { get; init; }
}

public sealed class TransitionEntry
{
    public Guid FromStageId { get; init; }
    public Guid ToStageId   { get; init; }
    public int  SortOrder   { get; init; }
}
