using BuildingBlocks.Authorization;
using BuildingBlocks.Exceptions;
using Tenant.Application.Interfaces;

namespace Tenant.Api.Endpoints;

/// <summary>
/// TENANT-B10: Logo management admin endpoints.
///
/// Switches the authoritative write path for tenant logo updates from the
/// Identity service to the Tenant service.  The Identity logo endpoints are
/// kept alive but return X-Deprecated response headers.
///
/// Routes (all require PlatformAdmin):
///   PATCH  /api/v1/admin/tenants/{id}/logo         — set primary logo
///   DELETE /api/v1/admin/tenants/{id}/logo         — clear primary logo
///   PATCH  /api/v1/admin/tenants/{id}/logo-white   — set white/reversed logo
///   DELETE /api/v1/admin/tenants/{id}/logo-white   — clear white/reversed logo
///
/// Each write endpoint:
///   1. Validates the tenant exists.
///   2. Calls Documents service to register / deregister the logo (non-fatal).
///   3. Updates only the relevant logo field on TenantBranding (no other fields
///      are touched — uses SetLogo / SetLogoWhite domain methods).
///   4. Evicts the public branding cache for this tenant.
/// </summary>
public static class LogoAdminEndpoints
{
    public static void MapLogoAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/tenants")
                       .RequireAuthorization(Policies.AdminOnly);

        // ── PATCH /api/v1/admin/tenants/{id}/logo ────────────────────────────

        group.MapPatch("/{id:guid}/logo", async (
            Guid               id,
            SetLogoRequest     body,
            HttpContext        httpContext,
            IBrandingService   brandingSvc,
            ITenantRepository  tenantRepo,
            IDocumentsAdapter  docsAdapter,
            CancellationToken  ct) =>
        {
            if (string.IsNullOrWhiteSpace(body.DocumentId))
                return Results.BadRequest(new { error = "documentId is required." });

            if (!Guid.TryParse(body.DocumentId, out var documentId))
                return Results.BadRequest(new { error = "documentId must be a valid UUID." });

            _ = await tenantRepo.GetByIdAsync(id, ct)
                ?? throw new NotFoundException($"Tenant '{id}' was not found.");

            var authHeader = httpContext.Request.Headers["Authorization"].FirstOrDefault();
            await docsAdapter.RegisterLogoAsync(documentId, id, authHeader, ct);

            var result = await brandingSvc.SetLogoAsync(id, documentId, ct);

            return Results.Ok(new
            {
                tenantId       = id,
                logoDocumentId = documentId,
                updatedAtUtc   = result.UpdatedAtUtc,
            });
        });

        // ── DELETE /api/v1/admin/tenants/{id}/logo ───────────────────────────

        group.MapDelete("/{id:guid}/logo", async (
            Guid              id,
            HttpContext        httpContext,
            IBrandingService  brandingSvc,
            ITenantRepository tenantRepo,
            IDocumentsAdapter docsAdapter,
            CancellationToken ct) =>
        {
            _ = await tenantRepo.GetByIdAsync(id, ct)
                ?? throw new NotFoundException($"Tenant '{id}' was not found.");

            var authHeader = httpContext.Request.Headers["Authorization"].FirstOrDefault();

            // Clear Documents logo-registration so /public/logo/{id} stops serving it.
            await docsAdapter.DeregisterLogoAsync(id, authHeader, ct);

            await brandingSvc.SetLogoAsync(id, documentId: null, ct);

            return Results.NoContent();
        });

        // ── PATCH /api/v1/admin/tenants/{id}/logo-white ──────────────────────

        group.MapPatch("/{id:guid}/logo-white", async (
            Guid               id,
            SetLogoRequest     body,
            HttpContext        httpContext,
            IBrandingService   brandingSvc,
            ITenantRepository  tenantRepo,
            IDocumentsAdapter  docsAdapter,
            CancellationToken  ct) =>
        {
            if (string.IsNullOrWhiteSpace(body.DocumentId))
                return Results.BadRequest(new { error = "documentId is required." });

            if (!Guid.TryParse(body.DocumentId, out var documentId))
                return Results.BadRequest(new { error = "documentId must be a valid UUID." });

            _ = await tenantRepo.GetByIdAsync(id, ct)
                ?? throw new NotFoundException($"Tenant '{id}' was not found.");

            var authHeader = httpContext.Request.Headers["Authorization"].FirstOrDefault();
            await docsAdapter.RegisterLogoAsync(documentId, id, authHeader, ct);

            var result = await brandingSvc.SetLogoWhiteAsync(id, documentId, ct);

            return Results.Ok(new
            {
                tenantId            = id,
                logoWhiteDocumentId = documentId,
                updatedAtUtc        = result.UpdatedAtUtc,
            });
        });

        // ── DELETE /api/v1/admin/tenants/{id}/logo-white ─────────────────────

        group.MapDelete("/{id:guid}/logo-white", async (
            Guid              id,
            IBrandingService  brandingSvc,
            ITenantRepository tenantRepo,
            CancellationToken ct) =>
        {
            _ = await tenantRepo.GetByIdAsync(id, ct)
                ?? throw new NotFoundException($"Tenant '{id}' was not found.");

            await brandingSvc.SetLogoWhiteAsync(id, documentId: null, ct);

            return Results.NoContent();
        });
    }

    // ── Shared request record ─────────────────────────────────────────────────

    private record SetLogoRequest(string? DocumentId);
}
