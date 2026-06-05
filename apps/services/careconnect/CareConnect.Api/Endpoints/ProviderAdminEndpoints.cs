// LSCC-002: Admin provider org-linkage backfill endpoint.
// LSCC-002-01: Extended with unlinked-list and bulk-link endpoints.
// LSCC-01-003: Extended with CareConnect receiver activation endpoint.
// BLK-OBS-01: Audit events added for activate-for-careconnect and link-organization.
// BLK-GOV-02: Uses AdminTenantScope helpers — fixes PlatformAdmin missing-tenantId 500
//             on link-organization, get-unlinked, and bulk-link-organization.
//             ActivateForCareConnect uses CheckOwnership for TenantAdmin boundary.
using BuildingBlocks.Authorization;
using BuildingBlocks.Context;
using CareConnect.Application.Interfaces;
using LegalSynq.AuditClient;
using LegalSynq.AuditClient.DTOs;
using LegalSynq.AuditClient.Enums;
using AuditVisibility = LegalSynq.AuditClient.Enums.VisibilityScope;
using Microsoft.AspNetCore.Mvc;

namespace CareConnect.Api.Endpoints;

/// <summary>
/// Admin-only endpoints for CareConnect provider management.
///
/// PUT  /api/admin/providers/{id}/link-organization           — single provider org-link (LSCC-002)
/// GET  /api/admin/providers/unlinked                         — list providers with no org link (LSCC-002-01)
/// POST /api/admin/providers/bulk-link-organization           — batch org-link from explicit mapping (LSCC-002-01)
/// POST /api/admin/providers/{id}/activate-for-careconnect   — idempotent CC receiver activation (LSCC-01-003)
///
/// All operations are explicit, idempotent, and require PlatformOrTenantAdmin.
/// PlatformAdmin must supply ?tenantId=&lt;guid&gt; for single-tenant operations (SingleTenant mode).
/// TenantAdmin is automatically scoped to their own tenant.
/// </summary>
public static class ProviderAdminEndpoints
{
    public static IEndpointRouteBuilder MapProviderAdminEndpoints(
        this IEndpointRouteBuilder routes)
    {
        routes
            .MapPut("/api/admin/providers/{id:guid}/link-organization", LinkOrganizationAsync)
            .RequireAuthorization(Policies.PlatformOrTenantAdmin);

        // LSCC-002-01: List all active providers that have no Identity OrganizationId set.
        routes
            .MapGet("/api/admin/providers/unlinked", GetUnlinkedAsync)
            .RequireAuthorization(Policies.PlatformOrTenantAdmin);

        // LSCC-002-01: Bulk-link providers to organizations from an explicit admin-supplied mapping.
        routes
            .MapPost("/api/admin/providers/bulk-link-organization", BulkLinkOrganizationAsync)
            .RequireAuthorization(Policies.PlatformOrTenantAdmin);

        // LSCC-01-003: Admin receiver provisioning — activate a specific provider for CareConnect.
        routes
            .MapPost("/api/admin/providers/{id:guid}/activate-for-careconnect", ActivateForCareConnectAsync)
            .RequireAuthorization(Policies.PlatformOrTenantAdmin);

        return routes;
    }

    // LSCC-002: Single provider org-link backfill — idempotent, explicit.
    // BLK-GOV-02: SingleTenant — PlatformAdmin must supply ?tenantId; TenantAdmin auto-scoped.
    // BLK-OBS-01: emits careconnect.provider.org-linked audit event.
    private static async Task<IResult> LinkOrganizationAsync(
        Guid                            id,
        [FromBody] LinkOrganizationRequest request,
        [FromQuery] Guid?               targetTenantId,
        IProviderService                service,
        ICurrentRequestContext          ctx,
        IAuditEventClient               auditClient,
        HttpContext                     http,
        CancellationToken               ct)
    {
        var scope = AdminTenantScope.SingleTenant(ctx, targetTenantId, http);
        if (scope.IsError) return scope.Error!;
        var tenantId = scope.TenantId!.Value;

        var result = await service.LinkOrganizationAsync(tenantId, id, request.OrganizationId, ct);

        // BLK-OBS-01: audit the admin org-link action.
        var correlationId = http.Items["CorrelationId"]?.ToString() ?? http.TraceIdentifier;
        _ = EmitAuditAsync(auditClient,
            eventType:     "careconnect.provider.org-linked",
            action:        "OrgLinked",
            description:   $"Provider '{id}' linked to organization '{request.OrganizationId}' by admin.",
            tenantId:      tenantId,
            actorUserId:   ctx.UserId,
            providerId:    id,
            correlationId: correlationId);

        return Results.Ok(result);
    }

    // LSCC-002-01: Returns all active providers that have no OrganizationId.
    // BLK-GOV-02: SingleTenant — PlatformAdmin must supply ?tenantId; TenantAdmin auto-scoped.
    // Response: 200 { providers: [...], count: N }
    private static async Task<IResult> GetUnlinkedAsync(
        IProviderService       service,
        ICurrentRequestContext ctx,
        [FromQuery] Guid?      targetTenantId,
        HttpContext            http,
        CancellationToken      ct)
    {
        var scope = AdminTenantScope.SingleTenant(ctx, targetTenantId, http);
        if (scope.IsError) return scope.Error!;
        var tenantId = scope.TenantId!.Value;

        var providers = await service.GetUnlinkedProvidersAsync(tenantId, ct);
        return Results.Ok(new { providers, count = providers.Count });
    }

    // LSCC-002-01: Bulk org-link from explicit mapping.
    // BLK-GOV-02: SingleTenant — PlatformAdmin must supply ?tenantId; TenantAdmin auto-scoped.
    // Body: { items: [{ providerId, organizationId }, ...] }
    // Response: 200 { total, updated, skipped, unresolved }
    private static async Task<IResult> BulkLinkOrganizationAsync(
        [FromBody] BulkLinkOrganizationRequest request,
        IProviderService       service,
        ICurrentRequestContext ctx,
        [FromQuery] Guid?      targetTenantId,
        HttpContext            http,
        CancellationToken      ct)
    {
        var scope = AdminTenantScope.SingleTenant(ctx, targetTenantId, http);
        if (scope.IsError) return scope.Error!;
        var tenantId = scope.TenantId!.Value;

        if (request.Items is null || request.Items.Count == 0)
            return Results.BadRequest("items must be a non-empty array.");

        var items = request.Items
            .Select(i => new ProviderOrgLinkItem(i.ProviderId, i.OrganizationId))
            .ToList();

        var report = await service.BulkLinkOrganizationAsync(tenantId, items, ct);
        return Results.Ok(report);
    }

    // LSCC-01-003: Activate provider IsActive + AcceptingReferrals = true (idempotent).
    // POST /api/admin/providers/{id}/activate-for-careconnect
    // BLK-GOV-02: CheckOwnership — PlatformAdmin may activate any; TenantAdmin only their own.
    // BLK-OBS-01: emits careconnect.provider.activated audit event.
    private static async Task<IResult> ActivateForCareConnectAsync(
        Guid                   id,
        IProviderService       service,
        ICurrentRequestContext ctx,
        IAuditEventClient      auditClient,
        HttpContext            http,
        CancellationToken      ct)
    {
        // BLK-GOV-02: For non-PlatformAdmin, verify provider belongs to caller's tenant.
        if (!ctx.IsPlatformAdmin)
        {
            var callerTenantId = ctx.TenantId
                ?? throw new InvalidOperationException("tenant_id claim is missing.");

            var provider = await service.GetByIdAsync(callerTenantId, id, ct);
            // GetByIdAsync throws NotFoundException if provider is not found in the tenant —
            // propagates as 404 through global error handler (correct; 403 would leak tenant info).
            _ = provider; // ownership confirmed by scoped lookup

            // Explicit cross-tenant ownership assertion via centralized guard.
            var deny = AdminTenantScope.CheckOwnership(ctx, provider.TenantId, http);
            if (deny is not null) return deny;
        }

        var result = await service.ActivateForCareConnectAsync(id, ct);

        // BLK-OBS-01: emit audit event for this admin activation.
        var tenantId      = ctx.TenantId;
        var correlationId = http.Items["CorrelationId"]?.ToString() ?? http.TraceIdentifier;

        if (tenantId.HasValue)
        {
            _ = EmitAuditAsync(auditClient,
                eventType:     "careconnect.provider.activated",
                action:        "ActivatedForCareConnect",
                description:   $"Provider '{id}' activated for CareConnect by admin. AlreadyActive={result.AlreadyActive}.",
                tenantId:      tenantId.Value,
                actorUserId:   ctx.UserId,
                providerId:    id,
                correlationId: correlationId);
        }

        return Results.Ok(new
        {
            providerId         = result.ProviderId,
            alreadyActive      = result.AlreadyActive,
            isActive           = result.IsActive,
            acceptingReferrals = result.AcceptingReferrals,
        });
    }

    // ── Shared audit helper ───────────────────────────────────────────────────

    private static Task EmitAuditAsync(
        IAuditEventClient auditClient,
        string            eventType,
        string            action,
        string            description,
        Guid              tenantId,
        Guid?             actorUserId,
        Guid              providerId,
        string            correlationId)
    {
        try
        {
            return auditClient.IngestAsync(new IngestAuditEventRequest
            {
                EventType     = eventType,
                EventCategory = EventCategory.Business,
                SourceSystem  = "care-connect",
                SourceService = "provider-admin",
                Visibility    = AuditVisibility.Tenant,
                Severity      = SeverityLevel.Info,
                OccurredAtUtc = DateTimeOffset.UtcNow,
                Scope = new AuditEventScopeDto
                {
                    ScopeType = ScopeType.Tenant,
                    TenantId  = tenantId.ToString(),
                },
                Actor = new AuditEventActorDto
                {
                    Type = actorUserId.HasValue ? ActorType.User : ActorType.System,
                    Id   = actorUserId?.ToString() ?? "system",
                    Name = "Admin",
                },
                Entity = new AuditEventEntityDto
                {
                    Type = "Provider",
                    Id   = providerId.ToString(),
                },
                Action        = action,
                Description   = description,
                Outcome       = "success",
                CorrelationId = correlationId,
                IdempotencyKey = IdempotencyKey.ForWithTimestamp(
                    DateTimeOffset.UtcNow, "care-connect", eventType, providerId.ToString()),
                Tags = ["admin", "provider", "activation"],
            });
        }
        catch
        {
            return Task.CompletedTask;
        }
    }
}

/// <summary>Request body for PUT /api/admin/providers/{id}/link-organization.</summary>
public sealed record LinkOrganizationRequest(Guid OrganizationId);

/// <summary>LSCC-002-01: Request body for POST /api/admin/providers/bulk-link-organization.</summary>
public sealed record BulkLinkOrganizationRequest(List<BulkLinkItemDto> Items);

/// <summary>LSCC-002-01: Single item in a bulk-link request.</summary>
public sealed record BulkLinkItemDto(Guid ProviderId, Guid OrganizationId);
