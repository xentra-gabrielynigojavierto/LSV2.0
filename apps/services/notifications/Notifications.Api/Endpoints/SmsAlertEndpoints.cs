using System.Security.Claims;
using BuildingBlocks.Authorization;
using Microsoft.AspNetCore.Mvc;
using Notifications.Application.DTOs;
using Notifications.Application.Interfaces;

namespace Notifications.Api.Endpoints;

/// <summary>
/// LS-NOTIF-SMS-010: Admin endpoints for SMS operational alert management.
///
/// All endpoints:
///  - Require PlatformAdmin role (Policies.AdminOnly).
///  - Never return CredentialsJson, SettingsJson, RecipientJson, or phone numbers.
///  - Never trigger SMS sends, retries, reconciliation, or provider calls.
///
/// Routes:
///   GET  /v1/admin/sms/alerts              — list alerts (paginated, filterable)
///   GET  /v1/admin/sms/alerts/summary      — aggregate counts
///   GET  /v1/admin/sms/alerts/{id}         — single alert
///   POST /v1/admin/sms/alerts/{id}/resolve — resolve an active alert
///   POST /v1/admin/sms/alerts/{id}/suppress — suppress an alert
///   POST /v1/admin/sms/alerts/evaluate     — manually trigger one evaluation cycle
/// </summary>
public static class SmsAlertEndpoints
{
    public static IEndpointRouteBuilder MapSmsAlertEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app
            .MapGroup("/v1/admin/sms/alerts")
            .WithTags("SMS Operational Alerts")
            .RequireAuthorization(Policies.AdminOnly);

        // ── GET /v1/admin/sms/alerts ──────────────────────────────────────────
        group.MapGet("/", async (
            [FromQuery] string?  status,
            [FromQuery] string?  severity,
            [FromQuery] string?  alertType,
            [FromQuery] Guid?    tenantId,
            [FromQuery] string?  provider,
            [FromQuery] Guid?    providerConfigId,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] int limit  = 50,
            [FromQuery] int offset = 0,
            ISmsOperationalAlertRepository repo = default!,
            CancellationToken ct = default) =>
        {
            var query = new SmsAlertQuery
            {
                Status           = status,
                Severity         = severity,
                AlertType        = alertType,
                TenantId         = tenantId,
                Provider         = provider,
                ProviderConfigId = providerConfigId,
                From             = from,
                To               = to,
                Limit            = Math.Max(1, Math.Min(limit,  200)),
                Offset           = Math.Max(0, offset),
            };

            var result = await repo.ListAsync(query, ct);
            return Results.Ok(result);
        });

        // ── GET /v1/admin/sms/alerts/summary ──────────────────────────────────
        group.MapGet("/summary", async (
            ISmsOperationalAlertRepository repo,
            CancellationToken ct) =>
        {
            var summary = await repo.GetSummaryAsync(ct);
            return Results.Ok(summary);
        });

        // ── GET /v1/admin/sms/alerts/{id} ─────────────────────────────────────
        group.MapGet("/{id:guid}", async (
            Guid id,
            ISmsOperationalAlertRepository repo,
            CancellationToken ct) =>
        {
            var alert = await repo.GetByIdAsync(id, ct);
            return alert is null ? Results.NotFound() : Results.Ok(alert);
        });

        // ── POST /v1/admin/sms/alerts/{id}/resolve ────────────────────────────
        group.MapPost("/{id:guid}/resolve", async (
            Guid id,
            [FromBody] SmsAlertResolveRequest? body,
            ClaimsPrincipal principal,
            ISmsOperationalAlertRepository repo,
            CancellationToken ct) =>
        {
            var resolvedBy = principal.FindFirst("sub")?.Value
                          ?? principal.FindFirst(ClaimTypes.Email)?.Value;

            var ok = await repo.ResolveAsync(id, resolvedBy, body?.ResolutionNote, ct);

            return ok
                ? Results.Ok(new { message = "Alert resolved." })
                : Results.NotFound(new { message = "Alert not found or already resolved." });
        });

        // ── POST /v1/admin/sms/alerts/{id}/suppress ───────────────────────────
        group.MapPost("/{id:guid}/suppress", async (
            Guid id,
            [FromBody] SmsAlertSuppressRequest? body,
            ISmsOperationalAlertRepository repo,
            CancellationToken ct) =>
        {
            var minutes         = Math.Max(1, Math.Min(body?.SuppressForMinutes ?? 60, 10080));
            var suppressedUntil = DateTime.UtcNow.AddMinutes(minutes);

            var ok = await repo.SuppressAsync(id, suppressedUntil, ct);

            return ok
                ? Results.Ok(new
                  {
                      message       = $"Alert suppressed for {minutes} minutes.",
                      suppressedUntil,
                  })
                : Results.NotFound(new { message = "Alert not found." });
        });

        // ── POST /v1/admin/sms/alerts/evaluate ────────────────────────────────
        // Manually triggers one evaluation cycle for operator use / testing.
        group.MapPost("/evaluate", async (
            [FromQuery] int windowMinutes                  = 60,
            ISmsOperationalAlertEvaluator evaluator        = default!,
            CancellationToken ct                           = default) =>
        {
            var clampedMinutes = Math.Max(1, Math.Min(windowMinutes, 1440));
            var windowEnd      = DateTime.UtcNow;
            var windowStart    = windowEnd.AddMinutes(-clampedMinutes);

            var result = await evaluator.EvaluateAsync(windowStart, windowEnd, ct);
            return Results.Ok(result);
        });

        return app;
    }
}
