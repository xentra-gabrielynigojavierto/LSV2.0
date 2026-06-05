using Microsoft.Extensions.Logging;
using Monitoring.Domain.Monitoring;

namespace Monitoring.Application.Scheduling;

/// <summary>
/// Default <see cref="IMonitoredEntityExecutor"/> used when no real
/// adapter is registered. Performs no monitoring work and returns a
/// <see cref="CheckOutcome.Skipped"/> <see cref="CheckResult"/> so the
/// cycle aggregation always has a row per entity. Replaced in production
/// by the HTTP executor (and, in later features, by additional adapters).
/// </summary>
public sealed class NoopMonitoredEntityExecutor : IMonitoredEntityExecutor
{
    private readonly ILogger<NoopMonitoredEntityExecutor> _logger;

    public NoopMonitoredEntityExecutor(ILogger<NoopMonitoredEntityExecutor> logger)
    {
        _logger = logger;
    }

    public Task<CheckResult> ExecuteAsync(MonitoredEntity entity, CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "No-op per-entity executor invoked for entity {EntityId} ({EntityName}). " +
            "Replace this implementation with the real check executor in DI.",
            entity.Id, entity.Name);

        var result = new CheckResult(
            EntityId: entity.Id,
            EntityName: entity.Name,
            MonitoringType: entity.MonitoringType,
            Target: entity.Target,
            Succeeded: false,
            Outcome: CheckOutcome.Skipped,
            StatusCode: null,
            ElapsedMs: 0,
            CheckedAtUtc: DateTime.UtcNow,
            Message: "skipped: noop executor");

        return Task.FromResult(result);
    }
}
