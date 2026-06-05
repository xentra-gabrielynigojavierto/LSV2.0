using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using CareConnect.Application.Authorization;
using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CareConnect.Api.Endpoints;

public static class AppointmentEndpoints
{
    public static void MapAppointmentEndpoints(this WebApplication app)
    {
        // LS-ID-TNT-012: appointment mutations gated on specific capability claims.
        // Filter-level (JWT) check runs before handler; handler also validates via IEffectivePermissionService.
        app.MapPost("/api/appointments", async (
            [FromBody] CreateAppointmentRequest request,
            IAppointmentService service,
            ICurrentRequestContext ctx,
            AuthorizationService authSvc,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            await CareConnectAuthHelper.RequireAsync(ctx, authSvc, PermissionCodes.AppointmentCreate, ct);
            var appointment = await service.CreateAppointmentAsync(tenantId, ctx.UserId, request, ct, actorName: ctx.Name ?? ctx.Email);
            return Results.Created($"/api/appointments/{appointment.Id}", appointment);
        })
        .RequireAuthorization(Policies.AuthenticatedUser)
        .RequireProductAccess(ProductCodes.SynqCareConnect)
        .RequireOrgProductAccess(ProductCodes.SynqCareConnect)
        .RequirePermission(PermissionCodes.AppointmentCreate);

        // LSCC-002: Org-participant scoping — mirrors referral list scoping:
        // receivers filter by receiving org; all others filter by referring org.
        // PlatformAdmin and TenantAdmin bypass filters and see all appointments in the tenant.
        app.MapGet("/api/appointments", async (
            [AsParameters] AppointmentSearchParams query,
            IAppointmentService service,
            ICurrentRequestContext ctx,
            AuthorizationService authSvc,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");

            var isReceiver = await authSvc.IsAuthorizedAsync(ctx, PermissionCodes.ReferralReadAddressed, ct);
            var (referringOrgId, receivingOrgId) =
                CareConnectParticipantHelper.GetAppointmentOrgScope(ctx, isReceiver);

            var result = await service.SearchAppointmentsAsync(
                tenantId, query, referringOrgId, receivingOrgId, ct);
            return Results.Ok(result);
        })
        .RequireAuthorization(Policies.AuthenticatedUser)
        .RequireProductAccess(ProductCodes.SynqCareConnect);

        // LSCC-002: Row-level access control — caller must be an admin or a participant
        // (ReferringOrganizationId or ReceivingOrganizationId matches their org).
        // Returns 404 (not 403) for non-participants to avoid confirming record existence.
        app.MapGet("/api/appointments/{id:guid}", async (
            Guid id,
            IAppointmentService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var appointment = await service.GetAppointmentByIdAsync(tenantId, id, ct);

            if (!CareConnectParticipantHelper.IsAdmin(ctx))
            {
                var isParticipant =
                    (ctx.OrgId.HasValue && appointment.ReferringOrganizationId == ctx.OrgId) ||
                    (ctx.OrgId.HasValue && appointment.ReceivingOrganizationId  == ctx.OrgId);

                if (!isParticipant)
                    return Results.NotFound();
            }

            return Results.Ok(appointment);
        })
        .RequireAuthorization(Policies.AuthenticatedUser)
        .RequireProductAccess(ProductCodes.SynqCareConnect);

        app.MapPut("/api/appointments/{id:guid}", async (
            Guid id,
            [FromBody] UpdateAppointmentRequest request,
            IAppointmentService service,
            ICurrentRequestContext ctx,
            AuthorizationService authSvc,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            await CareConnectAuthHelper.RequireAsync(ctx, authSvc, PermissionCodes.AppointmentUpdate, ct);
            var existing = await service.GetAppointmentByIdAsync(tenantId, id, ct);
            if (!RequireAppointmentParticipant(ctx, existing)) return Results.NotFound();
            var appointment = await service.UpdateAppointmentAsync(tenantId, id, ctx.UserId, request, ct);
            return Results.Ok(appointment);
        })
        .RequireAuthorization(Policies.AuthenticatedUser)
        .RequireProductAccess(ProductCodes.SynqCareConnect)
        .RequireOrgProductAccess(ProductCodes.SynqCareConnect)
        .RequirePermission(PermissionCodes.AppointmentUpdate);

        app.MapPost("/api/appointments/{id:guid}/confirm", async (
            Guid id,
            [FromBody] ConfirmAppointmentRequest request,
            IAppointmentService service,
            ICurrentRequestContext ctx,
            AuthorizationService authSvc,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            await CareConnectAuthHelper.RequireAsync(ctx, authSvc, PermissionCodes.AppointmentManage, ct);
            var existing = await service.GetAppointmentByIdAsync(tenantId, id, ct);
            if (!RequireAppointmentParticipant(ctx, existing)) return Results.NotFound();
            var appointment = await service.ConfirmAppointmentAsync(tenantId, id, ctx.UserId, request, ct);
            return Results.Ok(appointment);
        })
        .RequireAuthorization(Policies.AuthenticatedUser)
        .RequireProductAccess(ProductCodes.SynqCareConnect)
        .RequireOrgProductAccess(ProductCodes.SynqCareConnect)
        .RequirePermission(PermissionCodes.AppointmentManage);

        app.MapPost("/api/appointments/{id:guid}/complete", async (
            Guid id,
            [FromBody] CompleteAppointmentRequest request,
            IAppointmentService service,
            ICurrentRequestContext ctx,
            AuthorizationService authSvc,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            await CareConnectAuthHelper.RequireAsync(ctx, authSvc, PermissionCodes.AppointmentManage, ct);
            var existing = await service.GetAppointmentByIdAsync(tenantId, id, ct);
            if (!RequireAppointmentParticipant(ctx, existing)) return Results.NotFound();
            var appointment = await service.CompleteAppointmentAsync(tenantId, id, ctx.UserId, request, ct);
            return Results.Ok(appointment);
        })
        .RequireAuthorization(Policies.AuthenticatedUser)
        .RequireProductAccess(ProductCodes.SynqCareConnect)
        .RequireOrgProductAccess(ProductCodes.SynqCareConnect)
        .RequirePermission(PermissionCodes.AppointmentManage);

        app.MapPost("/api/appointments/{id:guid}/cancel", async (
            Guid id,
            [FromBody] CancelAppointmentRequest request,
            IAppointmentService service,
            ICurrentRequestContext ctx,
            AuthorizationService authSvc,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            await CareConnectAuthHelper.RequireAsync(ctx, authSvc, PermissionCodes.AppointmentManage, ct);
            var existing = await service.GetAppointmentByIdAsync(tenantId, id, ct);
            if (!RequireAppointmentParticipant(ctx, existing)) return Results.NotFound();
            var appointment = await service.CancelAppointmentAsync(tenantId, id, ctx.UserId, request, ct);
            return Results.Ok(appointment);
        })
        .RequireAuthorization(Policies.AuthenticatedUser)
        .RequireProductAccess(ProductCodes.SynqCareConnect)
        .RequireOrgProductAccess(ProductCodes.SynqCareConnect)
        .RequirePermission(PermissionCodes.AppointmentManage);

        app.MapPost("/api/appointments/{id:guid}/reschedule", async (
            Guid id,
            [FromBody] RescheduleAppointmentRequest request,
            IAppointmentService service,
            ICurrentRequestContext ctx,
            AuthorizationService authSvc,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            await CareConnectAuthHelper.RequireAsync(ctx, authSvc, PermissionCodes.AppointmentManage, ct);
            var existing = await service.GetAppointmentByIdAsync(tenantId, id, ct);
            if (!RequireAppointmentParticipant(ctx, existing)) return Results.NotFound();
            var appointment = await service.RescheduleAppointmentAsync(tenantId, id, ctx.UserId, request, ct);
            return Results.Ok(appointment);
        })
        .RequireAuthorization(Policies.AuthenticatedUser)
        .RequireProductAccess(ProductCodes.SynqCareConnect)
        .RequireOrgProductAccess(ProductCodes.SynqCareConnect)
        .RequirePermission(PermissionCodes.AppointmentManage);

        app.MapGet("/api/appointments/{id:guid}/history", async (
            Guid id,
            IAppointmentService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var existing = await service.GetAppointmentByIdAsync(tenantId, id, ct);
            if (!RequireAppointmentParticipant(ctx, existing)) return Results.NotFound();
            var history = await service.GetAppointmentHistoryAsync(tenantId, id, ct);
            return Results.Ok(history);
        })
        .RequireAuthorization(Policies.AuthenticatedUser)
        .RequireProductAccess(ProductCodes.SynqCareConnect);
    }

    private static bool RequireAppointmentParticipant(ICurrentRequestContext ctx, AppointmentResponse appointment)
    {
        if (CareConnectParticipantHelper.IsAdmin(ctx)) return true;
        return ctx.OrgId.HasValue
            && (appointment.ReferringOrganizationId == ctx.OrgId || appointment.ReceivingOrganizationId == ctx.OrgId);
    }
}
