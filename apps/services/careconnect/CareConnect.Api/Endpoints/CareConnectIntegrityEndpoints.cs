using BuildingBlocks.Authorization;
using CareConnect.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CareConnect.Api.Endpoints;

/// <summary>
/// Phase I: lightweight integrity reporting endpoint.
///
/// GET /api/admin/integrity
///
/// Returns operational integrity counters for CareConnect entities:
///   - referrals where both org IDs are set but OrganizationRelationshipId is null
///   - appointments missing OrganizationRelationshipId where the linked referral has one
///   - providers lacking an Identity OrganizationId link
///   - facilities lacking an Identity OrganizationId link
///
/// Never throws — any individual query failure produces -1 for that counter.
/// The endpoint always returns 200 so the dashboard always renders.
/// </summary>
public static class CareConnectIntegrityEndpoints
{
    public static IEndpointRouteBuilder MapCareConnectIntegrityEndpoints(
        this IEndpointRouteBuilder routes)
    {
        // BLK-SEC-01-FIX-02: Role-based auth on admin diagnostic endpoint.
        // Unauthenticated → 401 (ChallengeAsync).  Authenticated non-admin → 403 (ForbidAsync).
        // Previously AllowAnonymous — internal integrity counters must not be publicly accessible.
        routes.MapGet("/api/admin/integrity", GetIntegrityReport)
              .RequireAuthorization(policy => policy.RequireRole(Roles.PlatformAdmin));
        return routes;
    }

    private static async Task<IResult> GetIntegrityReport(
        CareConnectDbContext db,
        CancellationToken    ct)
    {
        var now = DateTime.UtcNow;

        int referralsWithOrgPairButNullRelationship     = -1;
        int appointmentsMissingRelWhenReferralHasOne    = -1;
        int providersWithoutOrgId                       = -1;
        int facilitiesWithoutOrgId                      = -1;

        try
        {
            // Referrals where both ReferringOrganizationId and ReceivingOrganizationId are
            // populated but OrganizationRelationshipId has not been resolved.
            referralsWithOrgPairButNullRelationship = await db.Referrals
                .CountAsync(r =>
                    r.ReferringOrganizationId != null &&
                    r.ReceivingOrganizationId  != null &&
                    r.OrganizationRelationshipId == null,
                    ct);
        }
        catch { /* count stays -1 */ }

        try
        {
            // Appointments that lack a relationship ID but whose referral has one set.
            // These are legacy appointments created before relationship resolution was active.
            appointmentsMissingRelWhenReferralHasOne = await db.Appointments
                .CountAsync(a =>
                    a.OrganizationRelationshipId == null &&
                    a.Referral != null &&
                    a.Referral.OrganizationRelationshipId != null,
                    ct);
        }
        catch { /* count stays -1 */ }

        try
        {
            providersWithoutOrgId = await db.Providers
                .CountAsync(p => p.IsActive && p.OrganizationId == null, ct);
        }
        catch { /* count stays -1 */ }

        try
        {
            facilitiesWithoutOrgId = await db.Facilities
                .CountAsync(f => f.IsActive && f.OrganizationId == null, ct);
        }
        catch { /* count stays -1 */ }

        var clean =
            referralsWithOrgPairButNullRelationship == 0 &&
            appointmentsMissingRelWhenReferralHasOne == 0 &&
            providersWithoutOrgId == 0 &&
            facilitiesWithoutOrgId == 0;

        return Results.Ok(new
        {
            generatedAtUtc = now,
            clean,

            referrals = new
            {
                withOrgPairButNullRelationship = referralsWithOrgPairButNullRelationship,
            },

            appointments = new
            {
                missingRelationshipWhereReferralHasOne = appointmentsMissingRelWhenReferralHasOne,
            },

            providers = new
            {
                withoutOrganizationId = providersWithoutOrgId,
            },

            facilities = new
            {
                withoutOrganizationId = facilitiesWithoutOrgId,
            },
        });
    }
}
