using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notifications.Application.Interfaces;
using Notifications.Application.Options;
using Notifications.Domain;
using Notifications.Infrastructure.Data;

namespace Notifications.Infrastructure.Services;

/// <summary>
/// LS-NOTIF-SMS-021: Approval workflow orchestration.
///
/// Stages are ordered (1, 2, …). Stage N starts only when Stage N-1 is approved.
/// Rejection at any stage rejects the release. Final-stage approval moves release to approved.
/// All decisions are append-only — records are never deleted.
///
/// LS-NOTIF-SMS-021-HARDENING: Role enforcement.
/// When EnforceApprovalRoles = true, the actor's declared role must match the stage's
/// ApproverRole. PlatformAdmin bypass is allowed when AllowPlatformAdminApprovalFallback = true.
/// </summary>
public sealed class SmsGovernanceApprovalWorkflowService : ISmsGovernanceApprovalWorkflowService
{
    private readonly NotificationsDbContext                     _db;
    private readonly SmsGovernanceReleaseManagementOptions      _opts;
    private readonly ILogger<SmsGovernanceApprovalWorkflowService> _logger;

    private static readonly JsonSerializerOptions _json = new() { WriteIndented = false };

    public SmsGovernanceApprovalWorkflowService(
        NotificationsDbContext                           db,
        IOptions<SmsGovernanceReleaseManagementOptions> opts,
        ILogger<SmsGovernanceApprovalWorkflowService>   logger)
    {
        _db     = db;
        _opts   = opts.Value;
        _logger = logger;
    }

    // ── Create initial approval requests ─────────────────────────────────────

    public async Task CreateApprovalRequestsAsync(Guid releaseId, CancellationToken ct = default)
    {
        var stages = ParseApprovalStages();
        if (stages.Count == 0)
        {
            _logger.LogWarning(
                "No approval stages configured — approval request creation skipped for release {Id}",
                releaseId);
            return;
        }

        // Only create the first stage; subsequent stages are unlocked after prior stage approval
        var firstStage = stages.OrderBy(s => s.Stage).First();
        var now        = DateTime.UtcNow;

        var request = new SmsGovernanceApprovalRequest
        {
            Id               = Guid.NewGuid(),
            ReleasePackageId = releaseId,
            ApprovalStage    = firstStage.Stage,
            ApproverRole     = firstStage.ApproverRole,
            RequiredApprovals = firstStage.RequiredApprovals,
            Status           = ApprovalRequestStatuses.Pending,
            RequestedAt      = now,
            CreatedAt        = now,
            UpdatedAt        = now,
        };
        _db.SmsGovernanceApprovalRequests.Add(request);

        _db.SmsGovernanceReleaseAuditEvents.Add(new SmsGovernanceReleaseAuditEvent
        {
            Id               = Guid.NewGuid(),
            ReleasePackageId = releaseId,
            EventType        = ReleaseAuditEventTypes.ApprovalRequested,
            Actor            = "system",
            MetadataJson     = JsonSerializer.Serialize(new
            {
                stage        = firstStage.Stage,
                approverRole = firstStage.ApproverRole,
                required     = firstStage.RequiredApprovals,
            }, _json),
            CreatedAt = now,
        });

        await _db.SaveChangesAsync(ct);
    }

    // ── Approve ───────────────────────────────────────────────────────────────

    public async Task<ReleaseOperationResult> ApproveAsync(
        Guid releaseId, ApproveReleaseRequest request, CancellationToken ct = default)
    {
        var pkg = await _db.SmsGovernanceReleasePackages
            .FirstOrDefaultAsync(p => p.Id == releaseId, ct);
        if (pkg is null)
            return Fail("Release package not found.");

        if (pkg.ReleaseState != ReleaseStates.PendingReview)
            return Fail($"Release is not pending review (current state: '{pkg.ReleaseState}').");

        // Find the lowest-numbered pending request (current active stage)
        var pendingRequest = await _db.SmsGovernanceApprovalRequests
            .Where(r => r.ReleasePackageId == releaseId && r.Status == ApprovalRequestStatuses.Pending)
            .OrderBy(r => r.ApprovalStage)
            .FirstOrDefaultAsync(ct);

        if (pendingRequest is null)
            return Fail("No pending approval request found for this release.");

        // ── LS-NOTIF-SMS-021-HARDENING: Role enforcement ──────────────────────
        var roleCheckResult = CheckApproverRole(releaseId, pendingRequest.ApproverRole, request.DecidedByRole, request.DecidedBy, ct);
        if (roleCheckResult is not null)
            return roleCheckResult;

        var now = DateTime.UtcNow;

        // Record the approval decision
        _db.SmsGovernanceApprovalDecisions.Add(new SmsGovernanceApprovalDecision
        {
            Id                = Guid.NewGuid(),
            ApprovalRequestId = pendingRequest.Id,
            ReleasePackageId  = releaseId,
            Decision          = ApprovalDecisions.Approve,
            DecisionReason    = request.Reason,
            DecidedBy         = request.DecidedBy,
            DecidedByRole     = request.DecidedByRole,
            CreatedAt         = now,
        });

        // Count total approvals for this request
        var approvalsSoFar = await _db.SmsGovernanceApprovalDecisions
            .CountAsync(d => d.ApprovalRequestId == pendingRequest.Id
                          && d.Decision == ApprovalDecisions.Approve, ct);
        // +1 for the record we just added (not yet saved)
        var totalApprovals = approvalsSoFar + 1;

        if (totalApprovals < pendingRequest.RequiredApprovals)
        {
            // Need more approvals for this stage
            pendingRequest.UpdatedAt = now;
            await _db.SaveChangesAsync(ct);
            return new ReleaseOperationResult(true);
        }

        // Stage is fully approved — mark it resolved
        pendingRequest.Status     = ApprovalRequestStatuses.Approved;
        pendingRequest.ResolvedAt = now;
        pendingRequest.UpdatedAt  = now;

        // Check if there's a next stage
        var allStages  = ParseApprovalStages().OrderBy(s => s.Stage).ToList();
        var nextStage  = allStages.FirstOrDefault(s => s.Stage > pendingRequest.ApprovalStage);

        if (nextStage is not null)
        {
            // Create next-stage request
            _db.SmsGovernanceApprovalRequests.Add(new SmsGovernanceApprovalRequest
            {
                Id               = Guid.NewGuid(),
                ReleasePackageId = releaseId,
                ApprovalStage    = nextStage.Stage,
                ApproverRole     = nextStage.ApproverRole,
                RequiredApprovals = nextStage.RequiredApprovals,
                Status           = ApprovalRequestStatuses.Pending,
                RequestedAt      = now,
                CreatedAt        = now,
                UpdatedAt        = now,
            });

            _db.SmsGovernanceReleaseAuditEvents.Add(new SmsGovernanceReleaseAuditEvent
            {
                Id               = Guid.NewGuid(),
                ReleasePackageId = releaseId,
                EventType        = ReleaseAuditEventTypes.ApprovalRequested,
                Actor            = "system",
                MetadataJson     = JsonSerializer.Serialize(new
                {
                    stage        = nextStage.Stage,
                    approverRole = nextStage.ApproverRole,
                    required     = nextStage.RequiredApprovals,
                }, _json),
                CreatedAt = now,
            });
        }
        else
        {
            // Final stage approved — move release to approved
            var prev         = pkg.ReleaseState;
            pkg.ReleaseState = ReleaseStates.Approved;
            pkg.UpdatedAt    = now;
            pkg.UpdatedBy    = request.DecidedBy;

            _db.SmsGovernanceReleaseAuditEvents.Add(new SmsGovernanceReleaseAuditEvent
            {
                Id               = Guid.NewGuid(),
                ReleasePackageId = releaseId,
                EventType        = ReleaseAuditEventTypes.Approved,
                PreviousState    = prev,
                NewState         = ReleaseStates.Approved,
                Actor            = request.DecidedBy,
                Reason           = request.Reason,
                CreatedAt        = now,
            });
        }

        await _db.SaveChangesAsync(ct);
        return new ReleaseOperationResult(true);
    }

    // ── Reject ────────────────────────────────────────────────────────────────

    public async Task<ReleaseOperationResult> RejectAsync(
        Guid releaseId, RejectReleaseRequest request, CancellationToken ct = default)
    {
        var pkg = await _db.SmsGovernanceReleasePackages
            .FirstOrDefaultAsync(p => p.Id == releaseId, ct);
        if (pkg is null)
            return Fail("Release package not found.");

        if (pkg.ReleaseState != ReleaseStates.PendingReview)
            return Fail($"Release is not pending review (current state: '{pkg.ReleaseState}').");

        var pendingRequest = await _db.SmsGovernanceApprovalRequests
            .Where(r => r.ReleasePackageId == releaseId && r.Status == ApprovalRequestStatuses.Pending)
            .OrderBy(r => r.ApprovalStage)
            .FirstOrDefaultAsync(ct);

        // ── LS-NOTIF-SMS-021-HARDENING: Role enforcement ──────────────────────
        if (pendingRequest is not null)
        {
            var roleCheckResult = CheckApproverRole(
                releaseId, pendingRequest.ApproverRole, request.DecidedByRole, request.DecidedBy, ct);
            if (roleCheckResult is not null)
                return roleCheckResult;
        }

        var now = DateTime.UtcNow;

        if (pendingRequest is not null)
        {
            _db.SmsGovernanceApprovalDecisions.Add(new SmsGovernanceApprovalDecision
            {
                Id                = Guid.NewGuid(),
                ApprovalRequestId = pendingRequest.Id,
                ReleasePackageId  = releaseId,
                Decision          = ApprovalDecisions.Reject,
                DecisionReason    = request.Reason,
                DecidedBy         = request.DecidedBy,
                DecidedByRole     = request.DecidedByRole,
                CreatedAt         = now,
            });

            pendingRequest.Status     = ApprovalRequestStatuses.Rejected;
            pendingRequest.ResolvedAt = now;
            pendingRequest.UpdatedAt  = now;
        }

        // Cancel all remaining pending requests
        var remainingPending = await _db.SmsGovernanceApprovalRequests
            .Where(r => r.ReleasePackageId == releaseId && r.Status == ApprovalRequestStatuses.Pending)
            .ToListAsync(ct);
        foreach (var r in remainingPending)
        {
            r.Status     = ApprovalRequestStatuses.Cancelled;
            r.ResolvedAt = now;
            r.UpdatedAt  = now;
        }

        var prev         = pkg.ReleaseState;
        pkg.ReleaseState = ReleaseStates.Rejected;
        pkg.RejectedAt   = now;
        pkg.UpdatedAt    = now;
        pkg.UpdatedBy    = request.DecidedBy;

        _db.SmsGovernanceReleaseAuditEvents.Add(new SmsGovernanceReleaseAuditEvent
        {
            Id               = Guid.NewGuid(),
            ReleasePackageId = releaseId,
            EventType        = ReleaseAuditEventTypes.Rejected,
            PreviousState    = prev,
            NewState         = ReleaseStates.Rejected,
            Actor            = request.DecidedBy,
            Reason           = request.Reason,
            CreatedAt        = now,
        });

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Release {ReleaseId} rejected by {Actor}", releaseId, request.DecidedBy);
        return new ReleaseOperationResult(true);
    }

    // ── Pending approvals queue ───────────────────────────────────────────────

    public async Task<IReadOnlyList<PendingApprovalDto>> GetPendingApprovalsAsync(
        ApprovalQuery query, CancellationToken ct = default)
    {
        var q = _db.SmsGovernanceApprovalRequests
            .AsNoTracking()
            .Where(r => r.Status == ApprovalRequestStatuses.Pending);

        if (!string.IsNullOrEmpty(query.ApproverRole))
            q = q.Where(r => r.ApproverRole == query.ApproverRole);

        var page     = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var requests = await q
            .OrderBy(r => r.RequestedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var releaseIds = requests.Select(r => r.ReleasePackageId).Distinct().ToList();
        var packages   = await _db.SmsGovernanceReleasePackages
            .AsNoTracking()
            .Where(p => releaseIds.Contains(p.Id))
            .ToListAsync(ct);
        var pkgMap = packages.ToDictionary(p => p.Id);

        var requestIds = requests.Select(r => r.Id).ToList();
        var approvalCounts = await _db.SmsGovernanceApprovalDecisions
            .AsNoTracking()
            .Where(d => requestIds.Contains(d.ApprovalRequestId)
                     && d.Decision == ApprovalDecisions.Approve)
            .GroupBy(d => d.ApprovalRequestId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var countMap = approvalCounts.ToDictionary(x => x.Key, x => x.Count);

        return requests
            .Where(r => pkgMap.ContainsKey(r.ReleasePackageId))
            .Select(r =>
            {
                var pkg = pkgMap[r.ReleasePackageId];
                return new PendingApprovalDto(
                    pkg.Id, pkg.Name, pkg.Description, pkg.TenantId,
                    r.ApprovalStage, r.ApproverRole, r.RequiredApprovals,
                    countMap.TryGetValue(r.Id, out var c) ? c : 0,
                    r.RequestedAt);
            })
            .ToList();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// LS-NOTIF-SMS-021-HARDENING: Checks whether the actor's declared role is allowed
    /// to act on the given stage. Returns a failure result if blocked, or null if allowed.
    /// Records an audit event if blocked.
    /// </summary>
    private ReleaseOperationResult? CheckApproverRole(
        Guid releaseId, string stageRole, string? actorRole, string? actor, CancellationToken ct)
    {
        if (!_opts.EnforceApprovalRoles)
            return null;   // enforcement disabled — allow all

        // PlatformAdmin fallback
        if (_opts.AllowPlatformAdminApprovalFallback
            && string.Equals(actorRole, "PlatformAdmin", StringComparison.OrdinalIgnoreCase))
            return null;   // PlatformAdmin always allowed

        // Exact role match
        if (string.Equals(actorRole, stageRole, StringComparison.OrdinalIgnoreCase))
            return null;   // roles match — allowed

        // Role mismatch — record audit event synchronously (already in a service scope)
        _logger.LogWarning(
            "Release {ReleaseId}: approval role mismatch — stage requires '{Required}', " +
            "actor '{Actor}' declared '{Actual}'",
            releaseId, stageRole, actor, actorRole);

        _db.SmsGovernanceReleaseAuditEvents.Add(new SmsGovernanceReleaseAuditEvent
        {
            Id               = Guid.NewGuid(),
            ReleasePackageId = releaseId,
            EventType        = ReleaseAuditEventTypes.ApprovalRoleMismatch,
            Actor            = actor,
            Reason           = $"Stage requires '{stageRole}', actor declared '{actorRole}'.",
            MetadataJson     = System.Text.Json.JsonSerializer.Serialize(new
            {
                stageRole,
                actorRole,
                actor,
            }, _json),
            CreatedAt = DateTime.UtcNow,
        });

        // SaveChanges will be called by the caller's catch — we must save now since
        // the caller returns early after this check.
        _db.SaveChanges();

        return Fail($"Approval role mismatch: stage requires '{stageRole}', " +
                    $"actor declared '{actorRole ?? "none"}'. " +
                    (_opts.AllowPlatformAdminApprovalFallback
                        ? "PlatformAdmin role would bypass this check."
                        : "No fallback role is configured."));
    }

    private sealed record ApprovalStageConfig(int Stage, string ApproverRole, int RequiredApprovals);

    private List<ApprovalStageConfig> ParseApprovalStages()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_opts.DefaultApprovalStagesJson))
                return [];

            var raw = JsonSerializer.Deserialize<List<JsonElement>>(_opts.DefaultApprovalStagesJson);
            if (raw is null) return [];

            return raw
                .Select(e => new ApprovalStageConfig(
                    e.TryGetProperty("stage",            out var s) ? s.GetInt32()    : 1,
                    e.TryGetProperty("approverRole",     out var r) ? r.GetString()!  : "PlatformAdmin",
                    e.TryGetProperty("requiredApprovals",out var a) ? a.GetInt32()    : 1))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to parse SmsGovernanceReleaseManagement:DefaultApprovalStagesJson — using empty");
            return [];
        }
    }

    private static ReleaseOperationResult Fail(string msg) => new(false, msg);
}
