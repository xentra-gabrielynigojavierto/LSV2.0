using System.Threading.Channels;
using Documents.Domain.Entities;
using Documents.Domain.Interfaces;
using Documents.Infrastructure.Observability;
using Microsoft.Extensions.Logging;

namespace Documents.Infrastructure.Scanner;

/// <summary>
/// In-process, bounded Channel-based scan job queue.
/// Suitable for local development and single-instance deployments.
/// Jobs are LOST on process restart — use RedisScanJobQueue for production.
///
/// Backpressure strategy: TryEnqueueAsync returns false immediately if the
/// channel is full (DropWrite mode), giving the caller the chance to fail fast
/// rather than blocking the HTTP request indefinitely.
/// </summary>
public sealed class InMemoryScanJobQueue : IScanJobQueue
{
    private readonly Channel<ScanJobLease>         _channel;
    private readonly ILogger<InMemoryScanJobQueue> _log;

    public int Count => _channel.Reader.Count;

    public InMemoryScanJobQueue(
        ILogger<InMemoryScanJobQueue> log,
        int capacity = 1_000)
    {
        _log = log;

        // DropWrite mode: TryWrite returns false when full — no blocking.
        var opts = new BoundedChannelOptions(capacity)
        {
            FullMode     = BoundedChannelFullMode.DropWrite,
            SingleReader = false,  // multiple workers allowed
            SingleWriter = false,
        };

        _channel = Channel.CreateBounded<ScanJobLease>(opts);
    }

    public ValueTask<bool> TryEnqueueAsync(ScanJob job, CancellationToken ct = default)
    {
        var lease = new ScanJobLease { Job = job, MessageId = string.Empty };
        var written = _channel.Writer.TryWrite(lease);

        if (written)
        {
            ScanMetrics.ScanJobsEnqueued.Inc();
            ScanMetrics.ScanQueueDepth.Set(_channel.Reader.Count);
            _log.LogDebug("ScanQueue(mem): enqueued Document={DocId} Version={VersionId} Attempt={Attempt}",
                job.DocumentId, job.VersionId, job.AttemptCount);
        }
        else
        {
            ScanMetrics.ScanQueueSaturations.Inc();
            _log.LogWarning("ScanQueue(mem): queue full — Document={DocId} rejected", job.DocumentId);
        }

        return ValueTask.FromResult(written);
    }

    public async ValueTask<ScanJobLease?> DequeueAsync(string consumerId, CancellationToken ct = default)
    {
        try
        {
            return await _channel.Reader.ReadAsync(ct);
        }
        catch (ChannelClosedException) { return null; }
        catch (OperationCanceledException) { return null; }
    }

    public ValueTask AcknowledgeAsync(ScanJobLease lease, CancellationToken ct = default)
        => ValueTask.CompletedTask;  // no persistence in-memory

    public async ValueTask NackAsync(ScanJobLease lease, CancellationToken ct = default)
    {
        // Re-enqueue with incremented attempt count for retry
        var src = lease.Job;
        var retryJob = new ScanJob
        {
            DocumentId   = src.DocumentId,
            TenantId     = src.TenantId,
            VersionId    = src.VersionId,
            StorageKey   = src.StorageKey,
            FileName     = src.FileName,
            MimeType     = src.MimeType,
            EnqueuedAt   = DateTime.UtcNow,
            AttemptCount = src.AttemptCount + 1,
        };
        var retryLease = new ScanJobLease { Job = retryJob, MessageId = string.Empty };

        var written = _channel.Writer.TryWrite(retryLease);
        if (!written)
        {
            _log.LogError(
                "ScanQueue(mem): failed to re-enqueue retry for Document={DocId} — queue full",
                lease.Job.DocumentId);
        }
        await ValueTask.CompletedTask;
    }
}
