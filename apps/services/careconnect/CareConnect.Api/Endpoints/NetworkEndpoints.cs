using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using CareConnect.Application.Cache;
using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;
using CareConnect.Infrastructure.Data;
using LegalSynq.AuditClient;
using LegalSynq.AuditClient.DTOs;
using LegalSynq.AuditClient.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using AuditVisibility = LegalSynq.AuditClient.Enums.VisibilityScope;

namespace CareConnect.Api.Endpoints;

// CC2-INT-B06 / CC2-INT-B06-01 — provider network management + shared provider registry.
// Access: CARECONNECT_NETWORK_MANAGER product role, or PlatformAdmin / TenantAdmin bypass.
public static class NetworkEndpoints
{
    public static void MapNetworkEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/networks")
            .RequireAuthorization(Policies.AuthenticatedUser);

        // ── List networks ──────────────────────────────────────────────────────
        group.MapGet("/", async (
            INetworkService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var networks = await service.GetAllAsync(tenantId, ct);
            return Results.Ok(networks);
        })
        .RequireProductRole(ProductCodes.SynqCareConnect, ProductRoleCodes.CareConnectNetworkManager);

        // ── Create network ─────────────────────────────────────────────────────
        group.MapPost("/", async (
            [FromBody] CreateNetworkRequest request,
            INetworkService service,
            ICurrentRequestContext ctx,
            IAuditEventClient auditClient,
            HttpContext http,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var network = await service.CreateAsync(tenantId, ctx.UserId, request, ct);
            // BLK-COMP-01: Audit network creation — every network lifecycle event is traceable.
            var correlationId = http.Items["CorrelationId"]?.ToString() ?? http.TraceIdentifier;
            _ = EmitNetworkAuditAsync(auditClient,
                eventType:     "careconnect.network.created",
                action:        "NetworkCreated",
                description:   $"Network '{network.Name}' created by user.",
                tenantId:      tenantId,
                actorUserId:   ctx.UserId,
                networkId:     network.Id,
                correlationId: correlationId);
            return Results.Created($"/api/networks/{network.Id}", network);
        })
        .RequireProductRole(ProductCodes.SynqCareConnect, ProductRoleCodes.CareConnectNetworkManager);

        // ── Get network detail ─────────────────────────────────────────────────
        group.MapGet("/{id:guid}", async (
            Guid id,
            INetworkService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var network = await service.GetByIdAsync(tenantId, id, ct);
            return Results.Ok(network);
        })
        .RequireProductRole(ProductCodes.SynqCareConnect, ProductRoleCodes.CareConnectNetworkManager);

        // ── Update network ─────────────────────────────────────────────────────
        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdateNetworkRequest request,
            INetworkService service,
            ICurrentRequestContext ctx,
            IAuditEventClient auditClient,
            IMemoryCache cache,
            HttpContext http,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var network = await service.UpdateAsync(tenantId, id, ctx.UserId, request, ct);
            // BLK-PERF-02: Network metadata changed — evict all public surface cache entries
            // for this tenant+network so the next public read reflects the update.
            foreach (var key in CareConnectCacheKeys.PublicNetworkInvalidationKeys(tenantId, id))
                cache.Remove(key);
            // BLK-COMP-01: Audit network update.
            var correlationId = http.Items["CorrelationId"]?.ToString() ?? http.TraceIdentifier;
            _ = EmitNetworkAuditAsync(auditClient,
                eventType:     "careconnect.network.updated",
                action:        "NetworkUpdated",
                description:   $"Network '{id}' updated by user.",
                tenantId:      tenantId,
                actorUserId:   ctx.UserId,
                networkId:     id,
                correlationId: correlationId);
            return Results.Ok(network);
        })
        .RequireProductRole(ProductCodes.SynqCareConnect, ProductRoleCodes.CareConnectNetworkManager);

        // ── Delete network ─────────────────────────────────────────────────────
        group.MapDelete("/{id:guid}", async (
            Guid id,
            INetworkService service,
            ICurrentRequestContext ctx,
            IAuditEventClient auditClient,
            IMemoryCache cache,
            HttpContext http,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            await service.DeleteAsync(tenantId, id, ct);
            // BLK-PERF-02: Network deleted — evict all public surface cache entries
            // for this tenant+network (list + detail + providers + markers).
            foreach (var key in CareConnectCacheKeys.PublicNetworkInvalidationKeys(tenantId, id))
                cache.Remove(key);
            // BLK-COMP-01: Audit network deletion.
            var correlationId = http.Items["CorrelationId"]?.ToString() ?? http.TraceIdentifier;
            _ = EmitNetworkAuditAsync(auditClient,
                eventType:     "careconnect.network.deleted",
                action:        "NetworkDeleted",
                description:   $"Network '{id}' deleted by user.",
                tenantId:      tenantId,
                actorUserId:   ctx.UserId,
                networkId:     id,
                correlationId: correlationId);
            return Results.NoContent();
        })
        .RequireProductRole(ProductCodes.SynqCareConnect, ProductRoleCodes.CareConnectNetworkManager);

        // ── Search shared provider registry ────────────────────────────────────
        // CC2-INT-B06-01: Global cross-tenant search. Returns up to 20 matching providers.
        // Used by the tenant portal "Add Provider" search box.
        group.MapGet("/{id:guid}/providers/search", async (
            Guid id,
            [FromQuery] string? name,
            [FromQuery] string? phone,
            [FromQuery] string? npi,
            [FromQuery] string? city,
            INetworkService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            // Access control — verify the network belongs to this tenant
            _ = await service.GetByIdAsync(tenantId, id, ct);
            var results = await service.SearchProvidersAsync(name, phone, npi, city, ct);
            return Results.Ok(results);
        })
        .RequireProductRole(ProductCodes.SynqCareConnect, ProductRoleCodes.CareConnectNetworkManager);

        // ── Add provider to network (match-or-create) ──────────────────────────
        // CC2-INT-B06-01: Body: { existingProviderId } | { newProvider: {...} }
        // Match → associate. No match + new data → create in registry → associate.
        group.MapPost("/{id:guid}/providers", async (
            Guid id,
            [FromBody] AddProviderToNetworkRequest request,
            INetworkService service,
            ICurrentRequestContext ctx,
            IAuditEventClient auditClient,
            IMemoryCache cache,
            HttpContext http,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var provider = await service.AddProviderAsync(tenantId, id, request, ctx.UserId, ct);
            // BLK-PERF-02: Provider membership changed — evict public provider/marker/detail/list
            // cache entries for this tenant+network so the directory reflects the addition.
            foreach (var key in CareConnectCacheKeys.PublicNetworkInvalidationKeys(tenantId, id))
                cache.Remove(key);
            // BLK-COMP-01: Audit provider association — every network membership change is traceable.
            var correlationId = http.Items["CorrelationId"]?.ToString() ?? http.TraceIdentifier;
            _ = EmitNetworkAuditAsync(auditClient,
                eventType:     "careconnect.network.provider_added",
                action:        "ProviderAdded",
                description:   $"Provider '{provider.Id}' added to network '{id}' by user.",
                tenantId:      tenantId,
                actorUserId:   ctx.UserId,
                networkId:     id,
                correlationId: correlationId,
                metadata:      JsonSerializer.Serialize(new { providerId = provider.Id }));
            return Results.Ok(provider);
        })
        .RequireProductRole(ProductCodes.SynqCareConnect, ProductRoleCodes.CareConnectNetworkManager);

        // ── Remove provider from network ───────────────────────────────────────
        group.MapDelete("/{id:guid}/providers/{providerId:guid}", async (
            Guid id,
            Guid providerId,
            INetworkService service,
            ICurrentRequestContext ctx,
            IAuditEventClient auditClient,
            IMemoryCache cache,
            HttpContext http,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            await service.RemoveProviderAsync(tenantId, id, providerId, ct);
            // BLK-PERF-02: Provider removed from network — evict public provider/marker/detail/list
            // cache entries for this tenant+network so the directory reflects the removal.
            foreach (var key in CareConnectCacheKeys.PublicNetworkInvalidationKeys(tenantId, id))
                cache.Remove(key);
            // BLK-COMP-01: Audit provider disassociation.
            var correlationId = http.Items["CorrelationId"]?.ToString() ?? http.TraceIdentifier;
            _ = EmitNetworkAuditAsync(auditClient,
                eventType:     "careconnect.network.provider_removed",
                action:        "ProviderRemoved",
                description:   $"Provider '{providerId}' removed from network '{id}' by user.",
                tenantId:      tenantId,
                actorUserId:   ctx.UserId,
                networkId:     id,
                correlationId: correlationId,
                metadata:      JsonSerializer.Serialize(new { providerId = providerId }));
            return Results.NoContent();
        })
        .RequireProductRole(ProductCodes.SynqCareConnect, ProductRoleCodes.CareConnectNetworkManager);

        // ── Map markers for providers in network ───────────────────────────────
        group.MapGet("/{id:guid}/providers/markers", async (
            Guid id,
            INetworkService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var markers = await service.GetMarkersAsync(tenantId, id, ct);
            return Results.Ok(markers);
        })
        .RequireProductRole(ProductCodes.SynqCareConnect, ProductRoleCodes.CareConnectNetworkManager);

        // ── List providers in network ──────────────────────────────────────────
        group.MapGet("/{id:guid}/providers", async (
            Guid id,
            INetworkService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var detail = await service.GetByIdAsync(tenantId, id, ct);
            return Results.Ok(detail.Providers);
        })
        .RequireProductRole(ProductCodes.SynqCareConnect, ProductRoleCodes.CareConnectNetworkManager);

        // ── Network Referral Monitor ────────────────────────────────────────────
        // GET /api/network/referrals — all referrals for this tenant's network,
        // accessible to the lien company's network manager to see law-firm → provider flows.
        app.MapGet("/api/network/referrals", GetNetworkReferralsAsync)
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductRole(ProductCodes.SynqCareConnect, ProductRoleCodes.CareConnectNetworkManager);

        // ── Referrer read-only network directory ────────────────────────────────
        // CC-REFERRER-BROWSE: Law firm portal users (CareConnectReferrer) can
        // browse all active networks within the tenant and view provider maps
        // so they know which providers to target when submitting referrals.
        var dirGroup = app.MapGroup("/api/networks/directory")
            .RequireAuthorization(Policies.AuthenticatedUser);

        // List all networks (summary) — referrers see name, description, provider count
        dirGroup.MapGet("/", async (
            INetworkService      service,
            ICurrentRequestContext ctx,
            CancellationToken    ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var networks = await service.GetAllAsync(tenantId, ct);
            return Results.Ok(networks);
        })
        .RequireProductRole(ProductCodes.SynqCareConnect, ProductRoleCodes.CareConnectReferrer);

        // Get network with full provider list
        dirGroup.MapGet("/{id:guid}", async (
            Guid                  id,
            INetworkService       service,
            ICurrentRequestContext ctx,
            CancellationToken     ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var detail = await service.GetByIdAsync(tenantId, id, ct);
            return Results.Ok(detail);
        })
        .RequireProductRole(ProductCodes.SynqCareConnect, ProductRoleCodes.CareConnectReferrer);

        // Get provider map markers for a specific network
        dirGroup.MapGet("/{id:guid}/markers", async (
            Guid                  id,
            INetworkService       service,
            ICurrentRequestContext ctx,
            CancellationToken     ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var markers = await service.GetMarkersAsync(tenantId, id, ct);
            return Results.Ok(markers);
        })
        .RequireProductRole(ProductCodes.SynqCareConnect, ProductRoleCodes.CareConnectReferrer);
    }

    // ── BLK-COMP-01: Shared audit helper ─────────────────────────────────────────
    // Mirrors the EmitAuditAsync pattern in ProviderAdminEndpoints.
    // Fire-and-observe: caller uses `_ = EmitNetworkAuditAsync(...)` — never awaited,
    // never gates the primary business operation on audit delivery success.
    private static Task EmitNetworkAuditAsync(
        IAuditEventClient auditClient,
        string            eventType,
        string            action,
        string            description,
        Guid              tenantId,
        Guid?             actorUserId,
        Guid              networkId,
        string            correlationId,
        string?           metadata = null)
    {
        try
        {
            return auditClient.IngestAsync(new IngestAuditEventRequest
            {
                EventType     = eventType,
                EventCategory = EventCategory.Business,
                SourceSystem  = "care-connect",
                SourceService = "network-management",
                Visibility    = AuditVisibility.Tenant,
                Severity      = SeverityLevel.Info,
                OccurredAtUtc = DateTimeOffset.UtcNow,
                Scope = new AuditEventScopeDto
                {
                    ScopeType = ScopeType.Tenant,
                    TenantId  = tenantId.ToString(),
                    UserId    = actorUserId?.ToString(),
                },
                Actor = new AuditEventActorDto
                {
                    Type = actorUserId.HasValue ? ActorType.User : ActorType.System,
                    Id   = actorUserId?.ToString() ?? "system",
                },
                Entity = new AuditEventEntityDto
                {
                    Type = "Network",
                    Id   = networkId.ToString(),
                },
                Action        = action,
                Description   = description,
                Outcome       = "success",
                CorrelationId = correlationId,
                Metadata      = metadata,
                IdempotencyKey = IdempotencyKey.ForWithTimestamp(
                    DateTimeOffset.UtcNow, "care-connect", eventType, networkId.ToString()),
                Tags = ["network", "careconnect"],
            });
        }
        catch
        {
            return Task.CompletedTask;
        }
    }

    // ── GET /api/network/referrals ─────────────────────────────────────────────
    // Network Manager Referral Monitor — scoped to the caller's tenant.
    // Returns all referrals flowing through the network (sent by law firms to providers).
    // Supports ?status=, ?search= (client name, case #, referrer, provider), ?page=, ?pageSize=.
    private static async Task<IResult> GetNetworkReferralsAsync(
        CareConnectDbContext    db,
        ICurrentRequestContext  ctx,
        [FromQuery] int     page     = 1,
        [FromQuery] int     pageSize = 50,
        [FromQuery] string? status   = null,
        [FromQuery] string? search   = null,
        CancellationToken   ct       = default)
    {
        var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");

        page     = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = db.Referrals
            .Include(r => r.Provider)
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(r => r.Status == status);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(r =>
                EF.Functions.Like((r.ClientFirstName + " " + r.ClientLastName).ToLower(), "%" + s + "%") ||
                (r.CaseNumber    != null && EF.Functions.Like(r.CaseNumber.ToLower(),    "%" + s + "%")) ||
                (r.ReferrerName  != null && EF.Functions.Like(r.ReferrerName.ToLower(),  "%" + s + "%")) ||
                (r.Provider      != null && EF.Functions.Like(r.Provider.Name.ToLower(), "%" + s + "%")));
        }

        query = query.OrderByDescending(r => r.CreatedAtUtc);

        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new
            {
                id                      = r.Id,
                status                  = r.Status,
                urgency                 = r.Urgency,
                clientFirstName         = r.ClientFirstName,
                clientLastName          = r.ClientLastName,
                caseNumber              = r.CaseNumber,
                requestedService        = r.RequestedService,
                providerName            = r.Provider != null ? r.Provider.Name : (string?)null,
                providerOrganizationName = r.Provider != null ? r.Provider.OrganizationName : (string?)null,
                referringOrganizationId = r.ReferringOrganizationId,
                referrerName            = r.ReferrerName,
                referrerEmail           = r.ReferrerEmail,
                createdAtUtc            = r.CreatedAtUtc,
                updatedAtUtc            = r.UpdatedAtUtc,
            })
            .ToListAsync(ct);

        return Results.Ok(new { items, total, page, pageSize });
    }
}
