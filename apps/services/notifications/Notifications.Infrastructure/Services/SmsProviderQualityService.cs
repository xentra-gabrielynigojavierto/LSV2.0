using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notifications.Application.Interfaces;
using Notifications.Application.Options;
using Notifications.Domain;
using Notifications.Infrastructure.Data;

namespace Notifications.Infrastructure.Services;

/// <summary>
/// LS-NOTIF-SMS-015: Calculates provider quality snapshots from local operational telemetry.
///
/// Security: Reads only NotificationAttempt aggregate data. No phone numbers, credentials,
/// CredentialsJson, SettingsJson, auth tokens, webhook URLs, or raw provider payloads
/// are accessed, stored, or returned.
///
/// All calculations use local DB data only. No external provider calls.
/// </summary>
public class SmsProviderQualityService : ISmsProviderQualityService
{
    private readonly NotificationsDbContext        _db;
    private readonly ISmsProviderQualityRepository _repo;
    private readonly IProviderHealthRepository     _healthRepo;
    private readonly SmsProviderQualityOptions     _opts;
    private readonly ILogger<SmsProviderQualityService> _logger;

    // Statuses considered "delivered/successful"
    private static readonly HashSet<string> DeliveredStatuses = new(StringComparer.OrdinalIgnoreCase)
        { "sent", "delivered", "completed" };

    // Statuses considered "failed"
    private static readonly HashSet<string> FailedStatuses = new(StringComparer.OrdinalIgnoreCase)
        { "failed", "dead_letter", "permanently_failed" };

    public SmsProviderQualityService(
        NotificationsDbContext db,
        ISmsProviderQualityRepository repo,
        IProviderHealthRepository healthRepo,
        IOptions<SmsProviderQualityOptions> opts,
        ILogger<SmsProviderQualityService> logger)
    {
        _db         = db;
        _repo       = repo;
        _healthRepo = healthRepo;
        _opts       = opts.Value;
        _logger     = logger;
    }

    public async Task CalculateSnapshotsAsync(
        DateTime windowStart,
        DateTime windowEnd,
        CancellationToken ct)
    {
        _logger.LogDebug(
            "SmsProviderQualityService: calculating snapshots [{Start} → {End}]",
            windowStart, windowEnd);

        // Fetch SMS attempts in window (bounded)
        var attempts = await _db.NotificationAttempts
            .Where(a => a.Channel == "sms"
                     && a.CreatedAt >= windowStart
                     && a.CreatedAt <= windowEnd)
            .Select(a => new AttemptProjection
            {
                Provider             = a.Provider,
                ProviderConfigId     = a.ProviderConfigId,
                ProviderOwnershipMode = a.ProviderOwnershipMode,
                TenantId             = a.TenantId,
                Status               = a.Status,
                AttemptNumber        = a.AttemptNumber,
                IsFailover           = a.IsFailover,
                CreatedAt            = a.CreatedAt,
                CompletedAt          = a.CompletedAt,
                EstimatedCostAmount  = a.EstimatedCostAmount,
                ActualCostAmount     = a.ActualCostAmount,
                LastReconciliationOutcome = a.LastReconciliationOutcome,
                ReconciliationAttemptCount = a.ReconciliationAttemptCount,
            })
            .Take(50_000) // safety cap
            .ToListAsync(ct);

        if (attempts.Count == 0)
        {
            _logger.LogDebug("SmsProviderQualityService: no SMS attempts in window — skipping snapshot save");
            return;
        }

        // Aggregate platform-level (TenantId = null) per provider
        var platformGroups = attempts
            .GroupBy(a => (a.Provider, a.ProviderConfigId, a.ProviderOwnershipMode))
            .ToList();

        var snapshots = new List<SmsProviderQualitySnapshot>();

        foreach (var g in platformGroups)
        {
            var snap = await BuildSnapshotAsync(
                providerType:          g.Key.Provider ?? string.Empty,
                providerConfigId:      g.Key.ProviderConfigId,
                providerOwnershipMode: g.Key.ProviderOwnershipMode,
                tenantId:              null,
                countryCode:           null,
                region:                null,
                windowStart:           windowStart,
                windowEnd:             windowEnd,
                attempts:              g.ToList(),
                ct:                    ct);
            snapshots.Add(snap);
        }

        await _repo.SaveSnapshotsAsync(snapshots, ct);

        _logger.LogInformation(
            "SmsProviderQualityService: saved {Count} quality snapshots for window [{Start} → {End}]",
            snapshots.Count, windowStart, windowEnd);
    }

    public async Task<ProviderQualityScore> GetLatestScoreAsync(
        string  providerType,
        Guid?   tenantId,
        Guid?   providerConfigId,
        string? countryCode,
        CancellationToken ct)
    {
        var snap = await _repo.GetLatestAsync(providerType, tenantId, providerConfigId, countryCode, ct);
        return MapToScore(snap, providerType, providerConfigId, countryCode);
    }

    public async Task<IReadOnlyList<ProviderQualityScore>> GetLatestScoresAsync(
        Guid?   tenantId,
        string? countryCode,
        CancellationToken ct)
    {
        var snaps = await _repo.GetLatestPerProviderAsync(tenantId, countryCode, ct);
        return snaps.Select(s => MapToScore(s, s.ProviderType, s.ProviderConfigId, s.CountryCode)).ToList();
    }

    public async Task<IReadOnlyList<SmsProviderQualitySnapshot>> QuerySnapshotsAsync(
        SmsQualitySnapshotQuery query,
        CancellationToken ct)
        => await _repo.QueryAsync(query, ct);

    // ── Internal helpers ──────────────────────────────────────────────────────

    private async Task<SmsProviderQualitySnapshot> BuildSnapshotAsync(
        string providerType,
        Guid?  providerConfigId,
        string? providerOwnershipMode,
        Guid?  tenantId,
        string? countryCode,
        string? region,
        DateTime windowStart,
        DateTime windowEnd,
        List<AttemptProjection> attempts,
        CancellationToken ct)
    {
        var total        = attempts.Count;
        var delivered    = attempts.Count(a => DeliveredStatuses.Contains(a.Status ?? ""));
        var failed       = attempts.Count(a => FailedStatuses.Contains(a.Status ?? ""));
        var deadLetter   = attempts.Count(a =>
            string.Equals(a.Status, "dead_letter", StringComparison.OrdinalIgnoreCase));
        var retries      = attempts.Count(a => a.AttemptNumber > 1 || a.IsFailover);
        var reconciled   = attempts.Count(a => a.ReconciliationAttemptCount > 0);
        var reconFailed  = attempts.Count(a =>
            string.Equals(a.LastReconciliationOutcome, "failed", StringComparison.OrdinalIgnoreCase)
         || string.Equals(a.LastReconciliationOutcome, "error", StringComparison.OrdinalIgnoreCase));

        decimal deliveryRate  = total > 0 ? (decimal)delivered / total : 0m;
        decimal failureRate   = total > 0 ? (decimal)failed / total    : 0m;
        decimal retryRate     = total > 0 ? (decimal)retries / total   : 0m;
        decimal dlRate        = total > 0 ? (decimal)deadLetter / total : 0m;
        decimal reconFailRate = reconciled > 0 ? (decimal)reconFailed / reconciled : 0m;

        // Latency
        decimal? avgLatencyMs = null;
        var withLatency = attempts
            .Where(a => a.CompletedAt.HasValue)
            .Select(a => (a.CompletedAt!.Value - a.CreatedAt).TotalMilliseconds)
            .Where(ms => ms >= 0 && ms < 300_000) // sanity cap 5 min
            .ToList();
        if (withLatency.Count > 0)
            avgLatencyMs = (decimal)withLatency.Average();

        // Cost
        decimal? avgEffectiveCost = null;
        decimal? costPerDelivered = null;
        var withCost = attempts
            .Select(a => a.ActualCostAmount ?? a.EstimatedCostAmount)
            .Where(c => c.HasValue)
            .Select(c => c!.Value)
            .ToList();
        if (withCost.Count > 0)
        {
            avgEffectiveCost = withCost.Average();
            if (delivered > 0)
                costPerDelivered = withCost.Sum() / delivered;
        }

        // Health penalty (0 = healthy, 0.5 = degraded, 1.0 = down)
        decimal healthPenalty = 0m;
        try
        {
            var health = await _healthRepo.FindByProviderAsync(
                providerType, "sms", providerOwnershipMode ?? "platform", providerConfigId);
            healthPenalty = health?.HealthStatus switch
            {
                "down"     => 1.0m,
                "degraded" => 0.5m,
                _          => 0m,
            };
        }
        catch { /* non-fatal — use zero penalty */ }

        // Cost efficiency score (0-100, higher = more efficient)
        decimal? costEfficiencyScore = null;
        if (costPerDelivered.HasValue && costPerDelivered.Value > 0)
        {
            // Normalize: assume $0.01/msg = score 100, $0.10/msg = score 0
            const decimal cheap = 0.01m;
            const decimal expensive = 0.10m;
            var ratio = (expensive - Math.Min(costPerDelivered.Value, expensive)) / (expensive - cheap);
            costEfficiencyScore = Math.Round(Math.Max(0m, Math.Min(100m, ratio * 100m)), 2);
        }

        // Quality score
        decimal quality;
        if (total < _opts.MinimumAttemptCount)
        {
            quality = _opts.InsufficientDataScore;
        }
        else
        {
            quality =
                (deliveryRate * _opts.DeliverySuccessWeight * 100m)
                - (failureRate   * _opts.FailurePenaltyWeight       * 100m)
                - (retryRate     * _opts.RetryPenaltyWeight         * 100m)
                - (reconFailRate * _opts.ReconciliationPenaltyWeight * 100m)
                - (healthPenalty * _opts.HealthPenaltyWeight         * 100m);
            quality = Math.Max(0m, Math.Min(100m, Math.Round(quality, 2)));
        }

        return new SmsProviderQualitySnapshot
        {
            ProviderType             = providerType,
            ProviderConfigId         = providerConfigId,
            ProviderOwnershipMode    = providerOwnershipMode,
            TenantId                 = tenantId,
            CountryCode              = countryCode,
            Region                   = region,
            WindowStart              = windowStart,
            WindowEnd                = windowEnd,
            TotalAttempts            = total,
            DeliveredAttempts        = delivered,
            FailedAttempts           = failed,
            RetryAttempts            = retries,
            DeadLetterAttempts       = deadLetter,
            ReconciledAttempts       = reconciled,
            ReconciliationFailures   = reconFailed,
            AverageLatencyMs         = avgLatencyMs.HasValue ? Math.Round(avgLatencyMs.Value, 4) : null,
            DeliverySuccessRate      = Math.Round(deliveryRate,  4),
            FailureRate              = Math.Round(failureRate,   4),
            RetryRate                = Math.Round(retryRate,     4),
            DeadLetterRate           = Math.Round(dlRate,        4),
            ReconciliationFailureRate = Math.Round(reconFailRate, 4),
            AverageEffectiveCost     = avgEffectiveCost.HasValue ? Math.Round(avgEffectiveCost.Value, 8) : null,
            CostPerDeliveredMessage  = costPerDelivered.HasValue  ? Math.Round(costPerDelivered.Value, 8)  : null,
            QualityScore             = quality,
            CostEfficiencyScore      = costEfficiencyScore,
            HealthPenalty            = healthPenalty,
            CalculatedAt             = DateTime.UtcNow,
        };
    }

    private ProviderQualityScore MapToScore(
        SmsProviderQualitySnapshot? snap,
        string  providerType,
        Guid?   providerConfigId,
        string? countryCode)
    {
        if (snap == null)
        {
            return new ProviderQualityScore
            {
                ProviderType      = providerType,
                ProviderConfigId  = providerConfigId,
                CountryCode       = countryCode,
                QualityScore      = _opts.DefaultQualityScore,
                HasSufficientData = false,
            };
        }

        return new ProviderQualityScore
        {
            ProviderType         = snap.ProviderType,
            ProviderConfigId     = snap.ProviderConfigId,
            CountryCode          = snap.CountryCode,
            QualityScore         = snap.QualityScore,
            CostEfficiencyScore  = snap.CostEfficiencyScore,
            AverageLatencyMs     = snap.AverageLatencyMs,
            DeliverySuccessRate  = snap.DeliverySuccessRate,
            TotalAttempts        = snap.TotalAttempts,
            HasSufficientData    = snap.TotalAttempts >= _opts.MinimumAttemptCount,
            CalculatedAt         = snap.CalculatedAt,
        };
    }

    // ── Projection ────────────────────────────────────────────────────────────

    private sealed class AttemptProjection
    {
        public string?   Provider              { get; set; }
        public Guid?     ProviderConfigId      { get; set; }
        public string?   ProviderOwnershipMode { get; set; }
        public Guid?     TenantId              { get; set; }
        public string?   Status                { get; set; }
        public int       AttemptNumber         { get; set; }
        public bool      IsFailover            { get; set; }
        public DateTime  CreatedAt             { get; set; }
        public DateTime? CompletedAt           { get; set; }
        public decimal?  EstimatedCostAmount   { get; set; }
        public decimal?  ActualCostAmount      { get; set; }
        public string?   LastReconciliationOutcome    { get; set; }
        public int       ReconciliationAttemptCount   { get; set; }
    }
}
