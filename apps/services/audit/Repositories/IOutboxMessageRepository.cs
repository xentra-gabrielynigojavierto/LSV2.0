using PlatformAuditEventService.Entities;

namespace PlatformAuditEventService.Repositories;

/// <summary>
/// Persistence contract for the transactional outbox.
///
/// The outbox relay (<see cref="Jobs.OutboxRelayHostedService"/>) polls for
/// unprocessed messages and marks them as processed after successful delivery.
/// </summary>
public interface IOutboxMessageRepository
{
    /// <summary>
    /// Persist a new outbox message (called in the same transaction as AuditEventRecord append).
    /// </summary>
    Task<OutboxMessage> CreateAsync(OutboxMessage message, CancellationToken ct = default);

    /// <summary>
    /// List messages that are pending delivery: ProcessedAtUtc is null AND IsPermanentlyFailed is false.
    /// Ordered by CreatedAtUtc ascending (FIFO delivery).
    /// </summary>
    Task<IReadOnlyList<OutboxMessage>> ListPendingAsync(int batchSize, CancellationToken ct = default);

    /// <summary>
    /// Mark a message as successfully published (sets ProcessedAtUtc).
    /// </summary>
    Task MarkProcessedAsync(long id, DateTimeOffset processedAtUtc, CancellationToken ct = default);

    /// <summary>
    /// Increment retry counter and record the error. If RetryCount >= maxRetries, sets IsPermanentlyFailed=true.
    /// </summary>
    Task MarkFailedAsync(long id, string error, int maxRetries, CancellationToken ct = default);
}
