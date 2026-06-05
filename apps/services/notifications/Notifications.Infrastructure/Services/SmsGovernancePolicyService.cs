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
/// LS-NOTIF-SMS-017: Central SMS governance policy evaluation service.
///
/// Evaluates active governance policies by priority (ascending).
/// Returns the first non-allow decision, or allow/no_applicable_policy when all pass.
///
/// Safe degradation:
/// - If FailOpenOnEvaluationError = true (default), all exceptions return allow.
/// - If FailOpenOnEvaluationError = false, exceptions return block/policy_evaluation_error.
/// - Disabled master switch returns allow immediately.
/// - Missing or corrupt PolicyJson returns allow with a warning log.
/// - Phone numbers used only transiently for country inference — never persisted.
/// </summary>
public sealed class SmsGovernancePolicyService : ISmsGovernancePolicyService
{
    private readonly NotificationsDbContext        _db;
    private readonly ISmsRegionalInferenceService  _inference;
    private readonly SmsGovernanceOptions          _options;
    private readonly ILogger<SmsGovernancePolicyService> _logger;

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public SmsGovernancePolicyService(
        NotificationsDbContext        db,
        ISmsRegionalInferenceService  inference,
        IOptions<SmsGovernanceOptions> options,
        ILogger<SmsGovernancePolicyService> logger)
    {
        _db        = db;
        _inference = inference;
        _options   = options.Value;
        _logger    = logger;
    }

    // ─── Public evaluation entry points ──────────────────────────────────────

    public Task<SmsGovernanceEvaluationResult> EvaluatePreSendAsync(
        SmsGovernanceEvaluationRequest request,
        CancellationToken ct = default)
        => EvaluateAsync(request, new[] { "quiet_hours", "geographic_restriction", "rate_limit", "provider_governance" }, ct);

    public Task<SmsGovernanceEvaluationResult> EvaluateRetryAsync(
        SmsGovernanceEvaluationRequest request,
        CancellationToken ct = default)
        => EvaluateAsync(request, new[] { "quiet_hours", "rate_limit", "provider_governance", "retry_governance" }, ct);

    public Task<SmsGovernanceEvaluationResult> EvaluateEscalationAsync(
        SmsGovernanceEvaluationRequest request,
        CancellationToken ct = default)
        => EvaluateAsync(request, new[] { "escalation_guardrail" }, ct);

    // ─── Core evaluation engine ───────────────────────────────────────────────

    private async Task<SmsGovernanceEvaluationResult> EvaluateAsync(
        SmsGovernanceEvaluationRequest request,
        string[] policyTypes,
        CancellationToken ct)
    {
        if (!_options.Enabled)
            return Allow("no_applicable_policy");

        // Emergency override short-circuit
        if (request.IsEmergencyOverride && _options.EmergencyOverrideEnabled)
            return new SmsGovernanceEvaluationResult { DecisionType = "override_allowed", ReasonCode = "emergency_override" };

        try
        {
            // Infer country once (transient — never stored as phone)
            var countryCode = _inference.InferCountryCode(request.RecipientPhoneTransient);
            var region      = countryCode != null ? _inference.InferRegion(countryCode) : null;

            // Load applicable policies: tenant-specific first, then global, ordered by priority
            var tenantId = request.TenantId;
            var policies = await _db.SmsGovernancePolicies
                .Where(p => p.Enabled
                         && policyTypes.Contains(p.PolicyType)
                         && (p.TenantId == tenantId || p.TenantId == null))
                .OrderBy(p => p.TenantId == null ? 1 : 0)   // tenant-specific first
                .ThenBy(p => p.Priority)
                .ToListAsync(ct);

            if (policies.Count == 0)
                return Allow("no_applicable_policy");

            // Evaluate each policy in priority order; return first non-allow result
            foreach (var policy in policies)
            {
                SmsGovernanceEvaluationResult result;
                try
                {
                    result = policy.PolicyType switch
                    {
                        "quiet_hours"            => EvaluateQuietHours(policy, request),
                        "geographic_restriction" => EvaluateGeographic(policy, request, countryCode, region),
                        "rate_limit"             => await EvaluateRateLimitAsync(policy, request, ct),
                        "provider_governance"    => EvaluateProviderGovernance(policy, request),
                        "retry_governance"       => await EvaluateRetryGovernanceAsync(policy, request, ct),
                        "escalation_guardrail"   => await EvaluateEscalationGuardrailAsync(policy, request, ct),
                        _                        => Allow("no_applicable_policy"),
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "SmsGovernancePolicyService: exception evaluating policy {PolicyId} ({PolicyType}) for notification {NotifId}",
                        policy.Id, policy.PolicyType, request.NotificationId);
                    result = _options.FailOpenOnEvaluationError
                        ? Allow("policy_evaluation_error")
                        : Block("block", "policy_evaluation_error", policy);
                }

                if (!result.ShouldProceed)
                {
                    result.CountryCode = result.CountryCode ?? countryCode;
                    result.Region      = result.Region ?? region;

                    // Persist decision asynchronously (best-effort)
                    _ = TryPersistDecisionAsync(request, result, policy);
                    return result;
                }
            }

            return Allow("no_applicable_policy");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "SmsGovernancePolicyService: outer evaluation failed for notification {NotifId} — defaulting to allow",
                request.NotificationId);
            return _options.FailOpenOnEvaluationError
                ? Allow("policy_evaluation_error")
                : Block("block", "policy_evaluation_error", null);
        }
    }

    // ─── Policy evaluators ────────────────────────────────────────────────────

    private SmsGovernanceEvaluationResult EvaluateQuietHours(
        SmsGovernancePolicy policy,
        SmsGovernanceEvaluationRequest request)
    {
        var cfg = ParsePolicyJson<QuietHoursConfig>(policy);
        if (cfg == null) return Allow("no_applicable_policy");

        var tzName  = string.IsNullOrWhiteSpace(cfg.Timezone) ? _options.DefaultTimezone : cfg.Timezone;
        TimeZoneInfo tz;
        try { tz = TimeZoneInfo.FindSystemTimeZoneById(tzName); }
        catch
        {
            _logger.LogWarning("SmsGovernancePolicyService: invalid timezone '{Tz}' in policy {PolicyId} — defaulting to UTC", tzName, policy.Id);
            tz = TimeZoneInfo.Utc;
        }

        var localNow = TimeZoneInfo.ConvertTimeFromUtc(request.NowUtc, tz);
        var dayName  = localNow.DayOfWeek.ToString();

        // Check day restriction
        if (cfg.DaysOfWeek != null && cfg.DaysOfWeek.Length > 0 &&
            !cfg.DaysOfWeek.Any(d => string.Equals(d, dayName, StringComparison.OrdinalIgnoreCase)))
            return Allow("no_applicable_policy"); // Today is not a restricted day

        if (!TimeSpan.TryParse(cfg.QuietStart, out var quietStart) ||
            !TimeSpan.TryParse(cfg.QuietEnd,   out var quietEnd))
            return Allow("no_applicable_policy");

        var currentTime = localNow.TimeOfDay;
        bool inQuietHours;

        if (quietStart <= quietEnd)
        {
            // Same-day window: e.g. 09:00 – 17:00
            inQuietHours = currentTime >= quietStart && currentTime < quietEnd;
        }
        else
        {
            // Overnight window: e.g. 21:00 – 08:00
            inQuietHours = currentTime >= quietStart || currentTime < quietEnd;
        }

        if (!inQuietHours)
            return Allow("no_applicable_policy");

        // Compute next allowed window for delay action
        DateTime? nextAllowed = null;
        if (string.Equals(cfg.Action, "delay", StringComparison.OrdinalIgnoreCase) && cfg.NextAllowedWindow)
        {
            // Next allowed = today's quietEnd, or tomorrow if quietEnd already passed
            var todayQuietEnd = localNow.Date.Add(quietEnd);
            if (todayQuietEnd <= localNow)
                todayQuietEnd = todayQuietEnd.AddDays(1);
            nextAllowed = TimeZoneInfo.ConvertTimeToUtc(todayQuietEnd, tz);
        }

        var action = string.Equals(cfg.Action, "block", StringComparison.OrdinalIgnoreCase) ? "block" : "delay";
        return new SmsGovernanceEvaluationResult
        {
            DecisionType = action,
            ReasonCode   = "quiet_hours_active",
            PolicyId     = policy.Id,
            PolicyName   = policy.Name,
            PolicyType   = policy.PolicyType,
            EffectiveAt  = nextAllowed,
            Metadata     = new Dictionary<string, object>
            {
                ["quietStart"]  = cfg.QuietStart ?? string.Empty,
                ["quietEnd"]    = cfg.QuietEnd   ?? string.Empty,
                ["timezone"]    = tzName,
                ["localTime"]   = localNow.ToString("HH:mm"),
            },
        };
    }

    private SmsGovernanceEvaluationResult EvaluateGeographic(
        SmsGovernancePolicy policy,
        SmsGovernanceEvaluationRequest request,
        string? countryCode,
        string? region)
    {
        var cfg = ParsePolicyJson<GeoRestrictionConfig>(policy);
        if (cfg == null) return Allow("no_applicable_policy");

        var action = string.IsNullOrEmpty(cfg.Action) ? "block" : cfg.Action;

        if (string.IsNullOrEmpty(countryCode))
        {
            // Country unknown
            if (string.Equals(action, "block", StringComparison.OrdinalIgnoreCase))
                return GeoBlock("geographic_not_allowed", policy, countryCode, region, "unknown_country");
            return Allow("no_applicable_policy"); // allow when unknown is not explicitly blocked
        }

        // Blocked countries
        if (cfg.BlockedCountries != null &&
            cfg.BlockedCountries.Any(c => string.Equals(c, countryCode, StringComparison.OrdinalIgnoreCase)))
        {
            return GeoBlock("block", policy, countryCode, region, "geographic_blocked");
        }

        // Allowed countries restriction
        if (cfg.AllowedCountries != null && cfg.AllowedCountries.Length > 0 &&
            !cfg.AllowedCountries.Any(c => string.Equals(c, countryCode, StringComparison.OrdinalIgnoreCase)))
        {
            var decisionType = string.Equals(action, "review_required", StringComparison.OrdinalIgnoreCase)
                ? "review_required" : "block";
            return GeoBlock(decisionType, policy, countryCode, region, "geographic_not_allowed");
        }

        return Allow("no_applicable_policy");
    }

    private async Task<SmsGovernanceEvaluationResult> EvaluateRateLimitAsync(
        SmsGovernancePolicy policy,
        SmsGovernanceEvaluationRequest request,
        CancellationToken ct)
    {
        var cfg = ParsePolicyJson<RateLimitConfig>(policy);
        if (cfg == null) return Allow("no_applicable_policy");

        var windowMinutes = cfg.WindowMinutes > 0 ? cfg.WindowMinutes : _options.RateLimitWindowMinutes;
        var maxMessages   = cfg.MaxMessages;
        if (maxMessages <= 0) return Allow("no_applicable_policy");

        var since = DateTime.UtcNow.AddMinutes(-windowMinutes);
        var scope = (cfg.Scope ?? "tenant").ToLowerInvariant();

        int count;
        try
        {
            count = scope switch
            {
                "platform" => await _db.Notifications
                    .CountAsync(n => n.Channel == "sms" && n.CreatedAt >= since, ct),

                "tenant" => request.TenantId.HasValue
                    ? await _db.Notifications
                        .CountAsync(n => n.Channel == "sms" && n.TenantId == request.TenantId && n.CreatedAt >= since, ct)
                    : 0,

                "provider" => !string.IsNullOrEmpty(request.ProviderType)
                    ? await _db.NotificationAttempts
                        .CountAsync(a => a.Channel == "sms" && a.Provider == request.ProviderType && a.CreatedAt >= since, ct)
                    : 0,

                "tenant_provider" => request.TenantId.HasValue && !string.IsNullOrEmpty(request.ProviderType)
                    ? await _db.NotificationAttempts
                        .CountAsync(a => a.Channel == "sms"
                                      && a.Provider == request.ProviderType
                                      && a.TenantId == request.TenantId
                                      && a.CreatedAt >= since, ct)
                    : 0,

                _ => 0,
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SmsGovernancePolicyService: rate limit count query failed for policy {PolicyId} — defaulting to allow", policy.Id);
            return Allow("no_applicable_policy"); // fail-open for rate limit counts
        }

        if (count < maxMessages)
            return Allow("no_applicable_policy");

        var action = (cfg.Action ?? "throttle").ToLowerInvariant();
        var decisionType = action switch
        {
            "block"  => "block",
            "delay"  => "delay",
            _        => "throttle",
        };

        return new SmsGovernanceEvaluationResult
        {
            DecisionType = decisionType,
            ReasonCode   = "tenant_rate_limit_exceeded",
            PolicyId     = policy.Id,
            PolicyName   = policy.Name,
            PolicyType   = policy.PolicyType,
            EffectiveAt  = DateTime.UtcNow.AddMinutes(windowMinutes),
            Metadata     = new Dictionary<string, object>
            {
                ["currentCount"]    = count,
                ["maxMessages"]     = maxMessages,
                ["windowMinutes"]   = windowMinutes,
                ["scope"]           = scope,
            },
        };
    }

    private SmsGovernanceEvaluationResult EvaluateProviderGovernance(
        SmsGovernancePolicy policy,
        SmsGovernanceEvaluationRequest request)
    {
        var cfg = ParsePolicyJson<ProviderGovernanceConfig>(policy);
        if (cfg == null) return Allow("no_applicable_policy");

        var provider = request.ProviderType?.ToLowerInvariant();
        if (string.IsNullOrEmpty(provider))
            return Allow("no_applicable_policy");

        // Blocked providers
        if (cfg.BlockedProviders != null &&
            cfg.BlockedProviders.Any(p => string.Equals(p, provider, StringComparison.OrdinalIgnoreCase)))
        {
            return new SmsGovernanceEvaluationResult
            {
                DecisionType = "block",
                ReasonCode   = "provider_blocked",
                PolicyId     = policy.Id,
                PolicyName   = policy.Name,
                PolicyType   = policy.PolicyType,
                Metadata     = new Dictionary<string, object> { ["provider"] = provider },
            };
        }

        // Allowed providers restriction
        if (cfg.AllowedProviders != null && cfg.AllowedProviders.Length > 0 &&
            !cfg.AllowedProviders.Any(p => string.Equals(p, provider, StringComparison.OrdinalIgnoreCase)))
        {
            var decisionType = string.Equals(cfg.Action, "review_required", StringComparison.OrdinalIgnoreCase)
                ? "review_required" : "block";
            return new SmsGovernanceEvaluationResult
            {
                DecisionType = decisionType,
                ReasonCode   = "provider_not_allowed",
                PolicyId     = policy.Id,
                PolicyName   = policy.Name,
                PolicyType   = policy.PolicyType,
                Metadata     = new Dictionary<string, object> { ["provider"] = provider },
            };
        }

        return Allow("no_applicable_policy");
    }

    private async Task<SmsGovernanceEvaluationResult> EvaluateRetryGovernanceAsync(
        SmsGovernancePolicy policy,
        SmsGovernanceEvaluationRequest request,
        CancellationToken ct)
    {
        var cfg = ParsePolicyJson<RetryGovernanceConfig>(policy);
        if (cfg == null) return Allow("no_applicable_policy");

        // Per-notification retry cap
        if (cfg.MaxRetriesPerNotification > 0 && request.RetryCount >= cfg.MaxRetriesPerNotification)
        {
            var action = (cfg.Action ?? "review_required").ToLowerInvariant();
            return new SmsGovernanceEvaluationResult
            {
                DecisionType = action is "block" or "review_required" ? action : "review_required",
                ReasonCode   = "retry_limit_exceeded",
                PolicyId     = policy.Id,
                PolicyName   = policy.Name,
                PolicyType   = policy.PolicyType,
                Metadata     = new Dictionary<string, object>
                {
                    ["retryCount"]           = request.RetryCount,
                    ["maxRetriesPerNotif"]   = cfg.MaxRetriesPerNotification,
                },
            };
        }

        // Per-notification dead-letter count (how many times this notif has been dead-lettered/resubmitted)
        if (cfg.BlockAfterDeadLetters > 0 && request.NotificationId.HasValue)
        {
            int deadLetterCount;
            try
            {
                deadLetterCount = await _db.NotificationAttempts
                    .CountAsync(a => a.NotificationId == request.NotificationId
                                  && a.Status == "failed", ct);
            }
            catch { deadLetterCount = 0; } // fail-open

            if (deadLetterCount >= cfg.BlockAfterDeadLetters)
            {
                return new SmsGovernanceEvaluationResult
                {
                    DecisionType = "block",
                    ReasonCode   = "retry_limit_exceeded",
                    PolicyId     = policy.Id,
                    PolicyName   = policy.Name,
                    PolicyType   = policy.PolicyType,
                    Metadata     = new Dictionary<string, object>
                    {
                        ["deadLetterCount"]      = deadLetterCount,
                        ["blockAfterDeadLetters"] = cfg.BlockAfterDeadLetters,
                    },
                };
            }
        }

        return Allow("no_applicable_policy");
    }

    private async Task<SmsGovernanceEvaluationResult> EvaluateEscalationGuardrailAsync(
        SmsGovernancePolicy policy,
        SmsGovernanceEvaluationRequest request,
        CancellationToken ct)
    {
        var cfg = ParsePolicyJson<EscalationGuardrailConfig>(policy);
        if (cfg == null) return Allow("no_applicable_policy");

        var since = DateTime.UtcNow.AddHours(-1);

        try
        {
            // Join escalations → alerts to filter by TenantId / AlertType
            var baseQuery =
                from e in _db.SmsAlertEscalations
                join a in _db.SmsOperationalAlerts on e.AlertId equals a.Id
                where e.CreatedAt >= since
                select new { e.CreatedAt, a.TenantId, a.AlertType };

            if (request.TenantId.HasValue)
                baseQuery = baseQuery.Where(x => x.TenantId == request.TenantId);

            var totalCount = await baseQuery.CountAsync(ct);

            if (cfg.MaxEscalationsPerHour > 0 && totalCount >= cfg.MaxEscalationsPerHour)
            {
                var action = (cfg.Action ?? "throttle").ToLowerInvariant();
                return new SmsGovernanceEvaluationResult
                {
                    DecisionType = action is "block" ? "block" : "throttle",
                    ReasonCode   = "escalation_rate_limit_exceeded",
                    PolicyId     = policy.Id,
                    PolicyName   = policy.Name,
                    PolicyType   = policy.PolicyType,
                    Metadata     = new Dictionary<string, object>
                    {
                        ["escalationsLastHour"]     = totalCount,
                        ["maxEscalationsPerHour"]   = cfg.MaxEscalationsPerHour,
                    },
                };
            }

            // Per-alert-type limit
            if (cfg.MaxEscalationsPerAlertTypePerHour > 0 && !string.IsNullOrEmpty(request.AlertType))
            {
                var typeCount = await baseQuery
                    .Where(x => x.AlertType == request.AlertType)
                    .CountAsync(ct);

                if (typeCount >= cfg.MaxEscalationsPerAlertTypePerHour)
                {
                    var action = (cfg.Action ?? "throttle").ToLowerInvariant();
                    return new SmsGovernanceEvaluationResult
                    {
                        DecisionType = action is "block" ? "block" : "throttle",
                        ReasonCode   = "escalation_rate_limit_exceeded",
                        PolicyId     = policy.Id,
                        PolicyName   = policy.Name,
                        PolicyType   = policy.PolicyType,
                        Metadata     = new Dictionary<string, object>
                        {
                            ["alertType"]                        = request.AlertType,
                            ["typeEscalationsLastHour"]          = typeCount,
                            ["maxEscalationsPerAlertTypePerHour"] = cfg.MaxEscalationsPerAlertTypePerHour,
                        },
                    };
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SmsGovernancePolicyService: escalation guardrail count failed for policy {PolicyId} — defaulting to allow", policy.Id);
            return Allow("no_applicable_policy");
        }

        return Allow("no_applicable_policy");
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static SmsGovernanceEvaluationResult Allow(string reasonCode)
        => new() { DecisionType = "allow", ReasonCode = reasonCode };

    private static SmsGovernanceEvaluationResult Block(
        string decisionType, string reasonCode, SmsGovernancePolicy? policy)
        => new()
        {
            DecisionType = decisionType,
            ReasonCode   = reasonCode,
            PolicyId     = policy?.Id,
            PolicyName   = policy?.Name,
            PolicyType   = policy?.PolicyType,
        };

    private static SmsGovernanceEvaluationResult GeoBlock(
        string decisionType, SmsGovernancePolicy policy,
        string? countryCode, string? region, string reasonCode)
        => new()
        {
            DecisionType = decisionType,
            ReasonCode   = reasonCode,
            PolicyId     = policy.Id,
            PolicyName   = policy.Name,
            PolicyType   = policy.PolicyType,
            CountryCode  = countryCode,
            Region       = region,
            Metadata     = new Dictionary<string, object>
            {
                ["countryCode"] = countryCode ?? "unknown",
                ["region"]      = region      ?? "unknown",
            },
        };

    private T? ParsePolicyJson<T>(SmsGovernancePolicy policy) where T : class
    {
        try
        {
            if (string.IsNullOrWhiteSpace(policy.PolicyJson)) return null;
            return JsonSerializer.Deserialize<T>(policy.PolicyJson, _jsonOpts);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SmsGovernancePolicyService: failed to parse PolicyJson for policy {PolicyId}", policy.Id);
            return null;
        }
    }

    private async Task TryPersistDecisionAsync(
        SmsGovernanceEvaluationRequest request,
        SmsGovernanceEvaluationResult  result,
        SmsGovernancePolicy            policy)
    {
        if (!_options.DecisionAuditEnabled && result.ShouldProceed)
            return;

        try
        {
            string? metaJson = null;
            if (result.Metadata.Count > 0)
            {
                try { metaJson = JsonSerializer.Serialize(result.Metadata); }
                catch { /* best-effort */ }
            }

            _db.SmsGovernanceDecisions.Add(new SmsGovernanceDecision
            {
                Id                   = Guid.NewGuid(),
                NotificationId       = request.NotificationId,
                AttemptId            = request.AttemptId,
                TenantId             = request.TenantId,
                PolicyId             = result.PolicyId,
                PolicyType           = result.PolicyType ?? policy.PolicyType,
                DecisionType         = result.DecisionType,
                ReasonCode           = result.ReasonCode,
                ProviderType         = request.ProviderType,
                ProviderConfigId     = request.ProviderConfigId,
                CountryCode          = result.CountryCode,
                Region               = result.Region,
                EffectiveAt          = result.EffectiveAt,
                DecisionMetadataJson = metaJson,
                CreatedAt            = DateTime.UtcNow,
            });
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SmsGovernancePolicyService: failed to persist governance decision — send continues");
        }
    }

    // ─── PolicyJson POCOs ─────────────────────────────────────────────────────

    private sealed class QuietHoursConfig
    {
        public string?   Timezone            { get; set; }
        public string?   QuietStart          { get; set; }
        public string?   QuietEnd            { get; set; }
        public string[]? DaysOfWeek          { get; set; }
        public string?   Action              { get; set; }
        public bool      NextAllowedWindow   { get; set; } = true;
    }

    private sealed class GeoRestrictionConfig
    {
        public string[]? AllowedCountries { get; set; }
        public string[]? BlockedCountries { get; set; }
        public string?   Action           { get; set; }
    }

    private sealed class RateLimitConfig
    {
        public int     WindowMinutes { get; set; }
        public int     MaxMessages   { get; set; }
        public string? Scope         { get; set; }
        public string? Action        { get; set; }
    }

    private sealed class ProviderGovernanceConfig
    {
        public string[]? AllowedProviders { get; set; }
        public string[]? BlockedProviders { get; set; }
        public string?   Action           { get; set; }
    }

    private sealed class RetryGovernanceConfig
    {
        public int    MaxRetriesPerRecipientPerDay { get; set; }
        public int    MaxRetriesPerNotification    { get; set; }
        public int    BlockAfterDeadLetters        { get; set; }
        public string? Action                      { get; set; }
    }

    private sealed class EscalationGuardrailConfig
    {
        public int    MaxEscalationsPerHour              { get; set; }
        public int    MaxEscalationsPerAlertTypePerHour  { get; set; }
        public string? Action                            { get; set; }
    }
}
