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
/// LS-NOTIF-SMS-021: Central release orchestration service.
///
/// Governs state transitions, item management, scheduling, activation, and archiving.
/// Activation is transactional — failure leaves existing governance intact.
/// No SMS is sent, no external APIs are called, no raw phones are stored.
///
/// LS-NOTIF-SMS-021-HARDENING:
/// - Activation concurrency locking (ActivationLockId / ExpiresAt).
/// - Retry/backoff tracking (ActivationAttemptCount / NextActivationRetryAt).
/// - Duplicate item detection within AddReleaseItemAsync.
/// </summary>
public sealed class SmsGovernanceReleaseService : ISmsGovernanceReleaseService
{
    private readonly NotificationsDbContext                     _db;
    private readonly SmsGovernanceReleaseManagementOptions      _opts;
    private readonly ISmsGovernanceVersioningService            _versioning;
    private readonly ISmsGovernanceApprovalWorkflowService      _approval;
    private readonly ILogger<SmsGovernanceReleaseService>       _logger;

    private static readonly JsonSerializerOptions _json = new() { WriteIndented = false };

    public SmsGovernanceReleaseService(
        NotificationsDbContext                              db,
        IOptions<SmsGovernanceReleaseManagementOptions>    opts,
        ISmsGovernanceVersioningService                    versioning,
        ISmsGovernanceApprovalWorkflowService              approval,
        ILogger<SmsGovernanceReleaseService>               logger)
    {
        _db         = db;
        _opts       = opts.Value;
        _versioning = versioning;
        _approval   = approval;
        _logger     = logger;
    }

    // ── Create ────────────────────────────────────────────────────────────────

    public async Task<ReleasePackageDto> CreateReleaseAsync(
        CreateReleaseRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Release name is required.", nameof(request));

        if (!ReleaseTypes.All.Contains(request.ReleaseType))
            throw new ArgumentException($"Invalid release type: {request.ReleaseType}", nameof(request));

        var now = DateTime.UtcNow;
        var pkg = new SmsGovernanceReleasePackage
        {
            Id           = Guid.NewGuid(),
            TenantId     = request.TenantId,
            Name         = request.Name.Trim(),
            Description  = request.Description?.Trim(),
            ReleaseState = ReleaseStates.Draft,
            ReleaseType  = request.ReleaseType,
            CreatedAt    = now,
            UpdatedAt    = now,
            CreatedBy    = request.RequestedBy,
            UpdatedBy    = request.RequestedBy,
        };

        _db.SmsGovernanceReleasePackages.Add(pkg);

        AddAuditEvent(pkg.Id, ReleaseAuditEventTypes.Created, null, ReleaseStates.Draft,
            request.RequestedBy, null, null);

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Release {ReleaseId} '{Name}' created by {Actor}", pkg.Id, pkg.Name, request.RequestedBy);

        return MapPackage(pkg, 0);
    }

    // ── Get / List ────────────────────────────────────────────────────────────

    public async Task<ReleaseDetailDto?> GetReleaseAsync(Guid releaseId, CancellationToken ct = default)
    {
        var pkg = await _db.SmsGovernanceReleasePackages
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == releaseId, ct);
        if (pkg is null) return null;

        var items = await _db.SmsGovernanceReleaseItems
            .AsNoTracking()
            .Where(i => i.ReleasePackageId == releaseId)
            .OrderBy(i => i.CreatedAt)
            .ToListAsync(ct);

        var requests = await _db.SmsGovernanceApprovalRequests
            .AsNoTracking()
            .Where(r => r.ReleasePackageId == releaseId)
            .OrderBy(r => r.ApprovalStage)
            .ToListAsync(ct);

        var requestIds = requests.Select(r => r.Id).ToList();
        var decisions = await _db.SmsGovernanceApprovalDecisions
            .AsNoTracking()
            .Where(d => requestIds.Contains(d.ApprovalRequestId))
            .OrderBy(d => d.CreatedAt)
            .ToListAsync(ct);

        var decisionsByRequest = decisions.GroupBy(d => d.ApprovalRequestId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var approvalDtos = requests.Select(r =>
        {
            var decs = decisionsByRequest.TryGetValue(r.Id, out var dl) ? dl : [];
            return new ApprovalRequestDto(
                r.Id, r.ReleasePackageId, r.ApprovalStage, r.ApproverRole,
                r.RequiredApprovals, r.Status, r.RequestedAt, r.ResolvedAt,
                decs.Count(d => d.Decision == ApprovalDecisions.Approve),
                decs.Select(d => new ApprovalDecisionDto(
                    d.Id, d.Decision, d.DecisionReason, d.DecidedBy, d.DecidedByRole, d.CreatedAt))
                .ToList());
        }).ToList();

        return new ReleaseDetailDto(
            MapPackage(pkg, items.Count),
            items.Select(MapItem).ToList(),
            approvalDtos);
    }

    public async Task<PaginatedReleaseResult> ListReleasesAsync(
        ReleaseListQuery query, CancellationToken ct = default)
    {
        var q = _db.SmsGovernanceReleasePackages.AsNoTracking().AsQueryable();

        if (query.TenantId.HasValue)
            q = q.Where(p => p.TenantId == query.TenantId);
        if (!string.IsNullOrEmpty(query.State))
            q = q.Where(p => p.ReleaseState == query.State);
        if (!string.IsNullOrEmpty(query.ReleaseType))
            q = q.Where(p => p.ReleaseType == query.ReleaseType);

        var total = await q.CountAsync(ct);

        var page     = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var packages = await q
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var ids = packages.Select(p => p.Id).ToList();
        var counts = await _db.SmsGovernanceReleaseItems
            .AsNoTracking()
            .Where(i => ids.Contains(i.ReleasePackageId))
            .GroupBy(i => i.ReleasePackageId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var countMap = counts.ToDictionary(x => x.Key, x => x.Count);

        return new PaginatedReleaseResult(
            packages.Select(p => MapPackage(p, countMap.TryGetValue(p.Id, out var c) ? c : 0)).ToList(),
            total, page, pageSize);
    }

    // ── Items ─────────────────────────────────────────────────────────────────

    public async Task<ReleaseItemDto> AddReleaseItemAsync(
        Guid releaseId, AddReleaseItemRequest request, CancellationToken ct = default)
    {
        var pkg = await RequirePackageAsync(releaseId, ct);
        RequireEditable(pkg);

        if (!ReleaseEntityTypes.All.Contains(request.EntityType))
            throw new ArgumentException($"Invalid entity type: {request.EntityType}");
        if (!ReleaseActionTypes.All.Contains(request.ActionType))
            throw new ArgumentException($"Invalid action type: {request.ActionType}");

        var existing = await _db.SmsGovernanceReleaseItems
            .Where(i => i.ReleasePackageId == releaseId)
            .ToListAsync(ct);

        if (existing.Count >= _opts.MaxReleaseItems)
            throw new InvalidOperationException(
                $"Release already has {existing.Count} items (max {_opts.MaxReleaseItems}).");

        // ── LS-NOTIF-SMS-021-HARDENING: Duplicate entity+action detection ─────
        var duplicate = existing.FirstOrDefault(i =>
            i.EntityId   == request.EntityId   &&
            i.EntityType == request.EntityType &&
            i.ActionType == request.ActionType);

        if (duplicate is not null)
            throw new InvalidOperationException(
                $"Release already contains entity '{request.EntityType}' id '{request.EntityId}' " +
                $"with action '{request.ActionType}'. Duplicate entries are not allowed.");

        var now  = DateTime.UtcNow;
        var item = new SmsGovernanceReleaseItem
        {
            Id                  = Guid.NewGuid(),
            ReleasePackageId    = releaseId,
            EntityType          = request.EntityType,
            EntityId            = request.EntityId,
            EntityVersionNumber = request.EntityVersionNumber,
            ActionType          = request.ActionType,
            EntitySnapshotJson  = request.EntitySnapshotJson,
            CreatedAt           = now,
            CreatedBy           = request.RequestedBy,
        };
        _db.SmsGovernanceReleaseItems.Add(item);

        pkg.UpdatedAt = now;
        pkg.UpdatedBy = request.RequestedBy;

        AddAuditEvent(releaseId, ReleaseAuditEventTypes.ItemAdded, null, null,
            request.RequestedBy, null,
            JsonSerializer.Serialize(new { item.EntityType, item.EntityId, item.ActionType }, _json));

        await _db.SaveChangesAsync(ct);
        return MapItem(item);
    }

    public async Task<ReleaseOperationResult> RemoveReleaseItemAsync(
        Guid releaseId, Guid itemId, string requestedBy, CancellationToken ct = default)
    {
        var pkg = await RequirePackageAsync(releaseId, ct);
        RequireEditable(pkg);

        var item = await _db.SmsGovernanceReleaseItems
            .FirstOrDefaultAsync(i => i.Id == itemId && i.ReleasePackageId == releaseId, ct);
        if (item is null)
            return new ReleaseOperationResult(false, "Item not found.");

        _db.SmsGovernanceReleaseItems.Remove(item);
        pkg.UpdatedAt = DateTime.UtcNow;
        pkg.UpdatedBy = requestedBy;

        AddAuditEvent(releaseId, ReleaseAuditEventTypes.ItemRemoved, null, null, requestedBy, null,
            JsonSerializer.Serialize(new { item.EntityType, item.EntityId, item.ActionType }, _json));

        await _db.SaveChangesAsync(ct);
        return new ReleaseOperationResult(true);
    }

    // ── State Transitions ─────────────────────────────────────────────────────

    public async Task<ReleaseOperationResult> SubmitForReviewAsync(
        Guid releaseId, string requestedBy, CancellationToken ct = default)
    {
        var pkg = await RequirePackageAsync(releaseId, ct);

        if (pkg.ReleaseState != ReleaseStates.Draft && pkg.ReleaseState != ReleaseStates.Rejected)
            return Fail($"Cannot submit release in state '{pkg.ReleaseState}' for review.");

        var itemCount = await _db.SmsGovernanceReleaseItems
            .CountAsync(i => i.ReleasePackageId == releaseId, ct);
        if (itemCount == 0)
            return Fail("Release must have at least one item before submission.");

        var now  = DateTime.UtcNow;
        var prev = pkg.ReleaseState;
        pkg.ReleaseState = _opts.RequireApprovalForActivation
            ? ReleaseStates.PendingReview
            : ReleaseStates.Approved;
        pkg.UpdatedAt = now;
        pkg.UpdatedBy = requestedBy;

        AddAuditEvent(releaseId, ReleaseAuditEventTypes.SubmittedForReview,
            prev, pkg.ReleaseState, requestedBy, null, null);

        await _db.SaveChangesAsync(ct);

        if (_opts.RequireApprovalForActivation)
            await _approval.CreateApprovalRequestsAsync(releaseId, ct);

        _logger.LogInformation("Release {ReleaseId} submitted for review by {Actor}", releaseId, requestedBy);
        return new ReleaseOperationResult(true);
    }

    public async Task<ReleaseOperationResult> ScheduleActivationAsync(
        Guid releaseId, DateTime activateAtUtc, string requestedBy, CancellationToken ct = default)
    {
        if (!_opts.AllowScheduledActivation)
            return Fail("Scheduled activation is disabled by configuration.");

        if (activateAtUtc <= DateTime.UtcNow)
            return Fail("Scheduled activation date must be in the future.");

        var pkg = await RequirePackageAsync(releaseId, ct);
        if (pkg.ReleaseState != ReleaseStates.Approved)
            return Fail($"Only approved releases can be scheduled. Current state: '{pkg.ReleaseState}'.");

        var prev         = pkg.ReleaseState;
        pkg.ReleaseState          = ReleaseStates.Scheduled;
        pkg.ScheduledActivationAt = activateAtUtc;
        pkg.UpdatedAt             = DateTime.UtcNow;
        pkg.UpdatedBy             = requestedBy;

        AddAuditEvent(releaseId, ReleaseAuditEventTypes.Scheduled,
            prev, ReleaseStates.Scheduled, requestedBy, null,
            JsonSerializer.Serialize(new { scheduledAt = activateAtUtc }, _json));

        await _db.SaveChangesAsync(ct);
        return new ReleaseOperationResult(true);
    }

    public async Task<ReleaseOperationResult> ActivateAsync(
        Guid releaseId, string requestedBy, CancellationToken ct = default)
    {
        if (!_opts.AllowImmediateActivation)
            return Fail("Immediate activation is disabled by configuration.");

        var pkg = await RequirePackageAsync(releaseId, ct);

        if (pkg.ReleaseState != ReleaseStates.Approved &&
            pkg.ReleaseState != ReleaseStates.Scheduled &&
            pkg.ReleaseState != ReleaseStates.ActivationFailed)
            return Fail($"Release in state '{pkg.ReleaseState}' cannot be activated.");

        // ── LS-NOTIF-SMS-021-HARDENING: Acquire activation lock ───────────────
        var lockAcquired = await TryAcquireActivationLockAsync(pkg, requestedBy, ct);
        if (!lockAcquired)
        {
            AddAuditEvent(releaseId, ReleaseAuditEventTypes.ActivationLockFailed,
                null, null, requestedBy, "Another activation is already in progress.", null);
            await _db.SaveChangesAsync(ct);
            return Fail("Another activation is already in progress for this release.");
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var items = await _db.SmsGovernanceReleaseItems
                .Where(i => i.ReleasePackageId == releaseId)
                .ToListAsync(ct);

            await ApplyReleaseItemsAsync(pkg, items, requestedBy, ct);

            var prev          = pkg.ReleaseState;
            pkg.ReleaseState  = ReleaseStates.Active;
            pkg.ActivatedAt   = DateTime.UtcNow;
            pkg.UpdatedAt     = DateTime.UtcNow;
            pkg.UpdatedBy     = requestedBy;

            // Clear retry/lock state on success
            pkg.ActivationAttemptCount      = 0;
            pkg.LastActivationAttemptAt     = DateTime.UtcNow;
            pkg.NextActivationRetryAt       = null;
            pkg.LastActivationFailureReason = null;
            ReleaseActivationLock(pkg);

            AddAuditEvent(releaseId, ReleaseAuditEventTypes.Activated,
                prev, ReleaseStates.Active, requestedBy, null,
                JsonSerializer.Serialize(new { itemCount = items.Count }, _json));

            AddAuditEvent(releaseId, ReleaseAuditEventTypes.ActivationLockReleased,
                null, null, requestedBy, "Activation succeeded.", null);

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            _logger.LogInformation("Release {ReleaseId} activated by {Actor}", releaseId, requestedBy);
            return new ReleaseOperationResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Activation failed for release {ReleaseId}", releaseId);
            try { await tx.RollbackAsync(ct); } catch { /* ignore rollback errors */ }

            // ── LS-NOTIF-SMS-021-HARDENING: Record failure + schedule retry ───
            try
            {
                var prev              = pkg.ReleaseState;
                var attemptCount      = pkg.ActivationAttemptCount + 1;
                var now               = DateTime.UtcNow;
                var failReason        = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;

                pkg.ActivationAttemptCount      = attemptCount;
                pkg.LastActivationAttemptAt     = now;
                pkg.LastActivationFailureReason = failReason;
                ReleaseActivationLock(pkg);

                if (attemptCount >= _opts.ActivationRetryLimit)
                {
                    // Terminal failure — no more retries
                    pkg.ReleaseState      = ReleaseStates.ActivationFailed;
                    pkg.NextActivationRetryAt = null;
                    pkg.UpdatedAt         = now;
                    pkg.UpdatedBy         = requestedBy;

                    AddAuditEvent(releaseId, ReleaseAuditEventTypes.ActivationFailed,
                        prev, ReleaseStates.ActivationFailed, requestedBy,
                        $"Terminal failure after {attemptCount} attempt(s): {failReason}", null);

                    AddAuditEvent(releaseId, ReleaseAuditEventTypes.ActivationLockReleased,
                        null, null, requestedBy, "Terminal activation failure.", null);

                    _logger.LogError(
                        "Release {ReleaseId} permanently marked activation_failed after {Count} attempts",
                        releaseId, attemptCount);
                }
                else
                {
                    // Schedule retry with linear backoff
                    var backoffMinutes    = _opts.ActivationRetryBackoffMinutes * attemptCount;
                    var retryAt           = now.AddMinutes(backoffMinutes);
                    pkg.ReleaseState      = ReleaseStates.ActivationFailed;
                    pkg.NextActivationRetryAt = retryAt;
                    pkg.UpdatedAt         = now;
                    pkg.UpdatedBy         = requestedBy;

                    AddAuditEvent(releaseId, ReleaseAuditEventTypes.ActivationFailed,
                        prev, ReleaseStates.ActivationFailed, requestedBy,
                        $"Attempt {attemptCount}/{_opts.ActivationRetryLimit}: {failReason}", null);

                    AddAuditEvent(releaseId, ReleaseAuditEventTypes.ActivationRetryScheduled,
                        null, null, requestedBy, null,
                        JsonSerializer.Serialize(new
                        {
                            attemptCount,
                            retryLimit    = _opts.ActivationRetryLimit,
                            retryAt       = retryAt.ToString("O"),
                            backoffMinutes,
                        }, _json));

                    AddAuditEvent(releaseId, ReleaseAuditEventTypes.ActivationLockReleased,
                        null, null, requestedBy, $"Retry scheduled at {retryAt:O}.", null);

                    _logger.LogWarning(
                        "Release {ReleaseId} activation failed (attempt {Count}/{Limit}), " +
                        "retry scheduled at {RetryAt}",
                        releaseId, attemptCount, _opts.ActivationRetryLimit, retryAt);
                }

                await _db.SaveChangesAsync(ct);
            }
            catch (Exception markEx)
            {
                _logger.LogError(markEx,
                    "Failed to record activation failure for release {ReleaseId}", releaseId);
            }

            return Fail($"Activation failed: {ex.Message}");
        }
    }

    public async Task<ReleaseOperationResult> ArchiveAsync(
        Guid releaseId, string requestedBy, string? reason, CancellationToken ct = default)
    {
        var pkg = await RequirePackageAsync(releaseId, ct);

        if (ReleaseStates.IsTerminal(pkg.ReleaseState))
            return Fail($"Release is already in terminal state '{pkg.ReleaseState}'.");

        var prev         = pkg.ReleaseState;
        pkg.ReleaseState = ReleaseStates.Archived;
        pkg.ArchivedAt   = DateTime.UtcNow;
        pkg.UpdatedAt    = DateTime.UtcNow;
        pkg.UpdatedBy    = requestedBy;

        AddAuditEvent(releaseId, ReleaseAuditEventTypes.Archived,
            prev, ReleaseStates.Archived, requestedBy, reason, null);

        // Cancel any pending approval requests
        var pendingRequests = await _db.SmsGovernanceApprovalRequests
            .Where(r => r.ReleasePackageId == releaseId
                     && r.Status == ApprovalRequestStatuses.Pending)
            .ToListAsync(ct);
        var now = DateTime.UtcNow;
        foreach (var req in pendingRequests)
        {
            req.Status     = ApprovalRequestStatuses.Cancelled;
            req.ResolvedAt = now;
            req.UpdatedAt  = now;
        }

        await _db.SaveChangesAsync(ct);
        return new ReleaseOperationResult(true);
    }

    // ── Audit Trail ───────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<ReleaseAuditEventDto>> GetAuditTrailAsync(
        Guid releaseId, CancellationToken ct = default)
    {
        var events = await _db.SmsGovernanceReleaseAuditEvents
            .AsNoTracking()
            .Where(e => e.ReleasePackageId == releaseId)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync(ct);

        return events.Select(e => new ReleaseAuditEventDto(
            e.Id, e.ReleasePackageId, e.EventType, e.PreviousState, e.NewState,
            e.Actor, e.Reason, e.MetadataJson, e.CreatedAt)).ToList();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<SmsGovernanceReleasePackage> RequirePackageAsync(
        Guid releaseId, CancellationToken ct)
    {
        var pkg = await _db.SmsGovernanceReleasePackages
            .FirstOrDefaultAsync(p => p.Id == releaseId, ct);
        if (pkg is null)
            throw new KeyNotFoundException($"Release package {releaseId} not found.");
        return pkg;
    }

    private static void RequireEditable(SmsGovernanceReleasePackage pkg)
    {
        if (!ReleaseStates.IsEditable(pkg.ReleaseState))
            throw new InvalidOperationException(
                $"Release in state '{pkg.ReleaseState}' cannot be edited.");
    }

    /// <summary>
    /// LS-NOTIF-SMS-021-HARDENING: Attempt to acquire an exclusive activation lock.
    /// Returns true when the lock was acquired, false when another non-expired lock is held.
    /// Lock expiry is checked inline — stale locks are forcibly expired.
    /// </summary>
    private async Task<bool> TryAcquireActivationLockAsync(
        SmsGovernanceReleasePackage pkg, string requestedBy, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        // A lock is considered held when ActivationLockId is set AND not expired
        if (pkg.ActivationLockId.HasValue
            && pkg.ActivationLockExpiresAt.HasValue
            && pkg.ActivationLockExpiresAt.Value > now)
        {
            _logger.LogWarning(
                "Release {ReleaseId}: activation lock held by '{LockedBy}', expires {Expires:O}",
                pkg.Id, pkg.ActivationLockedBy, pkg.ActivationLockExpiresAt);
            return false;
        }

        // No lock or stale lock — acquire
        var lockId          = Guid.NewGuid();
        pkg.ActivationLockId         = lockId;
        pkg.ActivationLockAcquiredAt = now;
        pkg.ActivationLockExpiresAt  = now.AddMinutes(_opts.ActivationLockTimeoutMinutes);
        pkg.ActivationLockedBy       = requestedBy;

        AddAuditEvent(pkg.Id, ReleaseAuditEventTypes.ActivationLockAcquired,
            null, null, requestedBy,
            $"Lock {lockId} acquired, expires {pkg.ActivationLockExpiresAt:O}.", null);

        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Clears all activation lock fields.</summary>
    private static void ReleaseActivationLock(SmsGovernanceReleasePackage pkg)
    {
        pkg.ActivationLockId         = null;
        pkg.ActivationLockAcquiredAt = null;
        pkg.ActivationLockExpiresAt  = null;
        pkg.ActivationLockedBy       = null;
    }

    private void AddAuditEvent(
        Guid releaseId, string eventType,
        string? prevState, string? newState,
        string? actor, string? reason, string? metadataJson)
    {
        _db.SmsGovernanceReleaseAuditEvents.Add(new SmsGovernanceReleaseAuditEvent
        {
            Id               = Guid.NewGuid(),
            ReleasePackageId = releaseId,
            EventType        = eventType,
            PreviousState    = prevState,
            NewState         = newState,
            Actor            = actor,
            Reason           = reason,
            MetadataJson     = metadataJson,
            CreatedAt        = DateTime.UtcNow,
        });
    }

    private async Task ApplyReleaseItemsAsync(
        SmsGovernanceReleasePackage pkg,
        IReadOnlyList<SmsGovernanceReleaseItem> items,
        string requestedBy,
        CancellationToken ct)
    {
        foreach (var item in items.Where(i => i.ActionType == ReleaseActionTypes.Activate))
        {
            switch (item.EntityType)
            {
                case ReleaseEntityTypes.RulePack:
                    await ActivateRulePackAsync(item, requestedBy, ct);
                    break;

                case ReleaseEntityTypes.Rule:
                    await ActivateRuleAsync(item, requestedBy, ct);
                    break;

                case ReleaseEntityTypes.ComplianceProfile:
                    await ActivateComplianceProfileAsync(item, requestedBy, ct);
                    break;

                default:
                    _logger.LogDebug(
                        "Release {ReleaseId}: item entity type '{Type}' — no activation mutator, skipping.",
                        pkg.Id, item.EntityType);
                    break;
            }
        }
    }

    private async Task ActivateRulePackAsync(
        SmsGovernanceReleaseItem item, string requestedBy, CancellationToken ct)
    {
        var pack = await _db.SmsGovernanceRulePacks
            .FirstOrDefaultAsync(p => p.Id == item.EntityId, ct);
        if (pack is null)
        {
            _logger.LogWarning(
                "Release item references rule pack {Id} which no longer exists — skipping.", item.EntityId);
            return;
        }

        if (pack.Status != "active")
        {
            pack.Status    = "active";
            pack.UpdatedAt = DateTime.UtcNow;
            await _versioning.SnapshotRulePackAsync(
                pack.Id, "activated", "governance release activation", requestedBy, includeRules: false, ct);
        }
    }

    private async Task ActivateRuleAsync(
        SmsGovernanceReleaseItem item, string requestedBy, CancellationToken ct)
    {
        var rule = await _db.SmsGovernanceRules
            .FirstOrDefaultAsync(r => r.Id == item.EntityId, ct);
        if (rule is null)
        {
            _logger.LogWarning(
                "Release item references rule {Id} which no longer exists — skipping.", item.EntityId);
            return;
        }

        if (!rule.Enabled)
        {
            rule.Enabled   = true;
            rule.UpdatedAt = DateTime.UtcNow;
            await _versioning.SnapshotRuleAsync(
                rule.Id, "activated", "governance release activation", requestedBy, ct);
        }
    }

    private async Task ActivateComplianceProfileAsync(
        SmsGovernanceReleaseItem item, string requestedBy, CancellationToken ct)
    {
        var profile = await _db.SmsComplianceProfiles
            .FirstOrDefaultAsync(p => p.Id == item.EntityId, ct);
        if (profile is null)
        {
            _logger.LogWarning(
                "Release item references compliance profile {Id} which no longer exists — skipping.", item.EntityId);
            return;
        }

        if (!profile.Enabled)
        {
            profile.Enabled   = true;
            profile.UpdatedAt = DateTime.UtcNow;
            _logger.LogInformation(
                "Compliance profile {Id} enabled by release activation (actor={Actor})", item.EntityId, requestedBy);
        }
    }

    private static ReleasePackageDto MapPackage(SmsGovernanceReleasePackage p, int itemCount) =>
        new(p.Id, p.TenantId, p.Name, p.Description, p.ReleaseState, p.ReleaseType,
            p.ScheduledActivationAt, p.ActivatedAt, p.RejectedAt, p.ArchivedAt,
            p.CreatedAt, p.UpdatedAt, p.CreatedBy, p.UpdatedBy, itemCount);

    private static ReleaseItemDto MapItem(SmsGovernanceReleaseItem i) =>
        new(i.Id, i.ReleasePackageId, i.EntityType, i.EntityId, i.EntityVersionNumber,
            i.ActionType, i.CreatedAt, i.CreatedBy);

    private static ReleaseOperationResult Fail(string msg) => new(false, msg);
}
