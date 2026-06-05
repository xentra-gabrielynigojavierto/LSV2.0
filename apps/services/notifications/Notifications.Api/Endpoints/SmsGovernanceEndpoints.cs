using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using BuildingBlocks.Authorization;
using Notifications.Api.Authorization;
using Notifications.Domain;
using Notifications.Infrastructure.Data;

namespace Notifications.Api.Endpoints;

/// <summary>
/// LS-NOTIF-SMS-017: SMS Governance Policy admin endpoints.
///
/// All endpoints require PlatformAdmin authorization.
/// Responses never expose: raw phone numbers, credentials, SettingsJson, CredentialsJson,
/// provider payloads, webhook URLs, or secrets.
/// PolicyJson is returned as-is (contains only safe operational config).
/// </summary>
public static class SmsGovernanceEndpoints
{
    public static IEndpointRouteBuilder MapSmsGovernanceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/admin/sms/governance")
            .WithTags("Admin — SMS Governance")
            .RequireAuthorization(Policies.AdminOnly);

        // ── Policy CRUD ───────────────────────────────────────────────────────

        group.MapGet("/policies", async (
            NotificationsDbContext db,
            string? policyType,
            string? tenantId,
            bool? enabled,
            int page = 1, int pageSize = 50) =>
        {
            var q = db.SmsGovernancePolicies.AsQueryable();

            if (!string.IsNullOrEmpty(policyType))
                q = q.Where(p => p.PolicyType == policyType);

            if (!string.IsNullOrEmpty(tenantId) && Guid.TryParse(tenantId, out var tid))
                q = q.Where(p => p.TenantId == tid);
            else if (tenantId == "global")
                q = q.Where(p => p.TenantId == null);

            if (enabled.HasValue)
                q = q.Where(p => p.Enabled == enabled.Value);

            var total  = await q.CountAsync();
            var items  = await q
                .OrderBy(p => p.TenantId == null ? 1 : 0)
                .ThenBy(p => p.Priority)
                .ThenBy(p => p.PolicyType)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new
                {
                    p.Id, p.TenantId, p.Name, p.PolicyType, p.Enabled,
                    p.Priority, p.PolicyJson, p.EmergencyOverrideAllowed,
                    p.CreatedAt, p.UpdatedAt, p.CreatedBy, p.UpdatedBy,
                })
                .ToListAsync();

            return Results.Ok(new { total, page, pageSize, items });
        })
        .WithSummary("List governance policies");

        group.MapGet("/policies/{id:guid}", async (Guid id, NotificationsDbContext db) =>
        {
            var p = await db.SmsGovernancePolicies.FindAsync(id);
            if (p == null) return Results.NotFound();
            return Results.Ok(new
            {
                p.Id, p.TenantId, p.Name, p.PolicyType, p.Enabled,
                p.Priority, p.PolicyJson, p.EmergencyOverrideAllowed,
                p.CreatedAt, p.UpdatedAt, p.CreatedBy, p.UpdatedBy,
            });
        })
        .WithSummary("Get governance policy by ID");

        group.MapPost("/policies", async (CreateGovernancePolicyRequest req, NotificationsDbContext db) =>
        {
            var validTypes = new[] { "quiet_hours", "geographic_restriction", "rate_limit", "provider_governance", "retry_governance", "escalation_guardrail" };
            if (!validTypes.Contains(req.PolicyType))
                return Results.BadRequest(new { error = $"Invalid policyType. Allowed: {string.Join(", ", validTypes)}" });

            if (string.IsNullOrWhiteSpace(req.Name))
                return Results.BadRequest(new { error = "name is required" });

            // Validate PolicyJson is parseable
            if (!string.IsNullOrEmpty(req.PolicyJson))
            {
                try { JsonDocument.Parse(req.PolicyJson); }
                catch { return Results.BadRequest(new { error = "policyJson must be valid JSON" }); }
            }

            var policy = new SmsGovernancePolicy
            {
                Id                       = Guid.NewGuid(),
                TenantId                 = req.TenantId,
                Name                     = req.Name,
                PolicyType               = req.PolicyType,
                Enabled                  = req.Enabled ?? true,
                Priority                 = req.Priority ?? 100,
                PolicyJson               = req.PolicyJson ?? "{}",
                EmergencyOverrideAllowed = req.EmergencyOverrideAllowed ?? false,
                CreatedAt                = DateTime.UtcNow,
                UpdatedAt                = DateTime.UtcNow,
                CreatedBy                = req.RequestedBy,
                UpdatedBy                = req.RequestedBy,
            };

            db.SmsGovernancePolicies.Add(policy);
            await db.SaveChangesAsync();

            return Results.Created($"/v1/admin/sms/governance/policies/{policy.Id}", new { policy.Id });
        })
        .WithSummary("Create governance policy");

        group.MapPut("/policies/{id:guid}", async (Guid id, UpdateGovernancePolicyRequest req, NotificationsDbContext db) =>
        {
            var policy = await db.SmsGovernancePolicies.FindAsync(id);
            if (policy == null) return Results.NotFound();

            if (!string.IsNullOrEmpty(req.PolicyJson))
            {
                try { JsonDocument.Parse(req.PolicyJson); }
                catch { return Results.BadRequest(new { error = "policyJson must be valid JSON" }); }
            }

            if (!string.IsNullOrWhiteSpace(req.Name))          policy.Name                     = req.Name;
            if (!string.IsNullOrEmpty(req.PolicyJson))         policy.PolicyJson               = req.PolicyJson;
            if (req.Enabled.HasValue)                           policy.Enabled                  = req.Enabled.Value;
            if (req.Priority.HasValue)                          policy.Priority                 = req.Priority.Value;
            if (req.EmergencyOverrideAllowed.HasValue)          policy.EmergencyOverrideAllowed = req.EmergencyOverrideAllowed.Value;
            policy.UpdatedAt = DateTime.UtcNow;
            policy.UpdatedBy = req.RequestedBy;

            await db.SaveChangesAsync();
            return Results.Ok(new { policy.Id, policy.UpdatedAt });
        })
        .WithSummary("Update governance policy");

        group.MapPost("/policies/{id:guid}/disable", async (Guid id, NotificationsDbContext db, string? requestedBy) =>
        {
            var policy = await db.SmsGovernancePolicies.FindAsync(id);
            if (policy == null) return Results.NotFound();

            policy.Enabled   = false;
            policy.UpdatedAt = DateTime.UtcNow;
            policy.UpdatedBy = requestedBy;
            await db.SaveChangesAsync();
            return Results.Ok(new { policy.Id, disabled = true });
        })
        .WithSummary("Disable governance policy");

        // ── Decision audit log ────────────────────────────────────────────────

        group.MapGet("/decisions", async (
            NotificationsDbContext db,
            string? tenantId,
            string? decisionType,
            string? policyType,
            string? reasonCode,
            DateTime? from, DateTime? to,
            int page = 1, int pageSize = 50) =>
        {
            var q = db.SmsGovernanceDecisions.AsQueryable();

            if (!string.IsNullOrEmpty(tenantId) && Guid.TryParse(tenantId, out var tid))
                q = q.Where(d => d.TenantId == tid);

            if (!string.IsNullOrEmpty(decisionType)) q = q.Where(d => d.DecisionType == decisionType);
            if (!string.IsNullOrEmpty(policyType))   q = q.Where(d => d.PolicyType   == policyType);
            if (!string.IsNullOrEmpty(reasonCode))   q = q.Where(d => d.ReasonCode   == reasonCode);
            if (from.HasValue) q = q.Where(d => d.CreatedAt >= from.Value);
            if (to.HasValue)   q = q.Where(d => d.CreatedAt <= to.Value);

            var total = await q.CountAsync();
            var items = await q
                .OrderByDescending(d => d.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(d => new
                {
                    d.Id, d.NotificationId, d.TenantId, d.PolicyId,
                    d.PolicyType, d.DecisionType, d.ReasonCode,
                    d.ProviderType, d.CountryCode, d.Region,
                    d.EffectiveAt, d.DecisionMetadataJson, d.CreatedAt,
                    // AttemptId and ProviderConfigId included for correlation (no phone/credentials)
                    d.AttemptId, d.ProviderConfigId,
                })
                .ToListAsync();

            return Results.Ok(new { total, page, pageSize, items });
        })
        .WithSummary("List governance decisions (audit log)");

        // ── Analytics ─────────────────────────────────────────────────────────

        group.MapGet("/summary", async (
            NotificationsDbContext db,
            string? tenantId,
            int hours = 24) =>
        {
            var since = DateTime.UtcNow.AddHours(-hours);
            var q = db.SmsGovernanceDecisions.Where(d => d.CreatedAt >= since);

            if (!string.IsNullOrEmpty(tenantId) && Guid.TryParse(tenantId, out var tid))
                q = q.Where(d => d.TenantId == tid);

            var byDecision = await q
                .GroupBy(d => d.DecisionType)
                .Select(g => new { decisionType = g.Key, count = g.Count() })
                .ToListAsync();

            var byPolicyType = await q
                .GroupBy(d => d.PolicyType)
                .Select(g => new { policyType = g.Key, count = g.Count() })
                .ToListAsync();

            var byReason = await q
                .GroupBy(d => d.ReasonCode)
                .Select(g => new { reasonCode = g.Key, count = g.Count() })
                .OrderByDescending(g => g.count)
                .Take(10)
                .ToListAsync();

            var totalPolicies = await db.SmsGovernancePolicies.CountAsync(p => p.Enabled);

            return Results.Ok(new
            {
                windowHours     = hours,
                since,
                totalDecisions  = byDecision.Sum(d => d.count),
                activePolicies  = totalPolicies,
                byDecisionType  = byDecision,
                byPolicyType    = byPolicyType,
                topReasonCodes  = byReason,
            });
        })
        .WithSummary("Governance decision summary analytics");

        group.MapGet("/rate-limits", async (
            NotificationsDbContext db,
            string? tenantId,
            int windowMinutes = 60) =>
        {
            var since = DateTime.UtcNow.AddMinutes(-windowMinutes);

            // Active rate limit policies
            var ratePolicies = await db.SmsGovernancePolicies
                .Where(p => p.Enabled && p.PolicyType == "rate_limit")
                .OrderBy(p => p.TenantId == null ? 1 : 0)
                .ThenBy(p => p.Priority)
                .Select(p => new { p.Id, p.Name, p.TenantId, p.Priority, p.PolicyJson })
                .ToListAsync();

            // Recent throttle/delay decisions from rate limit policies
            var recentDecisions = await db.SmsGovernanceDecisions
                .Where(d => d.PolicyType == "rate_limit"
                          && d.DecisionType != "allow"
                          && d.CreatedAt >= since)
                .GroupBy(d => d.TenantId)
                .Select(g => new
                {
                    tenantId        = g.Key,
                    decisionsCount  = g.Count(),
                    lastDecisionAt  = g.Max(d => d.CreatedAt),
                })
                .ToListAsync();

            return Results.Ok(new
            {
                windowMinutes,
                rateLimitPolicies   = ratePolicies,
                recentThrottling    = recentDecisions,
            });
        })
        .WithSummary("Rate limit status and recent throttling activity");

        group.MapGet("/geo", async (
            NotificationsDbContext db,
            int hours = 24) =>
        {
            var since = DateTime.UtcNow.AddHours(-hours);

            var geoPolicies = await db.SmsGovernancePolicies
                .Where(p => p.Enabled && p.PolicyType == "geographic_restriction")
                .OrderBy(p => p.TenantId == null ? 1 : 0)
                .ThenBy(p => p.Priority)
                .Select(p => new { p.Id, p.Name, p.TenantId, p.Priority, p.PolicyJson })
                .ToListAsync();

            var blockedByCountry = await db.SmsGovernanceDecisions
                .Where(d => d.PolicyType == "geographic_restriction"
                          && d.DecisionType != "allow"
                          && d.CreatedAt >= since
                          && d.CountryCode != null)
                .GroupBy(d => d.CountryCode)
                .Select(g => new { countryCode = g.Key, count = g.Count() })
                .OrderByDescending(g => g.count)
                .Take(20)
                .ToListAsync();

            return Results.Ok(new
            {
                windowHours      = hours,
                geoPolicies,
                blockedByCountry,
            });
        })
        .WithSummary("Geographic restriction policies and blocked-country summary");

        return app;
    }

    // ─── Request types ────────────────────────────────────────────────────────

    private sealed record CreateGovernancePolicyRequest(
        string  PolicyType,
        string  Name,
        Guid?   TenantId,
        bool?   Enabled,
        int?    Priority,
        string? PolicyJson,
        bool?   EmergencyOverrideAllowed,
        string? RequestedBy);

    private sealed record UpdateGovernancePolicyRequest(
        string? Name,
        string? PolicyJson,
        bool?   Enabled,
        int?    Priority,
        bool?   EmergencyOverrideAllowed,
        string? RequestedBy);
}
