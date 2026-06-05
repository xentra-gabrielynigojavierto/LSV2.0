namespace Liens.Application.DTOs;

/// <summary>TASK-MIG-04 — Transition as returned by the Task service.</summary>
public class TaskServiceTransitionResponse
{
    public Guid   Id                { get; set; }
    public Guid   TenantId          { get; set; }
    public string SourceProductCode { get; set; } = string.Empty;
    public Guid   FromStageId       { get; set; }
    public Guid   ToStageId         { get; set; }
    public bool   IsActive          { get; set; }
    public int    SortOrder         { get; set; }
    public DateTime CreatedAtUtc    { get; set; }
    public DateTime UpdatedAtUtc    { get; set; }
}

/// <summary>
/// TASK-MIG-04 — Batch upsert request sent to Task service.
/// Wraps the full desired set of active transitions for a (tenant, product) scope.
/// </summary>
public class TaskServiceTransitionsUpsertRequest
{
    public string                   SourceProductCode { get; set; } = string.Empty;
    public List<TransitionEntryDto> Transitions       { get; set; } = [];

    public class TransitionEntryDto
    {
        public Guid FromStageId { get; set; }
        public Guid ToStageId   { get; set; }
        public int  SortOrder   { get; set; }
    }
}
