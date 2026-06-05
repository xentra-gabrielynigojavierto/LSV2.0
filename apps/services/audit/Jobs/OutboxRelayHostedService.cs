using PlatformAuditEventService.Repositories;
using PlatformAuditEventService.Services.Forwarding;

namespace PlatformAuditEventService.Jobs;

/// <summary>
/// Periodic <see cref="BackgroundService"/> that relays pending outbox messages to the
/// configured integration event publisher.
///
/// This implements the "relay" half of the Transactional Outbox pattern:
///   1. Poll for unpublished messages (ProcessedAtUtc IS NULL AND IsPermanentlyFailed=false).
///   2. For each message, call <see cref="IIntegrationEventPublisher.PublishAsync"/>.
///   3. On success: call <see cref="IOutboxMessageRepository.MarkProcessedAsync"/>.
///   4. On failure: increment retry count. If retries exhausted, mark permanently failed.
///
/// Configuration:
///   Outbox:RelayIntervalSeconds (default: 10)
///   Outbox:BatchSize            (default: 100)
///   Outbox:MaxRetries           (default: 5)
///   Outbox:Enabled              (default: true — only active when EventForwarding:Enabled=true)
/// </summary>
public sealed class OutboxRelayHostedService : BackgroundService
{
    private readonly IServiceScopeFactory                 _scopeFactory;
    private readonly ILogger<OutboxRelayHostedService>    _logger;

    // Configuration — these are read at startup.
    private readonly int _relayIntervalSeconds;
    private readonly int _batchSize;
    private readonly int _maxRetries;
    private readonly bool _enabled;

    public OutboxRelayHostedService(
        IServiceScopeFactory              scopeFactory,
        IConfiguration                    configuration,
        ILogger<OutboxRelayHostedService> logger)
    {
        _scopeFactory         = scopeFactory;
        _logger               = logger;

        var section               = configuration.GetSection("Outbox");
        _relayIntervalSeconds     = section.GetValue("RelayIntervalSeconds", 10);
        _batchSize                = section.GetValue("BatchSize", 100);
        _maxRetries               = section.GetValue("MaxRetries", 5);
        _enabled                  = section.GetValue("Enabled", true);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            _logger.LogInformation(
                "OutboxRelayHostedService: disabled (Outbox:Enabled=false). " +
                "Set to true to enable transactional outbox relay.");
            return;
        }

        var interval = TimeSpan.FromSeconds(_relayIntervalSeconds > 0 ? _relayIntervalSeconds : 10);

        _logger.LogInformation(
            "OutboxRelayHostedService: starting. Interval={Interval:g} BatchSize={Batch} MaxRetries={Retries}",
            interval, _batchSize, _maxRetries);

        using var timer = new PeriodicTimer(interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RelayBatchAsync(stoppingToken);
        }
    }

    private async Task RelayBatchAsync(CancellationToken ct)
    {
        try
        {
            await using var scope     = _scopeFactory.CreateAsyncScope();
            var outboxRepo            = scope.ServiceProvider.GetRequiredService<IOutboxMessageRepository>();
            var publisher             = scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();

            var messages = await outboxRepo.ListPendingAsync(_batchSize, ct);

            if (messages.Count == 0) return;

            _logger.LogDebug(
                "OutboxRelayHostedService: relaying {Count} messages.", messages.Count);

            foreach (var message in messages)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    await publisher.PublishRawAsync(message.EventType, message.PayloadJson, ct);
                    await outboxRepo.MarkProcessedAsync(message.Id, DateTimeOffset.UtcNow, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "OutboxRelayHostedService: failed to relay MessageId={MessageId} " +
                        "EventType={EventType}. Retry {Retry}/{Max}.",
                        message.MessageId, message.EventType,
                        message.RetryCount + 1, _maxRetries);

                    await outboxRepo.MarkFailedAsync(message.Id, ex.Message, _maxRetries, ct);
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OutboxRelayHostedService: unexpected error in relay loop.");
        }
    }
}
