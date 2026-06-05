// LSCC-01-004: Admin Queue & Operational Visibility
//
// Three read-only admin endpoints that surface operational health data:
//
//   GET /api/admin/dashboard               — aggregate metrics (counts, trends)
//   GET /api/admin/providers/blocked       — paged blocked-access log, grouped per user
//   GET /api/admin/referrals               — referral monitor (platform-wide for PlatformAdmin,
//                                            tenant-scoped for TenantAdmin)
//
// All endpoints require PlatformOrTenantAdmin.
// BLK-GOV-02: Uses AdminTenantScope helpers — replaces fragile inline ternaries.
using BuildingBlocks.Authorization;
using BuildingBlocks.Context;
using CareConnect.Application.Cache;
using CareConnect.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CareConnect.Api.Endpoints;

/// <summary>
/// LSCC-01-004: Admin dashboard, blocked-provider queue, and referral-monitor endpoints.
/// </summary>
public static class AdminDashboardEndpoints
{
    public static IEndpointRouteBuilder MapAdminDashboardEndpoints(
        this IEndpointRouteBuilder routes)
    {
        routes
            .MapGet("/api/admin/dashboard", GetDashboardAsync)
            .RequireAuthorization(Policies.PlatformOrTenantAdmin);

        routes
            .MapGet("/api/admin/providers/blocked", GetBlockedProvidersAsync)
            .RequireAuthorization(Policies.PlatformOrTenantAdmin);

        routes
            .MapGet("/api/admin/referrals", GetAdminReferralsAsync)
            .RequireAuthorization(Policies.PlatformOrTenantAdmin);

        return routes;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GET /api/admin/dashboard
    // ──────────────────────────────────────────────────────────────────────────
    // Returns aggregate counts over rolling 24-hour and 7-day windows.
    // BLK-GOV-02: PlatformAdmin sees platform-wide (null scope); TenantAdmin scoped.
    // BLK-PERF-02: Results cached for 15 s per scope key so repeated page refreshes
    //              do not hammer the DB — short enough to remain operationally useful.
    private static async Task<IResult> GetDashboardAsync(
        CareConnectDbContext    db,
        ICurrentRequestContext  ctx,
        HttpContext             http,
        IMemoryCache            cache,
        CancellationToken       ct)
    {
        var scope = AdminTenantScope.PlatformWide(ctx, http);
        if (scope.IsError) return scope.Error!;

        // Scope key: tenantId GUID for TenantAdmin, "platform" for PlatformAdmin.
        var scopeKey = scope.TenantId.HasValue
            ? scope.TenantId.Value.ToString()
            : "platform";

        var result = await cache.GetOrCreateAsync(
            CareConnectCacheKeys.AdminDashboard(scopeKey),
            async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CareConnectCacheTtl.AdminDashboard;
                entry.Size = 1;

                var now   = DateTime.UtcNow;
                var today = now.AddHours(-24);
                var week  = now.AddDays(-7);

                var referralQuery = db.Referrals.AsQueryable();
                var blockedQuery  = db.BlockedProviderAccessLogs.AsQueryable();

                if (scope.TenantId.HasValue)
                {
                    referralQuery = referralQuery.Where(r => r.TenantId == scope.TenantId.Value);
                    blockedQuery  = blockedQuery.Where(l => l.TenantId == scope.TenantId.Value);
                }

                var referralToday = await referralQuery.CountAsync(r => r.CreatedAtUtc >= today, ct);
                var referralWeek  = await referralQuery.CountAsync(r => r.CreatedAtUtc >= week,  ct);
                var openReferrals = await referralQuery.CountAsync(
                    r => r.Status == "New" || r.Status == "NewOpened" || r.Status == "Accepted" || r.Status == "InProgress", ct);

                var blockedToday  = await blockedQuery.CountAsync(l => l.AttemptedAtUtc >= today, ct);
                var blockedWeek   = await blockedQuery.CountAsync(l => l.AttemptedAtUtc >= week,  ct);
                var blockedUsersToday = await blockedQuery
                    .Where(l => l.AttemptedAtUtc >= today && l.UserId != null)
                    .Select(l => l.UserId)
                    .Distinct()
                    .CountAsync(ct);

                return new
                {
                    referralCountToday        = referralToday,
                    referralCountLast7Days    = referralWeek,
                    openReferrals             = openReferrals,
                    blockedAccessToday        = blockedToday,
                    blockedAccessLast7Days    = blockedWeek,
                    distinctBlockedUsersToday = blockedUsersToday,
                    generatedAtUtc            = now,
                };
            });

        return Results.Ok(result);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GET /api/admin/providers/blocked
    // ──────────────────────────────────────────────────────────────────────────
    // Returns the most-recent blocked-access log entry per (UserId, FailureReason)
    // pair, with an attempt count over the last 7 days.
    // BLK-GOV-02: PlatformAdmin platform-wide; TenantAdmin scoped.
    private static async Task<IResult> GetBlockedProvidersAsync(
        CareConnectDbContext    db,
        ICurrentRequestContext  ctx,
        HttpContext             http,
        [FromQuery] int         page     = 1,
        [FromQuery] int         pageSize = 25,
        [FromQuery] string?     since    = null,
        CancellationToken       ct       = default)
    {
        var scope = AdminTenantScope.PlatformWide(ctx, http);
        if (scope.IsError) return scope.Error!;

        page     = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var window = since is not null && DateTime.TryParse(since, out var parsedSince)
            ? parsedSince.ToUniversalTime()
            : DateTime.UtcNow.AddDays(-7);

        var baseQuery = db.BlockedProviderAccessLogs.AsQueryable();
        if (scope.TenantId.HasValue)
            baseQuery = baseQuery.Where(l => l.TenantId == scope.TenantId.Value);

        var query = baseQuery
            .Where(l => l.AttemptedAtUtc >= window)
            .GroupBy(l => new { l.UserId, l.FailureReason })
            .Select(g => new
            {
                UserId         = g.Key.UserId,
                FailureReason  = g.Key.FailureReason,
                AttemptCount   = g.Count(),
                LastAttemptUtc = g.Max(l => l.AttemptedAtUtc),
                UserEmail      = g.OrderByDescending(l => l.AttemptedAtUtc)
                                  .Select(l => l.UserEmail)
                                  .FirstOrDefault(),
                OrganizationId = g.OrderByDescending(l => l.AttemptedAtUtc)
                                  .Select(l => l.OrganizationId)
                                  .FirstOrDefault(),
                TenantId       = g.OrderByDescending(l => l.AttemptedAtUtc)
                                  .Select(l => l.TenantId)
                                  .FirstOrDefault(),
            })
            .OrderByDescending(x => x.LastAttemptUtc);

        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var result = items.Select(x => new
        {
            userId          = x.UserId,
            userEmail       = x.UserEmail,
            organizationId  = x.OrganizationId,
            tenantId        = x.TenantId,
            failureReason   = x.FailureReason,
            attemptCount    = x.AttemptCount,
            lastAttemptUtc  = x.LastAttemptUtc,
            remediationPath = x.UserId is not null
                ? $"/careconnect/admin/providers/provisioning?userId={x.UserId}"
                : null,
        });

        return Results.Ok(new
        {
            items      = result,
            total,
            page,
            pageSize,
            windowFrom = window,
        });
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GET /api/admin/referrals
    // ──────────────────────────────────────────────────────────────────────────
    // Referral monitor for admins. Joins to Provider for name.
    // PlatformAdmin: platform-wide view (optional ?tenantId filter).
    // TenantAdmin: restricted to their own tenant only.
    // Supports:
    //   ?page=1&pageSize=25
    //   ?status=New|Accepted|InProgress|Completed|Declined|Cancelled
    //   ?tenantId=<guid>  (PlatformAdmin only; ignored for TenantAdmin)
    //   ?since=<ISO-datetime>
    //
    // BLK-GOV-02: PlatformWide scope applied; optional tenantId filter for PlatformAdmin.
    //             TenantAdmin callerTenantId override is always ignored for safety.
    private static async Task<IResult> GetAdminReferralsAsync(
        CareConnectDbContext    db,
        ICurrentRequestContext  ctx,
        HttpContext             http,
        [FromQuery] int      page     = 1,
        [FromQuery] int      pageSize = 25,
        [FromQuery] string?  status   = null,
        [FromQuery] Guid?    tenantId = null,
        [FromQuery] string?  since    = null,
        CancellationToken    ct       = default)
    {
        var scope = AdminTenantScope.PlatformWide(ctx, http);
        if (scope.IsError) return scope.Error!;

        page     = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Referrals
            .Include(r => r.Provider)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(r => r.Status == status);

        // PlatformAdmin: optional ?tenantId narrow.
        // TenantAdmin:   always forced to own tenant (scope.TenantId); caller-supplied tenantId ignored.
        var appliedTenantId = scope.IsPlatformWide ? tenantId : scope.TenantId;
        if (appliedTenantId.HasValue)
            query = query.Where(r => r.TenantId == appliedTenantId.Value);

        if (since is not null && DateTime.TryParse(since, out var parsedSince))
            query = query.Where(r => r.CreatedAtUtc >= parsedSince.ToUniversalTime());

        query = query.OrderByDescending(r => r.CreatedAtUtc);

        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new
            {
                id                      = r.Id,
                tenantId                = r.TenantId,
                status                  = r.Status,
                urgency                 = r.Urgency,
                requestedService        = r.RequestedService,
                providerName            = r.Provider != null ? r.Provider.Name : null,
                providerEmail           = r.Provider != null ? r.Provider.Email : null,
                referringOrganizationId = r.ReferringOrganizationId,
                receivingOrganizationId = r.ReceivingOrganizationId,
                referrerName            = r.ReferrerName,
                referrerEmail           = r.ReferrerEmail,
                createdAtUtc            = r.CreatedAtUtc,
                updatedAtUtc            = r.UpdatedAtUtc,
            })
            .ToListAsync(ct);

        return Results.Ok(new
        {
            items,
            total,
            page,
            pageSize,
        });
    }
}
