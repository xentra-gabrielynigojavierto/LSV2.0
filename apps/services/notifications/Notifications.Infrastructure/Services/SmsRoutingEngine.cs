using System.Text.Json;
using Microsoft.Extensions.Logging;
using Notifications.Application.Interfaces;
using Notifications.Application.Options;
using Notifications.Domain;
using Microsoft.Extensions.Options;

namespace Notifications.Infrastructure.Services;

/// <summary>
/// LS-NOTIF-SMS-014/015: SMS routing engine.
/// Selects the best provider route from a candidate list based on the active routing policy.
///
/// Routing modes (LS-NOTIF-SMS-014):
///   priority         — preserve existing ProviderRoutingService route order (backward compat)
///   cost_optimized   — select lowest estimated cost provider; fallback to priority
///   health_optimized — skip providers with health_status = "down"; fallback to priority
///   hybrid           — health gate first, then cost, then priority
///   regional         — prefer providers matching country/region; fallback to priority
///
/// Adaptive modes (LS-NOTIF-SMS-015):
///   adaptive_quality  — select provider with highest quality score; fallback to priority
///   adaptive_balanced — composite quality+cost score; fallback to hybrid → priority
///   adaptive_regional — regional quality score; fallback to adaptive_quality → priority
///
/// NEVER calls external providers. Uses only locally persisted health/cost/quality data.
/// Persists routing decision to DB (caller handles persistence using returned result).
/// RecipientPhoneForInferenceOnly is used transiently for country inference and never persisted.
/// </summary>
public class SmsRoutingEngine : ISmsRoutingEngine
{
    private readonly ISmsRoutingPolicyRepository   _policyRepo;
    private readonly IProviderHealthRepository     _healthRepo;
    private readonly ISmsProviderQualityService    _qualitySvc;
    private readonly ISmsRegionalInferenceService  _regionalInference;
    private readonly SmsCostAnalyticsOptions       _costOptions;
    private readonly ILogger<SmsRoutingEngine>     _logger;

    private static readonly HashSet<string> ValidModes = new(StringComparer.OrdinalIgnoreCase)
    {
        "priority", "cost_optimized", "health_optimized", "hybrid", "regional",
        // LS-NOTIF-SMS-015
        "adaptive_quality", "adaptive_balanced", "adaptive_regional",
    };

    public SmsRoutingEngine(
        ISmsRoutingPolicyRepository policyRepo,
        IProviderHealthRepository healthRepo,
        ISmsProviderQualityService qualitySvc,
        ISmsRegionalInferenceService regionalInference,
        IOptions<SmsCostAnalyticsOptions> costOptions,
        ILogger<SmsRoutingEngine> logger)
    {
        _policyRepo        = policyRepo;
        _healthRepo        = healthRepo;
        _qualitySvc        = qualitySvc;
        _regionalInference = regionalInference;
        _costOptions       = costOptions.Value;
        _logger            = logger;
    }

    public async Task<SmsRoutingDecisionResult> SelectRouteAsync(
        SmsRoutingRequest request,
        CancellationToken ct = default)
    {
        var candidates = request.CandidateRoutes.ToList();

        if (candidates.Count == 0)
        {
            _logger.LogDebug("SmsRoutingEngine: no candidate routes for tenant {TenantId}", request.TenantId);
            return SmsRoutingDecisionResult.NoRoute("priority", Array.Empty<string>(), "no_candidate_routes");
        }

        var candidateProviders = candidates.Select(r => r.ProviderType).ToList();

        // LS-NOTIF-SMS-015: Infer country/region from phone (transient — never persisted)
        string? inferredCountryCode = request.CountryCode; // honour explicit if set
        string? inferredRegion      = request.Region;
        if (string.IsNullOrEmpty(inferredCountryCode) &&
            !string.IsNullOrEmpty(request.RecipientPhoneForInferenceOnly))
        {
            inferredCountryCode = _regionalInference.InferCountryCode(request.RecipientPhoneForInferenceOnly);
            inferredRegion      = _regionalInference.InferRegion(inferredCountryCode);
            // Phone discarded here — never stored in local variables that persist
        }

        // Resolve active routing policy (tenant-specific first, then global)
        var policy = await ResolvePolicy(request.TenantId, ct);
        var mode   = policy?.Enabled == true ? (policy.RoutingMode ?? "priority") : "priority";
        if (!ValidModes.Contains(mode)) mode = "priority";

        _logger.LogDebug(
            "SmsRoutingEngine: tenant={TenantId}, mode={Mode}, policy={PolicyId}, candidates=[{Candidates}]",
            request.TenantId, mode, policy?.Id, string.Join(",", candidateProviders));

        // Apply excluded providers from policy
        var excluded = new List<string>();
        if (policy != null && !string.IsNullOrEmpty(policy.ExcludedProvidersJson))
        {
            try
            {
                var ex = JsonSerializer.Deserialize<List<string>>(policy.ExcludedProvidersJson);
                if (ex != null) excluded.AddRange(ex.Select(p => p.ToLowerInvariant()));
            }
            catch { /* ignore malformed JSON */ }
        }

        var filtered = candidates
            .Where(r => !excluded.Contains(r.ProviderType.ToLowerInvariant()))
            .ToList();

        if (filtered.Count == 0)
        {
            return SmsRoutingDecisionResult.NoRoute(mode, candidateProviders,
                "all_candidates_excluded_by_policy");
        }

        // Apply preferred order from policy (re-sort, don't remove)
        if (policy != null && !string.IsNullOrEmpty(policy.PreferredProvidersJson))
        {
            try
            {
                var preferred = JsonSerializer.Deserialize<List<string>>(policy.PreferredProvidersJson)
                    ?? new List<string>();
                filtered = ApplyPreferredOrder(filtered, preferred);
            }
            catch { /* ignore malformed JSON */ }
        }

        // Apply cost cap from policy
        decimal? maxCost = policy?.MaxEstimatedCostPerMessage;
        if (maxCost.HasValue)
        {
            filtered = filtered.Where(r =>
            {
                var est = _costOptions.GetEstimatedCost(r.ProviderType);
                return !est.HasValue || est.Value <= maxCost.Value;
            }).ToList();

            if (filtered.Count == 0)
            {
                return SmsRoutingDecisionResult.NoRoute(mode, candidateProviders,
                    "all_candidates_exceed_max_cost_policy");
            }
        }

        // Mode-specific selection
        ProviderRoute? selected;
        string decisionReason;
        decimal? providerQualityScore = null;
        decimal? adaptiveScore        = null;
        string?  adaptiveInputsJson   = null;

        switch (mode.ToLowerInvariant())
        {
            case "cost_optimized":
                (selected, decisionReason) = SelectCostOptimized(filtered);
                break;

            case "health_optimized":
                (selected, decisionReason) = await SelectHealthOptimizedAsync(
                    filtered, policy?.RequireHealthyProvider ?? false, ct);
                break;

            case "hybrid":
                (selected, decisionReason) = await SelectHybridAsync(
                    filtered, policy?.RequireHealthyProvider ?? false, ct);
                break;

            case "regional":
                (selected, decisionReason) = SelectRegional(
                    filtered, inferredCountryCode ?? request.CountryCode, inferredRegion ?? request.Region);
                break;

            // ── LS-NOTIF-SMS-015: Adaptive modes ─────────────────────────────
            case "adaptive_quality":
                (selected, decisionReason, providerQualityScore, adaptiveInputsJson) =
                    await SelectAdaptiveQualityAsync(filtered, request.TenantId, inferredCountryCode, ct);
                break;

            case "adaptive_balanced":
                (selected, decisionReason, providerQualityScore, adaptiveScore, adaptiveInputsJson) =
                    await SelectAdaptiveBalancedAsync(filtered, request.TenantId, inferredCountryCode, ct);
                break;

            case "adaptive_regional":
                (selected, decisionReason, providerQualityScore, adaptiveInputsJson) =
                    await SelectAdaptiveRegionalAsync(
                        filtered, request.TenantId, inferredCountryCode, inferredRegion, ct);
                break;

            default: // "priority"
                selected       = filtered[0];
                decisionReason = "priority_first_candidate";
                break;
        }

        if (selected == null)
        {
            return SmsRoutingDecisionResult.NoRoute(mode, candidateProviders, decisionReason);
        }

        var estimatedCost = _costOptions.GetEstimatedCost(selected.ProviderType);

        return new SmsRoutingDecisionResult
        {
            Success                  = true,
            SelectedRoute            = selected,
            RoutingMode              = mode,
            SelectedProvider         = selected.ProviderType,
            SelectedProviderConfigId = selected.TenantProviderConfigId,
            ProviderOwnershipMode    = selected.OwnershipMode,
            DecisionReason           = decisionReason,
            CandidateProviders       = candidateProviders,
            ExcludedProviders        = excluded,
            MatchedPolicyId          = policy?.Id,
            EstimatedCostAmount      = estimatedCost,
            CostCurrency             = estimatedCost.HasValue ? _costOptions.DefaultCurrency : null,
            CountryCode              = request.CountryCode,
            Region                   = request.Region,
            // LS-NOTIF-SMS-015
            InferredCountryCode      = inferredCountryCode,
            InferredRegion           = inferredRegion,
            ProviderQualityScore     = providerQualityScore,
            AdaptiveScore            = adaptiveScore,
            AdaptiveInputsJson       = adaptiveInputsJson,
        };
    }

    // ── Existing routing mode implementations (LS-NOTIF-SMS-014) ─────────────

    private (ProviderRoute? route, string reason) SelectCostOptimized(List<ProviderRoute> candidates)
    {
        var withCost = candidates
            .Select(r => (Route: r, Cost: _costOptions.GetEstimatedCost(r.ProviderType)))
            .Where(x => x.Cost.HasValue)
            .OrderBy(x => x.Cost!.Value)
            .ToList();

        if (withCost.Count > 0)
            return (withCost[0].Route,
                $"cost_optimized_cheapest_{withCost[0].Route.ProviderType}_{withCost[0].Cost:F4}");

        _logger.LogDebug("SmsRoutingEngine: cost_optimized — no estimates available, fallback to priority");
        return (candidates[0], "cost_optimized_fallback_priority_no_estimates");
    }

    private async Task<(ProviderRoute? route, string reason)> SelectHealthOptimizedAsync(
        List<ProviderRoute> candidates,
        bool requireHealthy,
        CancellationToken ct)
    {
        var healthy = new List<ProviderRoute>();
        foreach (var route in candidates)
        {
            var health = await _healthRepo.FindByProviderAsync(
                route.ProviderType, "sms", route.OwnershipMode, route.TenantProviderConfigId);

            if (health?.HealthStatus == "down")
            {
                _logger.LogDebug(
                    "SmsRoutingEngine: health_optimized — skipping {Provider} (down)", route.ProviderType);
                continue;
            }
            healthy.Add(route);
        }

        if (healthy.Count > 0)
            return (healthy[0], $"health_optimized_first_healthy_{healthy[0].ProviderType}");

        if (requireHealthy)
            return (null, "no_healthy_provider");

        _logger.LogDebug("SmsRoutingEngine: health_optimized — all providers unhealthy, fallback to priority");
        return (candidates[0], "health_optimized_fallback_priority_all_degraded");
    }

    private async Task<(ProviderRoute? route, string reason)> SelectHybridAsync(
        List<ProviderRoute> candidates,
        bool requireHealthy,
        CancellationToken ct)
    {
        var healthy = new List<ProviderRoute>();
        foreach (var route in candidates)
        {
            var health = await _healthRepo.FindByProviderAsync(
                route.ProviderType, "sms", route.OwnershipMode, route.TenantProviderConfigId);

            if (health?.HealthStatus != "down")
                healthy.Add(route);
        }

        var pool = healthy.Count > 0
            ? healthy
            : (requireHealthy ? new List<ProviderRoute>() : candidates);

        if (pool.Count == 0)
            return (null, "no_healthy_provider");

        var withCost = pool
            .Select(r => (Route: r, Cost: _costOptions.GetEstimatedCost(r.ProviderType)))
            .Where(x => x.Cost.HasValue)
            .OrderBy(x => x.Cost!.Value)
            .ToList();

        if (withCost.Count > 0)
            return (withCost[0].Route, $"hybrid_healthy_cheapest_{withCost[0].Route.ProviderType}");

        return (pool[0], $"hybrid_healthy_priority_fallback_{pool[0].ProviderType}");
    }

    private (ProviderRoute? route, string reason) SelectRegional(
        List<ProviderRoute> candidates,
        string? countryCode,
        string? region)
    {
        if (string.IsNullOrEmpty(countryCode) && string.IsNullOrEmpty(region))
        {
            _logger.LogDebug("SmsRoutingEngine: regional — no country/region data, fallback to priority");
            return (candidates[0], "regional_fallback_no_country_data");
        }
        return (candidates[0], $"regional_fallback_priority_{countryCode ?? region}");
    }

    // ── LS-NOTIF-SMS-015: Adaptive routing modes ──────────────────────────────

    /// <summary>
    /// adaptive_quality: Select the provider with the highest quality score.
    /// Falls back to priority when no quality data exists for any candidate.
    /// </summary>
    private async Task<(ProviderRoute? route, string reason, decimal? qualityScore, string? inputsJson)>
        SelectAdaptiveQualityAsync(
            List<ProviderRoute> candidates,
            Guid tenantId,
            string? countryCode,
            CancellationToken ct)
    {
        var scores = await GetQualityScoresForCandidates(candidates, tenantId, countryCode, ct);
        var withData = scores.Where(x => x.Score.HasSufficientData).ToList();

        if (withData.Count > 0)
        {
            var best = withData.OrderByDescending(x => x.Score.QualityScore).First();
            var inputsJson = JsonSerializer.Serialize(new
            {
                mode = "adaptive_quality",
                selected_provider = best.Route.ProviderType,
                quality_score = best.Score.QualityScore,
                total_attempts = best.Score.TotalAttempts,
                country_code = countryCode,
            });
            return (best.Route,
                    $"adaptive_quality_highest_score_{best.Route.ProviderType}_{best.Score.QualityScore:F1}",
                    best.Score.QualityScore,
                    inputsJson);
        }

        _logger.LogDebug(
            "SmsRoutingEngine: adaptive_quality — insufficient data for all candidates, fallback to priority");
        return (candidates[0],
                "adaptive_quality_fallback_priority_insufficient_data",
                null, null);
    }

    /// <summary>
    /// adaptive_balanced: Combine quality score + cost efficiency → composite score.
    /// Falls back to hybrid → priority when no quality data.
    /// </summary>
    private async Task<(ProviderRoute? route, string reason, decimal? qualityScore, decimal? compositeScore, string? inputsJson)>
        SelectAdaptiveBalancedAsync(
            List<ProviderRoute> candidates,
            Guid tenantId,
            string? countryCode,
            CancellationToken ct)
    {
        var scores = await GetQualityScoresForCandidates(candidates, tenantId, countryCode, ct);
        var withData = scores.Where(x => x.Score.HasSufficientData).ToList();

        if (withData.Count > 0)
        {
            var scored = withData.Select(x =>
            {
                var q = x.Score.QualityScore / 100m;
                var c = (x.Score.CostEfficiencyScore ?? 50m) / 100m;
                var composite = (q * 0.6m) + (c * 0.4m);
                return (x.Route, x.Score, Composite: composite);
            })
            .OrderByDescending(x => x.Composite)
            .ToList();

            var best = scored.First();
            var inputsJson = JsonSerializer.Serialize(new
            {
                mode = "adaptive_balanced",
                selected_provider = best.Route.ProviderType,
                quality_score = best.Score.QualityScore,
                cost_efficiency_score = best.Score.CostEfficiencyScore,
                composite_score = Math.Round(best.Composite * 100m, 2),
                quality_weight = 0.6,
                cost_weight = 0.4,
                country_code = countryCode,
            });
            return (best.Route,
                    $"adaptive_balanced_composite_{best.Route.ProviderType}_{best.Composite * 100m:F1}",
                    best.Score.QualityScore,
                    Math.Round(best.Composite * 100m, 2),
                    inputsJson);
        }

        // Fallback: hybrid
        _logger.LogDebug(
            "SmsRoutingEngine: adaptive_balanced — insufficient data, fallback to hybrid");
        var (hybridRoute, hybridReason) = await SelectHybridAsync(candidates, false, ct);
        return (hybridRoute,
                $"adaptive_balanced_fallback_hybrid_{hybridReason}",
                null, null, null);
    }

    /// <summary>
    /// adaptive_regional: Prefer provider with best quality score for the inferred country.
    /// Falls back: adaptive_quality → priority.
    /// </summary>
    private async Task<(ProviderRoute? route, string reason, decimal? qualityScore, string? inputsJson)>
        SelectAdaptiveRegionalAsync(
            List<ProviderRoute> candidates,
            Guid tenantId,
            string? countryCode,
            string? region,
            CancellationToken ct)
    {
        if (string.IsNullOrEmpty(countryCode))
        {
            _logger.LogDebug(
                "SmsRoutingEngine: adaptive_regional — no country code, fallback to adaptive_quality");
            var (aqRoute, aqReason, aqScore, aqInputs) =
                await SelectAdaptiveQualityAsync(candidates, tenantId, null, ct);
            return (aqRoute,
                    $"adaptive_regional_fallback_no_country_adaptive_quality_{aqReason}",
                    aqScore, aqInputs);
        }

        // Get regional quality scores (country-specific snapshots)
        var scores = await GetQualityScoresForCandidates(candidates, tenantId, countryCode, ct);
        var withRegionalData = scores.Where(x => x.Score.HasSufficientData).ToList();

        if (withRegionalData.Count > 0)
        {
            var best = withRegionalData.OrderByDescending(x => x.Score.QualityScore).First();
            var inputsJson = JsonSerializer.Serialize(new
            {
                mode = "adaptive_regional",
                selected_provider = best.Route.ProviderType,
                country_code = countryCode,
                region = region,
                quality_score = best.Score.QualityScore,
                total_attempts = best.Score.TotalAttempts,
            });
            return (best.Route,
                    $"adaptive_regional_{countryCode}_{best.Route.ProviderType}_{best.Score.QualityScore:F1}",
                    best.Score.QualityScore,
                    inputsJson);
        }

        // Fallback: adaptive_quality (ignoring country)
        _logger.LogDebug(
            "SmsRoutingEngine: adaptive_regional — no regional data for {Country}, fallback to adaptive_quality",
            countryCode);
        var (fbRoute, fbReason, fbScore, fbInputs) =
            await SelectAdaptiveQualityAsync(candidates, tenantId, null, ct);
        return (fbRoute,
                $"adaptive_regional_fallback_{countryCode}_adaptive_quality_{fbReason}",
                fbScore, fbInputs);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<List<(ProviderRoute Route, ProviderQualityScore Score)>>
        GetQualityScoresForCandidates(
            List<ProviderRoute> candidates,
            Guid tenantId,
            string? countryCode,
            CancellationToken ct)
    {
        var result = new List<(ProviderRoute, ProviderQualityScore)>();
        foreach (var route in candidates)
        {
            try
            {
                var score = await _qualitySvc.GetLatestScoreAsync(
                    route.ProviderType,
                    tenantId,
                    route.TenantProviderConfigId,
                    countryCode,
                    ct);
                result.Add((route, score));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "SmsRoutingEngine: failed to get quality score for {Provider} — treating as insufficient data",
                    route.ProviderType);
                result.Add((route, new ProviderQualityScore
                {
                    ProviderType      = route.ProviderType,
                    ProviderConfigId  = route.TenantProviderConfigId,
                    QualityScore      = 50m,
                    HasSufficientData = false,
                }));
            }
        }
        return result;
    }

    private static List<ProviderRoute> ApplyPreferredOrder(
        List<ProviderRoute> routes,
        IReadOnlyList<string> preferredOrder)
    {
        var preferred = new List<ProviderRoute>();
        var remaining = routes.ToList();

        foreach (var p in preferredOrder)
        {
            var match = remaining.FirstOrDefault(r =>
                string.Equals(r.ProviderType, p, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                preferred.Add(match);
                remaining.Remove(match);
            }
        }

        preferred.AddRange(remaining);
        return preferred;
    }

    private async Task<SmsRoutingPolicy?> ResolvePolicy(Guid tenantId, CancellationToken ct)
    {
        try
        {
            var tenantPolicies = await _policyRepo.GetActiveForTenantAsync(tenantId, ct);
            var tenantSpecific = tenantPolicies
                .Where(p => p.TenantId == tenantId)
                .OrderBy(p => p.Priority)
                .FirstOrDefault();
            if (tenantSpecific != null) return tenantSpecific;

            var global = tenantPolicies
                .Where(p => p.TenantId == null)
                .OrderBy(p => p.Priority)
                .FirstOrDefault();
            return global;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "SmsRoutingEngine: failed to load routing policy for tenant {TenantId} — using priority mode",
                tenantId);
            return null;
        }
    }
}
