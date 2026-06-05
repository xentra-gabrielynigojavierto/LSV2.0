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
/// Redis Pub/Sub publisher — publishes <see cref="DocumentScanCompletedEvent"/> as a JSON
/// message to a configurable Redis channel after each terminal scan outcome.
///
/// Delivery guarantee: best-effort, at-most-once.
/// Redis Pub/Sub delivers to currently connected subscribers only; messages are not persisted.
///
/// Production recommendation: prefer <see cref="RedisStreamScanCompletionPublisher"/>
/// (Provider="redis-stream") for durable, replayable delivery.
///
/// Wrapped in the shared <see cref="RedisResiliencePipeline"/> so Redis unavailability
/// opens the circuit breaker and fast-fails rather than hammering Redis.
/// Failure handling: all exceptions caught internally — scan pipeline is never interrupted.
/// </summary>
public sealed class RedisScanCompletionPublisher : IScanCompletionPublisher
{
    private readonly IConnectionMultiplexer                _redis;
    private readonly RedisResiliencePipeline               _resilience;
    private readonly ScanCompletionNotificationOptions     _opts;
    private readonly ILogger<RedisScanCompletionPublisher> _log;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented        = false,
    };

    public RedisScanCompletionPublisher(
        IConnectionMultiplexer                       redis,
        RedisResiliencePipeline                      resilience,
        IOptions<NotificationOptions>                opts,
        ILogger<RedisScanCompletionPublisher>        log)
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
            var channel = _opts.Redis.Channel;
            var payload = JsonSerializer.Serialize(evt, JsonOpts);

            long receiversHit = 0;
            await _resilience.ExecuteAsync(async () =>
            {
                var sub = _redis.GetSubscriber();
                receiversHit = await sub.PublishAsync(
                    RedisChannel.Literal(channel),
                    payload,
                    CommandFlags.FireAndForget);
            });

            RedisMetrics.ScanCompletionEventsEmitted.WithLabels(statusLabel).Inc();
            RedisMetrics.ScanCompletionDeliverySuccess.Inc();

            _log.LogDebug(
                "DocumentScanCompleted published: Channel={Channel} EventId={EventId} " +
                "DocumentId={DocId} Status={Status} Corr={Corr} Receivers={Count}",
                channel, evt.EventId, evt.DocumentId, evt.ScanStatus, evt.CorrelationId, receiversHit);
        }
        catch (Exception ex)
        {
            RedisMetrics.ScanCompletionDeliveryFailures.Inc();
            RedisMetrics.RedisConnectionFailures.Inc();
            // Increment emitted even on failure so operators can compute delivery rate
            RedisMetrics.ScanCompletionEventsEmitted.WithLabels(statusLabel).Inc();

            _log.LogWarning(ex,
                "RedisScanCompletionPublisher: publish failed for Document={DocId} " +
                "Status={Status} Corr={Corr}. Scan state is unaffected.",
                evt.DocumentId, evt.ScanStatus, evt.CorrelationId);
        }
    }
}
