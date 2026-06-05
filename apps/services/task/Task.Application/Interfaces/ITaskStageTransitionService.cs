using Task.Application.DTOs;

namespace Task.Application.Interfaces;

public interface ITaskStageTransitionService
{
    /// <summary>Returns all active transitions for a tenant+product pair.</summary>
    System.Threading.Tasks.Task<List<TaskStageTransitionDto>> GetActiveTransitionsAsync(
        Guid tenantId, string productCode, CancellationToken ct = default);

    /// <summary>
    /// Idempotent batch replace: deactivates all current active transitions for the
    /// (TenantId, SourceProductCode) scope, then inserts the supplied set.
    /// Running twice with the same input produces no duplicates.
    /// </summary>
    System.Threading.Tasks.Task UpsertFromSourceAsync(
        Guid tenantId, Guid actorId, UpsertFromSourceTransitionsRequest request,
        CancellationToken ct = default);
}
