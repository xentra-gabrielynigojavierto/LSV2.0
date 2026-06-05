using Support.Api.Auth;
using Support.Api.Domain;
using Support.Api.Services;
using Support.Api.Tenancy;
using Microsoft.AspNetCore.Mvc;

namespace Support.Api.Endpoints;

/// <summary>
/// Admin-only endpoints for managing tenant-scoped Support mode configuration.
///
/// Authorization:
///   GET  → SupportRead  (PlatformAdmin, SupportAdmin, SupportManager, SupportAgent, TenantAdmin, TenantUser)
///   PUT  → SupportManage (PlatformAdmin, SupportAdmin, SupportManager)
///
/// ExternalCustomer is intentionally excluded from all policies here — it does not appear in
/// SupportRoles.All or SupportRoles.Managers.
///
/// Tenant isolation: TenantId comes exclusively from ITenantContext (JWT-resolved).
/// The request body must not and does not supply tenantId.
/// </summary>
public static class TenantSettingsEndpoints
{
    public static void MapTenantSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var grp = app.MapGroup("/support/api/admin/tenant-settings")
            .WithTags("Tenant Settings");

        grp.MapGet("/", async (
            HttpContext ctx,
            ISupportTenantSettingsService svc,
            CancellationToken ct) =>
        {
            var tenantCtx = ctx.RequestServices.GetRequiredService<ITenantContext>();
            var tenantId  = tenantCtx.TenantId;
            if (string.IsNullOrWhiteSpace(tenantId))
                return Results.Problem(statusCode: 400, title: "Tenant context required");

            var settings = await svc.GetEffectiveSettingsAsync(tenantId, ct);
            return Results.Ok(settings);
        })
        .RequireAuthorization(SupportPolicies.SupportRead)
        .Produces<TenantSettingsResponse>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        grp.MapPut("/", async (
            [FromBody] UpdateTenantSettingsRequest req,
            HttpContext ctx,
            ISupportTenantSettingsService svc,
            CancellationToken ct) =>
        {
            var tenantCtx = ctx.RequestServices.GetRequiredService<ITenantContext>();
            var tenantId  = tenantCtx.TenantId;
            if (string.IsNullOrWhiteSpace(tenantId))
                return Results.Problem(statusCode: 400, title: "Tenant context required");

            if (!Enum.TryParse<SupportTenantMode>(req.SupportMode, ignoreCase: false, out var mode))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["supportMode"] = [$"Invalid supportMode value '{req.SupportMode}'. Valid values: InternalOnly, TenantCustomerSupport."]
                });
            }

            var actorId  = ctx.User.FindFirst("sub")?.Value;
            var settings = await svc.SetSupportModeAsync(tenantId, mode, req.CustomerPortalEnabled, actorId, ct);
            return Results.Ok(settings);
        })
        .RequireAuthorization(SupportPolicies.SupportManage)
        .Produces<TenantSettingsResponse>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .ProducesValidationProblem();
    }
}
