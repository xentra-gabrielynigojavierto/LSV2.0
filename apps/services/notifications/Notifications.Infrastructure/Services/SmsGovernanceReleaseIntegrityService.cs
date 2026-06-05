using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notifications.Application.Interfaces;
using Notifications.Application.Options;
using Notifications.Domain;
using Notifications.Infrastructure.Data;

namespace Notifications.Infrastructure.Services;

/// <summary>
/// LS-NOTIF-SMS-021-HARDENING: Read-only integrity and validation service for governance
/// release packages.
///
/// All operations are non-destructive — no state is mutated.
/// No raw phones, credentials, or provider payloads are accessed or returned.
/// </summary>
public sealed class SmsGovernanceReleaseIntegrityService : ISmsGovernanceReleaseIntegrityService
{
    private readonly NotificationsDbContext                _db;
    private readonly SmsGovernanceReleaseManagementOptions _opts;
    private readonly ILogger<SmsGovernanceReleaseIntegrityService> _logger;

    public SmsGovernanceReleaseIntegrityService(
        NotificationsDbContext                              db,
        IOptions<SmsGovernanceReleaseManagementOptions>    opts,
        ILogger<SmsGovernanceReleaseIntegrityService>      logger)
    {
        _db     = db;
        _opts   = opts.Value;
        _logger = logger;
    }

    // ── Item validation ───────────────────────────────────────────────────────

    public async Task<ReleaseValidationReport> ValidateReleaseItemsAsync(
        Guid releaseId, CancellationToken ct = default)
    {
        var items = await _db.SmsGovernanceReleaseItems
            .AsNoTracking()
            .Where(i => i.ReleasePackageId == releaseId)
            .ToListAsync(ct);

        var issues = new List<string>();

        // Max items cap
        if (items.Count > _opts.MaxReleaseItems)
            issues.Add($"Item count {items.Count} exceeds maximum {_opts.MaxReleaseItems}.");

        // Entity type + action type validation
        foreach (var item in items)
        {
            if (!ReleaseEntityTypes.All.Contains(item.EntityType))
                issues.Add($"Item {item.Id}: unknown entity type '{item.EntityType}'.");

            if (!ReleaseActionTypes.All.Contains(item.ActionType))
                issues.Add($"Item {item.Id}: unknown action type '{item.ActionType}'.");
        }

        // Duplicate entity + action detection
        var duplicates = items
            .GroupBy(i => new { i.EntityType, i.EntityId, i.ActionType })
            .Where(g => g.Count() > 1)
            .ToList();

        foreach (var dup in duplicates)
            issues.Add($"Duplicate entry: entity type '{dup.Key.EntityType}', " +
                       $"id '{dup.Key.EntityId}', action '{dup.Key.ActionType}' " +
                       $"appears {dup.Count()} times.");

        _logger.LogDebug(
            "Release {ReleaseId}: item validation complete — {Count} item(s), {Issues} issue(s)",
            releaseId, items.Count, issues.Count);

        return new ReleaseValidationReport(issues.Count == 0, issues, items.Count);
    }

    // ── Audit trail integrity ─────────────────────────────────────────────────

    public async Task<ReleaseIntegrityReport> ValidateReleaseIntegrityAsync(
        Guid releaseId, CancellationToken ct = default)
    {
        var pkg = await _db.SmsGovernanceReleasePackages
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == releaseId, ct);

        if (pkg is null)
            return new ReleaseIntegrityReport(false, ["Release package not found."], 0);

        var events = await _db.SmsGovernanceReleaseAuditEvents
            .AsNoTracking()
            .Where(e => e.ReleasePackageId == releaseId)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync(ct);

        var issues = new List<string>();
        var eventTypes = events.Select(e => e.EventType).ToHashSet();

        // Every release must have a 'created' event
        if (!eventTypes.Contains(ReleaseAuditEventTypes.Created))
            issues.Add("Audit trail missing 'created' event.");

        // Active releases must have an 'activated' event
        if (pkg.ReleaseState == ReleaseStates.Active
            && !eventTypes.Contains(ReleaseAuditEventTypes.Activated))
            issues.Add("Release is 'active' but audit trail has no 'activated' event.");

        // Rejected releases must have a 'rejected' event
        if (pkg.ReleaseState == ReleaseStates.Rejected
            && !eventTypes.Contains(ReleaseAuditEventTypes.Rejected))
            issues.Add("Release is 'rejected' but audit trail has no 'rejected' event.");

        // Archived releases must have an 'archived' event
        if (pkg.ReleaseState == ReleaseStates.Archived
            && !eventTypes.Contains(ReleaseAuditEventTypes.Archived))
            issues.Add("Release is 'archived' but audit trail has no 'archived' event.");

        // Approved releases should have approval evidence
        if (pkg.ReleaseState is ReleaseStates.Approved or ReleaseStates.Active or ReleaseStates.Scheduled
            && _opts.RequireApprovalForActivation
            && !eventTypes.Contains(ReleaseAuditEventTypes.Approved))
            issues.Add($"Release is '{pkg.ReleaseState}' but audit trail has no 'approved' event " +
                       "(RequireApprovalForActivation is enabled).");

        // Scheduled releases should have a 'scheduled' event
        if (pkg.ReleaseState == ReleaseStates.Scheduled
            && !eventTypes.Contains(ReleaseAuditEventTypes.Scheduled))
            issues.Add("Release is 'scheduled' but audit trail has no 'scheduled' event.");

        // ActivationFailed with zero audit failures is suspicious
        if (pkg.ReleaseState == ReleaseStates.ActivationFailed
            && !eventTypes.Contains(ReleaseAuditEventTypes.ActivationFailed))
            issues.Add("Release is 'activation_failed' but audit trail has no 'activation_failed' event.");

        // Audit event ordering sanity: CreatedAt must be non-decreasing
        DateTime? prev = null;
        foreach (var ev in events)
        {
            if (prev.HasValue && ev.CreatedAt < prev.Value)
            {
                issues.Add($"Audit event {ev.Id} ('{ev.EventType}') has CreatedAt " +
                           $"{ev.CreatedAt:O} which is before previous event {prev.Value:O}.");
                break; // Report first out-of-order event only
            }
            prev = ev.CreatedAt;
        }

        _logger.LogDebug(
            "Release {ReleaseId}: integrity check complete — {EventCount} event(s), {Issues} issue(s)",
            releaseId, events.Count, issues.Count);

        return new ReleaseIntegrityReport(issues.Count == 0, issues, events.Count);
    }

    // ── Activation lock status ────────────────────────────────────────────────

    public async Task<ReleaseActivationLockStatus> GetActivationLockStatusAsync(
        Guid releaseId, CancellationToken ct = default)
    {
        var pkg = await _db.SmsGovernanceReleasePackages
            .AsNoTracking()
            .Where(p => p.Id == releaseId)
            .Select(p => new
            {
                p.ActivationLockId,
                p.ActivationLockAcquiredAt,
                p.ActivationLockExpiresAt,
                p.ActivationLockedBy,
            })
            .FirstOrDefaultAsync(ct);

        if (pkg is null)
            return new ReleaseActivationLockStatus(false, null, null, null, null, false);

        var now       = DateTime.UtcNow;
        var isLocked  = pkg.ActivationLockId.HasValue;
        var isExpired = isLocked
            && pkg.ActivationLockExpiresAt.HasValue
            && pkg.ActivationLockExpiresAt.Value < now;

        return new ReleaseActivationLockStatus(
            isLocked && !isExpired,
            pkg.ActivationLockId,
            pkg.ActivationLockAcquiredAt,
            pkg.ActivationLockExpiresAt,
            pkg.ActivationLockedBy,
            isExpired);
    }
}
