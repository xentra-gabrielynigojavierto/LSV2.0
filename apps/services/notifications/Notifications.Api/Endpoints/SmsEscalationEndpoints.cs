using System.Security.Claims;
using BuildingBlocks.Authorization;
using Microsoft.AspNetCore.Mvc;
using Notifications.Application.DTOs;
using Notifications.Application.Interfaces;
using Notifications.Domain;
using Notifications.Infrastructure.Repositories;

namespace Notifications.Api.Endpoints;

/// <summary>
/// LS-NOTIF-SMS-011: Admin endpoints for SMS alert escalation policy and history management.
///
/// All endpoints:
///   - Require PlatformAdmin role (Policies.AdminOnly).
///   - Never return raw webhook URLs, full email addresses, CredentialsJson, or phone numbers.
///   - Never trigger SMS sends, provider calls, or reconciliation.
///
/// Policy routes:
///   GET  /v1/admin/sms/alerts/policies              — list policies (target masked)
///   GET  /v1/admin/sms/alerts/policies/{id}         — get policy by ID (target masked)
///   POST /v1/admin/sms/alerts/policies              — create policy
///   PUT  /v1/admin/sms/alerts/policies/{id}         — update policy
///   POST /v1/admin/sms/alerts/policies/{id}/disable — soft-disable policy
///
/// Escalation history routes:
///   GET  /v1/admin/sms/alerts/escalations              — list escalation history
///   GET  /v1/admin/sms/alerts/escalations/summary      — aggregate counts
///   GET  /v1/admin/sms/alerts/escalations/{id}         — single escalation
///   POST /v1/admin/sms/alerts/escalations/{id}/retry   — manually retry a failed escalation
/// </summary>
public static class SmsEscalationEndpoints
{
    private static readonly HashSet<string> ValidChannelTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "internal_notification", "email", "teams_webhook", "slack_webhook", "pagerduty", "opsgenie",
    };

    private static readonly HashSet<string> ValidSeverities = new(StringComparer.OrdinalIgnoreCase)
    {
        "warning", "critical",
    };

    public static IEndpointRouteBuilder MapSmsEscalationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app
            .MapGroup("/v1/admin/sms/alerts")
            .WithTags("SMS Alert Escalation")
            .RequireAuthorization(Policies.AdminOnly);

        MapPolicyEndpoints(group);
        MapEscalationHistoryEndpoints(group);

        return app;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Policy endpoints
    // ═══════════════════════════════════════════════════════════════════════════

    private static void MapPolicyEndpoints(IEndpointRouteBuilder group)
    {
        // ── GET /v1/admin/sms/alerts/policies ─────────────────────────────────
        group.MapGet("/policies", async (
            [FromQuery] bool?   enabled,
            [FromQuery] string? channelType,
            [FromQuery] string? alertType,
            [FromQuery] string? severity,
            [FromQuery] int     limit  = 50,
            [FromQuery] int     offset = 0,
            ISmsOperationalEscalationPolicyRepository repo = default!,
            CancellationToken ct = default) =>
        {
            var query = new SmsEscalationPolicyQuery
            {
                Enabled     = enabled,
                ChannelType = channelType,
                AlertType   = alertType,
                Severity    = severity,
                Limit       = Math.Max(1, Math.Min(limit,  200)),
                Offset      = Math.Max(0, offset),
            };
            var result = await repo.ListAsync(query, ct);
            return Results.Ok(result);
        });

        // ── GET /v1/admin/sms/alerts/policies/{id} ────────────────────────────
        group.MapGet("/policies/{id:guid}", async (
            Guid id,
            ISmsOperationalEscalationPolicyRepository repo,
            CancellationToken ct) =>
        {
            var policy = await repo.GetByIdAsync(id, ct);
            if (policy is null) return Results.NotFound();

            return Results.Ok(SmsEscalationPolicyRepository.MapToDto(policy));
        });

        // ── POST /v1/admin/sms/alerts/policies ───────────────────────────────
        group.MapPost("/policies", async (
            [FromBody] CreateSmsEscalationPolicyRequest body,
            ClaimsPrincipal principal,
            ISmsOperationalEscalationPolicyRepository repo,
            CancellationToken ct) =>
        {
            // Validate
            if (string.IsNullOrWhiteSpace(body.Name))
                return Results.BadRequest(new { error = "Name is required." });

            if (!ValidChannelTypes.Contains(body.ChannelType))
                return Results.BadRequest(new
                {
                    error = $"Invalid channelType '{body.ChannelType}'. " +
                            $"Valid values: {string.Join(", ", ValidChannelTypes)}."
                });

            if (string.IsNullOrWhiteSpace(body.Target))
                return Results.BadRequest(new { error = "Target is required." });

            if (body.Severity is not null && !ValidSeverities.Contains(body.Severity))
                return Results.BadRequest(new
                {
                    error = $"Invalid severity '{body.Severity}'. Valid values: warning, critical."
                });

            var cooldown = Math.Max(1, Math.Min(body.CooldownMinutes, 10080));
            var maxRetry = Math.Max(0, Math.Min(body.MaxRetryCount, 10));

            var createdBy = principal.FindFirst("sub")?.Value
                         ?? principal.FindFirst(ClaimTypes.Email)?.Value;

            var policy = new SmsOperationalEscalationPolicy
            {
                Id              = Guid.NewGuid(),
                Name            = body.Name.Trim()[..Math.Min(body.Name.Trim().Length, 200)],
                Enabled         = body.Enabled,
                AlertType       = string.IsNullOrWhiteSpace(body.AlertType)       ? null : body.AlertType.Trim(),
                Severity        = string.IsNullOrWhiteSpace(body.Severity)        ? null : body.Severity.Trim().ToLowerInvariant(),
                TenantId        = body.TenantId,
                Provider        = string.IsNullOrWhiteSpace(body.Provider)        ? null : body.Provider.Trim().ToLowerInvariant(),
                ProviderConfigId = body.ProviderConfigId,
                ChannelType     = body.ChannelType.Trim().ToLowerInvariant(),
                Target          = body.Target.Trim(),
                TargetDisplay   = string.IsNullOrWhiteSpace(body.TargetDisplay)   ? null : body.TargetDisplay.Trim()[..Math.Min(body.TargetDisplay.Trim().Length, 500)],
                CooldownMinutes = cooldown,
                RetryEnabled    = body.RetryEnabled,
                MaxRetryCount   = maxRetry,
                CreatedBy       = createdBy,
                UpdatedBy       = createdBy,
            };

            var created = await repo.CreateAsync(policy, ct);
            return Results.Created(
                $"/v1/admin/sms/alerts/policies/{created.Id}",
                SmsEscalationPolicyRepository.MapToDto(created));
        });

        // ── PUT /v1/admin/sms/alerts/policies/{id} ────────────────────────────
        group.MapPut("/policies/{id:guid}", async (
            Guid id,
            [FromBody] UpdateSmsEscalationPolicyRequest body,
            ClaimsPrincipal principal,
            ISmsOperationalEscalationPolicyRepository repo,
            CancellationToken ct) =>
        {
            var policy = await repo.GetByIdAsync(id, ct);
            if (policy is null) return Results.NotFound();

            if (body.ChannelType is not null && !ValidChannelTypes.Contains(body.ChannelType))
                return Results.BadRequest(new
                {
                    error = $"Invalid channelType '{body.ChannelType}'."
                });

            if (body.Severity is not null && !ValidSeverities.Contains(body.Severity))
                return Results.BadRequest(new
                {
                    error = $"Invalid severity '{body.Severity}'."
                });

            var updatedBy = principal.FindFirst("sub")?.Value
                          ?? principal.FindFirst(ClaimTypes.Email)?.Value;

            if (body.Name        is not null) policy.Name        = body.Name.Trim()[..Math.Min(body.Name.Trim().Length, 200)];
            if (body.Enabled     is not null) policy.Enabled     = body.Enabled.Value;
            if (body.AlertType   is not null) policy.AlertType   = string.IsNullOrWhiteSpace(body.AlertType) ? null : body.AlertType.Trim();
            if (body.Severity    is not null) policy.Severity    = string.IsNullOrWhiteSpace(body.Severity)  ? null : body.Severity.Trim().ToLowerInvariant();
            if (body.TenantId    is not null) policy.TenantId    = body.TenantId;
            if (body.Provider    is not null) policy.Provider    = string.IsNullOrWhiteSpace(body.Provider)  ? null : body.Provider.Trim().ToLowerInvariant();
            if (body.ProviderConfigId is not null) policy.ProviderConfigId = body.ProviderConfigId;
            if (body.ChannelType is not null) policy.ChannelType = body.ChannelType.Trim().ToLowerInvariant();
            if (body.Target      is not null && !string.IsNullOrWhiteSpace(body.Target)) policy.Target = body.Target.Trim();
            if (body.TargetDisplay is not null) policy.TargetDisplay = string.IsNullOrWhiteSpace(body.TargetDisplay) ? null : body.TargetDisplay.Trim()[..Math.Min(body.TargetDisplay.Trim().Length, 500)];
            if (body.CooldownMinutes is not null) policy.CooldownMinutes = Math.Max(1, Math.Min(body.CooldownMinutes.Value, 10080));
            if (body.RetryEnabled is not null) policy.RetryEnabled = body.RetryEnabled.Value;
            if (body.MaxRetryCount is not null) policy.MaxRetryCount = Math.Max(0, Math.Min(body.MaxRetryCount.Value, 10));

            policy.UpdatedBy = updatedBy;

            await repo.UpdateAsync(policy, ct);
            return Results.Ok(SmsEscalationPolicyRepository.MapToDto(policy));
        });

        // ── POST /v1/admin/sms/alerts/policies/{id}/disable ───────────────────
        group.MapPost("/policies/{id:guid}/disable", async (
            Guid id,
            ClaimsPrincipal principal,
            ISmsOperationalEscalationPolicyRepository repo,
            CancellationToken ct) =>
        {
            var updatedBy = principal.FindFirst("sub")?.Value
                          ?? principal.FindFirst(ClaimTypes.Email)?.Value;

            var ok = await repo.DisableAsync(id, updatedBy, ct);
            return ok
                ? Results.Ok(new { message = "Policy disabled." })
                : Results.NotFound(new { message = "Policy not found." });
        });
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Escalation history endpoints
    // ═══════════════════════════════════════════════════════════════════════════

    private static void MapEscalationHistoryEndpoints(IEndpointRouteBuilder group)
    {
        // ── GET /v1/admin/sms/alerts/escalations ──────────────────────────────
        group.MapGet("/escalations", async (
            [FromQuery] Guid?    alertId,
            [FromQuery] Guid?    policyId,
            [FromQuery] string?  status,
            [FromQuery] string?  channelType,
            [FromQuery] string?  severity,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] int limit  = 50,
            [FromQuery] int offset = 0,
            ISmsOperationalAlertEscalationRepository repo = default!,
            CancellationToken ct = default) =>
        {
            var query = new SmsAlertEscalationQuery
            {
                AlertId     = alertId,
                PolicyId    = policyId,
                Status      = status,
                ChannelType = channelType,
                Severity    = severity,
                From        = from,
                To          = to,
                Limit       = Math.Max(1, Math.Min(limit,  200)),
                Offset      = Math.Max(0, offset),
            };
            var result = await repo.ListAsync(query, ct);
            return Results.Ok(result);
        });

        // ── GET /v1/admin/sms/alerts/escalations/summary ─────────────────────
        group.MapGet("/escalations/summary", async (
            [FromQuery] Guid?    alertId,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            ISmsOperationalAlertEscalationRepository repo,
            CancellationToken ct) =>
        {
            var query = new SmsAlertEscalationQuery
            {
                AlertId = alertId,
                From    = from,
                To      = to,
            };
            var summary = await repo.SummarizeAsync(query, ct);
            return Results.Ok(summary);
        });

        // ── GET /v1/admin/sms/alerts/escalations/{id} ─────────────────────────
        group.MapGet("/escalations/{id:guid}", async (
            Guid id,
            ISmsOperationalAlertEscalationRepository repo,
            CancellationToken ct) =>
        {
            var escalation = await repo.GetByIdAsync(id, ct);
            return escalation is null ? Results.NotFound() : Results.Ok(escalation);
        });

        // ── POST /v1/admin/sms/alerts/escalations/{id}/retry ──────────────────
        group.MapPost("/escalations/{id:guid}/retry", async (
            Guid id,
            ClaimsPrincipal principal,
            ISmsAlertEscalationService escalationService,
            CancellationToken ct) =>
        {
            var requestedBy = principal.FindFirst("sub")?.Value
                           ?? principal.FindFirst(ClaimTypes.Email)?.Value;

            var ok = await escalationService.RetryEscalationAsync(id, requestedBy, ct);

            return ok
                ? Results.Ok(new { message = "Escalation retry initiated." })
                : Results.BadRequest(new { message = "Escalation not found or not eligible for retry." });
        });
    }
}
