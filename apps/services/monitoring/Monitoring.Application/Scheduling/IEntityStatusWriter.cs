namespace Monitoring.Application.Scheduling;

/// <summary>
/// Persists the evaluated current status for a monitored entity by
/// upserting its single durable current-state row.
///
/// <para>Implementations are responsible for:
/// <list type="bullet">
///   <item>evaluating the <c>EntityStatus</c> from the supplied
///   <see cref="CheckResult"/> via the domain status evaluator</item>
///   <item>creating the row on first execution or applying the new
///   state to the existing row on subsequent executions</item>
/// </list>
/// </para>
///
/// <para><b>Failure surfacing</b>: implementations do not catch their
/// own exceptions; the cycle executor wraps each call in its own
/// per-row try/catch so one bad upsert never breaks the cycle.</para>
/// </summary>
public interface IEntityStatusWriter
{
    /// <summary>
    /// Upserts the current-status row for <c>result.EntityId</c> based on
    /// the evaluated status of <paramref name="result"/>.
    /// </summary>
    Task UpsertFromResultAsync(CheckResult result, CancellationToken cancellationToken);
}
