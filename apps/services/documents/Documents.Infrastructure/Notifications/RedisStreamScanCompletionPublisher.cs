using Documents.Domain.Events;
using Documents.Domain.Interfaces;
using Documents.Infrastructure.Observability;
using Documents.Infrastructure.Redis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Text.Json;

namespace Documents.Infrastructure.Notifications;

/// <summary>
/// Durable scan completion publisher using Redis Streams (XADD).
///
/// Preferred production delivery mechanism over Redis Pub/Sub. Unlike Pub/Sub,
/// stream entries persist in Redis and can be replayed by new consumers.
///
/// Delivery guarantee: at-least-once (message survives Redis restarts if AOF/RDB persistence
/// is enabled; lost only if Redis is down at publish time AND retries are exhausted).
///
/// Consumer pattern: downstream systems (CareConnect, portals, automations) should
/// read via XREADGROUP for independent tracking and replay capability.
///
/// Failure handling: all exceptions caught internally — pipeline integrity is preserved.
/// Uses the shared <see cref="RedisResiliencePipeline"/> so Redis failures do not
/// produce uncontrolled command storms.
/// </summary>
public sealed class RedisStreamScanCompletionPublisher : IScanCompletionPublisher
{
    private readonly IConnectionMultiplexer                       _redis;
    private readonly RedisResiliencePipeline                      _resilience;
    private readonly ScanCompletionNotificationOptions            _opts;
    private readonly ILogger<RedisStreamScanCompletionPublisher>  _log;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented        = false,
    };

    public RedisStreamScanCompletionPublisher(
        IConnectionMultiplexer                          redis,
        RedisResiliencePipeline                         resilience,
        IOptions<NotificationOptions>                   opts,
        ILogger<RedisStreamScanCompletionPublisher>     log)
    {
        _redis      = redis;
        _resilience = resilience;
        _opts       = opts.Value.ScanCompletion;
        _log        = log;
    }

    public async ValueTask PublishAsync(DocumentScanCompletedEvent evt, CancellationToken ct = default)
    {
        var statusLabel = evt.ScanStatus.ToString().ToLowerInvariant();
        try
        {
            var streamKey = _opts.Redis.StreamKey;
            var payload   = JsonSerializer.Serialize(evt, JsonOpts);

            // Store event fields + full JSON payload so consumers can choose their parse path
            var fields = new NameValueEntry[]
            {
                new("eventId",       evt.EventId.ToString()),
                new("documentId",    evt.DocumentId.ToString()),
                new("tenantId",      evt.TenantId.ToString()),
                new("versionId",     evt.VersionId?.ToString() ?? string.Empty),
                new("scanStatus",    evt.ScanStatus.ToString()),
                new("occurredAt",    evt.OccurredAt.ToString("O")),
                new("correlationId", evt.CorrelationId ?? string.Empty),
                new("attemptCount",  evt.AttemptCount.ToString()),
                new("engineVersion", evt.EngineVersion ?? string.Empty),
                new("fileName",      evt.FileName ?? string.Empty),
                new("serviceName",   evt.ServiceName),
                new("payload",       payload),
            };

            await _resilience.ExecuteAsync(async () =>
            {
                var db = _redis.GetDatabase();
                await db.StreamAddAsync(
                    streamKey,
                    fields,
                    maxLength: _opts.Redis.StreamMaxLength > 0 ? (int?)_opts.Redis.StreamMaxLength : null,
                    useApproximateMaxLength: true);
            });

            RedisMetrics.ScanCompletionEventsEmitted.WithLabels(statusLabel).Inc();
            RedisMetrics.ScanCompletionDeliverySuccess.Inc();
            RedisMetrics.ScanCompletionStreamPublishTotal.Inc();

            _log.LogDebug(
                "DocumentScanCompleted XADD: StreamKey={Key} EventId={EventId} " +
                "DocumentId={DocId} Status={Status} CorrelationId={Corr}",
                streamKey, evt.EventId, evt.DocumentId, evt.ScanStatus, evt.CorrelationId);
        }
        catch (Exception ex)
        {
            RedisMetrics.ScanCompletionDeliveryFailures.Inc();
            RedisMetrics.ScanCompletionStreamPublishFailures.Inc();
            // Increment emitted even on failure so delivery rate can be computed
            RedisMetrics.ScanCompletionEventsEmitted.WithLabels(statusLabel).Inc();

            _log.LogWarning(ex,
                "RedisStreamScanCompletionPublisher: XADD failed for Document={DocId} " +
                "Status={Status} CorrelationId={Corr}. Scan state is unaffected.",
                evt.DocumentId, evt.ScanStatus, evt.CorrelationId);
        }
    }
}
