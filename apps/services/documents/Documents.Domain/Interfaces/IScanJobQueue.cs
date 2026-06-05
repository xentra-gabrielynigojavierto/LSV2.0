using Documents.Domain.Entities;

namespace Documents.Domain.Interfaces;

/// <summary>
/// Port interface for the scan job queue.
/// Implementations: InMemoryScanJobQueue (dev), RedisScanJobQueue (production).
/// The IScanJobQueue contract uses a lease pattern so each backend can
/// implement at-least-once delivery guarantees correctly.
/// </summary>
public interface IScanJobQueue
{
    /// <summary>
    /// Attempt to enqueue a scan job without blocking.
    /// Returns false if the queue is at capacity — callers MUST fail fast and
    /// NOT retry indefinitely; surface a 503 to the client.
    /// </summary>
    ValueTask<bool> TryEnqueueAsync(ScanJob job, CancellationToken ct = default);

    /// <summary>
    /// Dequeue a job lease. Blocks until a job is available, the token is
    /// cancelled, or the queue is permanently closed (returns null).
    /// </summary>
    ValueTask<ScanJobLease?> DequeueAsync(string consumerId, CancellationToken ct = default);

    /// <summary>
    /// Acknowledge successful processing. Removes the job from the queue
    /// and the delivery guarantee tracking (PEL for Redis Streams).
    /// </summary>
    ValueTask AcknowledgeAsync(ScanJobLease lease, CancellationToken ct = default);

    /// <summary>
    /// Negative-acknowledge — return the job for retry with its attempt
    /// count incremented. Callers should apply exponential backoff delay
    /// before calling this.
    /// </summary>
    ValueTask NackAsync(ScanJobLease lease, CancellationToken ct = default);

    /// <summary>Approximate number of pending/unacknowledged jobs.</summary>
    int Count { get; }
}
