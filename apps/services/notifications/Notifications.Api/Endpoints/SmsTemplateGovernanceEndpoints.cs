using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using BuildingBlocks.Authorization;
using Notifications.Api.Authorization;
using Notifications.Application.Interfaces;
using Notifications.Domain;
using Notifications.Infrastructure.Data;
using Notifications.Infrastructure.Services;

namespace Notifications.Api.Endpoints;

/// <summary>
/// LS-NOTIF-SMS-018: SMS Template Governance admin endpoints.
///
/// All endpoints require PlatformAdmin authorization.
/// Responses never expose: raw phone numbers, credentials, SettingsJson,
/// CredentialsJson, provider payloads, webhook URLs, or secrets.
/// </summary>
public static class SmsTemplateGovernanceEndpoints
{
    public static IEndpointRouteBuilder MapSmsTemplateGovernanceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/admin/sms/templates")
            .WithTags("Admin — SMS Template Governance")
            .RequireAuthorization(Policies.AdminOnly);

        // ── Template List ─────────────────────────────────────────────────────

        group.MapGet("/", async (
            NotificationsDbContext db,
            string? tenantId,
            string? status,
            string? classification,
            bool?   enabled,
            int     page     = 1,
            int     pageSize = 50) =>
        {
            var q = db.SmsTemplates.AsQueryable();

            if (!string.IsNullOrEmpty(tenantId) && Guid.TryParse(tenantId, out var tid))
                q = q.Where(t => t.TenantId == tid || t.TenantId == null);
            else if (tenantId == "global")
                q = q.Where(t => t.TenantId == null);

            if (!string.IsNullOrEmpty(status))         q = q.Where(t => t.Status == status);
            if (!string.IsNullOrEmpty(classification)) q = q.Where(t => t.ContentClassification == classification);
            if (enabled.HasValue)                      q = q.Where(t => t.Enabled == enabled.Value);

            var total = await q.CountAsync();
            var items = await q
                .OrderBy(t => t.TemplateKey)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(t => new
                {
                    t.Id, t.TenantId, t.TemplateKey, t.Name, t.Description, t.Category,
                    t.Status, t.CurrentVersion, t.LatestApprovedVersion,
                    t.ContentClassification, t.RequiresApproval, t.Enabled,
                    t.CreatedAt, t.UpdatedAt, t.CreatedBy, t.UpdatedBy,
                })
                .ToListAsync();

            return Results.Ok(new { total, page, pageSize, items });
        });

        // ── Template Detail ───────────────────────────────────────────────────

        group.MapGet("/{id:guid}", async (Guid id, NotificationsDbContext db) =>
        {
            var t = await db.SmsTemplates.AsNoTracking()
                .Where(t => t.Id == id)
                .Select(t => new
                {
                    t.Id, t.TenantId, t.TemplateKey, t.Name, t.Description, t.Category,
                    t.Status, t.CurrentVersion, t.LatestApprovedVersion,
                    t.ContentClassification, t.RequiresApproval, t.Enabled,
                    t.CreatedAt, t.UpdatedAt, t.CreatedBy, t.UpdatedBy,
                })
                .FirstOrDefaultAsync();

            return t is null ? Results.NotFound() : Results.Ok(t);
        });

        // ── Create Template ───────────────────────────────────────────────────

        group.MapPost("/", async (
            CreateSmsTemplateRequest request,
            ISmsTemplateGovernanceService svc) =>
        {
            if (string.IsNullOrWhiteSpace(request.TemplateKey))
                return Results.BadRequest(new { error = "TemplateKey is required" });
            if (string.IsNullOrWhiteSpace(request.Name))
                return Results.BadRequest(new { error = "Name is required" });

            var id = await svc.CreateTemplateAsync(request);
            return Results.Created($"/v1/admin/sms/templates/{id}", new { id });
        });

        // ── Update Template ───────────────────────────────────────────────────

        group.MapPut("/{id:guid}", async (Guid id, UpdateSmsTemplateRequest request, ISmsTemplateGovernanceService svc) =>
        {
            request.Id = id;
            var updated = await svc.UpdateTemplateAsync(request);
            return updated
                ? Results.Ok(new { id, updatedAt = DateTime.UtcNow })
                : Results.NotFound();
        });

        // ── Archive Template ──────────────────────────────────────────────────

        group.MapPost("/{id:guid}/archive", async (Guid id, ArchiveTemplateBody body, ISmsTemplateGovernanceService svc) =>
        {
            var ok = await svc.ArchiveTemplateAsync(id, body.RequestedBy);
            return ok ? Results.Ok(new { id, status = "archived" }) : Results.NotFound();
        });

        // ── Submit for Review ─────────────────────────────────────────────────

        group.MapPost("/{id:guid}/submit-review", async (Guid id, ReviewActionBody body, ISmsTemplateGovernanceService svc) =>
        {
            var ok = await svc.SubmitForReviewAsync(id, body.RequestedBy);
            if (!ok) return Results.BadRequest(new { error = "Template not found or not in draft/rejected state" });
            return Results.Ok(new { id, status = "pending_review" });
        });

        // ── Approve Version ───────────────────────────────────────────────────

        group.MapPost("/{id:guid}/approve", async (Guid id, ReviewActionBody body, ISmsTemplateGovernanceService svc) =>
        {
            if (string.IsNullOrWhiteSpace(body.RequestedBy))
                return Results.BadRequest(new { error = "RequestedBy (approver) is required" });

            var ok = await svc.ApproveVersionAsync(id, body.RequestedBy);
            if (!ok) return Results.BadRequest(new { error = "Template not found or not in pending_review state" });
            return Results.Ok(new { id, status = "approved", approvedBy = body.RequestedBy, approvedAt = DateTime.UtcNow });
        });

        // ── Reject Version ────────────────────────────────────────────────────

        group.MapPost("/{id:guid}/reject", async (Guid id, RejectVersionBody body, ISmsTemplateGovernanceService svc) =>
        {
            if (string.IsNullOrWhiteSpace(body.RequestedBy))
                return Results.BadRequest(new { error = "RequestedBy is required" });
            if (string.IsNullOrWhiteSpace(body.Reason))
                return Results.BadRequest(new { error = "Reason is required" });

            var ok = await svc.RejectVersionAsync(id, body.RequestedBy, body.Reason);
            if (!ok) return Results.BadRequest(new { error = "Template not found or not in pending_review state" });
            return Results.Ok(new { id, status = "rejected" });
        });

        // ── List Versions ─────────────────────────────────────────────────────

        group.MapGet("/{id:guid}/versions", async (Guid id, NotificationsDbContext db) =>
        {
            var versions = await db.SmsTemplateVersions
                .AsNoTracking()
                .Where(v => v.TemplateId == id)
                .OrderByDescending(v => v.VersionNumber)
                .Select(v => new
                {
                    v.Id, v.TemplateId, v.VersionNumber, v.TemplateBody,
                    v.VariableSchemaJson, v.ContentClassification, v.ApprovalStatus,
                    v.ApprovedBy, v.ApprovedAt, v.RejectionReason,
                    v.CreatedAt, v.CreatedBy,
                })
                .ToListAsync();

            return Results.Ok(new { templateId = id, total = versions.Count, items = versions });
        });

        // ── Create Version ────────────────────────────────────────────────────

        group.MapPost("/{id:guid}/versions", async (
            Guid id,
            CreateSmsTemplateVersionRequest request,
            ISmsTemplateGovernanceService svc) =>
        {
            if (string.IsNullOrWhiteSpace(request.TemplateBody))
                return Results.BadRequest(new { error = "TemplateBody is required" });

            request.TemplateId = id;
            var versionId = await svc.CreateVersionAsync(request);
            return Results.Created($"/v1/admin/sms/templates/{id}/versions/{versionId}", new { id = versionId });
        });

        // ── Governance Decision Audit ─────────────────────────────────────────

        group.MapGet("/governance-decisions", async (
            NotificationsDbContext db,
            string? tenantId,
            Guid?   templateId,
            string? decisionType,
            string? reasonCode,
            string? from,
            string? to,
            int     page     = 1,
            int     pageSize = 50) =>
        {
            var q = db.SmsTemplateGovernanceDecisions.AsQueryable();

            if (!string.IsNullOrEmpty(tenantId) && Guid.TryParse(tenantId, out var tid))
                q = q.Where(d => d.TenantId == tid);
            if (templateId.HasValue)
                q = q.Where(d => d.TemplateId == templateId);
            if (!string.IsNullOrEmpty(decisionType))
                q = q.Where(d => d.DecisionType == decisionType);
            if (!string.IsNullOrEmpty(reasonCode))
                q = q.Where(d => d.ReasonCode == reasonCode);
            if (DateTime.TryParse(from, out var dtFrom))
                q = q.Where(d => d.CreatedAt >= dtFrom);
            if (DateTime.TryParse(to, out var dtTo))
                q = q.Where(d => d.CreatedAt <= dtTo);

            var total = await q.CountAsync();
            var items = await q
                .OrderByDescending(d => d.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(d => new
                {
                    d.Id, d.NotificationId, d.AttemptId, d.TemplateId, d.TemplateVersionId,
                    d.TenantId, d.DecisionType, d.ReasonCode, d.ContentClassification,
                    d.VariableValidationPassed, d.DecisionMetadataJson, d.CreatedAt,
                })
                .ToListAsync();

            return Results.Ok(new { total, page, pageSize, items });
        });

        // ── Dry-run Evaluate Test ─────────────────────────────────────────────

        group.MapPost("/evaluate-test", async (
            EvaluateTestRequest request,
            ISmsTemplateGovernanceService svc) =>
        {
            if (string.IsNullOrWhiteSpace(request.RenderedBody) && string.IsNullOrWhiteSpace(request.TemplateKey))
                return Results.BadRequest(new { error = "Either RenderedBody or TemplateKey is required" });

            var evalRequest = new SmsTemplateGovernanceRequest
            {
                TenantId        = request.TenantId ?? Guid.Empty,
                TemplateKey     = request.TemplateKey,
                RenderedBody    = request.RenderedBody,
                VariablesUsed   = request.Variables,
                NotificationId  = null, // dry-run — no real notification
                IsRetry         = false,
                NowUtc          = DateTime.UtcNow,
            };

            var result = await svc.EvaluateAsync(evalRequest);

            return Results.Ok(new
            {
                result.DecisionType,
                result.ReasonCode,
                result.ShouldProceed,
                result.ShouldBlock,
                result.TemplateId,
                result.TemplateVersionId,
                result.Classification,
                result.VariableValidationPassed,
                result.ValidationErrors,
                // Never echo back RenderedBody in dry-run response (may contain PII tokens)
            });
        });

        return app;
    }

    // ─── Request body types ───────────────────────────────────────────────────

    private sealed record ArchiveTemplateBody(string? RequestedBy);
    private sealed record ReviewActionBody(string? RequestedBy);
    private sealed record RejectVersionBody(string? RequestedBy, string? Reason);

    private sealed class EvaluateTestRequest
    {
        public Guid?  TenantId     { get; set; }
        public string? TemplateKey  { get; set; }
        public string? RenderedBody { get; set; }
        public Dictionary<string, string>? Variables { get; set; }
    }
}
