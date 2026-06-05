using Flow.Application.Adapters.AuditAdapter;
using Flow.Application.DTOs;
using Flow.Application.Exceptions;
using Flow.Application.Interfaces;
using Flow.Application.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Flow.Api.Controllers.V1;

/// <summary>
/// LS-FLOW-E18 — Work Distribution Intelligence API.
///
/// <para>
/// Exposes two operations:
/// <list type="bullet">
///   <item>
///     <c>GET /api/v1/workflow-tasks/{id}/recommend-assignee</c> —
///     read-only; returns a ranked candidate list + full explanation.
///     No task state is mutated.
///   </item>
///   <item>
///     <c>POST /api/v1/workflow-tasks/{id}/auto-assign</c> —
///     governed; calls the recommendation engine then executes the
///     assignment through <see cref="IWorkflowTaskAssignmentService.ReassignAsync"/>
///     (the exclusive E14.2 assignment authority). No direct DB mutation.
///     Audited separately from the underlying reassign audit.
///   </item>
/// </list>
/// </para>
///
/// <para>
/// <b>Auth:</b> both endpoints are gated at
/// <c>Policies.PlatformOrTenantAdmin</c> — the same policy that guards
/// the existing reassign endpoint. Tenant isolation is enforced by the
/// global EF query filter on <c>WorkflowTask</c> throughout the
/// recommendation data path.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/workflow-tasks")]
[Authorize(Policy = "PlatformOrTenantAdmin")]
public sealed class WorkflowTaskRecommendationController : ControllerBase
{
    private readonly ITaskRecommendationService _recommendation;
    private readonly IWorkflowTaskAssignmentService _assignment;
    private readonly IAuditAdapter _audit;
    private readonly WorkDistributionOptions _opts;
    private readonly ILogger<WorkflowTaskRecommendationController> _log;

    public WorkflowTaskRecommendationController(
        ITaskRecommendationService recommendation,
        IWorkflowTaskAssignmentService assignment,
        IAuditAdapter audit,
        IOptions<WorkDistributionOptions> opts,
        ILogger<WorkflowTaskRecommendationController> log)
    {
        _recommendation = recommendation;
        _assignment     = assignment;
        _audit          = audit;
        _opts           = opts.Value;
        _log            = log;
    }

    // ── GET /api/v1/workflow-tasks/{id}/recommend-assignee ────────────────

    /// <summary>
    /// Returns a deterministic, explainable assignee recommendation for
    /// the given task without mutating any state.
    ///
    /// <para>
    /// Query params:
    ///   <c>candidateUserIds</c> (repeatable) — optional explicit user list.
    ///   When omitted the engine derives candidates from workload history.
    /// </para>
    /// </summary>
    /// <response code="200">Recommendation result (may have null RecommendedUserId if no candidates).</response>
    /// <response code="404">Task not found or not visible to caller's tenant.</response>
    /// <response code="503">Recommendation feature is disabled.</response>
    [HttpGet("{id:guid}/recommend-assignee")]
    [ProducesResponseType(typeof(RecommendAssigneeResult), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> RecommendAssignee(
        [FromRoute] Guid id,
        [FromQuery] IReadOnlyList<string>? candidateUserIds,
        CancellationToken ct)
    {
        if (!_opts.EnableRecommendation)
        {
            return StatusCode(503, new
            {
                error = "Recommendation feature is disabled.",
                code  = "feature_disabled",
            });
        }

        try
        {
            var result = await _recommendation.RecommendAsync(id, candidateUserIds, ct);
            return Ok(result);
        }
        catch (NotFoundException)
        {
            return NotFound(new { error = "Task not found.", code = "not_found" });
        }
    }

    // ── POST /api/v1/workflow-tasks/{id}/auto-assign ──────────────────────

    /// <summary>
    /// Executes a governed auto-assignment: derives a recommendation then
    /// assigns the task through <c>ReassignAsync</c> (the E14.2 authority).
    ///
    /// <para>
    /// <b>Safety rules:</b>
    /// <list type="bullet">
    ///   <item>If no recommendation can be made, returns 422 with explanation.
    ///     Task is left unchanged.</item>
    ///   <item>If <c>ReassignAsync</c> fails (eligibility, concurrency, terminal
    ///     status), the original exception is propagated — no partial state.</item>
    ///   <item>On success, a second audit record
    ///     <c>workflow.task.auto_assign.completed</c> is emitted carrying the
    ///     full recommendation explanation (in addition to the existing
    ///     <c>workflow.task.reassign</c> audit from the assignment service).</item>
    /// </list>
    /// </para>
    /// </summary>
    /// <response code="200">Auto-assignment result with assignment detail and recommendation explanation.</response>
    /// <response code="400">Missing or invalid reason.</response>
    /// <response code="404">Task not found.</response>
    /// <response code="409">Concurrency conflict — retry.</response>
    /// <response code="422">No recommendation available — see response for explanation.</response>
    /// <response code="503">Auto-assignment feature is disabled.</response>
    [HttpPost("{id:guid}/auto-assign")]
    [ProducesResponseType(typeof(AutoAssignResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    [ProducesResponseType(422)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> AutoAssign(
        [FromRoute] Guid id,
        [FromBody] AutoAssignRequest body,
        CancellationToken ct)
    {
        if (!_opts.EnableAutoAssignment)
        {
            return StatusCode(503, new
            {
                error = "Auto-assignment feature is disabled.",
                code  = "feature_disabled",
            });
        }

        var reason = body?.Reason?.Trim() ?? string.Empty;
        if (reason.Length == 0)
        {
            return BadRequest(new
            {
                error = "A reason is required for auto-assignment.",
                code  = "reason_required",
            });
        }

        // 1. Recommendation phase — read-only, no state change.
        RecommendAssigneeResult recommendation;
        try
        {
            recommendation = await _recommendation.RecommendAsync(
                id, body?.CandidateUserIds, ct);
        }
        catch (NotFoundException)
        {
            return NotFound(new { error = "Task not found.", code = "not_found" });
        }

        if (recommendation.RecommendedUserId is null)
        {
            _log.LogInformation(
                "E18 AutoAssign: TaskId={TaskId} — no recommendation ({Explanation})",
                id, recommendation.ExplanationSummary);

            return UnprocessableEntity(new
            {
                error       = "No recommendation available — task cannot be auto-assigned.",
                code        = "no_recommendation",
                explanation = recommendation.ExplanationSummary,
                candidateSource = recommendation.CandidateSource,
            });
        }

        // 2. Assignment phase — through the E14.2 governed path.
        var systemReason = $"[auto-assign] {reason} | " +
                           $"Recommended: {recommendation.RecommendedUserId} — " +
                           $"{recommendation.ExplanationSummary}";

        // Truncate if needed (assignment service rejects > 500 chars).
        const int maxReasonLength = 490;
        if (systemReason.Length > maxReasonLength)
            systemReason = systemReason[..maxReasonLength] + "…";

        var assignmentResult = await _assignment.ReassignAsync(
            id,
            new ReassignTaskRequest(
                TargetMode:     "DirectUser",
                AssignedUserId: recommendation.RecommendedUserId,
                AssignedRole:   null,
                AssignedOrgId:  null,
                Reason:         systemReason),
            ct);

        // 3. Secondary audit — recommendation explanation metadata.
        await EmitAutoAssignAuditAsync(id, assignmentResult, recommendation, reason, ct);

        _log.LogInformation(
            "E18 AutoAssign: TaskId={TaskId} → AssignedTo={User} " +
            "(SlaStatus={Sla} Priority={Priority} CandidateCount={Count} Source={Source})",
            id, recommendation.RecommendedUserId,
            recommendation.TaskSlaStatus, recommendation.TaskPriority,
            recommendation.Candidates.Count, recommendation.CandidateSource);

        return Ok(new AutoAssignResult
        {
            Assignment     = assignmentResult,
            Recommendation = recommendation,
        });
    }

    // ── Audit ─────────────────────────────────────────────────────────────

    private async Task EmitAutoAssignAuditAsync(
        Guid taskId,
        WorkflowTaskAssignmentResult assignment,
        RecommendAssigneeResult recommendation,
        string originalReason,
        CancellationToken ct)
    {
        try
        {
            var performedBy = ResolvePerformedBy();

            var metadata = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["taskId"]                  = taskId.ToString("D"),
                ["workflowInstanceId"]      = assignment.WorkflowInstanceId.ToString("D"),
                ["selectedUserId"]          = recommendation.RecommendedUserId,
                ["candidateSource"]         = recommendation.CandidateSource,
                ["candidateCount"]          = recommendation.Candidates.Count.ToString(),
                ["taskSlaStatus"]           = recommendation.TaskSlaStatus,
                ["taskPriority"]            = recommendation.TaskPriority,
                ["recommendationExplanation"] = recommendation.ExplanationSummary,
                ["originalReason"]          = originalReason,
                ["performedBy"]             = performedBy,
            };

            await _audit.WriteEventAsync(new AuditEvent(
                Action:       "workflow.task.auto_assign.completed",
                EntityType:   "WorkflowTask",
                EntityId:     taskId.ToString("D"),
                TenantId:     null,
                UserId:       performedBy,
                Description:  $"Task auto-assigned to {recommendation.RecommendedUserId} via recommendation engine.",
                Metadata:     metadata,
                OccurredAtUtc: assignment.OccurredAtUtc),
                ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex,
                "E18 AutoAssign audit write failed for TaskId={TaskId} — assignment already persisted, continuing.",
                taskId);
        }
    }

    private string? ResolvePerformedBy()
    {
        var claims = User?.Claims;
        if (claims is null) return null;

        foreach (var type in new[]
        {
            "sub", "uid", "userId", "user_id",
            System.Security.Claims.ClaimTypes.NameIdentifier,
            System.Security.Claims.ClaimTypes.Email,
        })
        {
            var v = User?.FindFirst(type)?.Value;
            if (!string.IsNullOrWhiteSpace(v)) return v;
        }
        return null;
    }
}
