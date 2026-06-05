using BuildingBlocks.Authorization;
using Notifications.Application.DTOs;
using Notifications.Application.Interfaces;

namespace Notifications.Api.Endpoints;

/// <summary>
/// LS-NOTIF-SMS-013 — SMS Cost and Billing Analytics APIs.
///
/// All endpoints require PlatformAdmin role (Policies.AdminOnly).
/// All endpoints are read-only — they never trigger sends, retries, reconciliation, or provider calls.
/// All endpoints call Notification Service-local cost data only — no external billing/payment systems.
///
/// Endpoints:
///   GET /v1/admin/sms/costs/summary   — Platform-wide cost KPI aggregate
///   GET /v1/admin/sms/costs/trends    — Time-series cost trend (bucket: hour/day/week)
///   GET /v1/admin/sms/costs/providers — Per-provider/config cost breakdown
///   GET /v1/admin/sms/costs/tenants   — Per-tenant cost breakdown (TenantId only, no names)
///   GET /v1/admin/sms/costs/failures  — Failure/retry cost breakdown
///   GET /v1/admin/sms/costs/export    — Export-ready JSON rows
///
/// Security guarantees:
///   - No CredentialsJson, SettingsJson, auth tokens, or webhook URLs in any response.
///   - No raw phone numbers or RecipientJson in any response.
///   - ProviderConfigId is an opaque Guid, not linked to credentials.
///   - TenantId is the only tenant identifier returned (no names — enrich from Identity in UI).
///   - No payment processor calls, no FX conversion, no external billing APIs.
///   - Estimated costs are clearly attributable via CostSource field.
/// </summary>
public static class SmsCostEndpoints
{
    public static void MapSmsCostEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app
            .MapGroup("/v1/admin/sms/costs")
            .WithTags("Admin — SMS Cost Analytics")
            .RequireAuthorization(Policies.AdminOnly);

        // ── GET /v1/admin/sms/costs/summary ───────────────────────────────────
        group.MapGet("/summary", async (
            ISmsCostAnalyticsService svc,
            CancellationToken ct,
            string?   tenantId,
            string?   provider,
            Guid?     providerConfigId,
            string?   providerOwnershipMode,
            string?   status,
            string?   failureCategory,
            string?   costSource,
            string?   currency,
            DateTime? from,
            DateTime? to) =>
        {
            var query = BuildQuery(tenantId, provider, providerConfigId, providerOwnershipMode,
                status, failureCategory, costSource, currency, from, to);
            var result = await svc.GetSummaryAsync(query, ct);
            return Results.Ok(result);
        });

        // ── GET /v1/admin/sms/costs/trends ────────────────────────────────────
        group.MapGet("/trends", async (
            ISmsCostAnalyticsService svc,
            CancellationToken ct,
            string?   tenantId,
            string?   provider,
            Guid?     providerConfigId,
            string?   providerOwnershipMode,
            string?   costSource,
            string?   currency,
            DateTime? from,
            DateTime? to,
            string?   bucket) =>
        {
            var query = BuildQuery(tenantId, provider, providerConfigId, providerOwnershipMode,
                null, null, costSource, currency, from, to);
            query.Bucket = bucket ?? "day";
            var result = await svc.GetTrendsAsync(query, ct);
            return Results.Ok(result);
        });

        // ── GET /v1/admin/sms/costs/providers ─────────────────────────────────
        group.MapGet("/providers", async (
            ISmsCostAnalyticsService svc,
            CancellationToken ct,
            string?   tenantId,
            string?   provider,
            Guid?     providerConfigId,
            string?   providerOwnershipMode,
            string?   status,
            string?   costSource,
            string?   currency,
            DateTime? from,
            DateTime? to,
            int?      limit) =>
        {
            var query = BuildQuery(tenantId, provider, providerConfigId, providerOwnershipMode,
                status, null, costSource, currency, from, to);
            if (limit.HasValue) query.ProviderBreakdownLimit = limit.Value;
            var result = await svc.GetProviderBreakdownAsync(query, ct);
            return Results.Ok(result);
        });

        // ── GET /v1/admin/sms/costs/tenants ───────────────────────────────────
        group.MapGet("/tenants", async (
            ISmsCostAnalyticsService svc,
            CancellationToken ct,
            string?   tenantId,
            string?   provider,
            Guid?     providerConfigId,
            string?   providerOwnershipMode,
            string?   status,
            string?   costSource,
            string?   currency,
            DateTime? from,
            DateTime? to,
            int?      limit) =>
        {
            var query = BuildQuery(tenantId, provider, providerConfigId, providerOwnershipMode,
                status, null, costSource, currency, from, to);
            if (limit.HasValue) query.TenantBreakdownLimit = limit.Value;
            var result = await svc.GetTenantBreakdownAsync(query, ct);
            return Results.Ok(result);
        });

        // ── GET /v1/admin/sms/costs/failures ──────────────────────────────────
        group.MapGet("/failures", async (
            ISmsCostAnalyticsService svc,
            CancellationToken ct,
            string?   tenantId,
            string?   provider,
            Guid?     providerConfigId,
            string?   providerOwnershipMode,
            string?   costSource,
            string?   currency,
            DateTime? from,
            DateTime? to,
            int?      limit) =>
        {
            var query = BuildQuery(tenantId, provider, providerConfigId, providerOwnershipMode,
                null, null, costSource, currency, from, to);
            if (limit.HasValue) query.FailureBreakdownLimit = limit.Value;
            var result = await svc.GetFailureCostBreakdownAsync(query, ct);
            return Results.Ok(result);
        });

        // ── GET /v1/admin/sms/costs/export ────────────────────────────────────
        // Returns export-ready JSON rows. No CSV — export formatting is left to the caller.
        // Max 10,000 rows. Truncated = true if result was capped.
        group.MapGet("/export", async (
            ISmsCostAnalyticsService svc,
            CancellationToken ct,
            string?   tenantId,
            string?   provider,
            Guid?     providerConfigId,
            string?   providerOwnershipMode,
            string?   status,
            string?   failureCategory,
            string?   costSource,
            string?   currency,
            DateTime? from,
            DateTime? to,
            int?      limit) =>
        {
            var query = BuildQuery(tenantId, provider, providerConfigId, providerOwnershipMode,
                status, failureCategory, costSource, currency, from, to);
            if (limit.HasValue) query.ExportLimit = limit.Value;
            var result = await svc.ExportAsync(query, ct);
            return Results.Ok(result);
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static SmsCostQuery BuildQuery(
        string?   tenantId,
        string?   provider,
        Guid?     providerConfigId,
        string?   providerOwnershipMode,
        string?   status,
        string?   failureCategory,
        string?   costSource,
        string?   currency,
        DateTime? from,
        DateTime? to)
        => new()
        {
            TenantId             = ParseOptionalGuid(tenantId),
            Provider             = provider,
            ProviderConfigId     = providerConfigId,
            ProviderOwnershipMode = providerOwnershipMode,
            Status               = status,
            FailureCategory      = failureCategory,
            CostSource           = costSource,
            Currency             = currency,
            From                 = from,
            To                   = to,
        };

    private static Guid? ParseOptionalGuid(string? raw)
        => !string.IsNullOrEmpty(raw) && Guid.TryParse(raw, out var id) ? id : null;
}
