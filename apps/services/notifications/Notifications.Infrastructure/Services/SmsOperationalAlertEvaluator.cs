using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Notifications.Application.DTOs;
using Notifications.Application.Interfaces;
using Notifications.Domain;
using Notifications.Infrastructure.Data;

namespace Notifications.Infrastructure.Services;

/// <summary>
/// LS-NOTIF-SMS-010: Evaluates all SMS operational alert threshold rules
/// against a minimal projection of ntf_NotificationAttempts.
///
/// Rules evaluated per cycle:
///   R1  sms.failure_rate_high              — platform-wide failure rate
///   R2  sms.dead_letter_spike              — platform-wide dead_letter count
///   R3  sms.retry_spike                    — platform-wide retrying count
///   R4  sms.reconciliation_failure_rate_high — reconciliation vendor_lookup_failed rate
///   R5  sms.provider_degraded              — per-provider failure rate (top providers)
///   R6  sms.provider_config_failure_spike  — provider config failure count
///   R7  sms.tenant_anomaly                 — per-tenant failure rate (active tenants)
///   R8  sms.reconciliation_stale           — attempts never reconciled past stale cutoff
///
/// All evaluation is done against an in-memory projection; no credentials,
/// phone numbers, RecipientJson, CredentialsJson, or SettingsJson are read.
/// </summary>
public sealed class SmsOperationalAlertEvaluator : ISmsOperationalAlertEvaluator
{
    private readonly NotificationsDbContext _db;
    private readonly ISmsOperationalAlertRepository _repo;
    private readonly ISmsAlertEscalationService? _escalationService;
    private readonly ILogger<SmsOperationalAlertEvaluator> _logger;

    // ── Threshold configuration (read from IConfiguration at construction) ───

    // R1: platform-wide failure rate
    private readonly decimal _failureRateWarning;
    private readonly decimal _failureRateCritical;
    private readonly int     _failureRateMinAttempts;

    // R2: dead-letter spike
    private readonly int _deadLetterWarning;
    private readonly int _deadLetterCritical;

    // R3: retry spike
    private readonly int _retryWarning;
    private readonly int _retryCritical;

    // R4: reconciliation failure rate
    private readonly decimal _reconFailureRateWarning;
    private readonly decimal _reconFailureRateCritical;
    private readonly int     _reconFailureMinReconciled;

    // R5: per-provider failure rate
    private readonly decimal _providerFailureRateWarning;
    private readonly decimal _providerFailureRateCritical;
    private readonly int     _providerFailureMinAttempts;

    // R6: provider config failure spike
    private readonly int _providerConfigFailureWarning;
    private readonly int _providerConfigFailureCritical;

    // R7: per-tenant anomaly
    private readonly decimal _tenantAnomalyRateWarning;
    private readonly decimal _tenantAnomalyRateCritical;
    private readonly int     _tenantAnomalyMinAttempts;

    // R8: reconciliation stale
    private readonly int _reconStaleWarning;
    private readonly int _reconStaleCritical;
    private readonly int _reconStaleAgeMinutes;

    // ── Cooldown ──────────────────────────────────────────────────────────────
    private readonly int _cooldownMinutes;

    // ── Outcome sets (mirror SmsDashboardRepository) ──────────────────────────

    private static readonly HashSet<string> ProviderConfigFailedOutcomes = new(StringComparer.OrdinalIgnoreCase)
    {
        "missing_provider_config_context",
        "provider_config_not_found",
        "provider_config_inactive",
        "provider_config_invalid",
        "provider_runtime_resolution_failed",
    };

    public SmsOperationalAlertEvaluator(
        NotificationsDbContext db,
        ISmsOperationalAlertRepository repo,
        ISmsAlertEscalationService escalationService,
        IConfiguration configuration,
        ILogger<SmsOperationalAlertEvaluator> logger)
    {
        _db                 = db;
        _repo               = repo;
        _escalationService  = escalationService;
        _logger             = logger;

        // R1
        _failureRateWarning    = configuration.GetValue("SMS_ALERT_FAILURE_RATE_WARNING",  0.10m);
        _failureRateCritical   = configuration.GetValue("SMS_ALERT_FAILURE_RATE_CRITICAL", 0.25m);
        _failureRateMinAttempts= configuration.GetValue("SMS_ALERT_FAILURE_RATE_MIN_ATTEMPTS", 10);

        // R2
        _deadLetterWarning  = configuration.GetValue("SMS_ALERT_DEAD_LETTER_WARNING",  5);
        _deadLetterCritical = configuration.GetValue("SMS_ALERT_DEAD_LETTER_CRITICAL", 20);

        // R3
        _retryWarning  = configuration.GetValue("SMS_ALERT_RETRY_SPIKE_WARNING",  10);
        _retryCritical = configuration.GetValue("SMS_ALERT_RETRY_SPIKE_CRITICAL", 30);

        // R4
        _reconFailureRateWarning    = configuration.GetValue("SMS_ALERT_RECON_FAILURE_RATE_WARNING",  0.20m);
        _reconFailureRateCritical   = configuration.GetValue("SMS_ALERT_RECON_FAILURE_RATE_CRITICAL", 0.40m);
        _reconFailureMinReconciled  = configuration.GetValue("SMS_ALERT_RECON_FAILURE_MIN_RECONCILED", 5);

        // R5
        _providerFailureRateWarning  = configuration.GetValue("SMS_ALERT_PROVIDER_FAILURE_RATE_WARNING",  0.15m);
        _providerFailureRateCritical = configuration.GetValue("SMS_ALERT_PROVIDER_FAILURE_RATE_CRITICAL", 0.30m);
        _providerFailureMinAttempts  = configuration.GetValue("SMS_ALERT_PROVIDER_FAILURE_MIN_ATTEMPTS",  10);

        // R6
        _providerConfigFailureWarning  = configuration.GetValue("SMS_ALERT_PROVIDER_CONFIG_FAILURE_WARNING",  3);
        _providerConfigFailureCritical = configuration.GetValue("SMS_ALERT_PROVIDER_CONFIG_FAILURE_CRITICAL", 10);

        // R7
        _tenantAnomalyRateWarning  = configuration.GetValue("SMS_ALERT_TENANT_ANOMALY_RATE_WARNING",  0.20m);
        _tenantAnomalyRateCritical = configuration.GetValue("SMS_ALERT_TENANT_ANOMALY_RATE_CRITICAL", 0.40m);
        _tenantAnomalyMinAttempts  = configuration.GetValue("SMS_ALERT_TENANT_ANOMALY_MIN_ATTEMPTS",  10);

        // R8
        _reconStaleWarning    = configuration.GetValue("SMS_ALERT_RECON_STALE_WARNING",    10);
        _reconStaleCritical   = configuration.GetValue("SMS_ALERT_RECON_STALE_CRITICAL",   50);
        _reconStaleAgeMinutes = configuration.GetValue("SMS_ALERT_RECON_STALE_AGE_MINUTES", 120);

        // Cooldown
        _cooldownMinutes = configuration.GetValue("SMS_ALERT_COOLDOWN_MINUTES", 60);
    }

    // ── Mutable counters (avoids ref-in-async restriction) ───────────────────

    private sealed class Counters
    {
        public int Created;
        public int Updated;
        public int Suppressed;

        /// <summary>
        /// Alerts created or moved to active in this cycle — used to trigger
        /// best-effort escalation after all rules have been evaluated.
        /// </summary>
        public List<SmsOperationalAlert> CreatedOrUpdatedAlerts = new();
    }

    // ── Main evaluation entry point ───────────────────────────────────────────

    public async Task<SmsAlertEvaluationResult> EvaluateAsync(
        DateTime windowStart,
        DateTime windowEnd,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        _logger.LogInformation(
            "SmsOperationalAlertEvaluator: starting evaluation window={Start:u}..{End:u}",
            windowStart, windowEnd);

        // ── Fetch minimal safe projection ─────────────────────────────────────

        var rows = await _db.NotificationAttempts
            .AsNoTracking()
            .Where(a =>
                a.Channel   == "sms" &&
                a.CreatedAt >= windowStart &&
                a.CreatedAt <= windowEnd)
            .Select(a => new AttemptRow
            {
                Status                     = a.Status,
                ReconciliationAttemptCount = a.ReconciliationAttemptCount,
                LastReconciliationOutcome  = a.LastReconciliationOutcome,
                Provider                   = a.Provider,
                ProviderConfigId           = a.ProviderConfigId,
                TenantId                   = a.TenantId,
                CreatedAt                  = a.CreatedAt,
            })
            .ToListAsync(ct);

        _logger.LogDebug(
            "SmsOperationalAlertEvaluator: fetched {Count} attempts for evaluation", rows.Count);

        var counters = new Counters();

        if (rows.Count > 0)
        {
            await EvaluateFailureRateAsync(rows, windowStart, windowEnd, counters, ct);
            await EvaluateDeadLetterSpikeAsync(rows, windowStart, windowEnd, counters, ct);
            await EvaluateRetrySpikeAsync(rows, windowStart, windowEnd, counters, ct);
            await EvaluateReconciliationFailureRateAsync(rows, windowStart, windowEnd, counters, ct);
            await EvaluateProviderDegradedAsync(rows, windowStart, windowEnd, counters, ct);
            await EvaluateProviderConfigFailureSpikeAsync(rows, windowStart, windowEnd, counters, ct);
            await EvaluateTenantAnomalyAsync(rows, windowStart, windowEnd, counters, ct);
            await EvaluateReconciliationStaleAsync(rows, windowStart, windowEnd, counters, ct);
        }

        // ── LS-NOTIF-SMS-011: best-effort escalation ──────────────────────────
        // After all rules are evaluated, trigger escalation for each alert that was
        // created or updated this cycle. Failures here must not affect evaluation result.
        if (_escalationService is not null && counters.CreatedOrUpdatedAlerts.Count > 0)
        {
            _logger.LogDebug(
                "SmsOperationalAlertEvaluator: triggering escalation for {Count} alert(s)",
                counters.CreatedOrUpdatedAlerts.Count);

            foreach (var alert in counters.CreatedOrUpdatedAlerts)
            {
                try
                {
                    await _escalationService.EscalateAlertAsync(alert, ct);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "SmsOperationalAlertEvaluator: escalation failed for alert {AlertId} — " +
                        "evaluation result is unaffected", alert.Id);
                }
            }
        }

        sw.Stop();

        _logger.LogInformation(
            "SmsOperationalAlertEvaluator: cycle complete — attempts={Attempts}, created={Created}, updated={Updated}, suppressed={Suppressed}, duration={Ms}ms",
            rows.Count, counters.Created, counters.Updated, counters.Suppressed, sw.ElapsedMilliseconds);

        return new SmsAlertEvaluationResult
        {
            WindowStart      = windowStart,
            WindowEnd        = windowEnd,
            DurationMs       = (int)sw.ElapsedMilliseconds,
            EvaluatedRules   =
            [
                "sms.failure_rate_high",
                "sms.dead_letter_spike",
                "sms.retry_spike",
                "sms.reconciliation_failure_rate_high",
                "sms.provider_degraded",
                "sms.provider_config_failure_spike",
                "sms.tenant_anomaly",
                "sms.reconciliation_stale",
            ],
            AlertsCreated    = counters.Created,
            AlertsUpdated    = counters.Updated,
            AlertsSuppressed = counters.Suppressed,
            AttemptsSampled  = rows.Count,
        };
    }

    // ── Rule R1: platform-wide failure rate ───────────────────────────────────

    private async Task EvaluateFailureRateAsync(
        List<AttemptRow> rows,
        DateTime windowStart, DateTime windowEnd,
        Counters counters, CancellationToken ct)
    {
        var total  = rows.Count;
        var failed = rows.Count(r => r.Status is "failed" or "dead_letter");

        if (total < _failureRateMinAttempts) return;

        var rate     = (decimal)failed / total;
        var severity = rate >= _failureRateCritical ? "critical"
                     : rate >= _failureRateWarning  ? "warning"
                     : null;
        if (severity is null) return;

        var threshold = rate >= _failureRateCritical ? _failureRateCritical : _failureRateWarning;
        var msg = $"Platform-wide SMS failure rate is {rate:P1} ({failed}/{total} attempts). " +
                  $"Threshold: warning={_failureRateWarning:P0}, critical={_failureRateCritical:P0}.";

        await UpsertAlertAsync(
            "sms.failure_rate_high", severity, null, null, null,
            rate, threshold, msg, windowStart, windowEnd, counters, ct);
    }

    // ── Rule R2: dead-letter spike ────────────────────────────────────────────

    private async Task EvaluateDeadLetterSpikeAsync(
        List<AttemptRow> rows,
        DateTime windowStart, DateTime windowEnd,
        Counters counters, CancellationToken ct)
    {
        var count    = rows.Count(r => r.Status == "dead_letter");
        var severity = count >= _deadLetterCritical ? "critical"
                     : count >= _deadLetterWarning  ? "warning"
                     : null;
        if (severity is null) return;

        var threshold = (decimal)(count >= _deadLetterCritical ? _deadLetterCritical : _deadLetterWarning);
        var msg = $"Platform-wide SMS dead-letter count is {count}. " +
                  $"Threshold: warning={_deadLetterWarning}, critical={_deadLetterCritical}.";

        await UpsertAlertAsync(
            "sms.dead_letter_spike", severity, null, null, null,
            count, threshold, msg, windowStart, windowEnd, counters, ct);
    }

    // ── Rule R3: retry spike ──────────────────────────────────────────────────

    private async Task EvaluateRetrySpikeAsync(
        List<AttemptRow> rows,
        DateTime windowStart, DateTime windowEnd,
        Counters counters, CancellationToken ct)
    {
        var count    = rows.Count(r => r.Status == "retrying");
        var severity = count >= _retryCritical ? "critical"
                     : count >= _retryWarning  ? "warning"
                     : null;
        if (severity is null) return;

        var threshold = (decimal)(count >= _retryCritical ? _retryCritical : _retryWarning);
        var msg = $"Platform-wide SMS retrying count is {count}. " +
                  $"Threshold: warning={_retryWarning}, critical={_retryCritical}.";

        await UpsertAlertAsync(
            "sms.retry_spike", severity, null, null, null,
            count, threshold, msg, windowStart, windowEnd, counters, ct);
    }

    // ── Rule R4: reconciliation failure rate ──────────────────────────────────

    private async Task EvaluateReconciliationFailureRateAsync(
        List<AttemptRow> rows,
        DateTime windowStart, DateTime windowEnd,
        Counters counters, CancellationToken ct)
    {
        var reconciled = rows.Where(r => r.ReconciliationAttemptCount > 0).ToList();
        if (reconciled.Count < _reconFailureMinReconciled) return;

        var lookupFailed = reconciled.Count(r =>
            string.Equals(r.LastReconciliationOutcome, "vendor_lookup_failed",
                          StringComparison.OrdinalIgnoreCase));

        var rate     = (decimal)lookupFailed / reconciled.Count;
        var severity = rate >= _reconFailureRateCritical ? "critical"
                     : rate >= _reconFailureRateWarning  ? "warning"
                     : null;
        if (severity is null) return;

        var threshold = rate >= _reconFailureRateCritical
            ? _reconFailureRateCritical : _reconFailureRateWarning;
        var msg = $"SMS reconciliation vendor_lookup_failed rate is {rate:P1} ({lookupFailed}/{reconciled.Count} reconciled). " +
                  $"Threshold: warning={_reconFailureRateWarning:P0}, critical={_reconFailureRateCritical:P0}.";

        await UpsertAlertAsync(
            "sms.reconciliation_failure_rate_high", severity, null, null, null,
            rate, threshold, msg, windowStart, windowEnd, counters, ct);
    }

    // ── Rule R5: per-provider failure rate ────────────────────────────────────

    private async Task EvaluateProviderDegradedAsync(
        List<AttemptRow> rows,
        DateTime windowStart, DateTime windowEnd,
        Counters counters, CancellationToken ct)
    {
        var grouped = rows
            .Where(r => !string.IsNullOrEmpty(r.Provider))
            .GroupBy(r => (r.Provider, r.ProviderConfigId));

        foreach (var g in grouped)
        {
            var total = g.Count();
            if (total < _providerFailureMinAttempts) continue;

            var failed   = g.Count(r => r.Status is "failed" or "dead_letter");
            var rate     = (decimal)failed / total;
            var severity = rate >= _providerFailureRateCritical ? "critical"
                         : rate >= _providerFailureRateWarning  ? "warning"
                         : null;
            if (severity is null) continue;

            var threshold = rate >= _providerFailureRateCritical
                ? _providerFailureRateCritical : _providerFailureRateWarning;
            var msg = $"Provider '{g.Key.Provider}' (configId={g.Key.ProviderConfigId}) SMS failure rate is {rate:P1} ({failed}/{total} attempts). " +
                      $"Threshold: warning={_providerFailureRateWarning:P0}, critical={_providerFailureRateCritical:P0}.";

            await UpsertAlertAsync(
                "sms.provider_degraded", severity, null, g.Key.Provider, g.Key.ProviderConfigId,
                rate, threshold, msg, windowStart, windowEnd, counters, ct);
        }
    }

    // ── Rule R6: provider config failure spike ────────────────────────────────

    private async Task EvaluateProviderConfigFailureSpikeAsync(
        List<AttemptRow> rows,
        DateTime windowStart, DateTime windowEnd,
        Counters counters, CancellationToken ct)
    {
        var count = rows.Count(r =>
            r.ReconciliationAttemptCount > 0 &&
            !string.IsNullOrEmpty(r.LastReconciliationOutcome) &&
            ProviderConfigFailedOutcomes.Contains(r.LastReconciliationOutcome));

        var severity = count >= _providerConfigFailureCritical ? "critical"
                     : count >= _providerConfigFailureWarning  ? "warning"
                     : null;
        if (severity is null) return;

        var threshold = (decimal)(count >= _providerConfigFailureCritical
            ? _providerConfigFailureCritical : _providerConfigFailureWarning);
        var msg = $"Platform-wide provider config failure count is {count}. " +
                  $"Threshold: warning={_providerConfigFailureWarning}, critical={_providerConfigFailureCritical}.";

        await UpsertAlertAsync(
            "sms.provider_config_failure_spike", severity, null, null, null,
            count, threshold, msg, windowStart, windowEnd, counters, ct);
    }

    // ── Rule R7: per-tenant anomaly ───────────────────────────────────────────

    private async Task EvaluateTenantAnomalyAsync(
        List<AttemptRow> rows,
        DateTime windowStart, DateTime windowEnd,
        Counters counters, CancellationToken ct)
    {
        var grouped = rows
            .Where(r => r.TenantId.HasValue)
            .GroupBy(r => r.TenantId!.Value);

        foreach (var g in grouped)
        {
            var total = g.Count();
            if (total < _tenantAnomalyMinAttempts) continue;

            var failed   = g.Count(r => r.Status is "failed" or "dead_letter");
            var rate     = (decimal)failed / total;
            var severity = rate >= _tenantAnomalyRateCritical ? "critical"
                         : rate >= _tenantAnomalyRateWarning  ? "warning"
                         : null;
            if (severity is null) continue;

            var threshold = rate >= _tenantAnomalyRateCritical
                ? _tenantAnomalyRateCritical : _tenantAnomalyRateWarning;
            var msg = $"Tenant {g.Key} SMS failure rate is {rate:P1} ({failed}/{total} attempts). " +
                      $"Threshold: warning={_tenantAnomalyRateWarning:P0}, critical={_tenantAnomalyRateCritical:P0}.";

            await UpsertAlertAsync(
                "sms.tenant_anomaly", severity, g.Key, null, null,
                rate, threshold, msg, windowStart, windowEnd, counters, ct);
        }
    }

    // ── Rule R8: reconciliation stale ─────────────────────────────────────────

    private async Task EvaluateReconciliationStaleAsync(
        List<AttemptRow> rows,
        DateTime windowStart, DateTime windowEnd,
        Counters counters, CancellationToken ct)
    {
        var staleCutoff = DateTime.UtcNow.AddMinutes(-_reconStaleAgeMinutes);

        var stale = rows.Count(r =>
            r.ReconciliationAttemptCount == 0 &&
            r.CreatedAt < staleCutoff &&
            r.Status is "sent" or "pending" or "sending" or "processing" or "retrying");

        var severity = stale >= _reconStaleCritical ? "critical"
                     : stale >= _reconStaleWarning  ? "warning"
                     : null;
        if (severity is null) return;

        var threshold = (decimal)(stale >= _reconStaleCritical ? _reconStaleCritical : _reconStaleWarning);
        var msg = $"{stale} SMS attempts have never been reconciled and are older than {_reconStaleAgeMinutes} minutes. " +
                  $"Threshold: warning={_reconStaleWarning}, critical={_reconStaleCritical}.";

        await UpsertAlertAsync(
            "sms.reconciliation_stale", severity, null, null, null,
            stale, threshold, msg, windowStart, windowEnd, counters, ct);
    }

    // ── Dedup + upsert helper ─────────────────────────────────────────────────

    private async Task UpsertAlertAsync(
        string alertType,
        string severity,
        Guid? tenantId,
        string? provider,
        Guid? providerConfigId,
        decimal metricValue,
        decimal thresholdValue,
        string message,
        DateTime windowStart,
        DateTime windowEnd,
        Counters counters,
        CancellationToken ct)
    {
        // 1) Check for existing ACTIVE (or suppressed) alert — update if found.
        var active = await _repo.FindActiveAlertAsync(alertType, tenantId, provider, providerConfigId, ct);
        if (active is not null)
        {
            // If suppressed and suppression has not yet expired, skip.
            if (active.Status == "suppressed" &&
                active.SuppressedUntil.HasValue &&
                active.SuppressedUntil.Value > DateTime.UtcNow)
            {
                counters.Suppressed++;
                return;
            }

            active.OccurrenceCount++;
            active.LastObservedAt        = DateTime.UtcNow;
            active.MetricValue           = metricValue;
            active.ThresholdValue        = thresholdValue;
            active.Message               = message;
            active.Severity              = severity;
            active.EvaluationWindowStart = windowStart;
            active.EvaluationWindowEnd   = windowEnd;

            // Expired suppression → restore to active.
            if (active.Status == "suppressed")
                active.Status = "active";

            await _repo.UpdateAsync(active, ct);
            counters.Updated++;
            counters.CreatedOrUpdatedAlerts.Add(active);
            return;
        }

        // 2) No active alert — check cooldown (recently resolved).
        var recentlyResolved = await _repo.FindRecentlyResolvedAlertAsync(
            alertType, tenantId, provider, providerConfigId, _cooldownMinutes, ct);

        if (recentlyResolved is not null)
        {
            counters.Suppressed++;
            return;
        }

        // 3) Create a new alert.
        var now = DateTime.UtcNow;
        var alert = new SmsOperationalAlert
        {
            Id                    = Guid.NewGuid(),
            AlertType             = alertType,
            Severity              = severity,
            TenantId              = tenantId,
            Provider              = provider,
            ProviderConfigId      = providerConfigId,
            MetricValue           = metricValue,
            ThresholdValue        = thresholdValue,
            Message               = message,
            EvaluationWindowStart = windowStart,
            EvaluationWindowEnd   = windowEnd,
            Status                = "active",
            OccurrenceCount       = 1,
            FirstObservedAt       = now,
            LastObservedAt        = now,
            CreatedAt             = now,
            UpdatedAt             = now,
        };

        await _repo.CreateAsync(alert, ct);
        counters.Created++;
        counters.CreatedOrUpdatedAlerts.Add(alert);

        _logger.LogInformation(
            "SmsOperationalAlertEvaluator: new alert {AlertType} {Severity} — {Message}",
            alertType, severity, message);
    }

    // ── Minimal projection record ─────────────────────────────────────────────

    private sealed class AttemptRow
    {
        public string? Status                     { get; init; }
        public int     ReconciliationAttemptCount { get; init; }
        public string? LastReconciliationOutcome  { get; init; }
        public string? Provider                   { get; init; }
        public Guid?   ProviderConfigId           { get; init; }
        public Guid?   TenantId                   { get; init; }
        public DateTime CreatedAt                 { get; init; }
    }
}
