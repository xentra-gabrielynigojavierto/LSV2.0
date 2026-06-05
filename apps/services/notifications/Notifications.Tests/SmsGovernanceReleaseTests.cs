using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Notifications.Application.Interfaces;
using Notifications.Application.Options;
using Notifications.Domain;
using Notifications.Infrastructure.Data;
using Notifications.Infrastructure.Services;
using Xunit;

namespace Notifications.Tests;

/// <summary>
/// LS-NOTIF-SMS-021-HARDENING — Governance release hardening tests.
///
/// Covers:
///   1. Approval role enforcement blocks mismatched role.
///   2. PlatformAdmin fallback bypasses role mismatch.
///   3. Duplicate entity+action rejected by AddReleaseItemAsync.
///   4. Concurrent activation lock returns fail result.
/// </summary>
public class SmsGovernanceReleaseTests : IDisposable
{
    private readonly NotificationsDbContext _db;

    public SmsGovernanceReleaseTests()
    {
        var dbOpts = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new NotificationsDbContext(dbOpts);
    }

    public void Dispose() => _db.Dispose();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private SmsGovernanceApprovalWorkflowService BuildApprovalSvc(
        SmsGovernanceReleaseManagementOptions? opts = null)
    {
        opts ??= DefaultHardeningOpts();
        return new SmsGovernanceApprovalWorkflowService(
            _db,
            Options.Create(opts),
            NullLogger<SmsGovernanceApprovalWorkflowService>.Instance);
    }

    private SmsGovernanceReleaseService BuildReleaseSvc(
        SmsGovernanceReleaseManagementOptions? opts = null)
    {
        opts ??= DefaultHardeningOpts();
        var approvalSvc = BuildApprovalSvc(opts);
        var versioningSvc = new StubVersioningService();
        return new SmsGovernanceReleaseService(
            _db,
            Options.Create(opts),
            versioningSvc,
            approvalSvc,
            NullLogger<SmsGovernanceReleaseService>.Instance);
    }

    private static SmsGovernanceReleaseManagementOptions DefaultHardeningOpts() => new()
    {
        Enabled                            = true,
        RequireApprovalForActivation       = true,
        AllowImmediateActivation           = true,
        EnforceApprovalRoles               = true,
        AllowPlatformAdminApprovalFallback = true,
        ActivationRetryLimit               = 3,
        ActivationRetryBackoffMinutes      = 10,
        ActivationLockTimeoutMinutes       = 10,
        DefaultApprovalStagesJson          =
            """[{"stage":1,"approverRole":"ComplianceReviewer","requiredApprovals":1}]""",
    };

    private async Task<SmsGovernanceReleasePackage> CreateAndSubmitRelease()
    {
        var pkg = new SmsGovernanceReleasePackage
        {
            Id           = Guid.NewGuid(),
            Name         = "Test Release",
            ReleaseState = ReleaseStates.Draft,
            ReleaseType  = ReleaseTypes.MixedGovernance,
            CreatedAt    = DateTime.UtcNow,
            UpdatedAt    = DateTime.UtcNow,
        };
        _db.SmsGovernanceReleasePackages.Add(pkg);

        var item = new SmsGovernanceReleaseItem
        {
            Id               = Guid.NewGuid(),
            ReleasePackageId = pkg.Id,
            EntityType       = ReleaseEntityTypes.Rule,
            EntityId         = Guid.NewGuid(),
            ActionType       = ReleaseActionTypes.Activate,
            CreatedAt        = DateTime.UtcNow,
        };
        _db.SmsGovernanceReleaseItems.Add(item);

        _db.SmsGovernanceReleaseAuditEvents.Add(new SmsGovernanceReleaseAuditEvent
        {
            Id               = Guid.NewGuid(),
            ReleasePackageId = pkg.Id,
            EventType        = ReleaseAuditEventTypes.Created,
            CreatedAt        = DateTime.UtcNow,
        });

        // Move to pending_review
        pkg.ReleaseState = ReleaseStates.PendingReview;
        _db.SmsGovernanceReleaseAuditEvents.Add(new SmsGovernanceReleaseAuditEvent
        {
            Id               = Guid.NewGuid(),
            ReleasePackageId = pkg.Id,
            EventType        = ReleaseAuditEventTypes.SubmittedForReview,
            CreatedAt        = DateTime.UtcNow,
        });

        // Create a pending approval request requiring ComplianceReviewer
        _db.SmsGovernanceApprovalRequests.Add(new SmsGovernanceApprovalRequest
        {
            Id                = Guid.NewGuid(),
            ReleasePackageId  = pkg.Id,
            ApprovalStage     = 1,
            ApproverRole      = "ComplianceReviewer",
            RequiredApprovals = 1,
            Status            = ApprovalRequestStatuses.Pending,
            RequestedAt       = DateTime.UtcNow,
            CreatedAt         = DateTime.UtcNow,
            UpdatedAt         = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync();
        return pkg;
    }

    // ── Test 1: Role enforcement blocks mismatched role ───────────────────────

    [Fact]
    public async Task ApproveAsync_RoleEnforced_ReturnsFail_WhenRoleMismatches()
    {
        var pkg = await CreateAndSubmitRelease();
        var svc = BuildApprovalSvc();

        var request = new ApproveReleaseRequest(
            DecidedBy:    "alice",
            DecidedByRole: "TenantAdmin",   // wrong role — stage needs ComplianceReviewer
            Reason:       "LGTM");

        var result = await svc.ApproveAsync(pkg.Id, request);

        Assert.False(result.Success);
        Assert.Contains("role mismatch", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        // Audit event should record the mismatch
        var mismatches = await _db.SmsGovernanceReleaseAuditEvents
            .Where(e => e.ReleasePackageId == pkg.Id
                     && e.EventType == ReleaseAuditEventTypes.ApprovalRoleMismatch)
            .ToListAsync();
        Assert.Single(mismatches);
    }

    // ── Test 2: PlatformAdmin fallback allows approval regardless of stage role ──

    [Fact]
    public async Task ApproveAsync_PlatformAdminFallback_Succeeds_EvenWithMismatchedStageRole()
    {
        var pkg = await CreateAndSubmitRelease();
        var svc = BuildApprovalSvc();

        // PlatformAdmin is allowed by AllowPlatformAdminApprovalFallback = true
        var request = new ApproveReleaseRequest(
            DecidedBy:    "superadmin",
            DecidedByRole: "PlatformAdmin",
            Reason:       "Platform override");

        var result = await svc.ApproveAsync(pkg.Id, request);

        Assert.True(result.Success);

        // Release should now be Approved (single stage, single required approval)
        var updated = await _db.SmsGovernanceReleasePackages.FindAsync(pkg.Id);
        Assert.Equal(ReleaseStates.Approved, updated!.ReleaseState);

        // No role-mismatch audit event should be recorded
        var mismatches = await _db.SmsGovernanceReleaseAuditEvents
            .CountAsync(e => e.ReleasePackageId == pkg.Id
                          && e.EventType == ReleaseAuditEventTypes.ApprovalRoleMismatch);
        Assert.Equal(0, mismatches);
    }

    // ── Test 3: Duplicate item detection ─────────────────────────────────────

    [Fact]
    public async Task AddReleaseItemAsync_ThrowsInvalidOperation_WhenDuplicateEntityAction()
    {
        var opts = new SmsGovernanceReleaseManagementOptions
        {
            Enabled              = true,
            AllowImmediateActivation = true,
            RequireApprovalForActivation = false,
        };
        var svc = BuildReleaseSvc(opts);

        // Create a draft release
        var created = await svc.CreateReleaseAsync(new CreateReleaseRequest(
            TenantId:    null,
            Name:        "Dup Test Release",
            Description: null,
            ReleaseType: ReleaseTypes.MixedGovernance,
            RequestedBy: "tester"));

        var entityId = Guid.NewGuid();

        var addReq = new AddReleaseItemRequest(
            EntityType:          ReleaseEntityTypes.Rule,
            EntityId:            entityId,
            EntityVersionNumber: null,
            ActionType:          ReleaseActionTypes.Activate,
            EntitySnapshotJson:  null,
            RequestedBy:         "tester");

        // First add — should succeed
        await svc.AddReleaseItemAsync(created.Id, addReq);

        // Second add of same entity+action — should throw
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.AddReleaseItemAsync(created.Id, addReq));
    }

    // ── Test 4: Activation lock blocks concurrent activation ─────────────────

    [Fact]
    public async Task ActivateAsync_ReturnsFail_WhenActivationLockHeld()
    {
        var opts = new SmsGovernanceReleaseManagementOptions
        {
            Enabled                       = true,
            AllowImmediateActivation      = true,
            RequireApprovalForActivation  = false,
            ActivationLockTimeoutMinutes  = 10,
            ActivationRetryLimit          = 3,
            ActivationRetryBackoffMinutes = 10,
        };

        var pkg = new SmsGovernanceReleasePackage
        {
            Id               = Guid.NewGuid(),
            Name             = "Locked Release",
            ReleaseState     = ReleaseStates.Approved,
            ReleaseType      = ReleaseTypes.MixedGovernance,
            CreatedAt        = DateTime.UtcNow,
            UpdatedAt        = DateTime.UtcNow,
            // Simulate an active (non-expired) lock
            ActivationLockId         = Guid.NewGuid(),
            ActivationLockAcquiredAt = DateTime.UtcNow.AddMinutes(-1),
            ActivationLockExpiresAt  = DateTime.UtcNow.AddMinutes(9),  // not expired yet
            ActivationLockedBy       = "worker-1",
        };
        _db.SmsGovernanceReleasePackages.Add(pkg);
        await _db.SaveChangesAsync();

        var svc = BuildReleaseSvc(opts);

        var result = await svc.ActivateAsync(pkg.Id, "worker-2");

        Assert.False(result.Success);
        Assert.Contains("Another activation is already in progress", result.ErrorMessage);

        // Lock-failed audit event should be present
        var lockFailEvents = await _db.SmsGovernanceReleaseAuditEvents
            .CountAsync(e => e.ReleasePackageId == pkg.Id
                          && e.EventType == ReleaseAuditEventTypes.ActivationLockFailed);
        Assert.Equal(1, lockFailEvents);
    }

    // ── Test 5: Integrity service reports missing 'created' event ─────────────

    [Fact]
    public async Task ValidateReleaseIntegrityAsync_ReturnsInvalid_WhenCreatedEventMissing()
    {
        var pkg = new SmsGovernanceReleasePackage
        {
            Id           = Guid.NewGuid(),
            Name         = "Orphan Release",
            ReleaseState = ReleaseStates.Draft,
            ReleaseType  = ReleaseTypes.MixedGovernance,
            CreatedAt    = DateTime.UtcNow,
            UpdatedAt    = DateTime.UtcNow,
        };
        _db.SmsGovernanceReleasePackages.Add(pkg);
        await _db.SaveChangesAsync();

        var opts = Options.Create(DefaultHardeningOpts());
        var integritySvc = new SmsGovernanceReleaseIntegrityService(
            _db, opts, NullLogger<SmsGovernanceReleaseIntegrityService>.Instance);

        var report = await integritySvc.ValidateReleaseIntegrityAsync(pkg.Id);

        Assert.False(report.IsValid);
        Assert.Contains(report.Issues, i => i.Contains("'created'"));
    }

    // ── Test 6: Stale lock is treated as expired by integrity service ─────────

    [Fact]
    public async Task GetActivationLockStatusAsync_ReportsExpired_WhenLockPastExpiry()
    {
        var pkg = new SmsGovernanceReleasePackage
        {
            Id                       = Guid.NewGuid(),
            Name                     = "Expired Lock Release",
            ReleaseState             = ReleaseStates.ActivationFailed,
            ReleaseType              = ReleaseTypes.MixedGovernance,
            CreatedAt                = DateTime.UtcNow,
            UpdatedAt                = DateTime.UtcNow,
            ActivationLockId         = Guid.NewGuid(),
            ActivationLockAcquiredAt = DateTime.UtcNow.AddHours(-2),
            ActivationLockExpiresAt  = DateTime.UtcNow.AddHours(-1),  // expired 1 hour ago
            ActivationLockedBy       = "stale-worker",
        };
        _db.SmsGovernanceReleasePackages.Add(pkg);
        await _db.SaveChangesAsync();

        var opts = Options.Create(DefaultHardeningOpts());
        var integritySvc = new SmsGovernanceReleaseIntegrityService(
            _db, opts, NullLogger<SmsGovernanceReleaseIntegrityService>.Instance);

        var status = await integritySvc.GetActivationLockStatusAsync(pkg.Id);

        Assert.True(status.IsExpired);
        Assert.False(status.IsLocked);   // expired lock is not considered actively held
        Assert.Equal(pkg.ActivationLockedBy, status.LockedBy);
    }

    // ── Stub versioning service ───────────────────────────────────────────────

    private sealed class StubVersioningService : ISmsGovernanceVersioningService
    {
        public Task SnapshotRulePackAsync(Guid rulePackId, string changeType,
            string? changeReason, string? requestedBy, bool includeRules = true,
            CancellationToken ct = default) => Task.CompletedTask;

        public Task SnapshotRuleAsync(Guid ruleId, string changeType,
            string? changeReason, string? requestedBy,
            CancellationToken ct = default) => Task.CompletedTask;

        public Task<IReadOnlyList<RuleVersionDto>> GetRuleVersionsAsync(
            Guid ruleId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<RuleVersionDto>>(Array.Empty<RuleVersionDto>());

        public Task<IReadOnlyList<RulePackVersionDto>> GetRulePackVersionsAsync(
            Guid rulePackId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<RulePackVersionDto>>(Array.Empty<RulePackVersionDto>());

        public Task<RollbackResult> RollbackRuleAsync(Guid ruleId, int versionNumber,
            string? requestedBy, string? reason, CancellationToken ct = default)
            => Task.FromResult(RollbackResult.Ok(versionNumber, versionNumber + 1));

        public Task<RollbackResult> RollbackRulePackAsync(Guid rulePackId, int versionNumber,
            string? requestedBy, string? reason, CancellationToken ct = default)
            => Task.FromResult(RollbackResult.Ok(versionNumber, versionNumber + 1));
    }
}
