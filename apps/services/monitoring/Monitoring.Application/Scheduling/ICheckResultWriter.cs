namespace Monitoring.Application.Scheduling;

/// <summary>
/// Persists a single executed <see cref="CheckResult"/> as a durable
/// history row. One call per executed entity per cycle.
///
/// <para><b>Failure semantics</b>: implementations must surface their
/// own failures by throwing. The cycle executor catches and isolates
/// per-result persistence failures so that one bad write neither stops
/// the cycle nor crashes the host. Implementations therefore do not
/// need to swallow exceptions internally.</para>
///
/// <para><b>Cancellation</b>: if the host is shutting down, callers
/// pass the host's <see cref="CancellationToken"/>. Implementations
/// should honor it but it is acceptable for in-flight DB writes to
/// complete on a best-effort basis.</para>
/// </summary>
public interface ICheckResultWriter
{
    Task WriteAsync(CheckResult result, CancellationToken cancellationToken);
}
