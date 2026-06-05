using System.Security.Claims;
using Identity.Application.Interfaces;

namespace Identity.Api.Endpoints;

public static class AccessSourceEndpoints
{
    public static IEndpointRouteBuilder MapAccessSourceEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/tenants/{tenantId:guid}/products", ListTenantProducts);
        routes.MapPut("/api/tenants/{tenantId:guid}/products/{productCode}", UpsertTenantProduct);
        routes.MapDelete("/api/tenants/{tenantId:guid}/products/{productCode}", DisableTenantProduct);

        routes.MapGet("/api/tenants/{tenantId:guid}/users/{userId:guid}/products", ListUserProducts);
        routes.MapPut("/api/tenants/{tenantId:guid}/users/{userId:guid}/products/{productCode}", GrantUserProduct);
        routes.MapDelete("/api/tenants/{tenantId:guid}/users/{userId:guid}/products/{productCode}", RevokeUserProduct);

        routes.MapGet("/api/tenants/{tenantId:guid}/users/{userId:guid}/roles", ListUserRoles);
        routes.MapPost("/api/tenants/{tenantId:guid}/users/{userId:guid}/roles", AssignUserRole);
        routes.MapDelete("/api/tenants/{tenantId:guid}/users/{userId:guid}/roles/{assignmentId:guid}", RemoveUserRole);

        routes.MapGet("/api/tenants/{tenantId:guid}/users/{userId:guid}/access-snapshot", GetAccessSnapshot);

        return routes;
    }

    private static Guid? GetActorUserId(HttpContext ctx)
    {
        var sub = ctx.User.FindFirstValue("sub") ?? ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    private static Guid? GetActorTenantId(HttpContext ctx)
    {
        var tid = ctx.User.FindFirstValue("tenantId") ?? ctx.User.FindFirstValue("tenant_id");
        return Guid.TryParse(tid, out var id) ? id : null;
    }

    private static bool IsPlatformAdmin(HttpContext ctx)
    {
        return ctx.User.IsInRole("PlatformAdmin") || ctx.User.IsInRole("SuperAdmin");
    }

    private static bool IsAdmin(HttpContext ctx)
    {
        return IsPlatformAdmin(ctx) || ctx.User.IsInRole("TenantAdmin");
    }

    private static bool CanReadTenant(HttpContext ctx, Guid tenantId)
    {
        if (IsPlatformAdmin(ctx)) return true;
        var actorTenantId = GetActorTenantId(ctx);
        return actorTenantId == tenantId;
    }

    private static bool CanMutateTenant(HttpContext ctx, Guid tenantId)
    {
        if (IsPlatformAdmin(ctx)) return true;
        if (!ctx.User.IsInRole("TenantAdmin")) return false;
        var actorTenantId = GetActorTenantId(ctx);
        return actorTenantId == tenantId;
    }

    private static async Task<IResult> ListTenantProducts(
        Guid tenantId,
        ITenantProductEntitlementService svc,
        HttpContext ctx)
    {
        if (!CanReadTenant(ctx, tenantId))
            return Results.Forbid();

        var items = await svc.GetByTenantAsync(tenantId);
        return Results.Ok(items.Select(e => new
        {
            e.Id,
            e.TenantId,
            e.ProductCode,
            Status = e.Status.ToString(),
            e.EnabledAtUtc,
            e.DisabledAtUtc,
            e.CreatedAtUtc,
            e.UpdatedAtUtc
        }));
    }

    private static async Task<IResult> UpsertTenantProduct(
        Guid tenantId,
        string productCode,
        ITenantProductEntitlementService svc,
        HttpContext ctx)
    {
        if (!CanMutateTenant(ctx, tenantId))
            return Results.Forbid();

        try
        {
            var result = await svc.UpsertAsync(tenantId, productCode, GetActorUserId(ctx));
            return Results.Ok(new
            {
                result.Id,
                result.TenantId,
                result.ProductCode,
                Status = result.Status.ToString(),
                result.EnabledAtUtc,
                result.CreatedAtUtc
            });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> DisableTenantProduct(
        Guid tenantId,
        string productCode,
        ITenantProductEntitlementService svc,
        HttpContext ctx)
    {
        if (!CanMutateTenant(ctx, tenantId))
            return Results.Forbid();

        var removed = await svc.DisableAsync(tenantId, productCode, GetActorUserId(ctx));
        return removed ? Results.NoContent() : Results.NotFound();
    }

    private static async Task<IResult> ListUserProducts(
        Guid tenantId,
        Guid userId,
        IUserProductAccessService svc,
        HttpContext ctx)
    {
        if (!CanReadTenant(ctx, tenantId))
            return Results.Forbid();

        var items = await svc.GetByTenantUserAsync(tenantId, userId);
        return Results.Ok(items.Select(a => new
        {
            a.Id,
            a.TenantId,
            a.UserId,
            a.ProductCode,
            AccessStatus = a.AccessStatus.ToString(),
            a.SourceType,
            a.GrantedAtUtc,
            a.RevokedAtUtc,
            a.CreatedAtUtc,
            a.UpdatedAtUtc
        }));
    }

    private static async Task<IResult> GrantUserProduct(
        Guid tenantId,
        Guid userId,
        string productCode,
        IUserProductAccessService svc,
        HttpContext ctx)
    {
        if (!CanMutateTenant(ctx, tenantId))
            return Results.Forbid();

        try
        {
            var result = await svc.GrantAsync(tenantId, userId, productCode, GetActorUserId(ctx));
            return Results.Ok(new
            {
                result.Id,
                result.TenantId,
                result.UserId,
                result.ProductCode,
                AccessStatus = result.AccessStatus.ToString(),
                result.GrantedAtUtc
            });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> RevokeUserProduct(
        Guid tenantId,
        Guid userId,
        string productCode,
        IUserProductAccessService svc,
        HttpContext ctx)
    {
        if (!CanMutateTenant(ctx, tenantId))
            return Results.Forbid();

        var revoked = await svc.RevokeAsync(tenantId, userId, productCode, GetActorUserId(ctx));
        return revoked ? Results.NoContent() : Results.NotFound();
    }

    private static async Task<IResult> ListUserRoles(
        Guid tenantId,
        Guid userId,
        IUserRoleAssignmentService svc,
        HttpContext ctx)
    {
        if (!CanReadTenant(ctx, tenantId))
            return Results.Forbid();

        var items = await svc.GetByTenantUserAsync(tenantId, userId);
        return Results.Ok(items.Select(a => new
        {
            a.Id,
            a.TenantId,
            a.UserId,
            a.RoleCode,
            a.ProductCode,
            a.OrganizationId,
            AssignmentStatus = a.AssignmentStatus.ToString(),
            a.SourceType,
            a.AssignedAtUtc,
            a.RemovedAtUtc,
            a.CreatedAtUtc,
            a.UpdatedAtUtc
        }));
    }

    private static async Task<IResult> AssignUserRole(
        Guid tenantId,
        Guid userId,
        AssignRoleRequest body,
        IUserRoleAssignmentService svc,
        HttpContext ctx)
    {
        if (!CanMutateTenant(ctx, tenantId))
            return Results.Forbid();

        if (string.IsNullOrWhiteSpace(body.RoleCode))
            return Results.BadRequest(new { error = "RoleCode is required." });

        try
        {
            var result = await svc.AssignAsync(
                tenantId, userId, body.RoleCode, body.ProductCode, body.OrganizationId,
                GetActorUserId(ctx));
            return Results.Created($"/api/tenants/{tenantId}/users/{userId}/roles/{result.Id}", new
            {
                result.Id,
                result.TenantId,
                result.UserId,
                result.RoleCode,
                result.ProductCode,
                result.OrganizationId,
                AssignmentStatus = result.AssignmentStatus.ToString(),
                result.AssignedAtUtc
            });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> RemoveUserRole(
        Guid tenantId,
        Guid userId,
        Guid assignmentId,
        IUserRoleAssignmentService svc,
        HttpContext ctx)
    {
        if (!CanMutateTenant(ctx, tenantId))
            return Results.Forbid();

        var removed = await svc.RemoveAsync(tenantId, userId, assignmentId, GetActorUserId(ctx));
        return removed ? Results.NoContent() : Results.NotFound();
    }

    private static async Task<IResult> GetAccessSnapshot(
        Guid tenantId,
        Guid userId,
        IAccessSourceQueryService svc,
        HttpContext ctx)
    {
        if (!CanReadTenant(ctx, tenantId))
            return Results.Forbid();

        var snapshot = await svc.GetSnapshotAsync(tenantId, userId);
        return Results.Ok(new
        {
            TenantProducts = snapshot.TenantProducts.Select(e => new
            {
                e.Id, e.ProductCode, Status = e.Status.ToString(), e.EnabledAtUtc, e.DisabledAtUtc
            }),
            UserProducts = snapshot.UserProducts.Select(a => new
            {
                a.Id, a.ProductCode, AccessStatus = a.AccessStatus.ToString(), a.GrantedAtUtc, a.RevokedAtUtc
            }),
            UserRoles = snapshot.UserRoles.Select(a => new
            {
                a.Id, a.RoleCode, a.ProductCode, a.OrganizationId,
                AssignmentStatus = a.AssignmentStatus.ToString(), a.AssignedAtUtc, a.RemovedAtUtc
            })
        });
    }
}

public record AssignRoleRequest(string RoleCode, string? ProductCode = null, Guid? OrganizationId = null);
