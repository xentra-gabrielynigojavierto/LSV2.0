using System.Collections.Concurrent;
using Reports.Contracts.Queue;

namespace Reports.Infrastructure.Queue;

public sealed class InMemoryJobQueue : IJobQueue
{
    private readonly ConcurrentQueue<ReportJob> _queue = new();

    public Task EnqueueAsync(ReportJob job, CancellationToken ct)
    {
        _queue.Enqueue(job);
        return Task.CompletedTask;
    }

    public Task<ReportJob?> DequeueAsync(CancellationToken ct)
    {
        _queue.TryDequeue(out var job);
        return Task.FromResult(job);
    }

    public Task<int> GetPendingCountAsync(CancellationToken ct)
    {
        return Task.FromResult(_queue.Count);
    }
}
