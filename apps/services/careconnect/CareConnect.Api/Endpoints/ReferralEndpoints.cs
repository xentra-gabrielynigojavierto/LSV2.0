using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using BuildingBlocks.Exceptions;
using CareConnect.Application.Authorization;
using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;
using CareConnect.Domain;
using Microsoft.AspNetCore.Mvc;

namespace CareConnect.Api.Endpoints;

public static class ReferralEndpoints
{
    public static void MapReferralEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/referrals");

        group.MapGet("/", async (
            [AsParameters] ReferralSearchParams p,
            IReferralService service,
            ICurrentRequestContext ctx,
            AuthorizationService authSvc,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");

            var isProviderOrg = string.Equals(ctx.OrgType, "PROVIDER", StringComparison.OrdinalIgnoreCase);
            var isAdmin = ctx.IsPlatformAdmin || ctx.Roles.Contains(Roles.TenantAdmin);

            // BLK-PERF-01: Clamp page size to protect against unbounded result-set queries.
            // Default 20; maximum 100 — callers requesting more receive exactly 100.
            var query = new GetReferralsQuery
            {
                Status             = p.Status,
                ProviderId         = p.ProviderId,
                ClientName         = p.ClientName,
                CaseNumber         = p.CaseNumber,
                Urgency            = p.Urgency,
                CreatedFrom        = p.CreatedFrom,
                CreatedTo          = p.CreatedTo,
                Page               = Math.Max(1, p.Page ?? 1),
                PageSize           = Math.Clamp(p.PageSize ?? 20, 1, 100),
            };

            if (ctx.IsPlatformAdmin)
            {
                // PlatformAdmin: all referrals in the tenant, no org scoping
            }
            else if (isProviderOrg)
            {
                query.CrossTenantReceiver = true;
                query.ReceivingOrgId      = ctx.OrgId;
            }
            else if (isAdmin)
            {
                // TenantAdmin on a referrer org: all referrals in the tenant
            }
            else
            {
                query.ReferringOrgId = ctx.OrgId;
                // CC-REFERRER-EMAIL: also surface public referrals submitted before the
                // law firm activated their portal (those have ReferrerEmail set but no
                // ReferringOrganizationId).
                query.ReferrerEmail = ctx.Email;
            }

            var result = await service.SearchAsync(tenantId, query, ct);
            return Results.Ok(result);
        })
        .RequireAuthorization(Policies.AuthenticatedUser)
        .RequireProductAccess(ProductCodes.SynqCareConnect);

        // LSCC-01-002-02: Returns the explicit readiness result for the current caller
        // as a provider in the CareConnect receiver path.
        // Checks whether the caller's JWT product roles satisfy the full receiver-ready bundle:
        //   CareConnectReceiver role + ReferralReadAddressed + ReferralAccept capabilities.
        // Read-only — no side effects, no provisioning, no role assignment.
        // LSCC-01-004: On IsProvisioned=false, fires a best-effort blocked-access log entry
        //   for admin operational visibility. Log failure never blocks the response.
        // NOTE: must be registered before /{id:guid} to avoid the GUID route capturing "access-readiness".
        group.MapGet("/access-readiness", async (
            IProviderAccessReadinessService readinessSvc,
            IBlockedAccessLogService        blockedLogSvc,
            ICurrentRequestContext          ctx,
            CancellationToken               ct) =>
        {
            // PlatformAdmin and TenantAdmin are always considered access-ready —
            // they bypass capability checks throughout the platform.
            if (ctx.IsPlatformAdmin || ctx.Roles.Contains(Roles.TenantAdmin, StringComparer.OrdinalIgnoreCase))
            {
                return Results.Ok(new ProviderAccessReadinessResult
                {
                    IsProvisioned     = true,
                    HasReceiverRole   = true,
                    HasReferralAccess = true,
                    HasReferralAccept = true,
                });
            }

            var result = await readinessSvc.GetReadinessAsync(ctx.ProductRoles, ct);

            // LSCC-01-004: Best-effort log — fire and observe. Must never block the response.
            if (!result.IsProvisioned)
            {
                _ = blockedLogSvc.LogAsync(
                    tenantId:       ctx.TenantId,
                    userId:         ctx.UserId,
                    userEmail:      ctx.Email,
                    organizationId: ctx.OrgId,
                    providerId:     null,
                    referralId:     null,
                    failureReason:  result.Reason ?? "unknown",
                    ct:             CancellationToken.None);
            }

            return Results.Ok(result);
        })
        .RequireAuthorization(Policies.AuthenticatedUser)
        .RequireProductAccess(ProductCodes.SynqCareConnect);

        // LSCC-002: Row-level access control — caller must be an admin or a participant
        // (ReferringOrganizationId or ReceivingOrganizationId matches their org).
        // CC-REFERRER-EMAIL: also grants access when the referral was submitted publicly
        // (no ReferringOrganizationId) but the caller's email matches ReferrerEmail.
        // Returns 404 (not 403) for non-participants to avoid confirming record existence.
        group.MapGet("/{id:guid}", async (
            Guid id,
            IReferralService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var isProviderOrg = string.Equals(ctx.OrgType, "PROVIDER", StringComparison.OrdinalIgnoreCase);
            var globalLookup = ctx.IsPlatformAdmin || isProviderOrg;
            var referral = await service.GetByIdAsync(tenantId, id, ct, isPlatformAdmin: globalLookup);

            if (!ctx.IsPlatformAdmin)
            {
                var isParticipant =
                    (ctx.OrgId.HasValue && referral.ReferringOrganizationId == ctx.OrgId) ||
                    (ctx.OrgId.HasValue && referral.ReceivingOrganizationId  == ctx.OrgId) ||
                    // CC-REFERRER-EMAIL: public referral matched by email
                    (!string.IsNullOrWhiteSpace(ctx.Email) &&
                     referral.ReferringOrganizationId == null &&
                     string.Equals(referral.ReferrerEmail, ctx.Email, StringComparison.OrdinalIgnoreCase));

                if (!isParticipant)
                    return Results.NotFound();
            }

            if (isProviderOrg && ctx.OrgId.HasValue && referral.ReceivingOrganizationId == ctx.OrgId
                && referral.Status == "New")
            {
                try { await service.MarkAsOpenedAsync(id, ct); }
                catch { }
            }

            return Results.Ok(referral);
        })
        .RequireAuthorization(Policies.AuthenticatedUser)
        .RequireProductAccess(ProductCodes.SynqCareConnect);

        group.MapGet("/{id:guid}/history", async (
            Guid id,
            IReferralService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var isProviderOrg = string.Equals(ctx.OrgType, "PROVIDER", StringComparison.OrdinalIgnoreCase);
            var globalLookup = ctx.IsPlatformAdmin || isProviderOrg;

            // Participant check — mirrors GET /{id:guid} to prevent cross-tenant data access.
            var referral = await service.GetByIdAsync(tenantId, id, ct, isPlatformAdmin: globalLookup);
            if (!ctx.IsPlatformAdmin)
            {
                var isParticipant =
                    (ctx.OrgId.HasValue && referral.ReferringOrganizationId == ctx.OrgId) ||
                    (ctx.OrgId.HasValue && referral.ReceivingOrganizationId  == ctx.OrgId) ||
                    // CC-REFERRER-EMAIL: public referral matched by email
                    (!string.IsNullOrWhiteSpace(ctx.Email) &&
                     referral.ReferringOrganizationId == null &&
                     string.Equals(referral.ReferrerEmail, ctx.Email, StringComparison.OrdinalIgnoreCase));
                if (!isParticipant)
                    return Results.NotFound();
            }

            var history = await service.GetHistoryAsync(tenantId, id, ct, isPlatformAdmin: globalLookup);
            return Results.Ok(history);
        })
        .RequireAuthorization(Policies.AuthenticatedUser)
        .RequireProductAccess(ProductCodes.SynqCareConnect);

        // LS-ID-TNT-012: filter-level JWT permission check; handler also validates via IEffectivePermissionService.
        group.MapPost("/", async (
            [FromBody] CreateReferralRequest request,
            IReferralService service,
            ICurrentRequestContext ctx,
            AuthorizationService authSvc,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            await CareConnectAuthHelper.RequireAsync(ctx, authSvc, PermissionCodes.ReferralCreate, ct);
            request.ReferringOrganizationId = ctx.OrgId;
            var referral = await service.CreateAsync(tenantId, ctx.UserId, request, ct, actorName: ctx.Name ?? ctx.Email);
            return Results.Created($"/api/referrals/{referral.Id}", referral);
        })
        .RequireAuthorization(Policies.AuthenticatedUser)
        .RequireProductAccess(ProductCodes.SynqCareConnect)
        .RequireOrgProductAccess(ProductCodes.SynqCareConnect)
        .RequirePermission(PermissionCodes.ReferralCreate);

        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdateReferralRequest request,
            IReferralService service,
            ICurrentRequestContext ctx,
            AuthorizationService authSvc,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");

            var requiredPermission = ReferralWorkflowRules.RequiredPermissionFor(request.Status);
            await CareConnectAuthHelper.RequireAsync(ctx, authSvc, requiredPermission, ct);

            var isProviderOrg = string.Equals(ctx.OrgType, "PROVIDER", StringComparison.OrdinalIgnoreCase);
            var bypassTenant = ctx.IsPlatformAdmin || isProviderOrg;

            // Participant check — verify caller is a participant before mutating the referral.
            // Returns 404 (not 403) to avoid confirming record existence across tenants.
            var existing = await service.GetByIdAsync(tenantId, id, ct, isPlatformAdmin: bypassTenant);
            if (!ctx.IsPlatformAdmin)
            {
                var isParticipant =
                    (ctx.OrgId.HasValue && existing.ReferringOrganizationId == ctx.OrgId) ||
                    (ctx.OrgId.HasValue && existing.ReceivingOrganizationId  == ctx.OrgId) ||
                    // CC-REFERRER-EMAIL: public referral matched by email
                    (!string.IsNullOrWhiteSpace(ctx.Email) &&
                     existing.ReferringOrganizationId == null &&
                     string.Equals(existing.ReferrerEmail, ctx.Email, StringComparison.OrdinalIgnoreCase));
                if (!isParticipant)
                    return Results.NotFound();
            }

            var referral = await service.UpdateAsync(tenantId, id, ctx.UserId, request, ct, bypassTenantScope: bypassTenant, actorName: ctx.Name ?? ctx.Email);
            return Results.Ok(referral);
        })
        .RequireAuthorization(Policies.AuthenticatedUser)
        .RequireProductAccess(ProductCodes.SynqCareConnect)
        .RequireOrgProductAccess(ProductCodes.SynqCareConnect);

        // ── LSCC-005-01: Hardening endpoints (authenticated) ────────────────────

        // GET /api/referrals/{id}/notifications — email delivery history for a referral
        group.MapGet("/{id:guid}/notifications", async (
            Guid id,
            IReferralService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var isProviderOrg = string.Equals(ctx.OrgType, "PROVIDER", StringComparison.OrdinalIgnoreCase);
            var globalLookup = ctx.IsPlatformAdmin || isProviderOrg;

            // Participant check — mirrors GET /{id:guid} to prevent cross-tenant data access.
            var referral = await service.GetByIdAsync(tenantId, id, ct, isPlatformAdmin: globalLookup);
            if (!ctx.IsPlatformAdmin)
            {
                var isParticipant =
                    (ctx.OrgId.HasValue && referral.ReferringOrganizationId == ctx.OrgId) ||
                    (ctx.OrgId.HasValue && referral.ReceivingOrganizationId  == ctx.OrgId) ||
                    // CC-REFERRER-EMAIL: public referral matched by email
                    (!string.IsNullOrWhiteSpace(ctx.Email) &&
                     referral.ReferringOrganizationId == null &&
                     string.Equals(referral.ReferrerEmail, ctx.Email, StringComparison.OrdinalIgnoreCase));
                if (!isParticipant)
                    return Results.NotFound();
            }

            var notifs = await service.GetNotificationsAsync(tenantId, id, ct, isPlatformAdmin: globalLookup);
            return Results.Ok(notifs);
        })
        .RequireAuthorization(Policies.AuthenticatedUser)
        .RequireProductAccess(ProductCodes.SynqCareConnect);

        // POST /api/referrals/{id}/resend-email — resend provider notification email
        // Only available while referral is in New status.
        group.MapPost("/{id:guid}/resend-email", async (
            Guid id,
            IReferralService service,
            ICurrentRequestContext ctx,
            AuthorizationService authSvc,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            await CareConnectAuthHelper.RequireAsync(ctx, authSvc, PermissionCodes.ReferralCreate, ct);

            try
            {
                // LSCC-01-005-01 (DEF-002)
                var referral = await service.ResendEmailAsync(tenantId, id, ct, isPlatformAdmin: ctx.IsPlatformAdmin);
                return Results.Ok(referral);
            }
            catch (NotFoundException)
            {
                return Results.NotFound();
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        })
        .RequireAuthorization(Policies.AuthenticatedUser)
        .RequireProductAccess(ProductCodes.SynqCareConnect)
        .RequireOrgProductAccess(ProductCodes.SynqCareConnect)
        .RequirePermission(PermissionCodes.ReferralCreate);

        // POST /api/referrals/{id}/reassign-provider — manually reassign the referral to a new provider.
        // Restricted to platform admins and tenant admins (enforced by PlatformOrTenantAdmin policy).
        // Fires a PROVIDER_ASSIGNED notification to the incoming provider and revokes old view tokens.
        group.MapPost("/{id:guid}/reassign-provider", async (
            Guid id,
            [FromBody] ReassignProviderRequest request,
            IReferralService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");

            if (request.NewProviderId == Guid.Empty)
                return Results.BadRequest(new { error = "newProviderId is required." });

            try
            {
                var referral = await service.ReassignProviderAsync(tenantId, id, request.NewProviderId, ctx.UserId, ctx.IsPlatformAdmin, ct);
                return Results.Ok(referral);
            }
            catch (NotFoundException)
            {
                return Results.NotFound();
            }
        })
        .RequireAuthorization(Policies.PlatformOrTenantAdmin)
        .RequireProductAccess(ProductCodes.SynqCareConnect);

        // POST /api/referrals/{id}/revoke-token — invalidate all previously issued view tokens
        group.MapPost("/{id:guid}/revoke-token", async (
            Guid id,
            IReferralService service,
            ICurrentRequestContext ctx,
            AuthorizationService authSvc,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            await CareConnectAuthHelper.RequireAsync(ctx, authSvc, PermissionCodes.ReferralCreate, ct);

            try
            {
                var referral = await service.RevokeTokenAsync(tenantId, id, ct);
                return Results.Ok(referral);
            }
            catch (NotFoundException)
            {
                return Results.NotFound();
            }
        })
        .RequireAuthorization(Policies.AuthenticatedUser)
        .RequireProductAccess(ProductCodes.SynqCareConnect)
        .RequireOrgProductAccess(ProductCodes.SynqCareConnect)
        .RequirePermission(PermissionCodes.ReferralCreate);

        // GET /api/referrals/{id}/audit — operational audit timeline (LSCC-005-02)
        // Returns status-history + notification events merged and sorted chronologically.
        group.MapGet("/{id:guid}/audit", async (
            Guid id,
            IReferralService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            try
            {
                // LSCC-01-005-01 (DEF-002)
                var timeline = await service.GetAuditTimelineAsync(tenantId, id, ct, isPlatformAdmin: ctx.IsPlatformAdmin);
                return Results.Ok(timeline);
            }
            catch (NotFoundException)
            {
                return Results.NotFound();
            }
        })
        .RequireAuthorization(Policies.AuthenticatedUser)
        .RequireProductAccess(ProductCodes.SynqCareConnect);

        // ── LSCC-005: Public token-based endpoints (no auth required) ──────────

        // Resolves a view token to determine how to route the provider:
        //   "pending"  → LSCC-01-002-01: route to login + returnTo=/careconnect/referrals/{id}
        //   "active"   → route to login + returnTo=/careconnect/referrals/{id}
        //   "invalid"  → token is bad or expired
        //   "notfound" → referral was deleted
        group.MapGet("/resolve-view-token", async (
            [FromQuery] string token,
            IReferralService service,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(token))
                return Results.BadRequest(new { error = "token is required." });

            var result = await service.ResolveViewTokenAsync(token, ct);
            return Results.Ok(result);
        });
        // Note: no .RequireAuthorization — intentionally public

        // ── LSCC-008: Provider activation funnel (public, token-gated) ──────────

        // GET /api/referrals/{id}/public-summary?token=...
        // Returns limited referral context for the activation landing page.
        // Token is HMAC-validated + version-checked before any data is returned.
        group.MapGet("/{id:guid}/public-summary", async (
            Guid id,
            [FromQuery] string token,
            IReferralService service,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(token))
                return Results.BadRequest(new { error = "token is required." });

            var summary = await service.GetPublicSummaryAsync(id, token, ct);
            if (summary is null)
                return Results.Unauthorized();

            return Results.Ok(summary);
        });
        // Note: no .RequireAuthorization — intentionally public, token-gated

        // POST /api/referrals/{id}/track-funnel
        // Body: { token, eventType }
        // Records a provider funnel event (ReferralViewed | ActivationStarted).
        group.MapPost("/{id:guid}/track-funnel", async (
            Guid id,
            [FromBody] TrackFunnelEventRequest request,
            IReferralService service,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Token))
                return Results.BadRequest(new { error = "token is required." });

            if (string.IsNullOrWhiteSpace(request.EventType))
                return Results.BadRequest(new { error = "eventType is required." });

            var ok = await service.TrackFunnelEventAsync(
                id, request.Token, request.EventType,
                request.RequesterName, request.RequesterEmail, ct);
            return ok ? Results.Ok() : Results.BadRequest(new { error = "Invalid token or unrecognised event type." });
        });
        // Note: no .RequireAuthorization — intentionally public, token-gated

        // POST /api/referrals/{id}/auto-provision
        // LSCC-010: Attempts instant provider activation.
        // On success:  provider is linked to an Identity org, returns loginUrl.
        // On fallback: upserts LSCC-009 activation request for admin review.
        // Public, token-gated (same HMAC-validated approach as track-funnel).
        group.MapPost("/{id:guid}/auto-provision", async (
            Guid id,
            [FromBody] AutoProvisionRequest request,
            IAutoProvisionService provisioner,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Token))
                return Results.BadRequest(new { error = "token is required." });

            var result = await provisioner.ProvisionAsync(
                id, request.Token, request.RequesterName, request.RequesterEmail, ct);

            return Results.Ok(result);
        });
        // Note: no .RequireAuthorization — intentionally public, token-gated

        // GET /api/referrals/{id}/public-attachments/{attachmentId}/url?token=...&download=false
        // Returns a short-lived signed URL for a shared-scope attachment.
        // Token-gated: the same HMAC view token used by the activation landing page.
        // Allows unauthenticated providers to download documents submitted with their referral.
        group.MapGet("/{id:guid}/public-attachments/{attachmentId:guid}/url", async (
            Guid                      id,
            Guid                      attachmentId,
            [FromQuery] string        token,
            [FromQuery] bool          download,
            IReferralService          service,
            IReferralAttachmentService attachmentSvc,
            CancellationToken         ct) =>
        {
            if (string.IsNullOrWhiteSpace(token))
                return Results.BadRequest(new { error = "token is required." });

            // Reuse GetPublicSummaryAsync to validate the token — null means invalid/expired.
            var summary = await service.GetPublicSummaryAsync(id, token, ct);
            if (summary is null)
                return Results.Unauthorized();

            try
            {
                // isAdmin=true bypasses scope enforcement — safe because token is already HMAC-validated.
                var result = await attachmentSvc.GetSignedUrlAsync(
                    summary.TenantId,
                    id,
                    attachmentId,
                    callerOrgId:   null,
                    callerOrgType: null,
                    isAdmin:       true,
                    isDownload:    download,
                    ct:            ct);

                if (result is null)
                    return Results.Problem("The document is not currently accessible.", statusCode: 503);

                return Results.Ok(result);
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Forbid();
            }
        });
        // Note: no .RequireAuthorization — token-gated, intentionally public

        // POST /api/referrals/{id}/accept-by-token
        // Public, HMAC view-token gated. Accepts the referral directly from the provider thread page.
        group.MapPost("/{id:guid}/accept-by-token", async (
            Guid id,
            [FromBody] AcceptByTokenRequest request,
            IReferralService referralService,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Token))
                return Results.BadRequest(new { error = "token is required." });
            try
            {
                var result = await referralService.AcceptByTokenAsync(id, request.Token, ct);
                return Results.Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status401Unauthorized, title: "Unauthorized");
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict, title: "Conflict");
            }
        });
        // Note: no .RequireAuthorization — intentionally public, token-gated

        // POST /api/referrals/{id}/decline-by-token
        // Public, HMAC view-token gated. Declines the referral directly from the provider thread page.
        group.MapPost("/{id:guid}/decline-by-token", async (
            Guid id,
            [FromBody] AcceptByTokenRequest request,
            IReferralService referralService,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Token))
                return Results.BadRequest(new { error = "token is required." });
            try
            {
                var result = await referralService.DeclineByTokenAsync(id, request.Token, ct);
                return Results.Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status401Unauthorized, title: "Unauthorized");
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict, title: "Conflict");
            }
        });
        // Note: no .RequireAuthorization — intentionally public, token-gated
    }
}

internal sealed class ReferralSearchParams
{
    public string?   Status      { get; init; }
    public Guid?     ProviderId  { get; init; }
    public string?   ClientName  { get; init; }
    public string?   CaseNumber  { get; init; }
    public string?   Urgency     { get; init; }
    public DateTime? CreatedFrom { get; init; }
    public DateTime? CreatedTo   { get; init; }
    public int?      Page        { get; init; }
    public int?      PageSize    { get; init; }
}

internal sealed class ReassignProviderRequest
{
    public Guid NewProviderId { get; init; }
}
