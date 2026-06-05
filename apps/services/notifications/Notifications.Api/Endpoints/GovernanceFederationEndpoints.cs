using BuildingBlocks.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Notifications.Application.Interfaces;

namespace Notifications.Api.Endpoints;

public static class GovernanceFederationEndpoints
{
    public static IEndpointRouteBuilder MapGovernanceFederationEndpoints(this IEndpointRouteBuilder app)
    {
        var grp = app.MapGroup("/notifications/v1/admin/governance")
                     .RequireAuthorization(Policies.AdminOnly);

        // ── Channel Scopes ──────────────────────────────────────────────────

        grp.MapGet("/channel-scopes", async (
            string? channelType, string? scopeMode, bool? enabled,
            int page, int pageSize,
            IGovernanceFederationService svc,
            CancellationToken ct) =>
        {
            var query  = new ChannelScopeQuery(channelType, scopeMode, enabled, Math.Max(1, page), Math.Clamp(pageSize, 1, 200));
            var result = await svc.ListChannelScopesAsync(query, ct);
            return Results.Ok(result);
        });

        grp.MapPost("/channel-scopes", async (
            CreateChannelScopeRequest req,
            IGovernanceFederationService svc,
            CancellationToken ct) =>
        {
            try
            {
                var dto = await svc.CreateChannelScopeAsync(req, ct);
                return Results.Created($"/notifications/v1/admin/governance/channel-scopes/{dto.Id}", dto);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        grp.MapPut("/channel-scopes/{id:guid}", async (
            Guid id,
            UpdateChannelScopeRequest req,
            IGovernanceFederationService svc,
            CancellationToken ct) =>
        {
            try
            {
                var dto = await svc.UpdateChannelScopeAsync(id, req, ct);
                return Results.Ok(dto);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        // ── Federated Rule Packs ────────────────────────────────────────────

        grp.MapGet("/federated-rule-packs", async (
            string? channelType, Guid? rulePackId, Guid? tenantId,
            string? federationGroup, bool? enabled,
            int page, int pageSize,
            IGovernanceFederationService svc,
            CancellationToken ct) =>
        {
            var query  = new FederatedRulePackQuery(channelType, rulePackId, tenantId, federationGroup, enabled,
                Math.Max(1, page), Math.Clamp(pageSize, 1, 200));
            var result = await svc.ListFederatedRulePacksAsync(query, ct);
            return Results.Ok(result);
        });

        grp.MapPost("/federated-rule-packs", async (
            FederateRulePackRequest req,
            IGovernanceFederationService svc,
            CancellationToken ct) =>
        {
            try
            {
                var dto = await svc.FederateRulePackAsync(req, ct);
                return Results.Created($"/notifications/v1/admin/governance/federated-rule-packs/{dto.Id}", dto);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        grp.MapPost("/federated-rule-packs/{id:guid}/disable", async (
            Guid id,
            DisableFederatedPackRequest req,
            IGovernanceFederationService svc,
            CancellationToken ct) =>
        {
            var result = await svc.DisableFederatedRulePackAsync(id, req.RequestedBy ?? "admin", req.Reason, ct);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        });

        // ── Federation Overlays ─────────────────────────────────────────────

        grp.MapGet("/federation-overlays", async (
            string? channelType, Guid? tenantId, Guid? rulePackId,
            string? overlayType, string? overlayState, bool? enabled,
            int page, int pageSize,
            IGovernanceFederationService svc,
            CancellationToken ct) =>
        {
            var query  = new FederationOverlayQuery(channelType, tenantId, rulePackId, overlayType, overlayState, enabled,
                Math.Max(1, page), Math.Clamp(pageSize, 1, 200));
            var result = await svc.ListFederationOverlaysAsync(query, ct);
            return Results.Ok(result);
        });

        grp.MapPost("/federation-overlays", async (
            CreateFederationOverlayRequest req,
            IGovernanceFederationService svc,
            CancellationToken ct) =>
        {
            try
            {
                var dto = await svc.CreateFederationOverlayAsync(req, ct);
                return Results.Created($"/notifications/v1/admin/governance/federation-overlays/{dto.Id}", dto);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        grp.MapPost("/federation-overlays/{id:guid}/activate", async (
            Guid id,
            ActorRequest req,
            IGovernanceFederationService svc,
            CancellationToken ct) =>
        {
            var result = await svc.ActivateFederationOverlayAsync(id, req.RequestedBy ?? "admin", ct);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        });

        grp.MapPost("/federation-overlays/{id:guid}/disable", async (
            Guid id,
            DisableOverlayRequest req,
            IGovernanceFederationService svc,
            CancellationToken ct) =>
        {
            var result = await svc.DisableFederationOverlayAsync(id, req.RequestedBy ?? "admin", req.Reason, ct);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        });

        // ── Topology ────────────────────────────────────────────────────────

        grp.MapGet("/topology", async (
            string channelType, Guid? tenantId, Guid? rolloutPlanId, Guid? releasePackageId,
            IGovernanceTopologyResolver resolver,
            CancellationToken ct) =>
        {
            var req    = new GovernanceTopologyRequest(tenantId, channelType ?? "sms", rolloutPlanId, releasePackageId);
            var result = await resolver.ResolveTopologyAsync(req, ct);
            return Results.Ok(result);
        });

        grp.MapGet("/topology/explain", async (
            string channelType, Guid? tenantId, Guid? rolloutPlanId,
            IGovernanceTopologyResolver resolver,
            CancellationToken ct) =>
        {
            var req    = new GovernanceTopologyRequest(tenantId, channelType ?? "sms", rolloutPlanId);
            var result = await resolver.ExplainTopologyAsync(req, ct);
            return Results.Ok(result);
        });

        // ── Audit Trail ─────────────────────────────────────────────────────

        grp.MapGet("/federation/audit", async (
            string? channelType, Guid? tenantId, string? eventType, string? entityType,
            int page, int pageSize,
            Notifications.Infrastructure.Data.NotificationsDbContext db,
            CancellationToken ct) =>
        {
            var q = db.GovernanceFederationAuditEvents.AsQueryable();
            if (channelType != null) q = q.Where(e => e.ChannelType  == channelType);
            if (tenantId    != null) q = q.Where(e => e.TenantId     == tenantId);
            if (eventType   != null) q = q.Where(e => e.EventType    == eventType);
            if (entityType  != null) q = q.Where(e => e.EntityType   == entityType);

            var p = Math.Max(1, page);
            var s = Math.Clamp(pageSize, 1, 200);
            var total = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(q, ct);
            var items = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                .ToListAsync(q.OrderByDescending(e => e.CreatedAt).Skip((p - 1) * s).Take(s), ct);

            return Results.Ok(new { total, page = p, pageSize = s, items });
        });

        // ── Analytics ───────────────────────────────────────────────────────

        grp.MapGet("/federation/analytics", async (
            string? channelType, string? federationGroup, Guid? tenantId,
            IGovernanceFederationAnalyticsService analytics,
            CancellationToken ct) =>
        {
            var query   = new FederationAnalyticsQuery(channelType, federationGroup, tenantId);
            var topology = await analytics.GetTopologyAnalyticsAsync(query, ct);
            var channel  = channelType != null
                ? await analytics.GetChannelGovernanceAnalyticsAsync(query, ct)
                : (ChannelGovernanceAnalyticsResult?)null;
            var rollout  = await analytics.GetCrossChannelRolloutAnalyticsAsync(query, ct);
            return Results.Ok(new { topology, channel, rollout });
        });

        return app;
    }
}

// ── Inline request helpers ───────────────────────────────────────────────────

file record DisableFederatedPackRequest(string? RequestedBy, string? Reason);
file record ActorRequest(string? RequestedBy);
file record DisableOverlayRequest(string? RequestedBy, string? Reason);
