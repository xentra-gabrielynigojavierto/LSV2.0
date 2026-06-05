using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notifications.Application.Interfaces;
using Notifications.Application.Options;
using Notifications.Domain;
using Notifications.Infrastructure.Data;

namespace Notifications.Infrastructure.Services;

/// <summary>
/// LS-NOTIF-SMS-016: Recipient intelligence aggregation and scoring service.
///
/// Reads local NotificationAttempt + Notification telemetry only.
/// No external provider calls. No external phone validation APIs.
///
/// Security: Extracts phone from Notification.RecipientJson, normalizes, and hashes it via
/// ISmsRecipientIdentityHasher. Raw phone is never stored, logged, or returned.
/// All snapshot fields are aggregate operational metrics only.
/// </summary>
public class SmsRecipientIntelligenceService : ISmsRecipientIntelligenceService
{
    private readonly NotificationsDbContext                _db;
    private readonly ISmsRecipientIdentityHasher           _hasher;
    private readonly SmsRecipientIntelligenceOptions       _opts;
    private readonly ILogger<SmsRecipientIntelligenceService> _logger;

    // Statuses considered delivered
    private static readonly HashSet<string> DeliveredStatuses = new(StringComparer.OrdinalIgnoreCase)
        { "sent", "delivered", "completed" };

    // Statuses considered failed / dead-letter
    private static readonly HashSet<string> FailedStatuses = new(StringComparer.OrdinalIgnoreCase)
        { "failed", "dead_letter", "permanently_failed" };

    // Failure categories that indicate an invalid destination
    private static readonly HashSet<string> InvalidDestinationCategories = new(StringComparer.OrdinalIgnoreCase)
        { "invalid_recipient", "invalid_destination", "non_retryable_failure" };

    // Failure categories that indicate carrier rejection
    private static readonly HashSet<string> CarrierRejectedCategories = new(StringComparer.OrdinalIgnoreCase)
        { "carrier_rejected", "non_retryable_failure" };

    public SmsRecipientIntelligenceService(
        NotificationsDbContext db,
        ISmsRecipientIdentityHasher hasher,
        IOptions<SmsRecipientIntelligenceOptions> opts,
        ILogger<SmsRecipientIntelligenceService> logger)
    {
        _db     = db;
        _hasher = hasher;
        _opts   = opts.Value;
        _logger = logger;
    }

    public async Task CalculateSnapshotsAsync(
        DateTime windowStart,
        DateTime windowEnd,
        CancellationToken ct)
    {
        _logger.LogDebug(
            "SmsRecipientIntelligenceService: calculating snapshots [{Start} → {End}]",
            windowStart, windowEnd);

        // Join attempts with notifications to extract RecipientJson.
        // Bounded to avoid unbounded queries.
        var data = await _db.NotificationAttempts
            .Where(a => a.Channel == "sms"
                     && a.CreatedAt >= windowStart
                     && a.CreatedAt <= windowEnd)
            .Join(
                _db.Notifications,
                a => a.NotificationId,
                n => n.Id,
                (a, n) => new AttemptRecipientProjection
                {
                    AttemptId         = a.Id,
                    NotificationId    = a.NotificationId,
                    TenantId          = a.TenantId,
                    Provider          = a.Provider,
                    Status            = a.Status,
                    AttemptNumber     = a.AttemptNumber,
                    IsFailover        = a.IsFailover,
                    FailureCategory   = a.FailureCategory,
                    CreatedAt         = a.CreatedAt,
                    CompletedAt       = a.CompletedAt,
                    RecipientJson     = n.RecipientJson,
                })
            .Take(_opts.MaxAttemptsPerWindow)
            .ToListAsync(ct);

        if (data.Count == 0)
        {
            _logger.LogDebug("SmsRecipientIntelligenceService: no SMS attempts in window — skipping");
            return;
        }

        // Assign recipient hash to each projection (never stored as phone)
        var enriched = new List<(AttemptRecipientProjection Row, string? Hash)>(data.Count);
        foreach (var row in data)
        {
            var phone = ExtractPhone(row.RecipientJson);
            var hash  = _hasher.HashRecipient(phone);
            enriched.Add((row, hash));
        }

        // Group by (hash, tenantId, provider) to compute per-recipient-provider stats
        var groups = enriched
            .Where(e => e.Hash != null)
            .GroupBy(e => (Hash: e.Hash!, TenantId: e.Row.TenantId, Provider: e.Row.Provider))
            .Take(_opts.MaxSnapshotsPerCycle);

        var snapshots = new List<SmsRecipientReputationSnapshot>();

        foreach (var g in groups)
        {
            var rows = g.Select(e => e.Row).ToList();
            var snap = BuildSnapshot(g.Key.Hash, g.Key.TenantId, g.Key.Provider, rows);
            snapshots.Add(snap);
        }

        // Upsert snapshots
        foreach (var snap in snapshots)
        {
            var existing = await _db.SmsRecipientReputationSnapshots
                .FirstOrDefaultAsync(s =>
                    s.RecipientHash == snap.RecipientHash &&
                    s.TenantId      == snap.TenantId &&
                    s.ProviderType  == snap.ProviderType, ct);

            if (existing == null)
                _db.SmsRecipientReputationSnapshots.Add(snap);
            else
            {
                existing.TotalAttempts               = snap.TotalAttempts;
                existing.DeliveredAttempts           = snap.DeliveredAttempts;
                existing.FailedAttempts              = snap.FailedAttempts;
                existing.RetryAttempts               = snap.RetryAttempts;
                existing.DeadLetterAttempts          = snap.DeadLetterAttempts;
                existing.CarrierRejectedAttempts     = snap.CarrierRejectedAttempts;
                existing.InvalidDestinationAttempts  = snap.InvalidDestinationAttempts;
                existing.AverageLatencyMs            = snap.AverageLatencyMs;
                existing.DeliverySuccessRate         = snap.DeliverySuccessRate;
                existing.FailureRate                 = snap.FailureRate;
                existing.RetryRate                   = snap.RetryRate;
                existing.DeadLetterRate              = snap.DeadLetterRate;
                existing.CarrierFailureRate          = snap.CarrierFailureRate;
                existing.InvalidNumberRisk           = snap.InvalidNumberRisk;
                existing.RetrySuppressionRisk        = snap.RetrySuppressionRisk;
                existing.QualityScore                = snap.QualityScore;
                existing.DestinationRiskLevel        = snap.DestinationRiskLevel;
                existing.LastAttemptAt               = snap.LastAttemptAt;
                existing.CalculatedAt                = snap.CalculatedAt;
            }
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "SmsRecipientIntelligenceService: saved {Count} recipient snapshots for window [{Start} → {End}]",
            snapshots.Count, windowStart, windowEnd);
    }

    public async Task<SmsRecipientReputationSnapshot?> GetRecipientSnapshotAsync(
        string recipientHash,
        Guid?  tenantId,
        CancellationToken ct)
    {
        return await _db.SmsRecipientReputationSnapshots
            .Where(s => s.RecipientHash == recipientHash &&
                        s.TenantId      == tenantId)
            .OrderByDescending(s => s.CalculatedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<(IReadOnlyList<SmsRecipientReputationSnapshot> Items, int Total)> QueryRecipientAnalyticsAsync(
        SmsRecipientAnalyticsQuery query,
        CancellationToken ct)
    {
        var q = _db.SmsRecipientReputationSnapshots.AsQueryable();

        if (query.TenantId.HasValue)    q = q.Where(s => s.TenantId    == query.TenantId);
        if (!string.IsNullOrEmpty(query.Provider))    q = q.Where(s => s.ProviderType == query.Provider);
        if (!string.IsNullOrEmpty(query.CountryCode)) q = q.Where(s => s.CountryCode  == query.CountryCode);
        if (!string.IsNullOrEmpty(query.Region))      q = q.Where(s => s.Region       == query.Region);
        if (!string.IsNullOrEmpty(query.RiskLevel))   q = q.Where(s => s.DestinationRiskLevel == query.RiskLevel);
        if (query.From.HasValue) q = q.Where(s => s.CalculatedAt >= query.From.Value);
        if (query.To.HasValue)   q = q.Where(s => s.CalculatedAt <= query.To.Value);

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(s => s.RetrySuppressionRisk)
            .ThenByDescending(s => s.CalculatedAt)
            .Skip(query.Offset)
            .Take(Math.Min(query.Limit, 200))
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<(IReadOnlyList<SmsSuppressionDecision> Items, int Total)> QuerySuppressionDecisionsAsync(
        SmsSuppressionDecisionQuery query,
        CancellationToken ct)
    {
        var q = _db.SmsSuppressionDecisions.AsQueryable();

        if (query.TenantId.HasValue)    q = q.Where(d => d.TenantId    == query.TenantId);
        if (!string.IsNullOrEmpty(query.DecisionType)) q = q.Where(d => d.DecisionType == query.DecisionType);
        if (!string.IsNullOrEmpty(query.ReasonCode))   q = q.Where(d => d.ReasonCode   == query.ReasonCode);
        if (!string.IsNullOrEmpty(query.Provider))     q = q.Where(d => d.ProviderType == query.Provider);
        if (!string.IsNullOrEmpty(query.CountryCode))  q = q.Where(d => d.CountryCode  == query.CountryCode);
        if (query.From.HasValue) q = q.Where(d => d.CreatedAt >= query.From.Value);
        if (query.To.HasValue)   q = q.Where(d => d.CreatedAt <= query.To.Value);

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(d => d.CreatedAt)
            .Skip(query.Offset)
            .Take(Math.Min(query.Limit, 200))
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task PersistSuppressionDecisionAsync(
        SmsSuppressionDecision decision,
        CancellationToken ct)
    {
        _db.SmsSuppressionDecisions.Add(decision);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<Dictionary<string, long>> GetRiskDistributionAsync(
        Guid? tenantId,
        string? countryCode,
        CancellationToken ct)
    {
        var q = _db.SmsRecipientReputationSnapshots.AsQueryable();
        if (tenantId.HasValue)             q = q.Where(s => s.TenantId    == tenantId);
        if (!string.IsNullOrEmpty(countryCode)) q = q.Where(s => s.CountryCode == countryCode);

        return await q
            .GroupBy(s => s.DestinationRiskLevel)
            .Select(g => new { Level = g.Key, Count = (long)g.Count() })
            .ToDictionaryAsync(x => x.Level, x => x.Count, ct);
    }

    // ── Internal helpers ─────────────────────────────────────────────────────

    private SmsRecipientReputationSnapshot BuildSnapshot(
        string hash,
        Guid?  tenantId,
        string? providerType,
        List<AttemptRecipientProjection> rows)
    {
        var total     = rows.Count;
        var delivered = rows.Count(r => DeliveredStatuses.Contains(r.Status));
        var failed    = rows.Count(r => FailedStatuses.Contains(r.Status));
        var retry     = rows.Count(r => r.AttemptNumber > 1 || r.IsFailover);
        var dead      = rows.Count(r => string.Equals(r.Status, "dead_letter", StringComparison.OrdinalIgnoreCase));
        var carrierRej = rows.Count(r => r.FailureCategory != null && IsCarrierRejection(r.FailureCategory));
        var invalDest  = rows.Count(r => r.FailureCategory != null && IsInvalidDestination(r.FailureCategory));

        var delivered_ms = rows
            .Where(r => DeliveredStatuses.Contains(r.Status) && r.CompletedAt.HasValue)
            .Select(r => (decimal)(r.CompletedAt!.Value - r.CreatedAt).TotalMilliseconds)
            .ToList();
        var avgLatency = delivered_ms.Count > 0 ? delivered_ms.Average() : (decimal?)null;

        var lastAttempt = rows.Max(r => (DateTime?)r.CreatedAt);

        var t = (decimal)total;
        var deliveryRate  = t > 0 ? (decimal)delivered / t : 0m;
        var failureRate   = t > 0 ? (decimal)failed    / t : 0m;
        var retryRate     = t > 0 ? (decimal)retry     / t : 0m;
        var deadRate      = t > 0 ? (decimal)dead      / t : 0m;
        var carrierRate   = t > 0 ? (decimal)carrierRej / t : 0m;

        var invalidRisk   = ComputeInvalidNumberRisk(invalDest, carrierRej, total);
        var suppressRisk  = ComputeRetrySuppressionRisk(failureRate, deadRate, retryRate, carrierRate);
        var qualityScore  = ComputeQualityScore(deliveryRate, failureRate, deadRate, carrierRate);
        var riskLevel     = ClassifyRisk(suppressRisk, invalidRisk, qualityScore);

        return new SmsRecipientReputationSnapshot
        {
            RecipientHash              = hash,
            TenantId                   = tenantId,
            ProviderType               = providerType,
            TotalAttempts              = total,
            DeliveredAttempts          = delivered,
            FailedAttempts             = failed,
            RetryAttempts              = retry,
            DeadLetterAttempts         = dead,
            CarrierRejectedAttempts    = carrierRej,
            InvalidDestinationAttempts = invalDest,
            AverageLatencyMs           = avgLatency.HasValue ? Math.Round(avgLatency.Value, 2) : null,
            DeliverySuccessRate        = Math.Round(deliveryRate, 4),
            FailureRate                = Math.Round(failureRate, 4),
            RetryRate                  = Math.Round(retryRate, 4),
            DeadLetterRate             = Math.Round(deadRate, 4),
            CarrierFailureRate         = Math.Round(carrierRate, 4),
            InvalidNumberRisk          = Math.Round(invalidRisk, 2),
            RetrySuppressionRisk       = Math.Round(suppressRisk, 2),
            QualityScore               = Math.Round(qualityScore, 2),
            DestinationRiskLevel       = riskLevel,
            LastAttemptAt              = lastAttempt,
            CalculatedAt               = DateTime.UtcNow,
        };
    }

    private decimal ComputeInvalidNumberRisk(int invalDest, int carrierRej, int total)
    {
        if (total == 0) return 0m;
        // Weight invalid destination higher than carrier rejection
        var score = ((invalDest * 1.5m) + (carrierRej * 0.7m)) / total;
        return Math.Min(100m, score * 100m);
    }

    private decimal ComputeRetrySuppressionRisk(
        decimal failureRate,
        decimal deadRate,
        decimal retryRate,
        decimal carrierRate)
    {
        var score = (failureRate   * _opts.FailurePenaltyWeight)
                  + (deadRate      * _opts.DeadLetterPenaltyWeight)
                  + (retryRate     * 0.10m)
                  + (carrierRate   * _opts.CarrierFailurePenaltyWeight);
        return Math.Min(100m, score * 200m); // scaled to 0-100
    }

    private decimal ComputeQualityScore(
        decimal deliveryRate,
        decimal failureRate,
        decimal deadRate,
        decimal carrierRate)
    {
        var reward  = deliveryRate  * _opts.DeliverySuccessWeight;
        var penalty = (failureRate  * _opts.FailurePenaltyWeight)
                    + (deadRate     * _opts.DeadLetterPenaltyWeight)
                    + (carrierRate  * _opts.CarrierFailurePenaltyWeight);
        var raw = (reward - penalty) * 100m;
        return Math.Max(0m, Math.Min(100m, raw + 50m)); // centre at 50, clamp
    }

    private string ClassifyRisk(decimal suppressRisk, decimal invalidRisk, decimal qualityScore)
    {
        if (suppressRisk >= _opts.HardSuppressionThreshold || invalidRisk >= _opts.InvalidNumberReviewThreshold)
            return "suppressed";
        if (suppressRisk >= _opts.SoftSuppressionThreshold || invalidRisk >= 60m)
            return "high";
        if (suppressRisk >= _opts.WarnSuppressionThreshold || qualityScore < 40m)
            return "medium";
        return "low";
    }

    private static string? ExtractPhone(string? recipientJson)
    {
        if (string.IsNullOrEmpty(recipientJson)) return null;
        try
        {
            var doc = JsonDocument.Parse(recipientJson);
            return doc.RootElement.TryGetProperty("phone", out var p) ? p.GetString() : null;
        }
        catch { return null; }
    }

    private static bool IsCarrierRejection(string failureCategory) =>
        CarrierRejectedCategories.Contains(failureCategory);

    private static bool IsInvalidDestination(string failureCategory) =>
        InvalidDestinationCategories.Contains(failureCategory);

    // ── Private projection ───────────────────────────────────────────────────

    private sealed class AttemptRecipientProjection
    {
        public Guid    AttemptId       { get; init; }
        public Guid    NotificationId  { get; init; }
        public Guid?   TenantId        { get; init; }
        public string  Provider        { get; init; } = string.Empty;
        public string  Status          { get; init; } = string.Empty;
        public int     AttemptNumber   { get; init; }
        public bool    IsFailover      { get; init; }
        public string? FailureCategory { get; init; }
        public DateTime  CreatedAt     { get; init; }
        public DateTime? CompletedAt   { get; init; }
        public string    RecipientJson { get; init; } = string.Empty;
    }
}
