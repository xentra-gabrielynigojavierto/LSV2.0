using BuildingBlocks.Authorization;
using Notifications.Api.Authorization;
using Notifications.Api.Middleware;
using Notifications.Application.DTOs;
using Notifications.Application.Interfaces;

namespace Notifications.Api.Endpoints;

/// <summary>
/// LS-NOTIF-SMS-006/007 — SMS activity log and summary APIs.
///
/// Tenant endpoints (/v1/sms/activity):
///   Scoped to the authenticated tenant's JWT tenant_id claim.
///   Platform-owned activity excluded by default.
///
/// Admin endpoints (/v1/admin/sms/activity):
///   PlatformAdmin role required. Supports optional cross-tenant tenantId filter
///   and includePlatformActivity flag — matching the existing admin notification pattern.
///
/// LS-NOTIF-SMS-007 adds reconciliation filter parameters to both endpoint groups:
///   lastReconciliationOutcome, lastReconciliationErrorCode, reconciledFrom,
///   reconciledTo, hasBeenReconciled.
/// </summary>
public static class SmsActivityEndpoints
{
    private const int MaxLimit     = 200;
    private const int DefaultLimit = 50;

    public static void MapSmsActivityEndpoints(this IEndpointRouteBuilder app)
    {
        MapTenantActivityEndpoints(app);
        MapAdminActivityEndpoints(app);
    }

    // ── Tenant-scoped endpoints ───────────────────────────────────────────────

    private static void MapTenantActivityEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/sms/activity").WithTags("SMS Activity");

        // GET /v1/sms/activity
        // Returns paginated SMS send attempts for the authenticated tenant.
        group.MapGet("", async (
            HttpContext context,
            ISmsActivityService svc,
            CancellationToken ct,
            string? provider,
            Guid?   providerConfigId,
            string? providerOwnershipMode,
            string? providerMessageId,
            string? status,
            string? failureCategory,
            DateTime? from,
            DateTime? to,
            // LS-NOTIF-SMS-007: reconciliation filters
            string?   lastReconciliationOutcome,
            string?   lastReconciliationErrorCode,
            DateTime? reconciledFrom,
            DateTime? reconciledTo,
            bool?     hasBeenReconciled,
            int? limit,
            int? offset) =>
        {
            var tenantId = context.GetTenantId();

            var query = BuildQuery(
                tenantId:                    tenantId,
                includePlatformActivity:     false,    // tenant callers never see platform activity
                provider:                    provider,
                providerConfigId:            providerConfigId,
                providerOwnershipMode:       providerOwnershipMode,
                providerMessageId:           providerMessageId,
                status:                      status,
                failureCategory:             failureCategory,
                from:                        from,
                to:                          to,
                lastReconciliationOutcome:   lastReconciliationOutcome,
                lastReconciliationErrorCode: lastReconciliationErrorCode,
                reconciledFrom:              reconciledFrom,
                reconciledTo:                reconciledTo,
                hasBeenReconciled:           hasBeenReconciled,
                limit:                       limit,
                offset:                      offset);

            var result = await svc.GetActivityAsync(query, ct);
            return Results.Ok(result);
        }).RequireAuthorization();

        // GET /v1/sms/activity/summary
        // Returns aggregate counts for the authenticated tenant's SMS sends.
        group.MapGet("/summary", async (
            HttpContext context,
            ISmsActivityService svc,
            CancellationToken ct,
            string? provider,
            Guid?   providerConfigId,
            string? providerOwnershipMode,
            string? providerMessageId,
            string? status,
            string? failureCategory,
            DateTime? from,
            DateTime? to,
            // LS-NOTIF-SMS-007: reconciliation filters
            string?   lastReconciliationOutcome,
            string?   lastReconciliationErrorCode,
            DateTime? reconciledFrom,
            DateTime? reconciledTo,
            bool?     hasBeenReconciled) =>
        {
            var tenantId = context.GetTenantId();

            var query = BuildQuery(
                tenantId:                    tenantId,
                includePlatformActivity:     false,
                provider:                    provider,
                providerConfigId:            providerConfigId,
                providerOwnershipMode:       providerOwnershipMode,
                providerMessageId:           providerMessageId,
                status:                      status,
                failureCategory:             failureCategory,
                from:                        from,
                to:                          to,
                lastReconciliationOutcome:   lastReconciliationOutcome,
                lastReconciliationErrorCode: lastReconciliationErrorCode,
                reconciledFrom:              reconciledFrom,
                reconciledTo:                reconciledTo,
                hasBeenReconciled:           hasBeenReconciled);

            var result = await svc.GetSummaryAsync(query, ct);
            return Results.Ok(result);
        }).RequireAuthorization();
    }

    // ── Admin cross-tenant endpoints ──────────────────────────────────────────

    private static void MapAdminActivityEndpoints(IEndpointRouteBuilder app)
    {
        var group = app
            .MapGroup("/v1/admin/sms/activity")
            .WithTags("Admin — SMS Activity")
            .RequireAuthorization(Policies.AdminOnly);

        // GET /v1/admin/sms/activity
        // Returns paginated SMS activity across all tenants.
        // Optional tenantId filter. Optional includePlatformActivity flag.
        group.MapGet("", async (
            HttpContext context,
            ISmsActivityService svc,
            CancellationToken ct,
            string? tenantId,
            bool?   includePlatformActivity,
            string? provider,
            Guid?   providerConfigId,
            string? providerOwnershipMode,
            string? providerMessageId,
            string? status,
            string? failureCategory,
            DateTime? from,
            DateTime? to,
            // LS-NOTIF-SMS-007: reconciliation filters
            string?   lastReconciliationOutcome,
            string?   lastReconciliationErrorCode,
            DateTime? reconciledFrom,
            DateTime? reconciledTo,
            bool?     hasBeenReconciled,
            int? limit,
            int? offset) =>
        {
            var tenantFilter = ParseOptionalTenantId(tenantId);

            var query = BuildQuery(
                tenantId:                    tenantFilter,
                includePlatformActivity:     includePlatformActivity ?? false,
                provider:                    provider,
                providerConfigId:            providerConfigId,
                providerOwnershipMode:       providerOwnershipMode,
                providerMessageId:           providerMessageId,
                status:                      status,
                failureCategory:             failureCategory,
                from:                        from,
                to:                          to,
                lastReconciliationOutcome:   lastReconciliationOutcome,
                lastReconciliationErrorCode: lastReconciliationErrorCode,
                reconciledFrom:              reconciledFrom,
                reconciledTo:                reconciledTo,
                hasBeenReconciled:           hasBeenReconciled,
                limit:                       limit,
                offset:                      offset);

            var result = await svc.GetActivityAsync(query, ct);
            return Results.Ok(result);
        });

        // GET /v1/admin/sms/activity/summary
        // Returns aggregate counts across all tenants (optional tenantId filter).
        group.MapGet("/summary", async (
            HttpContext context,
            ISmsActivityService svc,
            CancellationToken ct,
            string? tenantId,
            bool?   includePlatformActivity,
            string? provider,
            Guid?   providerConfigId,
            string? providerOwnershipMode,
            string? providerMessageId,
            string? status,
            string? failureCategory,
            DateTime? from,
            DateTime? to,
            // LS-NOTIF-SMS-007: reconciliation filters
            string?   lastReconciliationOutcome,
            string?   lastReconciliationErrorCode,
            DateTime? reconciledFrom,
            DateTime? reconciledTo,
            bool?     hasBeenReconciled) =>
        {
            var tenantFilter = ParseOptionalTenantId(tenantId);

            var query = BuildQuery(
                tenantId:                    tenantFilter,
                includePlatformActivity:     includePlatformActivity ?? false,
                provider:                    provider,
                providerConfigId:            providerConfigId,
                providerOwnershipMode:       providerOwnershipMode,
                providerMessageId:           providerMessageId,
                status:                      status,
                failureCategory:             failureCategory,
                from:                        from,
                to:                          to,
                lastReconciliationOutcome:   lastReconciliationOutcome,
                lastReconciliationErrorCode: lastReconciliationErrorCode,
                reconciledFrom:              reconciledFrom,
                reconciledTo:                reconciledTo,
                hasBeenReconciled:           hasBeenReconciled);

            var result = await svc.GetSummaryAsync(query, ct);
            return Results.Ok(result);
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static SmsActivityQuery BuildQuery(
        Guid? tenantId,
        bool includePlatformActivity,
        string? provider,
        Guid? providerConfigId,
        string? providerOwnershipMode,
        string? providerMessageId,
        string? status,
        string? failureCategory,
        DateTime? from,
        DateTime? to,
        string? lastReconciliationOutcome   = null,
        string? lastReconciliationErrorCode = null,
        DateTime? reconciledFrom            = null,
        DateTime? reconciledTo              = null,
        bool? hasBeenReconciled             = null,
        int? limit                          = null,
        int? offset                         = null)
        => new()
        {
            TenantId                    = tenantId,
            IncludePlatformActivity     = includePlatformActivity,
            Provider                    = provider,
            ProviderConfigId            = providerConfigId,
            ProviderOwnershipMode       = providerOwnershipMode,
            ProviderMessageId           = providerMessageId,
            Status                      = status,
            FailureCategory             = failureCategory,
            FromDate                    = from,
            ToDate                      = to,
            // LS-NOTIF-SMS-007
            LastReconciliationOutcome   = lastReconciliationOutcome,
            LastReconciliationErrorCode = lastReconciliationErrorCode,
            ReconciledFrom              = reconciledFrom,
            ReconciledTo                = reconciledTo,
            HasBeenReconciled           = hasBeenReconciled,
            Limit                       = Math.Min(limit ?? DefaultLimit, MaxLimit),
            Offset                      = Math.Max(offset ?? 0, 0),
        };

    private static Guid? ParseOptionalTenantId(string? raw)
        => !string.IsNullOrEmpty(raw) && Guid.TryParse(raw, out var id) ? id : null;
}
