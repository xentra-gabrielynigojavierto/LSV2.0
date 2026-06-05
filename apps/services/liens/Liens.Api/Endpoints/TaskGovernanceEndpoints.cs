using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Domain;
using Liens.Domain.Enums;

namespace Liens.Api.Endpoints;

/// <summary>
/// LS-LIENS-FLOW-006 — Task Creation Governance endpoints.
/// Tenant path requires workflow:manage permission.
/// Admin path requires PlatformOrTenantAdmin policy.
/// </summary>
public static class TaskGovernanceEndpoints
{
    public static void MapTaskGovernanceEndpoints(this WebApplication app)
    {
        // ── Tenant endpoints ──────────────────────────────────────────────────
        var group = app.MapGroup("/api/liens/task-governance")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(LiensPermissions.ProductCode)
            .WithTags("TaskGovernance");

        group.MapGet("/", GetGovernanceSettings)
            .RequirePermission(LiensPermissions.WorkflowManage);

        group.MapPut("/", UpdateGovernanceSettings)
            .RequirePermission(LiensPermissions.WorkflowManage);

        // ── Admin / Control-Center endpoints ──────────────────────────────────
        var adminGroup = app.MapGroup("/api/liens/admin/task-governance")
            .RequireAuthorization(Policies.PlatformOrTenantAdmin)
            .WithTags("TaskGovernance");

        adminGroup.MapGet("/tenants/{tenantId:guid}", AdminGetGovernanceSettings);
        adminGroup.MapPut("/tenants/{tenantId:guid}", AdminUpdateGovernanceSettings);
    }

    private static Guid RequireTenantId(ICurrentRequestContext ctx) =>
        ctx.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required.");

    private static Guid RequireUserId(ICurrentRequestContext ctx) =>
        ctx.UserId ?? throw new UnauthorizedAccessException("User context is required.");

    // ── Tenant handlers ───────────────────────────────────────────────────────

    private static async Task<IResult> GetGovernanceSettings(
        ILienTaskGovernanceService service,
        ICurrentRequestContext ctx,
        CancellationToken ct)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);

        var result = await service.GetOrCreateAsync(
            tenantId, userId, WorkflowUpdateSources.TenantProductSettings, ct);

        return Results.Ok(result);
    }

    private static async Task<IResult> UpdateGovernanceSettings(
        ILienTaskGovernanceService service,
        ICurrentRequestContext ctx,
        UpdateTaskGovernanceSettingsRequest request,
        CancellationToken ct)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);

        var result = await service.UpdateAsync(tenantId, userId, request, ct);
        return Results.Ok(result);
    }

    // ── Admin handlers ────────────────────────────────────────────────────────

    private static async Task<IResult> AdminGetGovernanceSettings(
        Guid tenantId,
        ILienTaskGovernanceService service,
        ICurrentRequestContext ctx,
        CancellationToken ct)
    {
        var userId = RequireUserId(ctx);

        var result = await service.GetOrCreateAsync(
            tenantId, userId, WorkflowUpdateSources.ControlCenter, ct);

        return Results.Ok(result);
    }

    private static async Task<IResult> AdminUpdateGovernanceSettings(
        Guid tenantId,
        ILienTaskGovernanceService service,
        ICurrentRequestContext ctx,
        UpdateTaskGovernanceSettingsRequest request,
        CancellationToken ct)
    {
        var userId = RequireUserId(ctx);

        var result = await service.UpdateAsync(tenantId, userId, request, ct);
        return Results.Ok(result);
    }
}
