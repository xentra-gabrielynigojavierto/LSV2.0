using Notifications.Application.Interfaces;
using Notifications.Infrastructure.Data;

namespace Notifications.Api.Endpoints;

/// <summary>
/// LS-NOTIF-SMS-020: Admin endpoints for governance versioning, import/export, and analytics.
///
/// All endpoints require PlatformAdmin authorization.
/// No raw phone numbers, credentials, or message content are accepted or returned.
///
/// Routes (prefix: /v1/admin/sms/governance):
///   GET  /rules/{id}/versions
///   POST /rules/{id}/rollback
///   GET  /rule-packs/{id}/versions
///   POST /rule-packs/{id}/rollback
///   POST /import/validate
///   POST /import
///   GET  /export
///   GET  /effectiveness
///   GET  /match-analytics
///   GET  /false-positive-candidates
///   GET  /pack-effectiveness
/// </summary>
public static class SmsGovernanceLifecycleEndpoints
{
    public static void MapSmsGovernanceLifecycleEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/v1/admin/sms/governance")
            .RequireAuthorization("PlatformAdmin")
            .WithTags("SMS Governance Lifecycle");

        // ── Rule Version History ──────────────────────────────────────────────

        group.MapGet("/rules/{id:guid}/versions", async (
            Guid id,
            ISmsGovernanceVersioningService versioning) =>
        {
            var versions = await versioning.GetRuleVersionsAsync(id);
            return Results.Ok(new { ruleId = id, total = versions.Count, versions });
        }).WithSummary("Get rule version history");

        // ── Rule Rollback ─────────────────────────────────────────────────────

        group.MapPost("/rules/{id:guid}/rollback", async (
            Guid id,
            RollbackRequest req,
            ISmsGovernanceVersioningService versioning) =>
        {
            if (req.VersionNumber <= 0)
                return Results.BadRequest(new { error = "versionNumber must be a positive integer" });

            var result = await versioning.RollbackRuleAsync(
                id, req.VersionNumber, req.RequestedBy, req.Reason);

            return result.Success
                ? Results.Ok(new
                {
                    ruleId = id,
                    result.RestoredToVersion,
                    result.NewVersionNumber,
                    message = $"Rule rolled back to version {result.RestoredToVersion}; new version is {result.NewVersionNumber}",
                })
                : Results.BadRequest(new { error = result.ErrorMessage });
        }).WithSummary("Roll back rule to a previous version");

        // ── Rule-Pack Version History ─────────────────────────────────────────

        group.MapGet("/rule-packs/{id:guid}/versions", async (
            Guid id,
            ISmsGovernanceVersioningService versioning) =>
        {
            var versions = await versioning.GetRulePackVersionsAsync(id);
            return Results.Ok(new { rulePackId = id, total = versions.Count, versions });
        }).WithSummary("Get rule-pack version history");

        // ── Rule-Pack Rollback ────────────────────────────────────────────────

        group.MapPost("/rule-packs/{id:guid}/rollback", async (
            Guid id,
            RollbackRequest req,
            ISmsGovernanceVersioningService versioning) =>
        {
            if (req.VersionNumber <= 0)
                return Results.BadRequest(new { error = "versionNumber must be a positive integer" });

            var result = await versioning.RollbackRulePackAsync(
                id, req.VersionNumber, req.RequestedBy, req.Reason);

            return result.Success
                ? Results.Ok(new
                {
                    rulePackId = id,
                    result.RestoredToVersion,
                    result.NewVersionNumber,
                    message = $"Rule pack rolled back to version {result.RestoredToVersion}; new version is {result.NewVersionNumber}",
                })
                : Results.BadRequest(new { error = result.ErrorMessage });
        }).WithSummary("Roll back rule pack to a previous version");

        // ── Import Validate (dry-run) ─────────────────────────────────────────

        group.MapPost("/import/validate", async (
            GovernanceImportRequest req,
            ISmsGovernanceImportService importService) =>
        {
            // Force dry-run regardless of request field
            var dryRunReq = new GovernanceImportRequest
            {
                Bundles    = req.Bundles,
                DryRun     = true,
                RequestedBy = req.RequestedBy,
            };
            var result = await importService.ValidateImportAsync(dryRunReq);
            return result.IsValid
                ? Results.Ok(new { valid = true, message = "Import payload is valid", errors = result.Errors })
                : Results.UnprocessableEntity(new { valid = false, errors = result.Errors });
        }).WithSummary("Validate bulk governance import (dry-run, no writes)");

        // ── Import ────────────────────────────────────────────────────────────

        group.MapPost("/import", async (
            GovernanceImportRequest req,
            ISmsGovernanceImportService importService) =>
        {
            var result = await importService.ImportAsync(req);

            if (!result.IsValid)
                return Results.UnprocessableEntity(new { valid = false, errors = result.Errors });

            return result.Persisted
                ? Results.Ok(new
                {
                    valid           = true,
                    persisted       = true,
                    bundlesImported = result.BundlesImported,
                    rulesImported   = result.RulesImported,
                    message         = $"Imported {result.BundlesImported} rule pack(s) with {result.RulesImported} rule(s)",
                })
                : Results.Ok(new { valid = true, persisted = false, message = "Dry-run validation passed — no records written" });
        }).WithSummary("Bulk import governance rules (transactional, all-or-nothing)");

        // ── Export ────────────────────────────────────────────────────────────

        group.MapGet("/export", async (
            ISmsGovernanceImportService importService,
            Guid?   tenantId         = null,
            Guid?   rulePackId       = null,
            string? status           = null,
            string? ruleType         = null,
            string? severity         = null,
            bool    includeProfiles  = false) =>
        {
            var query = new GovernanceExportQuery
            {
                TenantId        = tenantId,
                RulePackId      = rulePackId,
                Status          = status,
                RuleType        = ruleType,
                Severity        = severity,
                IncludeProfiles = includeProfiles,
            };
            var export = await importService.ExportAsync(query);
            return Results.Ok(export);
        }).WithSummary("Export governance rules as JSON");

        // ── Rule Effectiveness Analytics ──────────────────────────────────────

        group.MapGet("/effectiveness", async (
            ISmsGovernanceAnalyticsService analytics,
            Guid?     tenantId          = null,
            Guid?     rulePackId        = null,
            Guid?     ruleId            = null,
            string?   ruleType          = null,
            string?   severity          = null,
            DateTime? from              = null,
            DateTime? to                = null,
            bool      includeSimulation = true,
            bool      includeLive       = true,
            int       page              = 1,
            int       pageSize          = 50) =>
        {
            var query = new GovernanceAnalyticsQuery
            {
                TenantId          = tenantId,
                RulePackId        = rulePackId,
                RuleId            = ruleId,
                RuleType          = ruleType,
                Severity          = severity,
                From              = from,
                To                = to,
                IncludeSimulation = includeSimulation,
                IncludeLive       = includeLive,
                Page              = Math.Max(1, page),
                PageSize          = Math.Clamp(pageSize, 1, 200),
            };
            var (rows, total) = await analytics.GetRuleEffectivenessAsync(query);
            return Results.Ok(new { total, page, pageSize, rows });
        }).WithSummary("Rule effectiveness analytics (aggregate, no message content)");

        // ── Match Analytics ───────────────────────────────────────────────────

        group.MapGet("/match-analytics", async (
            ISmsGovernanceAnalyticsService analytics,
            Guid?     tenantId          = null,
            Guid?     rulePackId        = null,
            Guid?     ruleId            = null,
            string?   ruleType          = null,
            string?   severity          = null,
            DateTime? from              = null,
            DateTime? to                = null,
            bool      includeSimulation = true,
            bool      includeLive       = true) =>
        {
            var query = new GovernanceAnalyticsQuery
            {
                TenantId          = tenantId,
                RulePackId        = rulePackId,
                RuleId            = ruleId,
                RuleType          = ruleType,
                Severity          = severity,
                From              = from,
                To                = to,
                IncludeSimulation = includeSimulation,
                IncludeLive       = includeLive,
            };
            var rows = await analytics.GetRuleMatchAnalyticsAsync(query);
            return Results.Ok(new { total = rows.Count, rows });
        }).WithSummary("Time-series governance rule match analytics");

        // ── False-Positive Candidates ─────────────────────────────────────────

        group.MapGet("/false-positive-candidates", async (
            ISmsGovernanceAnalyticsService analytics,
            Guid?     tenantId  = null,
            Guid?     rulePackId = null,
            DateTime? from      = null,
            DateTime? to        = null) =>
        {
            var query = new GovernanceAnalyticsQuery
            {
                TenantId  = tenantId,
                RulePackId = rulePackId,
                From      = from,
                To        = to,
            };
            var candidates = await analytics.GetFalsePositiveCandidatesAsync(query);
            return Results.Ok(new { total = candidates.Count, candidates });
        }).WithSummary("Governance rules that may be false positives (heuristic analysis)");

        // ── Pack Effectiveness ────────────────────────────────────────────────

        group.MapGet("/pack-effectiveness", async (
            ISmsGovernanceAnalyticsService analytics,
            Guid?     tenantId  = null,
            DateTime? from      = null,
            DateTime? to        = null,
            int       page      = 1,
            int       pageSize  = 50) =>
        {
            var query = new GovernanceAnalyticsQuery
            {
                TenantId = tenantId,
                From     = from,
                To       = to,
                Page     = Math.Max(1, page),
                PageSize = Math.Clamp(pageSize, 1, 200),
            };
            var (rows, total) = await analytics.GetPackEffectivenessAsync(query);
            return Results.Ok(new { total, page, pageSize, rows });
        }).WithSummary("Rule pack effectiveness analytics");
    }
}

// ─── Request types ────────────────────────────────────────────────────────────

/// <summary>LS-NOTIF-SMS-020: Rollback request body.</summary>
file sealed record RollbackRequest(int VersionNumber, string? RequestedBy, string? Reason);
