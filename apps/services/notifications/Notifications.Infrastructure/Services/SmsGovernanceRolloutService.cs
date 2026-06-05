using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notifications.Application.Interfaces;
using Notifications.Application.Options;
using Notifications.Domain;
using Notifications.Infrastructure.Data;

namespace Notifications.Infrastructure.Services;

// LS-NOTIF-SMS-023 partial: rollout→tenant assignment integration notes
// StartRolloutAsync: after activating first stage, creates SmsGovernanceTenantRulePackAssignment
//   records for all cohorts associated with that stage (mode = rollout_canary or rollout_stage).
// AdvanceStageAsync: after activating next stage, creates assignments for that stage's cohorts.
// RollbackRolloutAsync: deactivates all rollout-created assignments (matching RolloutPlanId).
// These assignments are additive — they do not modify any existing global assignments.
// If release items reference global packs, the assignment maps those packs to cohort tenants.

/// <summary>
/// LS-NOTIF-SMS-022: Central rollout orchestration service.
///
/// State transitions are validated and all changes are audited.
/// Full-activation strategy delegates to ISmsGovernanceReleaseService.ActivateAsync,
/// which respects LS-021-HARDENING concurrency locking.
/// Canary/staged strategies record orchestration/visibility state — true per-tenant
/// enforcement scoping requires LS-NOTIF-SMS-023.
/// Rollout failures never corrupt active governance state.
/// </summary>
public sealed class SmsGovernanceRolloutService : ISmsGovernanceRolloutService
{
    private readonly NotificationsDbContext              _db;
    private readonly ISmsGovernanceReleaseService        _releaseService;
    private readonly ISmsGovernanceTenantAssignmentService _tenantAssignments;
    private readonly SmsGovernanceRolloutsOptions        _opts;
    private readonly ILogger<SmsGovernanceRolloutService> _logger;

    public SmsGovernanceRolloutService(
        NotificationsDbContext                    db,
        ISmsGovernanceReleaseService             releaseService,
        ISmsGovernanceTenantAssignmentService    tenantAssignments,
        IOptions<SmsGovernanceRolloutsOptions>   opts,
        ILogger<SmsGovernanceRolloutService>     logger)
    {
        _db                = db;
        _releaseService    = releaseService;
        _tenantAssignments = tenantAssignments;
        _opts              = opts.Value;
        _logger            = logger;
    }

    // ── Create ────────────────────────────────────────────────────────────────

    public async Task<RolloutPlanDto> CreateRolloutAsync(
        CreateRolloutRequest request, CancellationToken ct = default)
    {
        if (!RolloutStrategies.All.Contains(request.RolloutStrategy))
            throw new ArgumentException($"Unknown rollout strategy: {request.RolloutStrategy}");

        var now  = DateTime.UtcNow;
        var plan = new SmsGovernanceRolloutPlan
        {
            Id                   = Guid.NewGuid(),
            ReleasePackageId     = request.ReleasePackageId,
            TenantId             = request.TenantId,
            Name                 = request.Name,
            Description          = request.Description,
            RolloutState         = RolloutStates.Draft,
            RolloutStrategy      = request.RolloutStrategy,
            RollbackThresholdJson = request.RollbackThresholdJson,
            CreatedAt            = now,
            UpdatedAt            = now,
            CreatedBy            = request.RequestedBy,
            UpdatedBy            = request.RequestedBy,
        };

        _db.SmsGovernanceRolloutPlans.Add(plan);

        Audit(plan.Id, null, null, RolloutAuditEventTypes.RolloutCreated,
              null, RolloutStates.Draft, request.RequestedBy, null, now);

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Rollout plan {Id} created for release {ReleaseId}", plan.Id, plan.ReleasePackageId);

        return MapPlan(plan);
    }

    // ── Query ─────────────────────────────────────────────────────────────────

    public async Task<RolloutDetailDto?> GetRolloutAsync(
        Guid rolloutId, CancellationToken ct = default)
    {
        var plan = await _db.SmsGovernanceRolloutPlans
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == rolloutId, ct);

        if (plan is null) return null;

        var stages = await _db.SmsGovernanceRolloutStages
            .AsNoTracking()
            .Where(s => s.RolloutPlanId == rolloutId)
            .OrderBy(s => s.StageNumber)
            .ToListAsync(ct);

        var cohorts = await _db.SmsGovernanceTenantCohorts
            .AsNoTracking()
            .Where(c => c.RolloutPlanId == rolloutId)
            .ToListAsync(ct);

        return new RolloutDetailDto(MapPlan(plan), stages.Select(MapStage).ToList(), cohorts.Select(MapCohort).ToList());
    }

    public async Task<PaginatedRolloutResult> ListRolloutsAsync(
        RolloutListQuery query, CancellationToken ct = default)
    {
        var q = _db.SmsGovernanceRolloutPlans.AsNoTracking();

        if (query.ReleasePackageId.HasValue) q = q.Where(p => p.ReleasePackageId == query.ReleasePackageId.Value);
        if (query.TenantId.HasValue)         q = q.Where(p => p.TenantId         == query.TenantId.Value);
        if (!string.IsNullOrWhiteSpace(query.State))    q = q.Where(p => p.RolloutState   == query.State);
        if (!string.IsNullOrWhiteSpace(query.Strategy)) q = q.Where(p => p.RolloutStrategy == query.Strategy);

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(p => p.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return new PaginatedRolloutResult(items.Select(MapPlan).ToList(), total, query.Page, query.PageSize);
    }

    // ── Stage / Cohort ────────────────────────────────────────────────────────

    public async Task<RolloutStageDto> AddStageAsync(
        Guid rolloutId, AddRolloutStageRequest request, CancellationToken ct = default)
    {
        var plan = await RequirePlanAsync(rolloutId, ct);
        if (!RolloutStates.IsEditable(plan.RolloutState))
            throw new InvalidOperationException($"Cannot add stages to rollout in state '{plan.RolloutState}'.");

        var now   = DateTime.UtcNow;
        var stage = new SmsGovernanceRolloutStage
        {
            Id              = Guid.NewGuid(),
            RolloutPlanId   = rolloutId,
            StageNumber     = request.StageNumber,
            StageName       = request.StageName,
            StageState      = RolloutStageStates.Pending,
            TenantPercentage = request.TenantPercentage,
            DurationMinutes  = request.DurationMinutes,
            CreatedAt        = now,
            UpdatedAt        = now,
        };

        _db.SmsGovernanceRolloutStages.Add(stage);
        Audit(rolloutId, stage.Id, null, RolloutAuditEventTypes.StageAdded,
              null, null, request.RequestedBy, $"stage {request.StageNumber}", now);

        await _db.SaveChangesAsync(ct);
        return MapStage(stage);
    }

    public async Task<TenantCohortDto> AddCohortTenantAsync(
        Guid rolloutId, AddTenantCohortRequest request, CancellationToken ct = default)
    {
        var plan = await RequirePlanAsync(rolloutId, ct);
        if (!RolloutStates.IsEditable(plan.RolloutState))
            throw new InvalidOperationException($"Cannot add cohorts to rollout in state '{plan.RolloutState}'.");

        // Duplicate check: same TenantId + same StageId (or same plan-level)
        var exists = await _db.SmsGovernanceTenantCohorts.AnyAsync(
            c => c.RolloutPlanId == rolloutId
              && c.TenantId      == request.TenantId
              && c.StageId       == request.StageId, ct);

        if (exists)
            throw new InvalidOperationException(
                $"Tenant {request.TenantId} is already in a cohort for this rollout/stage.");

        var now    = DateTime.UtcNow;
        var cohort = new SmsGovernanceTenantCohort
        {
            Id            = Guid.NewGuid(),
            RolloutPlanId = rolloutId,
            StageId       = request.StageId,
            TenantId      = request.TenantId,
            CohortName    = request.CohortName,
            Enabled       = true,
            CreatedAt     = now,
            UpdatedAt     = now,
        };

        _db.SmsGovernanceTenantCohorts.Add(cohort);
        Audit(rolloutId, request.StageId, request.TenantId, RolloutAuditEventTypes.CohortAdded,
              null, null, request.RequestedBy, $"cohort '{request.CohortName}'", now);

        await _db.SaveChangesAsync(ct);
        return MapCohort(cohort);
    }

    // ── Lifecycle: Start ──────────────────────────────────────────────────────

    public async Task<RolloutOperationResult> StartRolloutAsync(
        Guid rolloutId, string requestedBy, CancellationToken ct = default)
    {
        var plan = await RequirePlanAsync(rolloutId, ct);

        if (plan.RolloutState != RolloutStates.Draft && plan.RolloutState != RolloutStates.PendingRollout)
            return Fail($"Cannot start rollout in state '{plan.RolloutState}'.");

        // Verify the release package is in an activation-compatible state
        var release = await _db.SmsGovernanceReleasePackages
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == plan.ReleasePackageId, ct);

        if (release is null)
            return Fail("Associated release package not found.");

        if (release.ReleaseState != ReleaseStates.Approved &&
            release.ReleaseState != ReleaseStates.Active   &&
            release.ReleaseState != ReleaseStates.Scheduled)
            return Fail($"Release package must be approved before rollout can start (current state: {release.ReleaseState}).");

        var now          = DateTime.UtcNow;
        var prevState    = plan.RolloutState;
        var newState     = plan.RolloutStrategy == RolloutStrategies.FullActivation
                          || plan.RolloutStrategy == RolloutStrategies.ManualProgression
                            ? RolloutStates.StagedRollout
                            : RolloutStates.CanaryActive;

        if (plan.RolloutStrategy == RolloutStrategies.FullActivation)
        {
            // Full activation: delegate to release service (respects concurrency locking)
            var activateResult = await _releaseService.ActivateAsync(plan.ReleasePackageId, requestedBy, ct);
            if (!activateResult.Success)
                return Fail($"Release activation failed: {activateResult.ErrorMessage}");

            newState = RolloutStates.RolloutCompleted;
            plan.CompletedAt = now;
        }

        plan.RolloutState   = newState;
        plan.StartedAt      = now;
        plan.UpdatedAt      = now;
        plan.UpdatedBy      = requestedBy;

        // Start the first stage if stages exist
        var firstStage = await _db.SmsGovernanceRolloutStages
            .Where(s => s.RolloutPlanId == rolloutId)
            .OrderBy(s => s.StageNumber)
            .FirstOrDefaultAsync(ct);

        if (firstStage is not null && plan.RolloutStrategy != RolloutStrategies.FullActivation)
        {
            firstStage.StageState = RolloutStageStates.Active;
            firstStage.StartedAt  = now;
            firstStage.UpdatedAt  = now;
            plan.CurrentStageNumber = firstStage.StageNumber;

            Audit(rolloutId, firstStage.Id, null, RolloutAuditEventTypes.StageStarted,
                  RolloutStageStates.Pending, RolloutStageStates.Active, requestedBy, "first stage started", now);

            // LS-NOTIF-SMS-023: Create tenant rule-pack assignments for cohorts in this stage
            await CreateStageAssignmentsAsync(rolloutId, firstStage.Id, plan, release, requestedBy, ct);
        }

        Audit(rolloutId, null, null, RolloutAuditEventTypes.RolloutStarted,
              prevState, newState, requestedBy, null, now);

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Rollout {Id} started → {State}", rolloutId, newState);
        return Ok();
    }

    // ── Lifecycle: Pause ──────────────────────────────────────────────────────

    public async Task<RolloutOperationResult> PauseRolloutAsync(
        Guid rolloutId, string requestedBy, string? reason, CancellationToken ct = default)
    {
        var plan = await RequirePlanAsync(rolloutId, ct);
        if (!RolloutStates.IsActive(plan.RolloutState))
            return Fail($"Cannot pause rollout in state '{plan.RolloutState}'.");

        var now = DateTime.UtcNow;
        var prev = plan.RolloutState;

        plan.RolloutState = RolloutStates.RolloutPaused;
        plan.PausedAt     = now;
        plan.UpdatedAt    = now;
        plan.UpdatedBy    = requestedBy;

        // Pause the active stage if any
        var activeStage = await _db.SmsGovernanceRolloutStages
            .FirstOrDefaultAsync(s => s.RolloutPlanId == rolloutId && s.StageState == RolloutStageStates.Active, ct);

        if (activeStage is not null)
        {
            activeStage.StageState = RolloutStageStates.Paused;
            activeStage.UpdatedAt  = now;
            Audit(rolloutId, activeStage.Id, null, RolloutAuditEventTypes.RolloutPaused,
                  RolloutStageStates.Active, RolloutStageStates.Paused, requestedBy, reason, now);
        }

        Audit(rolloutId, null, null, RolloutAuditEventTypes.RolloutPaused,
              prev, RolloutStates.RolloutPaused, requestedBy, reason, now);

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Rollout {Id} paused. Reason: {Reason}", rolloutId, reason ?? "none");
        return Ok();
    }

    // ── Lifecycle: Resume ─────────────────────────────────────────────────────

    public async Task<RolloutOperationResult> ResumeRolloutAsync(
        Guid rolloutId, string requestedBy, CancellationToken ct = default)
    {
        var plan = await RequirePlanAsync(rolloutId, ct);
        if (plan.RolloutState != RolloutStates.RolloutPaused)
            return Fail($"Cannot resume rollout in state '{plan.RolloutState}'.");

        var now      = DateTime.UtcNow;
        var newState = plan.RolloutStrategy == RolloutStrategies.Canary
            ? RolloutStates.CanaryActive
            : RolloutStates.StagedRollout;

        plan.RolloutState = newState;
        plan.ResumedAt    = now;
        plan.UpdatedAt    = now;
        plan.UpdatedBy    = requestedBy;

        // Resume the paused stage if any
        var pausedStage = await _db.SmsGovernanceRolloutStages
            .FirstOrDefaultAsync(s => s.RolloutPlanId == rolloutId && s.StageState == RolloutStageStates.Paused, ct);

        if (pausedStage is not null)
        {
            pausedStage.StageState = RolloutStageStates.Active;
            pausedStage.UpdatedAt  = now;
            Audit(rolloutId, pausedStage.Id, null, RolloutAuditEventTypes.RolloutResumed,
                  RolloutStageStates.Paused, RolloutStageStates.Active, requestedBy, null, now);
        }

        Audit(rolloutId, null, null, RolloutAuditEventTypes.RolloutResumed,
              RolloutStates.RolloutPaused, newState, requestedBy, null, now);

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Rollout {Id} resumed → {State}", rolloutId, newState);
        return Ok();
    }

    // ── Lifecycle: Rollback ───────────────────────────────────────────────────

    public async Task<RolloutOperationResult> RollbackRolloutAsync(
        Guid rolloutId, string requestedBy, string? reason, CancellationToken ct = default)
    {
        var plan = await RequirePlanAsync(rolloutId, ct);

        if (RolloutStates.IsTerminal(plan.RolloutState) && plan.RolloutState != RolloutStates.RolloutPaused)
            return Fail($"Cannot rollback rollout in terminal state '{plan.RolloutState}'.");

        var now  = DateTime.UtcNow;
        var prev = plan.RolloutState;

        plan.RolloutState  = RolloutStates.RolloutRolledBack;
        plan.RolledBackAt  = now;
        plan.UpdatedAt     = now;
        plan.UpdatedBy     = requestedBy;

        // Mark all non-terminal stages as rolled_back
        var activeStages = await _db.SmsGovernanceRolloutStages
            .Where(s => s.RolloutPlanId == rolloutId
                     && s.StageState != RolloutStageStates.Completed
                     && s.StageState != RolloutStageStates.Failed
                     && s.StageState != RolloutStageStates.RolledBack)
            .ToListAsync(ct);

        foreach (var stage in activeStages)
        {
            stage.StageState = RolloutStageStates.RolledBack;
            stage.UpdatedAt  = now;
        }

        // Mark active cohorts as rolled back
        var activeCohorts = await _db.SmsGovernanceTenantCohorts
            .Where(c => c.RolloutPlanId == rolloutId
                     && c.ActivatedAt.HasValue
                     && !c.RolledBackAt.HasValue)
            .ToListAsync(ct);

        foreach (var cohort in activeCohorts)
        {
            cohort.RolledBackAt = now;
            cohort.UpdatedAt    = now;
            Audit(rolloutId, cohort.StageId, cohort.TenantId, RolloutAuditEventTypes.CohortRolledBack,
                  null, null, requestedBy, reason, now);
        }

        // LS-NOTIF-SMS-023: Roll back all tenant assignments created by this rollout.
        // Only assignments with a matching RolloutPlanId are affected — unrelated assignments preserved.
        await RollbackRolloutAssignmentsAsync(rolloutId, requestedBy, reason, ct);

        Audit(rolloutId, null, null, RolloutAuditEventTypes.RolloutRolledBack,
              prev, RolloutStates.RolloutRolledBack, requestedBy, reason, now);

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Rollout {Id} rolled back. Reason: {Reason}", rolloutId, reason ?? "none");
        return Ok();
    }

    // ── Lifecycle: AdvanceStage ───────────────────────────────────────────────

    public async Task<RolloutOperationResult> AdvanceStageAsync(
        Guid rolloutId, string requestedBy, CancellationToken ct = default)
    {
        var plan = await RequirePlanAsync(rolloutId, ct);
        if (!RolloutStates.IsActive(plan.RolloutState))
            return Fail($"Cannot advance stage when rollout is in state '{plan.RolloutState}'.");

        var now = DateTime.UtcNow;

        var currentStage = await _db.SmsGovernanceRolloutStages
            .FirstOrDefaultAsync(s => s.RolloutPlanId == rolloutId && s.StageState == RolloutStageStates.Active, ct);

        if (currentStage is null)
            return Fail("No active stage found to advance from.");

        // Validate observation window
        if (currentStage.DurationMinutes.HasValue && currentStage.StartedAt.HasValue)
        {
            var elapsed = now - currentStage.StartedAt.Value;
            if (elapsed.TotalMinutes < currentStage.DurationMinutes.Value)
                return Fail($"Stage observation window has not elapsed. " +
                            $"{currentStage.DurationMinutes.Value - (int)elapsed.TotalMinutes} minute(s) remaining.");
        }

        // Complete current stage
        currentStage.StageState  = RolloutStageStates.Completed;
        currentStage.CompletedAt = now;
        currentStage.UpdatedAt   = now;

        Audit(rolloutId, currentStage.Id, null, RolloutAuditEventTypes.StageCompleted,
              RolloutStageStates.Active, RolloutStageStates.Completed, requestedBy, null, now);

        // Find and start next stage
        var nextStage = await _db.SmsGovernanceRolloutStages
            .Where(s => s.RolloutPlanId == rolloutId && s.StageState == RolloutStageStates.Pending)
            .OrderBy(s => s.StageNumber)
            .FirstOrDefaultAsync(ct);

        if (nextStage is not null)
        {
            nextStage.StageState = RolloutStageStates.Active;
            nextStage.StartedAt  = now;
            nextStage.UpdatedAt  = now;
            plan.CurrentStageNumber = nextStage.StageNumber;

            Audit(rolloutId, nextStage.Id, null, RolloutAuditEventTypes.StageStarted,
                  RolloutStageStates.Pending, RolloutStageStates.Active, requestedBy, "stage advanced", now);

            Audit(rolloutId, null, null, RolloutAuditEventTypes.StageAdvanced,
                  currentStage.StageNumber.ToString(), nextStage.StageNumber.ToString(),
                  requestedBy, null, now);

            plan.RolloutState  = RolloutStates.StagedRollout;
            plan.UpdatedAt     = now;
            plan.UpdatedBy     = requestedBy;

            // LS-NOTIF-SMS-023: Create tenant assignments for cohorts in the newly activated stage
            var release = await _db.SmsGovernanceReleasePackages
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == plan.ReleasePackageId, ct);
            if (release is not null)
                await CreateStageAssignmentsAsync(rolloutId, nextStage.Id, plan, release, requestedBy, ct);
        }
        else
        {
            // No more pending stages — complete rollout
            return await CompleteRolloutAsync(rolloutId, requestedBy, ct);
        }

        await _db.SaveChangesAsync(ct);
        return Ok();
    }

    // ── Lifecycle: Complete ───────────────────────────────────────────────────

    public async Task<RolloutOperationResult> CompleteRolloutAsync(
        Guid rolloutId, string requestedBy, CancellationToken ct = default)
    {
        var plan = await RequirePlanAsync(rolloutId, ct);
        if (RolloutStates.IsTerminal(plan.RolloutState))
            return Fail($"Rollout is already in terminal state '{plan.RolloutState}'.");

        var now  = DateTime.UtcNow;
        var prev = plan.RolloutState;

        plan.RolloutState = RolloutStates.RolloutCompleted;
        plan.CompletedAt  = now;
        plan.UpdatedAt    = now;
        plan.UpdatedBy    = requestedBy;

        Audit(rolloutId, null, null, RolloutAuditEventTypes.RolloutCompleted,
              prev, RolloutStates.RolloutCompleted, requestedBy, null, now);

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Rollout {Id} completed", rolloutId);
        return Ok();
    }

    // ── Audit trail ───────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<RolloutAuditEventDto>> GetAuditTrailAsync(
        Guid rolloutId, CancellationToken ct = default)
    {
        var events = await _db.SmsGovernanceRolloutAuditEvents
            .AsNoTracking()
            .Where(e => e.RolloutPlanId == rolloutId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(ct);

        return events.Select(e => new RolloutAuditEventDto(
            e.Id, e.RolloutPlanId, e.StageId, e.TenantId,
            e.EventType, e.PreviousState, e.NewState,
            e.Actor, e.Reason, e.MetadataJson, e.CreatedAt)).ToList();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<SmsGovernanceRolloutPlan> RequirePlanAsync(Guid id, CancellationToken ct)
    {
        var plan = await _db.SmsGovernanceRolloutPlans.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (plan is null) throw new KeyNotFoundException($"Rollout plan {id} not found.");
        return plan;
    }

    private void Audit(
        Guid rolloutId, Guid? stageId, Guid? tenantId, string eventType,
        string? prev, string? next, string? actor, string? reason, DateTime now)
    {
        _db.SmsGovernanceRolloutAuditEvents.Add(new SmsGovernanceRolloutAuditEvent
        {
            Id            = Guid.NewGuid(),
            RolloutPlanId = rolloutId,
            StageId       = stageId,
            TenantId      = tenantId,
            EventType     = eventType,
            PreviousState = prev,
            NewState      = next,
            Actor         = actor,
            Reason        = reason,
            CreatedAt     = now,
        });
    }

    private static RolloutOperationResult Ok()   => new(true);
    private static RolloutOperationResult Fail(string msg) => new(false, msg);

    // ── LS-NOTIF-SMS-023: Tenant assignment helpers ───────────────────────────

    /// <summary>
    /// For each cohort in the given stage, load the release items (global packs) and
    /// create a SmsGovernanceTenantRulePackAssignment per cohort tenant per pack.
    /// Assignments start in Draft state; they are activated immediately unless the
    /// stage is canary (mode = rollout_canary) vs staged (mode = rollout_stage).
    /// Failures are non-fatal — the rollout proceeds; a warning is logged.
    /// </summary>
    private async Task CreateStageAssignmentsAsync(
        Guid                         rolloutId,
        Guid                         stageId,
        SmsGovernanceRolloutPlan     plan,
        SmsGovernanceReleasePackage  release,
        string                       requestedBy,
        CancellationToken            ct)
    {
        try
        {
            var cohorts = await _db.SmsGovernanceTenantCohorts
                .AsNoTracking()
                .Where(c => c.RolloutPlanId == rolloutId && c.StageId == stageId && c.Enabled)
                .ToListAsync(ct);

            if (cohorts.Count == 0) return;

            var releaseItems = await _db.SmsGovernanceReleaseItems
                .AsNoTracking()
                .Where(i => i.ReleasePackageId == plan.ReleasePackageId)
                .ToListAsync(ct);

            if (releaseItems.Count == 0) return;

            // Determine assignment mode by rollout strategy
            var mode = plan.RolloutStrategy == RolloutStrategies.Canary
                ? SmsGovernanceTenantRulePackAssignment.AssignmentModes.RolloutCanary
                : SmsGovernanceTenantRulePackAssignment.AssignmentModes.RolloutStage;

            // Only rule_pack release items map to tenant assignments
            var packItems = releaseItems.Where(i => i.EntityType == ReleaseEntityTypes.RulePack).ToList();

            foreach (var cohort in cohorts)
            {
                foreach (var item in packItems)
                {
                    var packId = item.EntityId;

                    // Skip if an active assignment already exists for this tenant+pack from this rollout
                    var alreadyExists = await _db.SmsGovernanceTenantRulePackAssignments
                        .AnyAsync(a => a.TenantId     == cohort.TenantId &&
                                       a.RulePackId   == packId &&
                                       a.RolloutPlanId == rolloutId &&
                                       a.AssignmentState != SmsGovernanceTenantRulePackAssignment.AssignmentStates.RolledBack, ct);
                    if (alreadyExists) continue;

                    var request = new AssignRulePackRequest(
                        TenantId:        cohort.TenantId,
                        RulePackId:      packId,
                        AssignmentMode:  mode,
                        Priority:        100,
                        EffectiveFrom:   null,
                        EffectiveTo:     null,
                        RolloutPlanId:   rolloutId,
                        RolloutStageId:  stageId,
                        ReleasePackageId: plan.ReleasePackageId,
                        AssignedBy:      requestedBy);

                    var result = await _tenantAssignments.AssignRulePackAsync(request, ct);
                    if (result.Success && result.AssignmentId.HasValue)
                    {
                        // Immediately activate the assignment for the rollout stage
                        await _tenantAssignments.ActivateAssignmentAsync(
                            result.AssignmentId.Value, requestedBy, ct);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "LS-023: Could not create assignment for tenant {TenantId} pack {PackId} in stage {StageId}: {Error}",
                            cohort.TenantId, packId, stageId, result.ErrorMessage);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Non-fatal — log and continue; rollout state is not affected
            _logger.LogWarning(ex,
                "LS-023: CreateStageAssignmentsAsync failed for rollout {Id} stage {StageId} — proceeding without tenant assignments",
                rolloutId, stageId);
        }
    }

    /// <summary>
    /// Roll back all non-terminal tenant assignments created by this rollout.
    /// Scoped to RolloutPlanId — does not touch any other assignments.
    /// </summary>
    private async Task RollbackRolloutAssignmentsAsync(
        Guid rolloutId, string requestedBy, string? reason, CancellationToken ct)
    {
        try
        {
            var assignments = await _db.SmsGovernanceTenantRulePackAssignments
                .Where(a => a.RolloutPlanId == rolloutId &&
                            !SmsGovernanceTenantRulePackAssignment.AssignmentStates.Terminal.Contains(a.AssignmentState))
                .ToListAsync(ct);

            foreach (var assignment in assignments)
                await _tenantAssignments.RollbackAssignmentAsync(assignment.Id, requestedBy, reason, ct);

            _logger.LogInformation(
                "LS-023: Rolled back {Count} tenant assignment(s) for rollout {Id}", assignments.Count, rolloutId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "LS-023: RollbackRolloutAssignmentsAsync failed for rollout {Id} — proceeding", rolloutId);
        }
    }

    // ── Mappers ───────────────────────────────────────────────────────────────

    private static RolloutPlanDto MapPlan(SmsGovernanceRolloutPlan p) =>
        new(p.Id, p.ReleasePackageId, p.TenantId, p.Name, p.Description,
            p.RolloutState, p.RolloutStrategy, p.CurrentStageNumber, p.RollbackThresholdJson,
            p.StartedAt, p.PausedAt, p.ResumedAt, p.CompletedAt, p.RolledBackAt, p.FailedAt,
            p.FailureReason, p.CreatedAt, p.UpdatedAt, p.CreatedBy, p.UpdatedBy);

    private static RolloutStageDto MapStage(SmsGovernanceRolloutStage s) =>
        new(s.Id, s.RolloutPlanId, s.StageNumber, s.StageName, s.StageState,
            s.TenantPercentage, s.DurationMinutes, s.StartedAt, s.CompletedAt,
            s.FailedAt, s.FailureReason, s.CreatedAt, s.UpdatedAt);

    private static TenantCohortDto MapCohort(SmsGovernanceTenantCohort c) =>
        new(c.Id, c.RolloutPlanId, c.StageId, c.TenantId, c.CohortName,
            c.Enabled, c.ActivatedAt, c.RolledBackAt, c.CreatedAt, c.UpdatedAt);
}
