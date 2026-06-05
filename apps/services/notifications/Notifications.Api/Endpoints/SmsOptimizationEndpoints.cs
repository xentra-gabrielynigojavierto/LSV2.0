using BuildingBlocks.Authorization;
using Microsoft.AspNetCore.Mvc;
using Notifications.Application.DTOs;
using Notifications.Application.Interfaces;

namespace Notifications.Api.Endpoints;

/// <summary>
/// LS-NOTIF-SMS-015 — SMS Optimization Analytics Admin APIs.
///
/// All endpoints require PlatformAdmin role (Policies.AdminOnly).
/// All endpoints are read-only. No external provider calls. No phone numbers returned.
/// No credentials, CredentialsJson, SettingsJson, auth tokens, or webhook URLs returned.
///
/// GET /v1/admin/sms/routing/quality             — latest quality snapshots per provider
/// GET /v1/admin/sms/routing/quality/trends      — quality score trend over time
/// GET /v1/admin/sms/routing/latency             — latency analytics per provider
/// GET /v1/admin/sms/routing/regional            — regional delivery performance
/// GET /v1/admin/sms/routing/optimization        — optimization insight summary
/// </summary>
public static class SmsOptimizationEndpoints
{
    public static IEndpointRouteBuilder MapSmsOptimizationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app
            .MapGroup("/v1/admin/sms/routing")
            .WithTags("Admin — SMS Optimization")
            .RequireAuthorization(Policies.AdminOnly);

        // ── GET /quality ───────────────────────────────────────────────────────
        group.MapGet("/quality", async (
            ISmsProviderQualityService qualitySvc,
            CancellationToken ct,
            [FromQuery] string?  provider              = null,
            [FromQuery] string?  providerOwnershipMode = null,
            [FromQuery] string?  countryCode           = null,
            [FromQuery] string?  region                = null,
            [FromQuery] string?  tenantId              = null,
            [FromQuery] string?  providerConfigId      = null,
            [FromQuery] string?  from                  = null,
            [FromQuery] string?  to                    = null,
            [FromQuery] int      limit                 = 50,
            [FromQuery] int      offset                = 0) =>
        {
            var query = BuildQuery(provider, providerOwnershipMode, countryCode, region,
                                   tenantId, providerConfigId, from, to, limit, offset);
            var snapshots = await qualitySvc.QuerySnapshotsAsync(query, ct);

            var items = snapshots.Select(s => new SmsProviderQualityDto
            {
                ProviderType              = s.ProviderType,
                ProviderOwnershipMode     = s.ProviderOwnershipMode,
                CountryCode               = s.CountryCode,
                Region                    = s.Region,
                QualityScore              = s.QualityScore,
                CostEfficiencyScore       = s.CostEfficiencyScore,
                DeliverySuccessRate       = s.DeliverySuccessRate,
                FailureRate               = s.FailureRate,
                RetryRate                 = s.RetryRate,
                ReconciliationFailureRate = s.ReconciliationFailureRate,
                AverageLatencyMs          = s.AverageLatencyMs,
                AverageEffectiveCost      = s.AverageEffectiveCost,
                CostPerDeliveredMessage   = s.CostPerDeliveredMessage,
                TotalAttempts             = s.TotalAttempts,
                DeliveredAttempts         = s.DeliveredAttempts,
                HasSufficientData         = s.TotalAttempts >= 20,
                WindowStart               = s.WindowStart,
                WindowEnd                 = s.WindowEnd,
                CalculatedAt              = s.CalculatedAt,
            }).ToList();

            return Results.Ok(new SmsQualityListResponse { Items = items, Total = items.Count });
        });

        // ── GET /quality/trends ────────────────────────────────────────────────
        group.MapGet("/quality/trends", async (
            ISmsProviderQualityService qualitySvc,
            CancellationToken ct,
            [FromQuery] string? provider         = null,
            [FromQuery] string? countryCode      = null,
            [FromQuery] string? tenantId         = null,
            [FromQuery] string? from             = null,
            [FromQuery] string? to               = null,
            [FromQuery] int     limit            = 200,
            [FromQuery] int     offset           = 0) =>
        {
            var query = BuildQuery(provider, null, countryCode, null,
                                   tenantId, null, from, to, limit, offset);
            var snapshots = await qualitySvc.QuerySnapshotsAsync(query, ct);

            var items = snapshots
                .OrderBy(s => s.CalculatedAt)
                .Select(s => new SmsQualityTrendPoint
                {
                    ProviderType  = s.ProviderType,
                    CountryCode   = s.CountryCode,
                    QualityScore  = s.QualityScore,
                    CalculatedAt  = s.CalculatedAt,
                    TotalAttempts = s.TotalAttempts,
                }).ToList();

            return Results.Ok(new SmsQualityTrendResponse { Items = items, Total = items.Count });
        });

        // ── GET /latency ───────────────────────────────────────────────────────
        group.MapGet("/latency", async (
            ISmsProviderQualityService qualitySvc,
            CancellationToken ct,
            [FromQuery] string? provider              = null,
            [FromQuery] string? providerOwnershipMode = null,
            [FromQuery] string? countryCode           = null,
            [FromQuery] string? region                = null,
            [FromQuery] string? tenantId              = null,
            [FromQuery] string? providerConfigId      = null,
            [FromQuery] string? from                  = null,
            [FromQuery] string? to                    = null,
            [FromQuery] int     limit                 = 50,
            [FromQuery] int     offset                = 0) =>
        {
            var query = BuildQuery(provider, providerOwnershipMode, countryCode, region,
                                   tenantId, providerConfigId, from, to, limit, offset);
            var snapshots = await qualitySvc.QuerySnapshotsAsync(query, ct);

            var items = snapshots
                .Where(s => s.AverageLatencyMs.HasValue)
                .Select(s => new SmsLatencyDto
                {
                    ProviderType     = s.ProviderType,
                    CountryCode      = s.CountryCode,
                    Region           = s.Region,
                    AverageLatencyMs = s.AverageLatencyMs,
                    TotalAttempts    = s.TotalAttempts,
                    WindowStart      = s.WindowStart,
                    WindowEnd        = s.WindowEnd,
                    CalculatedAt     = s.CalculatedAt,
                }).ToList();

            return Results.Ok(new SmsLatencyListResponse { Items = items, Total = items.Count });
        });

        // ── GET /regional ──────────────────────────────────────────────────────
        group.MapGet("/regional", async (
            ISmsProviderQualityService qualitySvc,
            CancellationToken ct,
            [FromQuery] string? provider    = null,
            [FromQuery] string? countryCode = null,
            [FromQuery] string? region      = null,
            [FromQuery] string? tenantId    = null,
            [FromQuery] string? from        = null,
            [FromQuery] string? to          = null,
            [FromQuery] int     limit       = 100,
            [FromQuery] int     offset      = 0) =>
        {
            var query = BuildQuery(provider, null, countryCode, region,
                                   tenantId, null, from, to, limit, offset);

            // Only return snapshots that have a country code (regional dimension)
            var snapshots = (await qualitySvc.QuerySnapshotsAsync(query, ct))
                .Where(s => !string.IsNullOrEmpty(s.CountryCode))
                .ToList();

            var items = snapshots.Select(s => new SmsRegionalDto
            {
                CountryCode          = s.CountryCode,
                Region               = s.Region,
                ProviderType         = s.ProviderType,
                DeliverySuccessRate  = s.DeliverySuccessRate,
                QualityScore         = s.QualityScore,
                AverageLatencyMs     = s.AverageLatencyMs,
                TotalAttempts        = s.TotalAttempts,
                CalculatedAt         = s.CalculatedAt,
            }).ToList();

            return Results.Ok(new SmsRegionalListResponse { Items = items, Total = items.Count });
        });

        // ── GET /optimization ──────────────────────────────────────────────────
        group.MapGet("/optimization", async (
            ISmsProviderQualityService qualitySvc,
            CancellationToken ct,
            [FromQuery] string? tenantId    = null,
            [FromQuery] string? countryCode = null) =>
        {
            Guid? tid = Guid.TryParse(tenantId, out var tg) ? tg : null;
            var scores = await qualitySvc.GetLatestScoresAsync(tid, countryCode, ct);

            if (scores.Count == 0)
            {
                return Results.Ok(new SmsOptimizationResponse
                {
                    DataSummary = "No quality snapshots available. Enable SmsProviderQuality worker to generate data.",
                    GeneratedAt = DateTime.UtcNow,
                });
            }

            var insights = scores.Select(s => new SmsOptimizationInsight
            {
                ProviderType            = s.ProviderType,
                QualityScore            = s.QualityScore,
                CostEfficiencyScore     = s.CostEfficiencyScore,
                DeliverySuccessRate     = s.DeliverySuccessRate,
                AverageLatencyMs        = s.AverageLatencyMs,
                CostPerDeliveredMessage = null,
                TotalAttempts           = s.TotalAttempts,
                HasSufficientData       = s.HasSufficientData,
                Recommendation          = BuildRecommendation(s),
            }).ToList();

            var withData = insights.Where(i => i.HasSufficientData).ToList();
            string? topQuality   = withData.OrderByDescending(i => i.QualityScore).FirstOrDefault()?.ProviderType;
            string? topCost      = withData
                .Where(i => i.CostEfficiencyScore.HasValue)
                .OrderByDescending(i => i.CostEfficiencyScore!.Value)
                .FirstOrDefault()?.ProviderType;
            string? topBalanced  = withData
                .OrderByDescending(i => i.QualityScore * 0.6m + (i.CostEfficiencyScore ?? 50m) * 0.4m)
                .FirstOrDefault()?.ProviderType;

            return Results.Ok(new SmsOptimizationResponse
            {
                Providers               = insights,
                TopQualityProvider      = topQuality,
                TopCostEfficiencyProvider = topCost,
                TopBalancedProvider     = topBalanced,
                GeneratedAt             = DateTime.UtcNow,
                DataSummary = $"{scores.Count} provider(s) evaluated; " +
                              $"{withData.Count} with sufficient data (≥20 attempts).",
            });
        });

        return app;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static SmsQualitySnapshotQuery BuildQuery(
        string? provider, string? ownershipMode, string? countryCode, string? region,
        string? tenantId, string? providerConfigId, string? from, string? to,
        int limit, int offset)
    {
        return new SmsQualitySnapshotQuery
        {
            ProviderType          = provider,
            ProviderOwnershipMode = ownershipMode,
            CountryCode           = countryCode,
            Region                = region,
            TenantId              = Guid.TryParse(tenantId, out var tid) ? tid : null,
            ProviderConfigId      = Guid.TryParse(providerConfigId, out var pid) ? pid : null,
            From                  = DateTime.TryParse(from, out var fd) ? fd.ToUniversalTime() : null,
            To                    = DateTime.TryParse(to, out var td) ? td.ToUniversalTime() : null,
            Limit                 = Math.Min(Math.Max(1, limit), 500),
            Offset                = Math.Max(0, offset),
        };
    }

    private static string BuildRecommendation(ProviderQualityScore s)
    {
        if (!s.HasSufficientData)
            return "Insufficient data — accumulate more SMS attempts to enable adaptive recommendations.";
        if (s.QualityScore >= 85m)
            return "Excellent quality. Suitable as primary provider for adaptive_quality mode.";
        if (s.QualityScore >= 70m)
            return "Good quality. Monitor failure and retry rates.";
        if (s.QualityScore >= 50m)
            return "Moderate quality. Consider as secondary or cost-optimized fallback.";
        return "Low quality score. Investigate failure and reconciliation rates before using as primary.";
    }
}
