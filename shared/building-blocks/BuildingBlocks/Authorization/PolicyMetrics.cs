using System.Diagnostics.Metrics;

namespace BuildingBlocks.Authorization;

public class PolicyMetrics
{
    public const string MeterName = "LegalSynq.Policy";

    private long _evaluationCount;
    private long _cacheHits;
    private long _cacheMisses;
    private long _cacheErrors;
    private long _versionReadCount;
    private long _totalEvaluationMs;
    private long _totalCacheReadMs;
    private long _totalVersionReadMs;
    private long _stampedeCoalesced;
    private long _freezeEvents;

    private readonly Meter _meter;
    private readonly Counter<long> _evalCounter;
    private readonly Counter<long> _cacheHitCounter;
    private readonly Counter<long> _cacheMissCounter;
    private readonly Counter<long> _cacheErrorCounter;
    private readonly Counter<long> _stampedeCounter;
    private readonly Counter<long> _freezeCounter;
    private readonly Histogram<double> _evalLatency;
    private readonly Histogram<double> _cacheReadLatency;
    private readonly Histogram<double> _versionReadLatency;

    public PolicyMetrics()
    {
        _meter = new Meter(MeterName, "1.0.0");

        _evalCounter = _meter.CreateCounter<long>("policy.evaluations.total", "evaluations", "Total policy evaluations");
        _cacheHitCounter = _meter.CreateCounter<long>("policy.cache.hits", "hits", "Cache hits");
        _cacheMissCounter = _meter.CreateCounter<long>("policy.cache.misses", "misses", "Cache misses");
        _cacheErrorCounter = _meter.CreateCounter<long>("policy.cache.errors", "errors", "Cache errors");
        _stampedeCounter = _meter.CreateCounter<long>("policy.stampede.coalesced", "requests", "Stampede-coalesced requests");
        _freezeCounter = _meter.CreateCounter<long>("policy.version.freeze_events", "events", "Version freeze events");
        _evalLatency = _meter.CreateHistogram<double>("policy.evaluation.duration_ms", "ms", "Evaluation latency");
        _cacheReadLatency = _meter.CreateHistogram<double>("policy.cache.read_duration_ms", "ms", "Cache read latency");
        _versionReadLatency = _meter.CreateHistogram<double>("policy.version.read_duration_ms", "ms", "Version read latency");

        _meter.CreateObservableGauge("policy.cache.hit_rate", () => CacheHitRate, "%", "Cache hit rate percentage");
        _meter.CreateObservableGauge("policy.evaluation.avg_duration_ms", () => AverageEvaluationMs, "ms", "Average evaluation latency");
    }

    public long EvaluationCount => Interlocked.Read(ref _evaluationCount);
    public long CacheHits => Interlocked.Read(ref _cacheHits);
    public long CacheMisses => Interlocked.Read(ref _cacheMisses);
    public long CacheErrors => Interlocked.Read(ref _cacheErrors);
    public long VersionReadCount => Interlocked.Read(ref _versionReadCount);
    public long TotalEvaluationMs => Interlocked.Read(ref _totalEvaluationMs);
    public long TotalCacheReadMs => Interlocked.Read(ref _totalCacheReadMs);
    public long TotalVersionReadMs => Interlocked.Read(ref _totalVersionReadMs);
    public long StampedeCoalesced => Interlocked.Read(ref _stampedeCoalesced);
    public long FreezeEvents => Interlocked.Read(ref _freezeEvents);

    public double AverageEvaluationMs => EvaluationCount > 0 ? (double)TotalEvaluationMs / EvaluationCount : 0;
    public double AverageCacheReadMs => (CacheHits + CacheMisses) > 0 ? (double)TotalCacheReadMs / (CacheHits + CacheMisses) : 0;
    public double CacheHitRate => (CacheHits + CacheMisses) > 0 ? (double)CacheHits / (CacheHits + CacheMisses) * 100 : 0;

    public void RecordEvaluation(long elapsedMs)
    {
        Interlocked.Increment(ref _evaluationCount);
        Interlocked.Add(ref _totalEvaluationMs, elapsedMs);
        _evalCounter.Add(1);
        _evalLatency.Record(elapsedMs);
    }

    public void RecordCacheHit(long elapsedMs)
    {
        Interlocked.Increment(ref _cacheHits);
        Interlocked.Add(ref _totalCacheReadMs, elapsedMs);
        _cacheHitCounter.Add(1);
        _cacheReadLatency.Record(elapsedMs);
    }

    public void RecordCacheMiss(long elapsedMs)
    {
        Interlocked.Increment(ref _cacheMisses);
        Interlocked.Add(ref _totalCacheReadMs, elapsedMs);
        _cacheMissCounter.Add(1);
        _cacheReadLatency.Record(elapsedMs);
    }

    public void RecordCacheError()
    {
        Interlocked.Increment(ref _cacheErrors);
        _cacheErrorCounter.Add(1);
    }

    public void RecordVersionRead(long elapsedMs)
    {
        Interlocked.Increment(ref _versionReadCount);
        Interlocked.Add(ref _totalVersionReadMs, elapsedMs);
        _versionReadLatency.Record(elapsedMs);
    }

    public void RecordStampedeCoalesced()
    {
        Interlocked.Increment(ref _stampedeCoalesced);
        _stampedeCounter.Add(1);
    }

    public void RecordFreezeEvent()
    {
        Interlocked.Increment(ref _freezeEvents);
        _freezeCounter.Add(1);
    }

    public PolicyMetricsSnapshot GetSnapshot() => new()
    {
        EvaluationCount = EvaluationCount,
        CacheHits = CacheHits,
        CacheMisses = CacheMisses,
        CacheErrors = CacheErrors,
        CacheHitRate = CacheHitRate,
        AverageEvaluationMs = AverageEvaluationMs,
        AverageCacheReadMs = AverageCacheReadMs,
        VersionReadCount = VersionReadCount,
        StampedeCoalesced = StampedeCoalesced,
        FreezeEvents = FreezeEvents,
    };
}

public class PolicyMetricsSnapshot
{
    public long EvaluationCount { get; set; }
    public long CacheHits { get; set; }
    public long CacheMisses { get; set; }
    public long CacheErrors { get; set; }
    public double CacheHitRate { get; set; }
    public double AverageEvaluationMs { get; set; }
    public double AverageCacheReadMs { get; set; }
    public long VersionReadCount { get; set; }
    public long StampedeCoalesced { get; set; }
    public long FreezeEvents { get; set; }
}
