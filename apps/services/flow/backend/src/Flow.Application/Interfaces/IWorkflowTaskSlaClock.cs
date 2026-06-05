namespace Flow.Application.Interfaces;

/// <summary>
/// LS-FLOW-E10.3 (task slice) — abstraction over the per-priority SLA
/// duration policy used to stamp <c>WorkflowTask.DueAt</c> at task
/// creation time.
///
/// <para>
/// Lives in <c>Flow.Application</c> so the
/// <c>WorkflowTaskFromWorkflowFactory</c> can depend on the policy
/// without taking a transitive reference on
/// <c>Microsoft.Extensions.Options</c>. The production implementation
/// is options-bound and lives in <c>Flow.Infrastructure</c>; tests can
/// substitute a fake clock + policy here without spinning up the
/// hosting stack.
/// </para>
///
/// <para>
/// Returning a nullable lets the implementation say "no SLA applies to
/// this task" (e.g. an unknown priority, SLA disabled by config) by
/// returning null, which the factory persists as a null
/// <c>DueAt</c> — the evaluator skips such rows and operator surfaces
/// render no SLA badge.
/// </para>
/// </summary>
public interface IWorkflowTaskSlaClock
{
    /// <summary>
    /// Compute the deadline for a brand-new task with the given
    /// priority. Returns <c>null</c> when no SLA applies.
    /// </summary>
    System.DateTime? ComputeDueAt(System.DateTime createdAt, string priority);
}
