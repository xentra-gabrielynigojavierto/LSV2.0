using Notifications.Application.DTOs;
using Notifications.Application.Interfaces;
using Notifications.Domain;
using BuildingBlocks.Authorization;

namespace Notifications.Api.Endpoints;

/// <summary>
/// LS-NOTIF-SMS-016: Read-only admin APIs for recipient intelligence, suppression analytics,
/// destination risk, and carrier failure analytics.
///
/// All endpoints require PlatformAdmin (Policies.AdminOnly).
/// No raw phone numbers, credentials, or raw provider payloads returned.
/// RecipientHash is an opaque 64-char HMAC-SHA256 token only.
/// </summary>
public static class SmsRecipientIntelligenceEndpoints
{
    public static WebApplication MapSmsRecipientIntelligenceEndpoints(this WebApplication app)
    {
        var group = app
            .MapGroup("/v1/admin/sms/recipients")
            .WithTags("SMS Recipient Intelligence")
            .RequireAuthorization(Policies.AdminOnly);

        // GET /v1/admin/sms/recipients/quality
        group.MapGet("/quality", async (
            Guid?    tenantId,
            string?  provider,
            string?  countryCode,
            string?  region,
            string?  riskLevel,
            DateTime? from,
            DateTime? to,
            int      limit  = 50,
            int      offset = 0,
            ISmsRecipientIntelligenceService svc = default!) =>
        {
            var query = new SmsRecipientAnalyticsQuery
            {
                TenantId    = tenantId,
                Provider    = provider,
                CountryCode = countryCode,
                Region      = region,
                RiskLevel   = riskLevel,
                From        = from,
                To          = to,
                Limit       = Math.Min(limit, 200),
                Offset      = offset,
            };

            var (items, total) = await svc.QueryRecipientAnalyticsAsync(query, default);

            return Results.Ok(new SmsRecipientReputationListResult
            {
                Items  = items.Select(MapToDto).ToList(),
                Total  = total,
                Limit  = query.Limit,
                Offset = query.Offset,
            });
        });

        // GET /v1/admin/sms/recipients/failures
        // High-failure recipients ordered by failure risk descending
        group.MapGet("/failures", async (
            Guid?    tenantId,
            string?  provider,
            string?  countryCode,
            string?  riskLevel,
            DateTime? from,
            DateTime? to,
            int limit  = 50,
            int offset = 0,
            ISmsRecipientIntelligenceService svc = default!) =>
        {
            var query = new SmsRecipientAnalyticsQuery
            {
                TenantId    = tenantId,
                Provider    = provider,
                CountryCode = countryCode,
                RiskLevel   = riskLevel ?? "high",
                From        = from,
                To          = to,
                Limit       = Math.Min(limit, 200),
                Offset      = offset,
            };

            var (items, total) = await svc.QueryRecipientAnalyticsAsync(query, default);

            var failures = items.Select(s => new SmsRecipientFailureDto
            {
                RecipientHash              = s.RecipientHash,
                TenantId                   = s.TenantId,
                CountryCode                = s.CountryCode,
                FailureRate                = s.FailureRate,
                CarrierFailureRate         = s.CarrierFailureRate,
                InvalidNumberRisk          = s.InvalidNumberRisk,
                RetrySuppressionRisk       = s.RetrySuppressionRisk,
                TotalAttempts              = s.TotalAttempts,
                FailedAttempts             = s.FailedAttempts,
                CarrierRejectedAttempts    = s.CarrierRejectedAttempts,
                InvalidDestinationAttempts = s.InvalidDestinationAttempts,
                DestinationRiskLevel       = s.DestinationRiskLevel,
                LastAttemptAt              = s.LastAttemptAt,
                CalculatedAt               = s.CalculatedAt,
            }).ToList();

            return Results.Ok(new SmsRecipientFailureListResult
            {
                Items  = failures,
                Total  = total,
                Limit  = query.Limit,
                Offset = query.Offset,
            });
        });

        // GET /v1/admin/sms/recipients/suppressions
        group.MapGet("/suppressions", async (
            Guid?    tenantId,
            string?  decisionType,
            string?  reasonCode,
            string?  provider,
            string?  countryCode,
            DateTime? from,
            DateTime? to,
            int limit  = 50,
            int offset = 0,
            ISmsRecipientIntelligenceService svc = default!) =>
        {
            var query = new SmsSuppressionDecisionQuery
            {
                TenantId     = tenantId,
                DecisionType = decisionType,
                ReasonCode   = reasonCode,
                Provider     = provider,
                CountryCode  = countryCode,
                From         = from,
                To           = to,
                Limit        = Math.Min(limit, 200),
                Offset       = offset,
            };

            var (items, total) = await svc.QuerySuppressionDecisionsAsync(query, default);

            return Results.Ok(new SmsSuppressionDecisionListResult
            {
                Items = items.Select(MapDecisionToDto).ToList(),
                Total  = total,
                Limit  = query.Limit,
                Offset = query.Offset,
            });
        });

        // GET /v1/admin/sms/recipients/risk
        group.MapGet("/risk", async (
            Guid?   tenantId,
            string? countryCode,
            ISmsRecipientIntelligenceService svc = default!) =>
        {
            var dist = await svc.GetRiskDistributionAsync(tenantId, countryCode, default);

            var low        = dist.GetValueOrDefault("low",        0L);
            var medium     = dist.GetValueOrDefault("medium",     0L);
            var high       = dist.GetValueOrDefault("high",       0L);
            var suppressed = dist.GetValueOrDefault("suppressed", 0L);

            return Results.Ok(new SmsDestinationRiskSummaryDto
            {
                LowRiskCount    = low,
                MediumRiskCount = medium,
                HighRiskCount   = high,
                SuppressedCount = suppressed,
                TotalRecipients = low + medium + high + suppressed,
                ByCountry       = new Dictionary<string, long>(),
                ByProvider      = new Dictionary<string, long>(),
                GeneratedAt     = DateTime.UtcNow,
            });
        });

        // GET /v1/admin/sms/recipients/trends
        // Daily aggregated trend points from the recipient reputation snapshots
        group.MapGet("/trends", async (
            Guid?    tenantId,
            string?  countryCode,
            DateTime? from,
            DateTime? to,
            ISmsRecipientIntelligenceService svc = default!) =>
        {
            var query = new SmsRecipientAnalyticsQuery
            {
                TenantId    = tenantId,
                CountryCode = countryCode,
                From        = from ?? DateTime.UtcNow.AddDays(-30),
                To          = to   ?? DateTime.UtcNow,
                Limit       = 200,
                Offset      = 0,
            };

            var (items, _) = await svc.QueryRecipientAnalyticsAsync(query, default);

            // Group by day to produce trend points
            var grouped = items
                .GroupBy(s => s.CalculatedAt.Date)
                .OrderBy(g => g.Key)
                .Select(g => new SmsRecipientTrendPoint
                {
                    WindowDate          = g.Key,
                    TotalRecipients     = g.Count(),
                    AverageDeliveryRate = g.Average(s => s.DeliverySuccessRate),
                    AverageFailureRate  = g.Average(s => s.FailureRate),
                    AverageQualityScore = g.Average(s => s.QualityScore),
                    SuppressedCount     = g.Count(s => s.DestinationRiskLevel == "suppressed"),
                    HighRiskCount       = g.Count(s => s.DestinationRiskLevel == "high"),
                })
                .ToList();

            return Results.Ok(new SmsRecipientTrendResult
            {
                Points      = grouped,
                GeneratedAt = DateTime.UtcNow,
            });
        });

        return app;
    }

    // ── Projection helpers ───────────────────────────────────────────────────

    private static SmsRecipientReputationDto MapToDto(SmsRecipientReputationSnapshot s) => new()
    {
        Id                         = s.Id,
        RecipientHash              = s.RecipientHash,
        TenantId                   = s.TenantId,
        ProviderType               = s.ProviderType,
        CountryCode                = s.CountryCode,
        Region                     = s.Region,
        TotalAttempts              = s.TotalAttempts,
        DeliveredAttempts          = s.DeliveredAttempts,
        FailedAttempts             = s.FailedAttempts,
        RetryAttempts              = s.RetryAttempts,
        DeadLetterAttempts         = s.DeadLetterAttempts,
        CarrierRejectedAttempts    = s.CarrierRejectedAttempts,
        InvalidDestinationAttempts = s.InvalidDestinationAttempts,
        DeliverySuccessRate        = s.DeliverySuccessRate,
        FailureRate                = s.FailureRate,
        RetryRate                  = s.RetryRate,
        DeadLetterRate             = s.DeadLetterRate,
        CarrierFailureRate         = s.CarrierFailureRate,
        InvalidNumberRisk          = s.InvalidNumberRisk,
        RetrySuppressionRisk       = s.RetrySuppressionRisk,
        QualityScore               = s.QualityScore,
        DestinationRiskLevel       = s.DestinationRiskLevel,
        LastAttemptAt              = s.LastAttemptAt,
        CalculatedAt               = s.CalculatedAt,
    };

    private static SmsSuppressionDecisionDto MapDecisionToDto(SmsSuppressionDecision d) => new()
    {
        Id             = d.Id,
        RecipientHash  = d.RecipientHash,
        TenantId       = d.TenantId,
        NotificationId = d.NotificationId,
        AttemptId      = d.AttemptId,
        DecisionType   = d.DecisionType,
        ReasonCode     = d.ReasonCode,
        RiskScore      = d.RiskScore,
        QualityScore   = d.QualityScore,
        RetryCount     = d.RetryCount,
        ProviderType   = d.ProviderType,
        CountryCode    = d.CountryCode,
        Region         = d.Region,
        CreatedAt      = d.CreatedAt,
    };
}
