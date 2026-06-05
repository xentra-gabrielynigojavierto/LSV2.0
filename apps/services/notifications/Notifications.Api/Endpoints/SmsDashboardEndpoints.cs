using BuildingBlocks.Authorization;
using Notifications.Api.Authorization;
using Notifications.Application.DTOs;
using Notifications.Application.Interfaces;

namespace Notifications.Api.Endpoints;

/// <summary>
/// LS-NOTIF-SMS-008 — Control Center SMS Dashboard APIs.
///
/// All endpoints require PlatformAdmin role (Policies.AdminOnly).
/// All endpoints are read-only — they never trigger sends, retries,
/// reconciliation, or provider calls.
///
/// Endpoints:
///   GET /v1/admin/sms/dashboard/summary   — High-level SMS KPI aggregate
///   GET /v1/admin/sms/dashboard/trends    — Time-series delivery/reconciliation trends
///   GET /v1/admin/sms/dashboard/failures  — Failure category/error breakdown
///   GET /v1/admin/sms/dashboard/tenants   — Per-tenant activity breakdown
///   GET /v1/admin/sms/dashboard/providers — Per-provider/config activity breakdown
///
/// Security guarantees:
///   - No CredentialsJson, SettingsJson, or auth tokens in any response.
///   - No raw phone numbers or RecipientJson in any response.
///   - ProviderConfigId is returned as an opaque Guid, not linked to credentials.
///   - Tenant names are not returned (enrich from Identity service in the UI layer).
///
/// Distinction from LS-NOTIF-SMS-006 activity APIs:
///   - /v1/admin/sms/activity — paginated per-attempt log (for drill-down)
///   - /v1/admin/sms/dashboard/* — aggregated KPIs (for overview/reporting)
/// </summary>
public static class SmsDashboardEndpoints
{
    public static void MapSmsDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app
            .MapGroup("/v1/admin/sms/dashboard")
            .WithTags("Admin — SMS Dashboard")
            .RequireAuthorization(Policies.AdminOnly);

        // ── GET /v1/admin/sms/dashboard/summary ───────────────────────────────
        // Returns high-level SMS delivery and reconciliation KPI aggregate.
        group.MapGet("/summary", async (
            ISmsDashboardService svc,
            CancellationToken ct,
            string?   tenantId,
            string?   provider,
            Guid?     providerConfigId,
            string?   providerOwnershipMode,
            string?   status,
            string?   failureCategory,
            DateTime? from,
            DateTime? to) =>
        {
            var query = BuildQuery(tenantId, provider, providerConfigId,
                providerOwnershipMode, status, failureCategory, from, to);
            var result = await svc.GetSummaryAsync(query, ct);
            return Results.Ok(result);
        });

        // ── GET /v1/admin/sms/dashboard/trends ────────────────────────────────
        // Returns time-series trend data bucketed by hour/day/week (default: day).
        // Default window: last 30 days when from/to are omitted.
        group.MapGet("/trends", async (
            ISmsDashboardService svc,
            CancellationToken ct,
            string?   tenantId,
            string?   provider,
            Guid?     providerConfigId,
            string?   providerOwnershipMode,
            string?   status,
            DateTime? from,
            DateTime? to,
            string?   bucket) =>
        {
            var query = BuildQuery(tenantId, provider, providerConfigId,
                providerOwnershipMode, status, null, from, to);
            query.Bucket = bucket ?? "day";
            var result = await svc.GetTrendsAsync(query, ct);
            return Results.Ok(result);
        });

        // ── GET /v1/admin/sms/dashboard/failures ──────────────────────────────
        // Returns failure category and error code breakdown.
        // Groups rows where Status=failed/dead_letter or FailureCategory is set.
        group.MapGet("/failures", async (
            ISmsDashboardService svc,
            CancellationToken ct,
            string?   tenantId,
            string?   provider,
            Guid?     providerConfigId,
            string?   providerOwnershipMode,
            DateTime? from,
            DateTime? to,
            int?      limit) =>
        {
            var query = BuildQuery(tenantId, provider, providerConfigId,
                providerOwnershipMode, null, null, from, to);
            if (limit.HasValue) query.FailureBreakdownLimit = limit.Value;
            var result = await svc.GetFailureBreakdownAsync(query, ct);
            return Results.Ok(result);
        });

        // ── GET /v1/admin/sms/dashboard/tenants ───────────────────────────────
        // Returns per-tenant activity breakdown ordered by attempt count descending.
        // Tenant names are not returned — enrich from Identity service in the UI.
        group.MapGet("/tenants", async (
            ISmsDashboardService svc,
            CancellationToken ct,
            string?   tenantId,
            string?   provider,
            Guid?     providerConfigId,
            string?   providerOwnershipMode,
            string?   status,
            DateTime? from,
            DateTime? to,
            int?      limit) =>
        {
            var query = BuildQuery(tenantId, provider, providerConfigId,
                providerOwnershipMode, status, null, from, to);
            if (limit.HasValue) query.TenantBreakdownLimit = limit.Value;
            var result = await svc.GetTenantBreakdownAsync(query, ct);
            return Results.Ok(result);
        });

        // ── GET /v1/admin/sms/dashboard/providers ─────────────────────────────
        // Returns per-provider/config activity breakdown.
        // No credentials or settings are included in any response field.
        group.MapGet("/providers", async (
            ISmsDashboardService svc,
            CancellationToken ct,
            string?   tenantId,
            string?   provider,
            Guid?     providerConfigId,
            string?   providerOwnershipMode,
            string?   status,
            DateTime? from,
            DateTime? to,
            int?      limit) =>
        {
            var query = BuildQuery(tenantId, provider, providerConfigId,
                providerOwnershipMode, status, null, from, to);
            if (limit.HasValue) query.ProviderBreakdownLimit = limit.Value;
            var result = await svc.GetProviderBreakdownAsync(query, ct);
            return Results.Ok(result);
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static SmsDashboardQuery BuildQuery(
        string?   tenantId,
        string?   provider,
        Guid?     providerConfigId,
        string?   providerOwnershipMode,
        string?   status,
        string?   failureCategory,
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
            From                 = from,
            To                   = to,
        };

    private static Guid? ParseOptionalGuid(string? raw)
        => !string.IsNullOrEmpty(raw) && Guid.TryParse(raw, out var id) ? id : null;
}
