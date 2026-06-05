namespace Liens.Application.Interfaces;

/// <summary>
/// LS-LIENS-FLOW-007 — resolves the best active Flow workflow instance for a given case.
/// Implementations MUST be non-blocking: any exception from the Flow service must be
/// caught internally and result in a null return, not an exception to callers.
/// Task creation must never fail because of a Flow lookup failure.
/// </summary>
public interface IFlowInstanceResolver
{
    /// <summary>
    /// Returns the <c>WorkflowInstanceId</c> and <c>CurrentStepKey</c> for the most
    /// recently updated active Flow instance linked to <paramref name="caseId"/>,
    /// or <c>(null, null)</c> when no active instance is found or the Flow service
    /// is unavailable.
    /// </summary>
    Task<(Guid? WorkflowInstanceId, string? WorkflowStepKey)> ResolveAsync(
        Guid caseId,
        CancellationToken ct = default);
}
