using System.Security.Claims;
using System.Text.Json;
using BuildingBlocks.Authorization;
using Microsoft.AspNetCore.Mvc;
using Notifications.Application.DTOs;
using Notifications.Application.Interfaces;
using Notifications.Domain;
using Notifications.Infrastructure.Repositories;

namespace Notifications.Api.Endpoints;

/// <summary>
/// LS-NOTIF-SMS-014 — Multi-Provider SMS Routing Admin APIs.
///
/// All endpoints require PlatformAdmin role (Policies.AdminOnly).
/// All write endpoints validate input and return structured errors.
/// No credentials, CredentialsJson, SettingsJson, auth tokens, webhook URLs,
/// or raw phone numbers returned in any response.
///
/// GET /v1/admin/sms/routing/capabilities           — all provider capability metadata
/// GET /v1/admin/sms/routing/policies               — list routing policies (paged)
/// GET /v1/admin/sms/routing/policies/{id}          — single policy
/// POST /v1/admin/sms/routing/policies              — create policy
/// PUT /v1/admin/sms/routing/policies/{id}          — update policy
/// POST /v1/admin/sms/routing/policies/{id}/disable — soft-disable policy
/// GET /v1/admin/sms/routing/decisions              — routing decisions (paged, read-only)
/// GET /v1/admin/sms/routing/decisions/summary      — aggregate decision statistics
/// GET /v1/admin/sms/routing/providers/health       — provider health snapshot
/// </summary>
public static class SmsRoutingEndpoints
{
    private static readonly HashSet<string> ValidRoutingModes = new(StringComparer.OrdinalIgnoreCase)
    {
        // LS-NOTIF-SMS-014
        "priority", "cost_optimized", "health_optimized", "hybrid", "regional",
        // LS-NOTIF-SMS-015: Adaptive modes
        "adaptive_quality", "adaptive_balanced", "adaptive_regional",
    };

    public static IEndpointRouteBuilder MapSmsRoutingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app
            .MapGroup("/v1/admin/sms/routing")
            .WithTags("Admin — SMS Routing")
            .RequireAuthorization(Policies.AdminOnly);

        // ── GET /capabilities ─────────────────────────────────────────────────
        group.MapGet("/capabilities", (ISmsProviderCapabilityService capabilitySvc) =>
        {
            var caps = capabilitySvc.GetAll();
            return Results.Ok(new { items = caps, total = caps.Count });
        });

        // ── GET /policies ─────────────────────────────────────────────────────
        group.MapGet("/policies", async (
            ISmsRoutingPolicyRepository repo,
            CancellationToken ct,
            [FromQuery] string? tenantId,
            [FromQuery] bool?   enabled,
            [FromQuery] string? routingMode,
            [FromQuery] int     limit  = 50,
            [FromQuery] int     offset = 0) =>
        {
            var query = new SmsRoutingPolicyQuery
            {
                TenantId    = ParseGuid(tenantId),
                Enabled     = enabled,
                RoutingMode = routingMode,
                Limit       = Math.Max(1, Math.Min(limit, 200)),
                Offset      = Math.Max(0, offset),
            };
            var (items, total) = await repo.ListAsync(query, ct);
            return Results.Ok(new SmsRoutingPolicyListResult
            {
                Items  = items.Select(SmsRoutingPolicyRepository.ToDto).ToList(),
                Total  = total,
                Limit  = query.Limit,
                Offset = query.Offset,
            });
        });

        // ── GET /policies/{id} ────────────────────────────────────────────────
        group.MapGet("/policies/{id:guid}", async (
            Guid id,
            ISmsRoutingPolicyRepository repo,
            CancellationToken ct) =>
        {
            var policy = await repo.GetByIdAsync(id, ct);
            return policy is null ? Results.NotFound() : Results.Ok(SmsRoutingPolicyRepository.ToDto(policy));
        });

        // ── POST /policies ────────────────────────────────────────────────────
        group.MapPost("/policies", async (
            [FromBody] CreateSmsRoutingPolicyRequest body,
            ClaimsPrincipal principal,
            ISmsRoutingPolicyRepository repo,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(body.Name))
                return Results.BadRequest(new { error = "Name is required." });

            if (!ValidRoutingModes.Contains(body.RoutingMode))
                return Results.BadRequest(new
                {
                    error = $"Invalid routingMode '{body.RoutingMode}'. " +
                            $"Valid values: {string.Join(", ", ValidRoutingModes)}."
                });

            if (!ValidateProvidersJson(body.PreferredProvidersJson, out var prefErr))
                return Results.BadRequest(new { error = $"PreferredProvidersJson: {prefErr}" });
            if (!ValidateProvidersJson(body.ExcludedProvidersJson, out var exclErr))
                return Results.BadRequest(new { error = $"ExcludedProvidersJson: {exclErr}" });

            var actor = GetActor(principal);
            var policy = new SmsRoutingPolicy
            {
                Id                         = Guid.NewGuid(),
                TenantId                   = body.TenantId,
                Name                       = body.Name.Trim()[..Math.Min(body.Name.Trim().Length, 200)],
                Enabled                    = body.Enabled,
                Region                     = body.Region?.Trim(),
                CountryCode                = body.CountryCode?.Trim().ToUpperInvariant(),
                RoutingMode                = body.RoutingMode.Trim().ToLowerInvariant(),
                PreferredProvidersJson     = NormalizeProvidersJson(body.PreferredProvidersJson),
                ExcludedProvidersJson      = NormalizeProvidersJson(body.ExcludedProvidersJson),
                MaxEstimatedCostPerMessage = body.MaxEstimatedCostPerMessage,
                RequireHealthyProvider     = body.RequireHealthyProvider,
                FallbackToPlatform         = body.FallbackToPlatform,
                Priority                   = Math.Max(0, body.Priority),
                CreatedAt                  = DateTime.UtcNow,
                UpdatedAt                  = DateTime.UtcNow,
                CreatedBy                  = actor,
                UpdatedBy                  = actor,
            };

            await repo.CreateAsync(policy, ct);
            return Results.Created($"/v1/admin/sms/routing/policies/{policy.Id}",
                SmsRoutingPolicyRepository.ToDto(policy));
        });

        // ── PUT /policies/{id} ────────────────────────────────────────────────
        group.MapPut("/policies/{id:guid}", async (
            Guid id,
            [FromBody] UpdateSmsRoutingPolicyRequest body,
            ClaimsPrincipal principal,
            ISmsRoutingPolicyRepository repo,
            CancellationToken ct) =>
        {
            var policy = await repo.GetByIdAsync(id, ct);
            if (policy is null) return Results.NotFound();

            if (string.IsNullOrWhiteSpace(body.Name))
                return Results.BadRequest(new { error = "Name is required." });

            if (!ValidRoutingModes.Contains(body.RoutingMode))
                return Results.BadRequest(new
                {
                    error = $"Invalid routingMode '{body.RoutingMode}'. " +
                            $"Valid values: {string.Join(", ", ValidRoutingModes)}."
                });

            if (!ValidateProvidersJson(body.PreferredProvidersJson, out var prefErr))
                return Results.BadRequest(new { error = $"PreferredProvidersJson: {prefErr}" });
            if (!ValidateProvidersJson(body.ExcludedProvidersJson, out var exclErr))
                return Results.BadRequest(new { error = $"ExcludedProvidersJson: {exclErr}" });

            policy.Name                       = body.Name.Trim()[..Math.Min(body.Name.Trim().Length, 200)];
            policy.Enabled                    = body.Enabled;
            policy.Region                     = body.Region?.Trim();
            policy.CountryCode                = body.CountryCode?.Trim().ToUpperInvariant();
            policy.RoutingMode                = body.RoutingMode.Trim().ToLowerInvariant();
            policy.PreferredProvidersJson     = NormalizeProvidersJson(body.PreferredProvidersJson);
            policy.ExcludedProvidersJson      = NormalizeProvidersJson(body.ExcludedProvidersJson);
            policy.MaxEstimatedCostPerMessage = body.MaxEstimatedCostPerMessage;
            policy.RequireHealthyProvider     = body.RequireHealthyProvider;
            policy.FallbackToPlatform         = body.FallbackToPlatform;
            policy.Priority                   = Math.Max(0, body.Priority);
            policy.UpdatedAt                  = DateTime.UtcNow;
            policy.UpdatedBy                  = GetActor(principal);

            await repo.UpdateAsync(policy, ct);
            return Results.Ok(SmsRoutingPolicyRepository.ToDto(policy));
        });

        // ── POST /policies/{id}/disable ───────────────────────────────────────
        group.MapPost("/policies/{id:guid}/disable", async (
            Guid id,
            ClaimsPrincipal principal,
            ISmsRoutingPolicyRepository repo,
            CancellationToken ct) =>
        {
            var policy = await repo.GetByIdAsync(id, ct);
            if (policy is null) return Results.NotFound();

            if (!policy.Enabled)
                return Results.Ok(new { message = "Policy is already disabled.", policy = SmsRoutingPolicyRepository.ToDto(policy) });

            policy.Enabled   = false;
            policy.UpdatedAt = DateTime.UtcNow;
            policy.UpdatedBy = GetActor(principal);
            await repo.UpdateAsync(policy, ct);

            return Results.Ok(new { message = "Policy disabled.", policy = SmsRoutingPolicyRepository.ToDto(policy) });
        });

        // ── GET /decisions ────────────────────────────────────────────────────
        group.MapGet("/decisions", async (
            ISmsRoutingDecisionRepository repo,
            CancellationToken ct,
            [FromQuery] string?   tenantId,
            [FromQuery] string?   notificationId,
            [FromQuery] string?   provider,
            [FromQuery] string?   routingMode,
            [FromQuery] string?   policyId,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] int       limit  = 50,
            [FromQuery] int       offset = 0) =>
        {
            var query = new SmsRoutingDecisionQuery
            {
                TenantId       = ParseGuid(tenantId),
                NotificationId = ParseGuid(notificationId),
                Provider       = provider,
                RoutingMode    = routingMode,
                PolicyId       = ParseGuid(policyId),
                From           = from,
                To             = to,
                Limit          = Math.Max(1, Math.Min(limit, 200)),
                Offset         = Math.Max(0, offset),
            };
            var (items, total) = await repo.ListAsync(query, ct);
            return Results.Ok(new SmsRoutingDecisionListResult
            {
                Items  = items.Select(SmsRoutingDecisionRepository.ToDto).ToList(),
                Total  = total,
                Limit  = query.Limit,
                Offset = query.Offset,
            });
        });

        // ── GET /decisions/summary ────────────────────────────────────────────
        group.MapGet("/decisions/summary", async (
            ISmsRoutingDecisionRepository repo,
            CancellationToken ct,
            [FromQuery] string?   tenantId,
            [FromQuery] string?   provider,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to) =>
        {
            var query = new SmsRoutingDecisionQuery
            {
                TenantId = ParseGuid(tenantId),
                Provider = provider,
                From     = from,
                To       = to,
            };
            var summary = await repo.GetSummaryAsync(query, ct);
            return Results.Ok(summary);
        });

        // ── GET /providers/health ─────────────────────────────────────────────
        // Returns locally cached provider health — never calls external providers.
        group.MapGet("/providers/health", async (
            IProviderHealthRepository healthRepo,
            CancellationToken ct) =>
        {
            var all = await healthRepo.GetAllAsync();
            var records = all
                .Where(r => string.Equals(r.Channel, "sms", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var dtos = records.Select(r => new SmsProviderHealthDto
            {
                ProviderType     = r.ProviderType,
                OwnershipMode    = r.OwnershipMode,
                ProviderConfigId = r.TenantProviderConfigId,
                HealthStatus     = r.HealthStatus,
                LatencyMs        = r.LastLatencyMs,
                CheckedAt        = r.LastCheckAt,
            }).ToList();
            return Results.Ok(new { items = dtos, total = dtos.Count });
        });

        return app;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Guid? ParseGuid(string? raw)
        => !string.IsNullOrEmpty(raw) && Guid.TryParse(raw, out var g) ? g : null;

    private static string? GetActor(ClaimsPrincipal p)
        => p.FindFirst("sub")?.Value ?? p.FindFirst(ClaimTypes.Email)?.Value;

    private static bool ValidateProvidersJson(string? json, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrEmpty(json)) return true;
        try
        {
            var list = JsonSerializer.Deserialize<List<string>>(json);
            if (list == null) { error = "Must be a JSON string array."; return false; }
            return true;
        }
        catch
        {
            error = "Must be a valid JSON string array (e.g., [\"twilio\",\"vonage\"]).";
            return false;
        }
    }

    private static string? NormalizeProvidersJson(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            var list = JsonSerializer.Deserialize<List<string>>(json);
            if (list == null || list.Count == 0) return null;
            return JsonSerializer.Serialize(list.Select(p => p.Trim().ToLowerInvariant()).ToList());
        }
        catch { return json; }
    }
}
