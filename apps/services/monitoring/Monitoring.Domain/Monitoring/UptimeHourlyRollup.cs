namespace Monitoring.Domain.Monitoring;

/// <summary>
/// Durable hourly uptime rollup for a single monitored entity.
///
/// <para>Each row covers exactly one UTC hour window (bucket_hour_utc is the
/// start of the hour) for one monitored entity. Rows are created or replaced
/// by the <c>UptimeAggregationHostedService</c>, which derives them from the
/// canonical <see cref="CheckResultRecord"/> history. They are never derived
/// from alert state.</para>
///
/// <para><b>Immutability</b>: rows are fully recomputed (upserted) by the
/// aggregation engine. The engine is idempotent — rerunning it over the same
/// check results produces the same rollup values.</para>
///
/// <para><b>State mapping</b>: CheckOutcome is mapped to uptime states:
/// <list type="bullet">
///   <item>Success → Up</item>
///   <item>NonSuccessStatusCode → Degraded (reachable but non-2xx)</item>
///   <item>Timeout | NetworkFailure | InvalidTarget | UnexpectedFailure → Down</item>
///   <item>Skipped → Unknown (excluded from uptime denominator)</item>
/// </list>
/// </para>
///
/// <para><b>Uptime formula</b>: denominator = up + down + degraded (unknown
/// excluded). <c>UptimeRatio = up / denominator</c>. If denominator is zero
/// (no countable checks), both ratios are null and
/// <c>InsufficientData = true</c>.</para>
/// </summary>
public class UptimeHourlyRollup
{
    public const int EntityNameMaxLength = 200;

    public Guid Id { get; private set; }

    /// <summary>Foreign key to the monitored entity. Snapshot of identity only.</summary>
    public Guid MonitoredEntityId { get; private set; }

    /// <summary>Snapshotted display name for query convenience.</summary>
    public string EntityName { get; private set; } = string.Empty;

    /// <summary>UTC start of the one-hour window this rollup covers.</summary>
    public DateTime BucketHourUtc { get; private set; }

    /// <summary>Checks that produced <see cref="CheckOutcome.Success"/>.</summary>
    public int UpCount { get; private set; }

    /// <summary>Checks that produced <see cref="CheckOutcome.NonSuccessStatusCode"/> (reachable but non-2xx).</summary>
    public int DegradedCount { get; private set; }

    /// <summary>Checks classified as hard-down outcomes.</summary>
    public int DownCount { get; private set; }

    /// <summary>Checks that produced <see cref="CheckOutcome.Skipped"/> (excluded from denominator).</summary>
    public int UnknownCount { get; private set; }

    /// <summary>Total checks in this bucket (Up + Degraded + Down + Unknown).</summary>
    public int TotalCount { get; private set; }

    /// <summary>Sum of elapsed_ms for average-latency computation.</summary>
    public long SumElapsedMs { get; private set; }

    /// <summary>Maximum elapsed_ms observed in this bucket.</summary>
    public long MaxElapsedMs { get; private set; }

    /// <summary>
    /// Strict uptime ratio: <c>up / (up + degraded + down)</c>.
    /// Null when no countable checks exist (InsufficientData).
    /// </summary>
    public double? UptimeRatio { get; private set; }

    /// <summary>
    /// Weighted availability: <c>(up + degraded * 0.5) / (up + degraded + down)</c>.
    /// Null when no countable checks exist.
    /// </summary>
    public double? WeightedAvailability { get; private set; }

    /// <summary>True when the denominator was zero and no ratio could be computed.</summary>
    public bool InsufficientData { get; private set; }

    /// <summary>Wall-clock time when this row was last recomputed.</summary>
    public DateTime ComputedAtUtc { get; private set; }

    /// <summary>Row insert time (set once at creation; not updated on recompute).</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>EF Core constructor.</summary>
    private UptimeHourlyRollup() { }

    public UptimeHourlyRollup(
        Guid   id,
        Guid   monitoredEntityId,
        string entityName,
        DateTime bucketHourUtc,
        int  upCount,
        int  degradedCount,
        int  downCount,
        int  unknownCount,
        long sumElapsedMs,
        long maxElapsedMs,
        DateTime computedAtUtc,
        DateTime createdAtUtc)
    {
        if (id == Guid.Empty) throw new ArgumentException("Id must not be empty.", nameof(id));
        if (monitoredEntityId == Guid.Empty)
            throw new ArgumentException("MonitoredEntityId must not be empty.", nameof(monitoredEntityId));

        Id                = id;
        MonitoredEntityId = monitoredEntityId;
        EntityName        = (entityName ?? string.Empty).Trim();
        BucketHourUtc     = DateTime.SpecifyKind(bucketHourUtc, DateTimeKind.Utc);
        UpCount           = upCount;
        DegradedCount     = degradedCount;
        DownCount         = downCount;
        UnknownCount      = unknownCount;
        TotalCount        = upCount + degradedCount + downCount + unknownCount;
        SumElapsedMs      = sumElapsedMs;
        MaxElapsedMs      = maxElapsedMs;
        ComputedAtUtc     = DateTime.SpecifyKind(computedAtUtc, DateTimeKind.Utc);
        CreatedAtUtc      = DateTime.SpecifyKind(createdAtUtc, DateTimeKind.Utc);

        ComputeRatios();
    }

    /// <summary>
    /// Updates this rollup in-place with recomputed statistics.
    /// Called by the aggregation engine on subsequent runs over the same bucket.
    /// </summary>
    public void Update(
        string entityName,
        int  upCount,
        int  degradedCount,
        int  downCount,
        int  unknownCount,
        long sumElapsedMs,
        long maxElapsedMs,
        DateTime computedAtUtc)
    {
        EntityName    = (entityName ?? string.Empty).Trim();
        UpCount       = upCount;
        DegradedCount = degradedCount;
        DownCount     = downCount;
        UnknownCount  = unknownCount;
        TotalCount    = upCount + degradedCount + downCount + unknownCount;
        SumElapsedMs  = sumElapsedMs;
        MaxElapsedMs  = maxElapsedMs;
        ComputedAtUtc = DateTime.SpecifyKind(computedAtUtc, DateTimeKind.Utc);

        ComputeRatios();
    }

    private void ComputeRatios()
    {
        var denominator = UpCount + DegradedCount + DownCount;
        if (denominator == 0)
        {
            UptimeRatio          = null;
            WeightedAvailability = null;
            InsufficientData     = true;
        }
        else
        {
            UptimeRatio          = (double)UpCount / denominator;
            WeightedAvailability = (UpCount + DegradedCount * 0.5) / denominator;
            InsufficientData     = false;
        }
    }
}
