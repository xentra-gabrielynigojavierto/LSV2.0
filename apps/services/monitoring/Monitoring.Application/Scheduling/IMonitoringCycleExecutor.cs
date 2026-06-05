namespace Monitoring.Application.Scheduling;

/// <summary>
/// Hook invoked by the background scheduler on every cycle. Implementations
/// perform the actual work for a single tick — for example, in later
/// features, loading <c>MonitoredEntity</c> records, executing checks, and
/// recording results.
///
/// <para><b>DI scope</b>: the scheduler resolves this service from a fresh
/// DI scope on every cycle, so implementations can safely depend on scoped
/// services such as <c>DbContext</c>. Cross-cycle state must not be kept on
/// the executor instance.</para>
///
/// <para><b>Cancellation contract</b>: implementations <i>must</i> be
/// cancellation-cooperative — the supplied <see cref="CancellationToken"/>
/// is the host's stopping token, and respecting it is what makes shutdown
/// prompt. Pass it through to async I/O, EF Core, HTTP clients, etc. Do
/// not catch <see cref="OperationCanceledException"/> originating from this
/// token.</para>
///
/// <para><b>Duration contract</b>: implementations <i>should</i> complete
/// well within the configured cycle interval. The scheduler uses
/// fixed-delay scheduling (next cycle = previous cycle finish + interval),
/// so a slow cycle delays subsequent cycles rather than overlapping with
/// them. Long-running work should be bounded, batched, or moved out of the
/// cycle path.</para>
///
/// <para><b>Failure contract</b>: exceptions that escape this method are
/// caught by the scheduler, logged once with the cycle id, and the loop
/// continues with the next interval. Implementations should not swallow
/// meaningful errors silently; let them bubble up so the scheduler can
/// record the failure consistently.</para>
/// </summary>
public interface IMonitoringCycleExecutor
{
    Task ExecuteCycleAsync(CancellationToken cancellationToken);
}
