using Flow.Application.DTOs;

namespace Flow.Application.Interfaces;

/// <summary>
/// LS-FLOW-E18 — deterministic, explainable assignee recommendation
/// service for the work-distribution intelligence layer.
///
/// <para>
/// Produces a ranked candidate list and a recommended user id for a
/// given task. All decisioning is rule-based and fully explained in
/// the returned <see cref="RecommendAssigneeResult"/>. No randomness,
/// no ML, no opaque heuristics.
/// </para>
///
/// <para>
/// <b>Eligibility contract:</b> the service only ranks candidates that
/// are safe to assign to the task — it never introduces users that
/// would not be allowed to claim/be-assigned the task through the
/// existing E14.2 authority. The candidate set is either (a) explicitly
/// supplied by the caller or (b) derived from workload history, which
/// only surfaces users who have already interacted with the same
/// role/org within the tenant.
/// </para>
///
/// <para>
/// <b>Security:</b> tenant isolation is enforced by the global EF
/// query filter on <c>WorkflowTask</c> throughout the recommendation
/// data path. Cross-tenant candidate ids supplied by the caller cannot
/// match any task within the caller's tenant so their workload counts
/// resolve to 0 and they are never promoted by bad input.
/// </para>
/// </summary>
public interface ITaskRecommendationService
{
    /// <summary>
    /// Computes a recommendation for the given task.
    /// </summary>
    /// <param name="taskId">The task to evaluate.</param>
    /// <param name="candidateUserIds">
    /// Optional explicit candidate list. When <c>null</c> or empty the
    /// engine derives candidates from workload history (see
    /// <see cref="RecommendAssigneeResult.CandidateSource"/>).
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    Task<RecommendAssigneeResult> RecommendAsync(
        Guid taskId,
        IReadOnlyList<string>? candidateUserIds,
        CancellationToken ct = default);
}
