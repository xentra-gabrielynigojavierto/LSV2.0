namespace Task.Application.DTOs;

/// <summary>TASK-MIG-04 — Single transition returned to callers.</summary>
public record TaskStageTransitionDto(
    Guid   Id,
    Guid   TenantId,
    string SourceProductCode,
    Guid   FromStageId,
    Guid   ToStageId,
    bool   IsActive,
    int    SortOrder,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

/// <summary>
/// TASK-MIG-04 — Batch upsert payload pushed by product-source services
/// (e.g. Liens service during migration or write-through).
/// </summary>
public class UpsertFromSourceTransitionsRequest
{
    public string SourceProductCode { get; set; } = string.Empty;

    /// <summary>
    /// Complete desired set of active transitions.
    /// All existing transitions for (TenantId, SourceProductCode) are deactivated
    /// before the new set is applied, making this an idempotent replace operation.
    /// </summary>
    public List<TransitionEntry> Transitions { get; set; } = [];

    public record TransitionEntry(Guid FromStageId, Guid ToStageId, int SortOrder = 0);
}
