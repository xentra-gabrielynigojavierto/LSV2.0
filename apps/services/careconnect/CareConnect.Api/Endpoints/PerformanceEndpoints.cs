// LSCC-01-005: Referral Performance Metrics API.
//
//   GET /api/admin/performance
//     Query params:
//       days=<int>   — preset window in days (default 7, max 90)
//       since=<ISO>  — explicit UTC start (overrides days if both provided)
//
//   Auth: PlatformOrTenantAdmin only
//   BLK-GOV-02: Uses AdminTenantScope.PlatformWide — replaces inline ternary.
//
// Response includes:
//   - summary (totalReferrals, acceptedReferrals, acceptanceRate, avgTimeToAcceptHours,
//               currentNewReferrals)
//   - aging   (lt1h, h1to24, d1to3, gt3d, total)
//   - providers (per-provider totals, rates, avg TTA)
//   - windowFrom, windowTo (UTC)
using BuildingBlocks.Authorization;
using BuildingBlocks.Context;
using CareConnect.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CareConnect.Api.Endpoints;

/// <summary>
/// LSCC-01-005: Admin referral performance metrics endpoint.
/// </summary>
public static class PerformanceEndpoints
{
    public static IEndpointRouteBuilder MapPerformanceEndpoints(
        this IEndpointRouteBuilder routes)
    {
        routes
            .MapGet("/api/admin/performance", GetPerformanceAsync)
            .RequireAuthorization(Policies.PlatformOrTenantAdmin);

        return routes;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GET /api/admin/performance
    // ──────────────────────────────────────────────────────────────────────────
    // Time window selection:
    //   1. If `since` is provided and parseable as UTC, use it.
    //   2. Else use `days` (default 7, clamped to [1, 90]).
    // Both params are optional and shareable in bookmarks/links.
    private static async Task<IResult> GetPerformanceAsync(
        IReferralPerformanceService perf,
        ICurrentRequestContext      ctx,
        HttpContext                 http,
        [FromQuery] string?         since    = null,
        [FromQuery] int             days     = 7,
        CancellationToken           ct       = default)
    {
        var nowUtc = DateTime.UtcNow;

        DateTime windowFrom;
        if (since is not null && DateTime.TryParse(since, null,
                System.Globalization.DateTimeStyles.RoundtripKind, out var parsedSince))
        {
            windowFrom = parsedSince.ToUniversalTime();
        }
        else
        {
            var clampedDays = Math.Clamp(days, 1, 90);
            windowFrom = nowUtc.AddDays(-clampedDays);
        }

        // BLK-GOV-02: PlatformAdmin sees platform-wide (null scope).
        //             TenantAdmin is scoped to their own tenant.
        var scope = AdminTenantScope.PlatformWide(ctx, http);
        if (scope.IsError) return scope.Error!;

        var result = await perf.GetPerformanceAsync(windowFrom, scope.TenantId, ct);

        return Results.Ok(new
        {
            windowFrom = result.WindowFrom,
            windowTo   = result.WindowTo,
            summary    = new
            {
                totalReferrals        = result.Summary.TotalReferrals,
                acceptedReferrals     = result.Summary.AcceptedReferrals,
                acceptanceRate        = result.Summary.AcceptanceRate,
                avgTimeToAcceptHours  = result.Summary.AvgTimeToAcceptHours,
                currentNewReferrals   = result.Summary.CurrentNewReferrals,
            },
            aging    = new
            {
                lt1h   = result.Aging.Lt1h,
                h1to24 = result.Aging.H1To24,
                d1to3  = result.Aging.D1To3,
                gt3d   = result.Aging.Gt3d,
                total  = result.Aging.Total,
            },
            providers = result.Providers.Select(p => new
            {
                providerId            = p.ProviderId,
                providerName          = p.ProviderName,
                totalReferrals        = p.TotalReferrals,
                acceptedReferrals     = p.AcceptedReferrals,
                acceptanceRate        = p.AcceptanceRate,
                avgTimeToAcceptHours  = p.AvgTimeToAcceptHours,
            }),
        });
    }
}
