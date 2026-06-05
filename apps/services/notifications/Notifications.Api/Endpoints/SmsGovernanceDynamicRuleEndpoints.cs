using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Notifications.Application.Interfaces;
using Notifications.Application.Options;
using Notifications.Domain;
using Notifications.Infrastructure.Data;

namespace Notifications.Api.Endpoints;

/// <summary>
/// LS-NOTIF-SMS-019: Admin endpoints for dynamic governance rule management.
///
/// All endpoints require PlatformAdmin authorization.
/// No raw phone numbers, credentials, or provider secrets are accepted/returned.
/// Pattern length capped at MaxPatternLength; regex patterns validated for safety.
///
/// Routes (prefix: /v1/admin/sms/governance):
///   GET  /rule-packs
///   GET  /rule-packs/{id}
///   POST /rule-packs
///   PUT  /rule-packs/{id}
///   POST /rule-packs/{id}/disable
///   GET  /rules
///   POST /rules
///   PUT  /rules/{id}
///   POST /rules/{id}/disable
///   GET  /profiles
///   POST /profiles
///   PUT  /profiles/{id}
///   POST /simulate
///   GET  /rule-analytics
/// </summary>
public static class SmsGovernanceDynamicRuleEndpoints
{
    private static readonly string[] ValidRuleTypes =
    [
        "prohibited_phrase", "restricted_pattern", "classification_override",
        "variable_rule", "link_rule", "delivery_restriction", "escalation_rule",
    ];

    private static readonly string[] ValidSeverities =
        ["allow", "warn", "review_required", "block", "override_allowed"];

    private static readonly string[] ValidStatuses =
        ["draft", "active", "inactive", "archived"];

    private static readonly string[] ValidInheritanceModes =
        ["merge", "override", "append_only"];

    private static readonly string[] ValidEnforcementModes =
        ["permissive", "standard", "strict"];

    private static readonly string[] ValidScopes =
        ["tenant", "provider", "template_category", "escalation"];

    public static void MapSmsGovernanceDynamicRuleEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/v1/admin/sms/governance")
            .RequireAuthorization("PlatformAdmin")
            .WithTags("SMS Dynamic Governance Rules");

        // ── Rule Packs ────────────────────────────────────────────────────────

        group.MapGet("/rule-packs", async (
            NotificationsDbContext db,
            Guid?   tenantId   = null,
            string? status     = null,
            bool?   enabled    = null,
            int     page       = 1,
            int     pageSize   = 50) =>
        {
            var q = db.SmsGovernanceRulePacks.AsNoTracking().AsQueryable();
            if (tenantId.HasValue)             q = q.Where(p => p.TenantId == tenantId);
            if (!string.IsNullOrEmpty(status)) q = q.Where(p => p.Status == status);
            if (enabled.HasValue)              q = q.Where(p => p.Enabled == enabled.Value);

            var total = await q.CountAsync();
            var items = await q
                .OrderBy(p => p.Priority).ThenBy(p => p.Name)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(p => new
                {
                    p.Id, p.TenantId, p.Name, p.Description, p.Version,
                    p.Status, p.Enabled, p.InheritanceMode, p.Priority,
                    p.EffectiveFrom, p.EffectiveTo,
                    p.CreatedAt, p.UpdatedAt, p.CreatedBy, p.UpdatedBy,
                })
                .ToListAsync();

            return Results.Ok(new { total, page, pageSize, items });
        }).WithSummary("List governance rule packs");

        group.MapGet("/rule-packs/{id:guid}", async (Guid id, NotificationsDbContext db) =>
        {
            var p = await db.SmsGovernanceRulePacks.FindAsync(id);
            if (p == null) return Results.NotFound();
            var ruleCount = await db.SmsGovernanceRules.CountAsync(r => r.RulePackId == id);
            return Results.Ok(new
            {
                p.Id, p.TenantId, p.Name, p.Description, p.Version,
                p.Status, p.Enabled, p.InheritanceMode, p.Priority,
                p.EffectiveFrom, p.EffectiveTo,
                p.CreatedAt, p.UpdatedAt, p.CreatedBy, p.UpdatedBy,
                ruleCount,
            });
        }).WithSummary("Get rule pack by ID");

        group.MapPost("/rule-packs", async (
            CreateRulePackRequest req,
            NotificationsDbContext db,
            ISmsGovernanceVersioningService vs) =>
        {
            if (string.IsNullOrWhiteSpace(req.Name))
                return Results.BadRequest(new { error = "name is required" });
            if (!string.IsNullOrEmpty(req.Status) && !ValidStatuses.Contains(req.Status))
                return Results.BadRequest(new { error = $"Invalid status. Allowed: {string.Join(", ", ValidStatuses)}" });
            if (!string.IsNullOrEmpty(req.InheritanceMode) && !ValidInheritanceModes.Contains(req.InheritanceMode))
                return Results.BadRequest(new { error = $"Invalid inheritanceMode. Allowed: {string.Join(", ", ValidInheritanceModes)}" });

            var pack = new SmsGovernanceRulePack
            {
                Id              = Guid.NewGuid(),
                TenantId        = req.TenantId,
                Name            = req.Name.Trim(),
                Description     = req.Description,
                Version         = 1,
                Status          = req.Status ?? "draft",
                Enabled         = req.Enabled ?? true,
                InheritanceMode = req.InheritanceMode ?? "merge",
                Priority        = req.Priority ?? 100,
                EffectiveFrom   = req.EffectiveFrom,
                EffectiveTo     = req.EffectiveTo,
                CreatedAt       = DateTime.UtcNow,
                UpdatedAt       = DateTime.UtcNow,
                CreatedBy       = req.RequestedBy,
                UpdatedBy       = req.RequestedBy,
            };
            db.SmsGovernanceRulePacks.Add(pack);
            await db.SaveChangesAsync();
            // LS-NOTIF-SMS-020: immutable version snapshot
            await vs.SnapshotRulePackAsync(pack.Id, "created", null, req.RequestedBy, includeRules: false);
            return Results.Created($"/v1/admin/sms/governance/rule-packs/{pack.Id}", new { pack.Id });
        }).WithSummary("Create governance rule pack");

        group.MapPut("/rule-packs/{id:guid}", async (
            Guid id,
            UpdateRulePackRequest req,
            NotificationsDbContext db,
            ISmsGovernanceVersioningService vs) =>
        {
            var pack = await db.SmsGovernanceRulePacks.FindAsync(id);
            if (pack == null) return Results.NotFound();

            if (!string.IsNullOrEmpty(req.Status) && !ValidStatuses.Contains(req.Status))
                return Results.BadRequest(new { error = $"Invalid status" });
            if (!string.IsNullOrEmpty(req.InheritanceMode) && !ValidInheritanceModes.Contains(req.InheritanceMode))
                return Results.BadRequest(new { error = $"Invalid inheritanceMode" });

            if (!string.IsNullOrWhiteSpace(req.Name))        pack.Name            = req.Name.Trim();
            if (req.Description != null)                      pack.Description     = req.Description;
            if (!string.IsNullOrEmpty(req.Status))            pack.Status          = req.Status;
            if (!string.IsNullOrEmpty(req.InheritanceMode))   pack.InheritanceMode = req.InheritanceMode;
            if (req.Enabled.HasValue)                         pack.Enabled         = req.Enabled.Value;
            if (req.Priority.HasValue)                        pack.Priority        = req.Priority.Value;
            if (req.EffectiveFrom.HasValue)                   pack.EffectiveFrom   = req.EffectiveFrom;
            if (req.EffectiveTo.HasValue)                     pack.EffectiveTo     = req.EffectiveTo;
            pack.Version++;
            pack.UpdatedAt = DateTime.UtcNow;
            pack.UpdatedBy = req.RequestedBy;

            await db.SaveChangesAsync();
            // LS-NOTIF-SMS-020: immutable version snapshot
            await vs.SnapshotRulePackAsync(id, "updated", null, req.RequestedBy, includeRules: false);
            return Results.Ok(new { pack.Id, pack.Version, pack.UpdatedAt });
        }).WithSummary("Update governance rule pack");

        group.MapPost("/rule-packs/{id:guid}/disable", async (
            Guid id, NotificationsDbContext db, ISmsGovernanceVersioningService vs, string? requestedBy) =>
        {
            var pack = await db.SmsGovernanceRulePacks.FindAsync(id);
            if (pack == null) return Results.NotFound();
            pack.Enabled   = false;
            pack.Status    = "inactive";
            pack.UpdatedAt = DateTime.UtcNow;
            pack.UpdatedBy = requestedBy;
            await db.SaveChangesAsync();
            // LS-NOTIF-SMS-020: immutable version snapshot
            await vs.SnapshotRulePackAsync(id, "disabled", null, requestedBy, includeRules: false);
            return Results.Ok(new { pack.Id, disabled = true });
        }).WithSummary("Disable governance rule pack");

        // ── Rules ─────────────────────────────────────────────────────────────

        group.MapGet("/rules", async (
            NotificationsDbContext db,
            Guid?   rulePackId = null,
            string? ruleType   = null,
            string? severity   = null,
            bool?   enabled    = null,
            int     page       = 1,
            int     pageSize   = 50) =>
        {
            var q = db.SmsGovernanceRules.AsNoTracking().AsQueryable();
            if (rulePackId.HasValue)              q = q.Where(r => r.RulePackId == rulePackId.Value);
            if (!string.IsNullOrEmpty(ruleType))  q = q.Where(r => r.RuleType == ruleType);
            if (!string.IsNullOrEmpty(severity))  q = q.Where(r => r.Severity == severity);
            if (enabled.HasValue)                 q = q.Where(r => r.Enabled == enabled.Value);

            var total = await q.CountAsync();
            var items = await q
                .OrderBy(r => r.Priority).ThenBy(r => r.Name)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(r => new
                {
                    r.Id, r.RulePackId, r.Name, r.Description, r.RuleType,
                    r.Pattern, r.Severity, r.Enabled, r.Priority,
                    r.MetadataJson,
                    r.CreatedAt, r.UpdatedAt, r.CreatedBy, r.UpdatedBy,
                })
                .ToListAsync();

            return Results.Ok(new { total, page, pageSize, items });
        }).WithSummary("List governance rules");

        group.MapPost("/rules", async (
            CreateRuleRequest req,
            NotificationsDbContext db,
            ISmsGovernanceVersioningService vs,
            Microsoft.Extensions.Options.IOptions<SmsGovernanceDynamicOptions> options) =>
        {
            var opts = options.Value;

            if (string.IsNullOrWhiteSpace(req.Name))
                return Results.BadRequest(new { error = "name is required" });
            if (string.IsNullOrWhiteSpace(req.RuleType) || !ValidRuleTypes.Contains(req.RuleType))
                return Results.BadRequest(new { error = $"Invalid ruleType. Allowed: {string.Join(", ", ValidRuleTypes)}" });
            if (string.IsNullOrWhiteSpace(req.Severity) || !ValidSeverities.Contains(req.Severity))
                return Results.BadRequest(new { error = $"Invalid severity. Allowed: {string.Join(", ", ValidSeverities)}" });

            // Validate pattern length
            if (!string.IsNullOrEmpty(req.Pattern) && req.Pattern.Length > opts.MaxPatternLength)
                return Results.BadRequest(new { error = $"Pattern exceeds max length of {opts.MaxPatternLength}" });

            // Validate regex safety for restricted_pattern
            if (req.RuleType == "restricted_pattern")
            {
                if (!opts.AllowRegexRules)
                    return Results.BadRequest(new { error = "Regex rules are disabled by platform configuration" });

                if (string.IsNullOrEmpty(req.Pattern))
                    return Results.BadRequest(new { error = "pattern is required for restricted_pattern rules" });

                var (regexOk, regexError) = ValidateRegexSafety(req.Pattern, opts.RegexTimeoutMs);
                if (!regexOk)
                    return Results.BadRequest(new { error = $"Unsafe or invalid regex: {regexError}" });
            }

            // Validate MetadataJson is parseable JSON
            if (!string.IsNullOrEmpty(req.MetadataJson))
            {
                try { JsonDocument.Parse(req.MetadataJson); }
                catch { return Results.BadRequest(new { error = "metadataJson must be valid JSON" }); }
            }

            // Verify rule pack exists
            var packExists = await db.SmsGovernanceRulePacks.AnyAsync(p => p.Id == req.RulePackId);
            if (!packExists)
                return Results.BadRequest(new { error = "rulePackId does not exist" });

            var rule = new SmsGovernanceRule
            {
                Id           = Guid.NewGuid(),
                RulePackId   = req.RulePackId,
                Name         = req.Name.Trim(),
                Description  = req.Description,
                RuleType     = req.RuleType,
                Pattern      = req.Pattern,
                Severity     = req.Severity,
                Enabled      = req.Enabled ?? true,
                Priority     = req.Priority ?? 100,
                MetadataJson = req.MetadataJson,
                CreatedAt    = DateTime.UtcNow,
                UpdatedAt    = DateTime.UtcNow,
                CreatedBy    = req.RequestedBy,
                UpdatedBy    = req.RequestedBy,
            };
            db.SmsGovernanceRules.Add(rule);
            await db.SaveChangesAsync();
            // LS-NOTIF-SMS-020: immutable version snapshot
            await vs.SnapshotRuleAsync(rule.Id, "created", null, req.RequestedBy);
            return Results.Created($"/v1/admin/sms/governance/rules/{rule.Id}", new { rule.Id });
        }).WithSummary("Create governance rule");

        group.MapPut("/rules/{id:guid}", async (
            Guid id,
            UpdateRuleRequest req,
            NotificationsDbContext db,
            ISmsGovernanceVersioningService vs,
            Microsoft.Extensions.Options.IOptions<SmsGovernanceDynamicOptions> options) =>
        {
            var opts = options.Value;
            var rule = await db.SmsGovernanceRules.FindAsync(id);
            if (rule == null) return Results.NotFound();

            if (!string.IsNullOrEmpty(req.Severity) && !ValidSeverities.Contains(req.Severity))
                return Results.BadRequest(new { error = "Invalid severity" });

            if (!string.IsNullOrEmpty(req.Pattern) && req.Pattern.Length > opts.MaxPatternLength)
                return Results.BadRequest(new { error = $"Pattern exceeds max length of {opts.MaxPatternLength}" });

            if (rule.RuleType == "restricted_pattern" && !string.IsNullOrEmpty(req.Pattern))
            {
                var (ok, err) = ValidateRegexSafety(req.Pattern, opts.RegexTimeoutMs);
                if (!ok) return Results.BadRequest(new { error = $"Unsafe or invalid regex: {err}" });
            }

            if (!string.IsNullOrEmpty(req.MetadataJson))
            {
                try { JsonDocument.Parse(req.MetadataJson); }
                catch { return Results.BadRequest(new { error = "metadataJson must be valid JSON" }); }
            }

            if (!string.IsNullOrWhiteSpace(req.Name))       rule.Name        = req.Name.Trim();
            if (req.Description != null)                     rule.Description = req.Description;
            if (!string.IsNullOrEmpty(req.Pattern))         rule.Pattern     = req.Pattern;
            if (!string.IsNullOrEmpty(req.Severity))        rule.Severity    = req.Severity;
            if (req.Enabled.HasValue)                        rule.Enabled     = req.Enabled.Value;
            if (req.Priority.HasValue)                       rule.Priority    = req.Priority.Value;
            if (req.MetadataJson != null)                    rule.MetadataJson = req.MetadataJson;
            rule.UpdatedAt = DateTime.UtcNow;
            rule.UpdatedBy = req.RequestedBy;

            await db.SaveChangesAsync();
            // LS-NOTIF-SMS-020: immutable version snapshot
            await vs.SnapshotRuleAsync(id, "updated", null, req.RequestedBy);
            return Results.Ok(new { rule.Id, rule.UpdatedAt });
        }).WithSummary("Update governance rule");

        group.MapPost("/rules/{id:guid}/disable", async (
            Guid id, NotificationsDbContext db, ISmsGovernanceVersioningService vs, string? requestedBy) =>
        {
            var rule = await db.SmsGovernanceRules.FindAsync(id);
            if (rule == null) return Results.NotFound();
            rule.Enabled   = false;
            rule.UpdatedAt = DateTime.UtcNow;
            rule.UpdatedBy = requestedBy;
            await db.SaveChangesAsync();
            // LS-NOTIF-SMS-020: immutable version snapshot
            await vs.SnapshotRuleAsync(id, "disabled", null, requestedBy);
            return Results.Ok(new { rule.Id, disabled = true });
        }).WithSummary("Disable governance rule");

        // ── Compliance Profiles ───────────────────────────────────────────────

        group.MapGet("/profiles", async (
            NotificationsDbContext db,
            Guid?  tenantId = null,
            bool?  enabled  = null,
            int    page     = 1,
            int    pageSize = 50) =>
        {
            var q = db.SmsComplianceProfiles.AsNoTracking().AsQueryable();
            if (tenantId.HasValue) q = q.Where(p => p.TenantId == tenantId);
            if (enabled.HasValue)  q = q.Where(p => p.Enabled == enabled.Value);

            var total = await q.CountAsync();
            var items = await q
                .OrderBy(p => p.Name)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(p => new
                {
                    p.Id, p.TenantId, p.Name, p.Description,
                    p.Enabled, p.DefaultRulePackIdsJson, p.EnforcementMode,
                    p.CreatedAt, p.UpdatedAt, p.CreatedBy, p.UpdatedBy,
                    assignmentCount = db.SmsComplianceProfileAssignments
                        .Count(a => a.ProfileId == p.Id && a.Enabled),
                })
                .ToListAsync();

            return Results.Ok(new { total, page, pageSize, items });
        }).WithSummary("List compliance profiles");

        group.MapPost("/profiles", async (
            CreateProfileRequest req,
            NotificationsDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(req.Name))
                return Results.BadRequest(new { error = "name is required" });
            if (!string.IsNullOrEmpty(req.EnforcementMode) && !ValidEnforcementModes.Contains(req.EnforcementMode))
                return Results.BadRequest(new { error = $"Invalid enforcementMode. Allowed: {string.Join(", ", ValidEnforcementModes)}" });
            if (!string.IsNullOrEmpty(req.DefaultRulePackIdsJson))
            {
                try { JsonDocument.Parse(req.DefaultRulePackIdsJson); }
                catch { return Results.BadRequest(new { error = "defaultRulePackIdsJson must be a valid JSON array" }); }
            }

            var profile = new SmsComplianceProfile
            {
                Id                     = Guid.NewGuid(),
                TenantId               = req.TenantId,
                Name                   = req.Name.Trim(),
                Description            = req.Description,
                Enabled                = req.Enabled ?? true,
                DefaultRulePackIdsJson = req.DefaultRulePackIdsJson,
                EnforcementMode        = req.EnforcementMode ?? "standard",
                CreatedAt              = DateTime.UtcNow,
                UpdatedAt              = DateTime.UtcNow,
                CreatedBy              = req.RequestedBy,
                UpdatedBy              = req.RequestedBy,
            };
            db.SmsComplianceProfiles.Add(profile);
            await db.SaveChangesAsync();
            return Results.Created($"/v1/admin/sms/governance/profiles/{profile.Id}", new { profile.Id });
        }).WithSummary("Create compliance profile");

        group.MapPost("/profiles/{profileId:guid}/assignments", async (
            Guid profileId,
            CreateProfileAssignmentRequest req,
            NotificationsDbContext db) =>
        {
            var profileExists = await db.SmsComplianceProfiles.AnyAsync(p => p.Id == profileId);
            if (!profileExists) return Results.NotFound();

            if (!ValidScopes.Contains(req.Scope ?? "tenant"))
                return Results.BadRequest(new { error = $"Invalid scope. Allowed: {string.Join(", ", ValidScopes)}" });

            var assignment = new SmsComplianceProfileAssignment
            {
                Id        = Guid.NewGuid(),
                TenantId  = req.TenantId,
                ProfileId = profileId,
                Scope     = req.Scope ?? "tenant",
                Enabled   = req.Enabled ?? true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            db.SmsComplianceProfileAssignments.Add(assignment);
            await db.SaveChangesAsync();
            return Results.Created(
                $"/v1/admin/sms/governance/profiles/{profileId}/assignments/{assignment.Id}",
                new { assignment.Id });
        }).WithSummary("Assign compliance profile to tenant");

        group.MapPut("/profiles/{id:guid}", async (
            Guid id,
            UpdateProfileRequest req,
            NotificationsDbContext db) =>
        {
            var profile = await db.SmsComplianceProfiles.FindAsync(id);
            if (profile == null) return Results.NotFound();

            if (!string.IsNullOrEmpty(req.EnforcementMode) && !ValidEnforcementModes.Contains(req.EnforcementMode))
                return Results.BadRequest(new { error = "Invalid enforcementMode" });
            if (!string.IsNullOrEmpty(req.DefaultRulePackIdsJson))
            {
                try { JsonDocument.Parse(req.DefaultRulePackIdsJson); }
                catch { return Results.BadRequest(new { error = "defaultRulePackIdsJson must be a valid JSON array" }); }
            }

            if (!string.IsNullOrWhiteSpace(req.Name))                   profile.Name                   = req.Name.Trim();
            if (req.Description != null)                                 profile.Description            = req.Description;
            if (req.Enabled.HasValue)                                    profile.Enabled                = req.Enabled.Value;
            if (!string.IsNullOrEmpty(req.EnforcementMode))             profile.EnforcementMode        = req.EnforcementMode;
            if (req.DefaultRulePackIdsJson != null)                      profile.DefaultRulePackIdsJson = req.DefaultRulePackIdsJson;
            profile.UpdatedAt = DateTime.UtcNow;
            profile.UpdatedBy = req.RequestedBy;

            await db.SaveChangesAsync();
            return Results.Ok(new { profile.Id, profile.UpdatedAt });
        }).WithSummary("Update compliance profile");

        // ── Governance Simulation ─────────────────────────────────────────────

        group.MapPost("/simulate", async (
            SimulateRequest req,
            ISmsGovernanceSimulationService simulationService) =>
        {
            if (string.IsNullOrWhiteSpace(req.RenderedBody))
                return Results.BadRequest(new { error = "renderedBody is required" });
            if (req.RenderedBody.Length > 2000)
                return Results.BadRequest(new { error = "renderedBody must not exceed 2000 characters" });

            var simReq = new SmsGovernanceSimulationRequest
            {
                TenantId              = req.TenantId,
                RenderedBody          = req.RenderedBody,
                TemplateKey           = req.TemplateKey,
                TemplateBody          = req.TemplateBody,
                Variables             = req.Variables,
                ContentClassification = req.ContentClassification,
                Context               = req.Context ?? "content",
                IncludeRuleTrace      = req.IncludeRuleTrace ?? false,
                PersistDecision       = false, // simulation never persists
            };

            var result = await simulationService.SimulateAsync(simReq);
            return Results.Ok(result);
        }).WithSummary("Dry-run governance simulation (no SMS sent)");

        // ── Rule Analytics ────────────────────────────────────────────────────

        group.MapGet("/rule-analytics", async (
            NotificationsDbContext db,
            Guid?   tenantId    = null,
            string? ruleType    = null,
            int     windowHours = 24) =>
        {
            var since = DateTime.UtcNow.AddHours(-windowHours);

            // Pack counts
            var globalPacks = await db.SmsGovernanceRulePacks
                .AsNoTracking()
                .CountAsync(p => p.TenantId == null && p.Enabled && p.Status == "active");

            var tenantPacks = tenantId.HasValue
                ? await db.SmsGovernanceRulePacks
                    .AsNoTracking()
                    .CountAsync(p => p.TenantId == tenantId && p.Enabled && p.Status == "active")
                : 0;

            // Rules by type
            var rulesQuery = db.SmsGovernanceRules.AsNoTracking()
                .Where(r => r.Enabled);
            if (!string.IsNullOrEmpty(ruleType))
                rulesQuery = rulesQuery.Where(r => r.RuleType == ruleType);

            var rulesByType = await rulesQuery
                .GroupBy(r => r.RuleType)
                .Select(g => new { ruleType = g.Key, count = g.Count() })
                .ToListAsync();

            var rulesBySeverity = await rulesQuery
                .GroupBy(r => r.Severity)
                .Select(g => new { severity = g.Key, count = g.Count() })
                .ToListAsync();

            // Profile assignments
            var profileAssignments = tenantId.HasValue
                ? await db.SmsComplianceProfileAssignments
                    .AsNoTracking()
                    .CountAsync(a => a.TenantId == tenantId && a.Enabled)
                : await db.SmsComplianceProfileAssignments
                    .AsNoTracking()
                    .CountAsync(a => a.Enabled);

            return Results.Ok(new
            {
                windowHours,
                since,
                tenantId,
                globalActivePacks     = globalPacks,
                tenantActivePacks     = tenantPacks,
                totalActiveRules      = rulesByType.Sum(r => r.count),
                rulesByType,
                rulesBySeverity,
                activeProfileAssignments = profileAssignments,
            });
        }).WithSummary("Rule analytics and coverage summary");
    }

    // ─── Regex safety validator ───────────────────────────────────────────────

    private static (bool ok, string? error) ValidateRegexSafety(string pattern, int timeoutMs)
    {
        if (pattern.Length > 500)
            return (false, "Pattern too long (max 500 chars)");

        // Detect common catastrophic backtracking patterns
        // E.g. (a+)+, (.*)+, ([a-z]+)+, nested quantifiers
        if (Regex.IsMatch(pattern, @"\(.+[+*]\).+[+*]"))
            return (false, "Pattern contains potentially catastrophic nested quantifiers");

        // Attempt to compile and do a bounded test match
        try
        {
            var rx = new Regex(pattern, RegexOptions.None, TimeSpan.FromMilliseconds(timeoutMs));
            rx.IsMatch("test_probe_string_governance_check_019");
            return (true, null);
        }
        catch (ArgumentException ex)
        {
            return (false, $"Invalid regex: {ex.Message}");
        }
        catch (RegexMatchTimeoutException)
        {
            return (false, "Regex timed out during safety probe — pattern rejected");
        }
    }
}

// ─── Request DTOs ─────────────────────────────────────────────────────────────

internal sealed record CreateRulePackRequest(
    string  Name,
    Guid?   TenantId        = null,
    string? Description     = null,
    string? Status          = null,
    bool?   Enabled         = null,
    string? InheritanceMode = null,
    int?    Priority        = null,
    DateTime? EffectiveFrom = null,
    DateTime? EffectiveTo   = null,
    string? RequestedBy     = null);

internal sealed record UpdateRulePackRequest(
    string?   Name            = null,
    string?   Description     = null,
    string?   Status          = null,
    bool?     Enabled         = null,
    string?   InheritanceMode = null,
    int?      Priority        = null,
    DateTime? EffectiveFrom   = null,
    DateTime? EffectiveTo     = null,
    string?   RequestedBy     = null);

internal sealed record CreateRuleRequest(
    Guid    RulePackId,
    string  Name,
    string  RuleType,
    string  Severity,
    string? Description  = null,
    string? Pattern      = null,
    bool?   Enabled      = null,
    int?    Priority     = null,
    string? MetadataJson = null,
    string? RequestedBy  = null);

internal sealed record UpdateRuleRequest(
    string? Name        = null,
    string? Description = null,
    string? Pattern     = null,
    string? Severity    = null,
    bool?   Enabled     = null,
    int?    Priority    = null,
    string? MetadataJson = null,
    string? RequestedBy = null);

internal sealed record CreateProfileRequest(
    string  Name,
    Guid?   TenantId               = null,
    string? Description            = null,
    bool?   Enabled                = null,
    string? EnforcementMode        = null,
    string? DefaultRulePackIdsJson = null,
    string? RequestedBy            = null);

internal sealed record UpdateProfileRequest(
    string? Name                   = null,
    string? Description            = null,
    bool?   Enabled                = null,
    string? EnforcementMode        = null,
    string? DefaultRulePackIdsJson = null,
    string? RequestedBy            = null);

internal sealed record CreateProfileAssignmentRequest(
    Guid    TenantId,
    string? Scope    = null,
    bool?   Enabled  = null);

internal sealed record SimulateRequest(
    string  RenderedBody,
    Guid?   TenantId              = null,
    string? TemplateKey           = null,
    string? TemplateBody          = null,
    Dictionary<string, string>? Variables = null,
    string? ContentClassification = null,
    string? Context               = null,
    bool?   IncludeRuleTrace      = null);
