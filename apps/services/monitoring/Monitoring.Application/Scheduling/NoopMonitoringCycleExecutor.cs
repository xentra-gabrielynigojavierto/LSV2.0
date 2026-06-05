using Microsoft.Extensions.Logging;

namespace Monitoring.Application.Scheduling;

/// <summary>
/// Default cycle executor used by the scheduler foundation
/// (<see cref="IMonitoringCycleExecutor"/>). Performs no monitoring work —
/// it only emits a debug log line confirming the cycle ran. Replaced by
/// the real executor in a later feature once entity loading and check
/// execution exist.
/// </summary>
public sealed class NoopMonitoringCycleExecutor : IMonitoringCycleExecutor
{
    private readonly ILogger<NoopMonitoringCycleExecutor> _logger;

    public NoopMonitoringCycleExecutor(ILogger<NoopMonitoringCycleExecutor> logger)
    {
        _logger = logger;
    }

    public Task ExecuteCycleAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Monitoring cycle no-op executor ran. " +
            "Replace this implementation with the real cycle work in a later feature.");
        return Task.CompletedTask;
    }
}
