// CC2-INT-B09: Provider tenant self-onboarding endpoints.
// Authenticated — requires a valid JWT (COMMON_PORTAL provider).
// Gateway route: /careconnect/api/provider/onboarding/* → RequireAuthorization (protected)
//
// BLK-GOV-01 access model (EXPECTED BY DESIGN):
//   All three endpoints use RequireAuthorization() (JWT required, no product role).
//   This is intentional: COMMON_PORTAL providers hold a regular JWT without a product role
//   because they have not yet completed onboarding.  Product-role gates would block the
//   very flow that assigns those roles.
//
//   Access control is enforced at the service layer via two ownership checks:
//     1. identityUserId from JWT → provider record lookup (returns 404 if no record).
//     2. Provider.AccessStage == COMMON_PORTAL guard in ProvisionToTenantAsync
//        (returns WrongAccessStage for already-provisioned or admin-created providers).
//
//   TenantAdmin / PlatformAdmin users who hit these endpoints receive a 404 because they
//   have no COMMON_PORTAL provider record, which is the correct behaviour.
using BuildingBlocks.Context;
using CareConnect.Api.Helpers;
using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;
using CareConnect.Application.Repositories;
using CareConnect.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;

namespace CareConnect.Api.Endpoints;

public static class ProviderOnboardingEndpoints
{
    public static IEndpointRouteBuilder MapProviderOnboardingEndpoints(
        this IEndpointRouteBuilder app)
    {
        // ── GET /api/provider/onboarding/status ──────────────────────────────
        // Returns the provider's current onboarding stage so the frontend can
        // decide whether to show the "Set up your workspace" CTA.
        app.MapGet("/api/provider/onboarding/status", async (
            ICurrentRequestContext     ctx,
            IProviderRepository        providerRepo,
            CancellationToken          ct) =>
        {
            var identityUserId = ctx.UserId;
            if (identityUserId is null)
                return Results.Unauthorized();

            var provider = await providerRepo.GetByIdentityUserIdAsync(identityUserId.Value, ct);
            if (provider is null)
                return Results.NotFound(new { message = "No provider record linked to this account." });

            return Results.Ok(new
            {
                providerId   = provider.Id,
                accessStage  = provider.AccessStage,
                canOnboard   = provider.AccessStage == ProviderAccessStage.CommonPortal,
            });
        }).RequireAuthorization();

        // ── GET /api/provider/onboarding/check-code ───────────────────────────
        // Checks whether a tenant code is available for self-provisioning.
        // Called live from the onboarding form as the user types.
        // RequireAuthorization: prevents anonymous probing of all tenant subdomain names.
        app.MapGet("/api/provider/onboarding/check-code", async (
            string                      code,
            IProviderOnboardingService  onboardingSvc,
            CancellationToken           ct) =>
        {
            if (string.IsNullOrWhiteSpace(code))
                return Results.BadRequest(new { message = "code query parameter is required." });

            var trimmed = code.Trim().ToLowerInvariant();

            if (!TenantCodeValidator.IsValid(trimmed))
                return Results.Ok(new TenantCodeAvailabilityResponse
                {
                    Available      = false,
                    NormalizedCode = trimmed,
                    Message        = TenantCodeValidator.FormatHint,
                });

            var result = await onboardingSvc.CheckCodeAvailableAsync(trimmed, ct);

            if (result is null)
            {
                // Identity service unreachable — optimistic response; provision step enforces uniqueness.
                return Results.Ok(new TenantCodeAvailabilityResponse
                {
                    Available      = true,
                    NormalizedCode = trimmed,
                    Message        = "Availability could not be confirmed — the code will be validated on submission.",
                });
            }

            return Results.Ok(new TenantCodeAvailabilityResponse
            {
                Available      = result.Available,
                NormalizedCode = result.NormalizedCode,
                Message        = result.Message,
            });
        }).RequireAuthorization();

        // ── POST /api/provider/onboarding/provision-tenant ────────────────────
        // Creates a new tenant workspace for the authenticated COMMON_PORTAL provider.
        // Transitions provider AccessStage: COMMON_PORTAL → TENANT.
        app.MapPost("/api/provider/onboarding/provision-tenant", async (
            ProviderOnboardingRequest  req,
            ICurrentRequestContext     ctx,
            IProviderOnboardingService onboardingSvc,
            CancellationToken          ct) =>
        {
            // Guard: authenticated user is required.
            var identityUserId = ctx.UserId;
            if (identityUserId is null)
                return Results.Unauthorized();

            // Input validation — name.
            if (string.IsNullOrWhiteSpace(req.TenantName) || req.TenantName.Trim().Length < 2)
                return Results.UnprocessableEntity(new
                {
                    message = "Validation failed.",
                    errors  = new Dictionary<string, string>
                    {
                        ["tenantName"] = "Organization name must be at least 2 characters.",
                    },
                });

            // Input validation — code (format + length).
            var codeNormalized = req.TenantCode?.Trim().ToLowerInvariant() ?? string.Empty;
            if (!TenantCodeValidator.IsValid(codeNormalized))
                return Results.UnprocessableEntity(new
                {
                    message = "Validation failed.",
                    errors  = new Dictionary<string, string>
                    {
                        ["tenantCode"] = TenantCodeValidator.FormatHint,
                    },
                });

            try
            {
                var result = await onboardingSvc.ProvisionToTenantAsync(
                    identityUserId.Value,
                    req.TenantName.Trim(),
                    codeNormalized,         // already lowercased + trimmed above
                    ct);

                var message = result.IsResumed
                    ? "We found an existing workspace setup. Setup is now complete."
                    : "Your workspace is being set up. DNS provisioning may take a few minutes.";

                return Results.Created(
                    $"/api/provider/onboarding/provision-tenant",
                    new ProviderOnboardingResponse
                    {
                        ProviderId         = result.ProviderId,
                        TenantId           = result.TenantId,
                        TenantCode         = result.TenantCode,
                        Subdomain          = result.Subdomain,
                        ProvisioningStatus = result.ProvisioningStatus,
                        PortalUrl          = result.PortalUrl,
                        IsResumed          = result.IsResumed,
                        Message            = message,
                    });
            }
            catch (ProviderOnboardingException ex)
            {
                return ex.Code switch
                {
                    ProviderOnboardingErrorCode.ProviderNotFound     => Results.NotFound(new { message = ex.Message }),
                    ProviderOnboardingErrorCode.WrongAccessStage     => Results.UnprocessableEntity(new { message = ex.Message }),
                    ProviderOnboardingErrorCode.TenantCodeUnavailable => Results.Conflict(new { message = ex.Message }),
                    ProviderOnboardingErrorCode.IdentityServiceFailed => Results.Problem(ex.Message, statusCode: 503),
                    _                                                 => Results.Problem(ex.Message),
                };
            }
        }).RequireAuthorization(); // JWT required — enforced by gateway + ASP.NET Core auth pipeline

        return app;
    }
}
