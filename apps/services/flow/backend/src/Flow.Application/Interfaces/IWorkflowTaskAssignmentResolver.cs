using Flow.Domain.Entities;

namespace Flow.Application.Interfaces;

/// <summary>
/// LS-FLOW-E11.3 — deterministic decision the resolver returns for a
/// single (workflow instance, step) pair.
///
/// <para>
/// <b>Precedence:</b> User &gt; Role &gt; Org. The static factory
/// methods construct an instance with <i>exactly one</i> field set so
/// callers cannot accidentally produce a multi-target assignment.
/// </para>
///
/// <para>
/// <b>Normalisation:</b> empty / whitespace inputs collapse to
/// <see cref="None"/>. Trimmed values are stored verbatim.
/// </para>
///
/// <para>
/// <b>Tenant safety:</b> the record carries no tenant id of its own —
/// the caller (the task factory) is responsible for confirming that
/// any user / role / org id is valid for the parent workflow
/// instance's tenant. See <c>analysis/E11.3-report.md</c> §"Tenant
/// Boundary Notes".
/// </para>
/// </summary>
public sealed record WorkflowTaskAssignment(
    string? AssignedUserId,
    string? AssignedRole,
    string? AssignedOrgId)
{
    /// <summary>
    /// "No assignment" — task is left unassigned. The documented
    /// safe-fallback when no rule matches.
    /// </summary>
    public static WorkflowTaskAssignment None { get; } = new(null, null, null);

    public static WorkflowTaskAssignment ForUser(string? userId)
    {
        var v = Norm(userId);
        return v is null ? None : new WorkflowTaskAssignment(v, null, null);
    }

    public static WorkflowTaskAssignment ForRole(string? role)
    {
        var v = Norm(role);
        return v is null ? None : new WorkflowTaskAssignment(null, v, null);
    }

    public static WorkflowTaskAssignment ForOrg(string? orgId)
    {
        var v = Norm(orgId);
        return v is null ? None : new WorkflowTaskAssignment(null, null, v);
    }

    /// <summary>
    /// True iff at least one assignment target is set. The static
    /// factories (<see cref="ForUser"/>, <see cref="ForRole"/>,
    /// <see cref="ForOrg"/>) only ever produce instances with exactly
    /// one field set, so under normal use this is equivalent to
    /// "single target set". A hand-rolled multi-target record would
    /// also report <c>true</c> here — single-target persistence is
    /// then enforced downstream by
    /// <c>WorkflowTaskFromWorkflowFactory.ApplyAssignment</c>.
    /// </summary>
    public bool IsAssigned =>
        AssignedUserId is not null || AssignedRole is not null || AssignedOrgId is not null;

    private static string? Norm(string? v) =>
        string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}

/// <summary>
/// LS-FLOW-E11.3 — resolves the assignment for a newly created
/// <see cref="WorkflowTask"/>. Pure, stateless, local-only — must not
/// call external services, identity providers, or perform DB writes.
///
/// <para>
/// Invoked by <c>WorkflowTaskFromWorkflowFactory</c> after dedup but
/// before <c>_db.WorkflowTasks.Add(...)</c>, so the resolved assignment
/// is committed in the same unit-of-work as the task itself.
/// </para>
///
/// <para>
/// Returning <see cref="WorkflowTaskAssignment.None"/> is the
/// documented safe fallback — the task is created unassigned and
/// downstream operators may pick it up via the future "My Tasks"
/// surface (E11.5).
/// </para>
/// </summary>
public interface IWorkflowTaskAssignmentResolver
{
    /// <summary>
    /// Resolve an assignment for the (instance, step) pair. Must not
    /// throw on unknown keys — return <see cref="WorkflowTaskAssignment.None"/>
    /// instead. Caller is responsible for tenant-scoping any returned
    /// id against <c>instance.TenantId</c>.
    /// </summary>
    WorkflowTaskAssignment Resolve(WorkflowInstance instance, string stepKey);
}
