using Documents.Domain.Events;
using Documents.Domain.Interfaces;
using Documents.Infrastructure.Observability;
using Microsoft.Extensions.Logging;

namespace Documents.Infrastructure.Notifications;

/// <summary>
/// Log-only publisher — emits a structured log entry for each terminal scan event.
///
/// Default provider for dev/test environments. Zero dependencies beyond ILogger.
/// Guarantees: best-effort, at-most-once (logging is synchronous + buffered).
/// </summary>
public sealed class LogScanCompletionPublisher : IScanCompletionPublisher
{
    private readonly ILogger<LogScanCompletionPublisher> _log;

    public LogScanCompletionPublisher(ILogger<LogScanCompletionPublisher> log)
        => _log = log;

    public ValueTask PublishAsync(DocumentScanCompletedEvent evt, CancellationToken ct = default)
    {
        try
        {
            _log.LogInformation(
                "DocumentScanCompleted: EventId={EventId} DocumentId={DocId} TenantId={TenantId} " +
                "VersionId={VersionId} Status={Status} OccurredAt={OccurredAt} " +
                "AttemptCount={Attempts} EngineVersion={Engine} FileName={File} " +
                "CorrelationId={Corr} ServiceName={Service}",
                evt.EventId, evt.DocumentId, evt.TenantId,
                evt.VersionId, evt.ScanStatus, evt.OccurredAt,
                evt.AttemptCount, evt.EngineVersion, evt.FileName,
                evt.CorrelationId, evt.ServiceName);

            var statusLabel = evt.ScanStatus.ToString().ToLowerInvariant();
            RedisMetrics.ScanCompletionEventsEmitted.WithLabels(statusLabel).Inc();
            RedisMetrics.ScanCompletionDeliverySuccess.Inc();
        }
        catch (Exception ex)
        {
            RedisMetrics.ScanCompletionDeliveryFailures.Inc();
            _log.LogWarning(ex,
                "LogScanCompletionPublisher: unexpected error emitting event for Document={DocId}",
                evt.DocumentId);
        }

        return ValueTask.CompletedTask;
    }
}
