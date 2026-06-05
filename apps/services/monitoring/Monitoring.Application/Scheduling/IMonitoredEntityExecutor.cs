using Monitoring.Domain.Monitoring;

namespace Monitoring.Application.Scheduling;

/// <summary>
/// Per-entity execution hook invoked by the registry-driven cycle executor
/// for every enabled <see cref="MonitoredEntity"/> on each scheduler cycle.
/// Implementations (HTTP probes, DB connectivity checks, queue depth checks,
/// etc.) are registered via DI; the production HTTP executor overrides the
/// default no-op.
///
/// <para><b>Invocation</b>: called once per enabled entity per cycle, in the
/// stable order documented by the cycle executor (Name ASC, then
/// CreatedAtUtc ASC, then Id ASC). Implementations should be stateless
/// across calls — the same instance may be reused for every entity in a
/// cycle.</para>
///
/// <para><b>Return contract</b>: every execution returns a structured
/// <see cref="CheckResult"/> describing the outcome. Implementations
/// <i>must</i> never return <c>null</c>, and <i>must</i> not throw for
/// classified failure modes (timeout, network failure, non-2xx,
/// invalid target, skipped). Translate those into the corresponding
/// <see cref="CheckOutcome"/> on the returned <see cref="CheckResult"/>.</para>
///
/// <para><b>Cancellation contract</b>: implementations <i>must</i> be
/// cancellation-cooperative. The supplied token is the host's stopping
/// token; respect it in all async I/O. Do not catch
/// <see cref="OperationCanceledException"/> originating from this token —
/// rethrow it so the cycle executor can unwind cleanly.</para>
///
/// <para><b>Unexpected failure contract</b>: any exception that is not a
/// classified failure and not a host-shutdown cancellation is caught by
/// the cycle executor and translated into an
/// <see cref="CheckOutcome.UnexpectedFailure"/> <see cref="CheckResult"/>
/// for that entity. The cycle continues with the next entity. Implementations
/// should not swallow meaningful errors silently.</para>
/// </summary>
public interface IMonitoredEntityExecutor
{
    Task<CheckResult> ExecuteAsync(MonitoredEntity entity, CancellationToken cancellationToken);
}
