using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notifications.Application.Interfaces;
using Notifications.Application.Options;
using Notifications.Domain;
using Notifications.Infrastructure.Data;

namespace Notifications.Infrastructure.Services;

/// <summary>
/// LS-NOTIF-SMS-023: Orchestrates tenant rule-pack assignments and overlay lifecycle.
/// All state transitions are audited. No raw phones or credentials.
/// </summary>
public sealed class SmsGovernanceTenantAssignmentService : ISmsGovernanceTenantAssignmentService
{
    private readonly NotificationsDbContext                    _db;
    private readonly SmsGovernanceTenantScopingOptions        _opts;
    private readonly ISmsGovernanceTenantIsolationValidator   _validator;
    private readonly ILogger<SmsGovernanceTenantAssignmentService> _logger;

    public SmsGovernanceTenantAssignmentService(
        NotificationsDbContext                              db,
        IOptions<SmsGovernanceTenantScopingOptions>        options,
        ISmsGovernanceTenantIsolationValidator             validator,
        ILogger<SmsGovernanceTenantAssignmentService>      logger)
    {
        _db        = db;
        _opts      = options.Value;
        _validator = validator;
        _logger    = logger;
    }

    // ── Assignments ───────────────────────────────────────────────────────────

    public async Task<AssignmentOperationResult> AssignRulePackAsync(
        AssignRulePackRequest request, CancellationToken ct = default)
    {
        try
        {
            var validation = await _validator.ValidateAssignmentAsync(request, ct);
            if (!validation.IsValid)
                return Fail(string.Join("; ", validation.Errors), "VALIDATION_FAILED");

            var now = DateTime.UtcNow;
            var assignment = new SmsGovernanceTenantRulePackAssignment
            {
                Id              = Guid.NewGuid(),
                TenantId        = request.TenantId,
                RulePackId      = request.RulePackId,
                AssignmentState = SmsGovernanceTenantRulePackAssignment.AssignmentStates.Draft,
                AssignmentMode  = request.AssignmentMode,
                Priority        = request.Priority,
                EffectiveFrom   = request.EffectiveFrom,
                EffectiveTo     = request.EffectiveTo,
                RolloutPlanId   = request.RolloutPlanId,
                RolloutStageId  = request.RolloutStageId,
                ReleasePackageId = request.ReleasePackageId,
                AssignedBy      = request.AssignedBy,
                CreatedAt       = now,
                UpdatedAt       = now,
            };

            _db.SmsGovernanceTenantRulePackAssignments.Add(assignment);

            Audit(assignment.TenantId, assignment.Id, null,
                  SmsGovernanceTenantAssignmentAuditEvent.EventTypes.AssignmentCreated,
                  null, assignment.AssignmentState, request.AssignedBy,
                  $"pack:{request.RulePackId} mode:{request.AssignmentMode}", now);

            await _db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "TenantAssignmentService: Created assignment {Id} for tenant {TenantId}, pack {PackId}",
                assignment.Id, request.TenantId, request.RulePackId);

            return Ok(assignment.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "TenantAssignmentService.AssignRulePackAsync failed for tenant {TenantId}", request.TenantId);
            return Fail("Internal error creating assignment.", "INTERNAL_ERROR");
        }
    }

    public async Task<AssignmentOperationResult> ActivateAssignmentAsync(
        Guid assignmentId, string requestedBy, CancellationToken ct = default)
    {
        try
        {
            var assignment = await _db.SmsGovernanceTenantRulePackAssignments
                .FirstOrDefaultAsync(a => a.Id == assignmentId, ct);
            if (assignment == null)
                return Fail("Assignment not found.", "NOT_FOUND");
            if (assignment.AssignmentState == SmsGovernanceTenantRulePackAssignment.AssignmentStates.Active)
                return Ok(assignmentId);

            var prev   = assignment.AssignmentState;
            var now    = DateTime.UtcNow;
            assignment.AssignmentState = SmsGovernanceTenantRulePackAssignment.AssignmentStates.Active;
            assignment.ActivatedAt     = now;
            assignment.UpdatedAt       = now;

            Audit(assignment.TenantId, assignmentId, null,
                  SmsGovernanceTenantAssignmentAuditEvent.EventTypes.AssignmentActivated,
                  prev, assignment.AssignmentState, requestedBy, null, now);

            await _db.SaveChangesAsync(ct);
            return Ok(assignmentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ActivateAssignmentAsync failed for {Id}", assignmentId);
            return Fail("Internal error activating assignment.", "INTERNAL_ERROR");
        }
    }

    public async Task<AssignmentOperationResult> DeactivateAssignmentAsync(
        Guid assignmentId, string requestedBy, string? reason, CancellationToken ct = default)
    {
        try
        {
            var assignment = await _db.SmsGovernanceTenantRulePackAssignments
                .FirstOrDefaultAsync(a => a.Id == assignmentId, ct);
            if (assignment == null)
                return Fail("Assignment not found.", "NOT_FOUND");

            var prev   = assignment.AssignmentState;
            var now    = DateTime.UtcNow;
            assignment.AssignmentState   = SmsGovernanceTenantRulePackAssignment.AssignmentStates.Inactive;
            assignment.DeactivatedAt     = now;
            assignment.DeactivationReason = reason;
            assignment.UpdatedAt         = now;

            Audit(assignment.TenantId, assignmentId, null,
                  SmsGovernanceTenantAssignmentAuditEvent.EventTypes.AssignmentDeactivated,
                  prev, assignment.AssignmentState, requestedBy, reason, now);

            await _db.SaveChangesAsync(ct);
            return Ok(assignmentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeactivateAssignmentAsync failed for {Id}", assignmentId);
            return Fail("Internal error deactivating assignment.", "INTERNAL_ERROR");
        }
    }

    public async Task<AssignmentOperationResult> RollbackAssignmentAsync(
        Guid assignmentId, string requestedBy, string? reason, CancellationToken ct = default)
    {
        try
        {
            var assignment = await _db.SmsGovernanceTenantRulePackAssignments
                .FirstOrDefaultAsync(a => a.Id == assignmentId, ct);
            if (assignment == null)
                return Fail("Assignment not found.", "NOT_FOUND");
            if (SmsGovernanceTenantRulePackAssignment.AssignmentStates.Terminal.Contains(assignment.AssignmentState))
                return Fail($"Cannot rollback assignment in terminal state '{assignment.AssignmentState}'.", "TERMINAL_STATE");

            var prev   = assignment.AssignmentState;
            var now    = DateTime.UtcNow;
            assignment.AssignmentState    = SmsGovernanceTenantRulePackAssignment.AssignmentStates.RolledBack;
            assignment.DeactivatedAt      = now;
            assignment.DeactivationReason = reason ?? "Rolled back";
            assignment.UpdatedAt          = now;

            Audit(assignment.TenantId, assignmentId, null,
                  SmsGovernanceTenantAssignmentAuditEvent.EventTypes.AssignmentRolledBack,
                  prev, assignment.AssignmentState, requestedBy, reason, now);

            await _db.SaveChangesAsync(ct);
            return Ok(assignmentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RollbackAssignmentAsync failed for {Id}", assignmentId);
            return Fail("Internal error rolling back assignment.", "INTERNAL_ERROR");
        }
    }

    public async Task<PaginatedAssignmentResult> ListAssignmentsAsync(
        TenantAssignmentQuery query, CancellationToken ct = default)
    {
        var q = _db.SmsGovernanceTenantRulePackAssignments.AsNoTracking();
        if (query.TenantId.HasValue)       q = q.Where(a => a.TenantId == query.TenantId.Value);
        if (query.RulePackId.HasValue)     q = q.Where(a => a.RulePackId == query.RulePackId.Value);
        if (!string.IsNullOrEmpty(query.AssignmentState)) q = q.Where(a => a.AssignmentState == query.AssignmentState);
        if (!string.IsNullOrEmpty(query.AssignmentMode))  q = q.Where(a => a.AssignmentMode  == query.AssignmentMode);
        if (query.RolloutPlanId.HasValue)  q = q.Where(a => a.RolloutPlanId == query.RolloutPlanId.Value);

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(a => a.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return new PaginatedAssignmentResult(items.Select(MapAssignment).ToList(), total, query.Page, query.PageSize);
    }

    public async Task<TenantAssignmentDto?> GetAssignmentAsync(Guid assignmentId, CancellationToken ct = default)
    {
        var a = await _db.SmsGovernanceTenantRulePackAssignments
            .AsNoTracking().FirstOrDefaultAsync(x => x.Id == assignmentId, ct);
        return a == null ? null : MapAssignment(a);
    }

    // ── Overlays ──────────────────────────────────────────────────────────────

    public async Task<OverlayOperationResult> CreateOverlayAsync(
        CreateTenantOverlayRequest request, CancellationToken ct = default)
    {
        try
        {
            var validation = await _validator.ValidateOverlayAsync(request, ct);
            if (!validation.IsValid)
                return FailOverlay(string.Join("; ", validation.Errors), "VALIDATION_FAILED");

            var now = DateTime.UtcNow;
            var overlay = new SmsGovernanceTenantOverlay
            {
                Id           = Guid.NewGuid(),
                TenantId     = request.TenantId,
                RulePackId   = request.RulePackId,
                RuleId       = request.RuleId,
                OverlayType  = request.OverlayType,
                OverlayState = SmsGovernanceTenantOverlay.OverlayStates.Draft,
                OverrideJson = request.OverrideJson,
                Priority     = request.Priority,
                Enabled      = true,
                EffectiveFrom = request.EffectiveFrom,
                EffectiveTo   = request.EffectiveTo,
                CreatedAt    = now,
                UpdatedAt    = now,
                CreatedBy    = request.CreatedBy,
            };

            _db.SmsGovernanceTenantOverlays.Add(overlay);

            Audit(request.TenantId, null, overlay.Id,
                  SmsGovernanceTenantAssignmentAuditEvent.EventTypes.OverlayCreated,
                  null, overlay.OverlayState, request.CreatedBy,
                  $"type:{request.OverlayType}", now);

            await _db.SaveChangesAsync(ct);
            return OkOverlay(overlay.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreateOverlayAsync failed for tenant {TenantId}", request.TenantId);
            return FailOverlay("Internal error creating overlay.", "INTERNAL_ERROR");
        }
    }

    public async Task<OverlayOperationResult> ActivateOverlayAsync(
        Guid overlayId, string requestedBy, CancellationToken ct = default)
    {
        try
        {
            var overlay = await _db.SmsGovernanceTenantOverlays.FirstOrDefaultAsync(o => o.Id == overlayId, ct);
            if (overlay == null) return FailOverlay("Overlay not found.", "NOT_FOUND");

            var prev = overlay.OverlayState;
            overlay.OverlayState = SmsGovernanceTenantOverlay.OverlayStates.Active;
            overlay.UpdatedBy    = requestedBy;
            overlay.UpdatedAt    = DateTime.UtcNow;

            Audit(overlay.TenantId, null, overlayId,
                  SmsGovernanceTenantAssignmentAuditEvent.EventTypes.OverlayActivated,
                  prev, overlay.OverlayState, requestedBy, null, DateTime.UtcNow);

            await _db.SaveChangesAsync(ct);
            return OkOverlay(overlayId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ActivateOverlayAsync failed for {Id}", overlayId);
            return FailOverlay("Internal error activating overlay.", "INTERNAL_ERROR");
        }
    }

    public async Task<OverlayOperationResult> DisableOverlayAsync(
        Guid overlayId, string requestedBy, string? reason, CancellationToken ct = default)
    {
        try
        {
            var overlay = await _db.SmsGovernanceTenantOverlays.FirstOrDefaultAsync(o => o.Id == overlayId, ct);
            if (overlay == null) return FailOverlay("Overlay not found.", "NOT_FOUND");

            var prev = overlay.OverlayState;
            overlay.OverlayState = SmsGovernanceTenantOverlay.OverlayStates.Inactive;
            overlay.Enabled      = false;
            overlay.UpdatedBy    = requestedBy;
            overlay.UpdatedAt    = DateTime.UtcNow;

            Audit(overlay.TenantId, null, overlayId,
                  SmsGovernanceTenantAssignmentAuditEvent.EventTypes.OverlayDisabled,
                  prev, overlay.OverlayState, requestedBy, reason, DateTime.UtcNow);

            await _db.SaveChangesAsync(ct);
            return OkOverlay(overlayId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DisableOverlayAsync failed for {Id}", overlayId);
            return FailOverlay("Internal error disabling overlay.", "INTERNAL_ERROR");
        }
    }

    public async Task<PaginatedOverlayResult> ListOverlaysAsync(
        TenantOverlayQuery query, CancellationToken ct = default)
    {
        var q = _db.SmsGovernanceTenantOverlays.AsNoTracking();
        if (query.TenantId.HasValue)   q = q.Where(o => o.TenantId == query.TenantId.Value);
        if (query.RulePackId.HasValue) q = q.Where(o => o.RulePackId == query.RulePackId.Value);
        if (query.RuleId.HasValue)     q = q.Where(o => o.RuleId == query.RuleId.Value);
        if (!string.IsNullOrEmpty(query.OverlayType))  q = q.Where(o => o.OverlayType  == query.OverlayType);
        if (!string.IsNullOrEmpty(query.OverlayState)) q = q.Where(o => o.OverlayState == query.OverlayState);
        if (query.Enabled.HasValue)    q = q.Where(o => o.Enabled == query.Enabled.Value);

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(o => o.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return new PaginatedOverlayResult(items.Select(MapOverlay).ToList(), total, query.Page, query.PageSize);
    }

    public async Task<TenantOverlayDto?> GetOverlayAsync(Guid overlayId, CancellationToken ct = default)
    {
        var o = await _db.SmsGovernanceTenantOverlays.AsNoTracking().FirstOrDefaultAsync(x => x.Id == overlayId, ct);
        return o == null ? null : MapOverlay(o);
    }

    // ── Audit ─────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<TenantAssignmentAuditEventDto>> GetAuditTrailAsync(
        TenantAuditQuery query, CancellationToken ct = default)
    {
        var q = _db.SmsGovernanceTenantAssignmentAuditEvents.AsNoTracking();
        if (query.TenantId.HasValue)     q = q.Where(e => e.TenantId == query.TenantId.Value);
        if (query.AssignmentId.HasValue) q = q.Where(e => e.AssignmentId == query.AssignmentId.Value);
        if (query.OverlayId.HasValue)    q = q.Where(e => e.OverlayId == query.OverlayId.Value);
        if (!string.IsNullOrEmpty(query.EventType)) q = q.Where(e => e.EventType == query.EventType);

        return await q
            .OrderByDescending(e => e.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(e => new TenantAssignmentAuditEventDto(
                e.Id, e.TenantId, e.AssignmentId, e.OverlayId,
                e.EventType, e.PreviousState, e.NewState,
                e.Actor, e.Reason, e.MetadataJson, e.CreatedAt))
            .ToListAsync(ct);
    }

    // ── Audit helper ──────────────────────────────────────────────────────────

    private void Audit(
        Guid tenantId, Guid? assignmentId, Guid? overlayId,
        string eventType, string? prev, string? next,
        string? actor, string? reason, DateTime now)
    {
        _db.SmsGovernanceTenantAssignmentAuditEvents.Add(new SmsGovernanceTenantAssignmentAuditEvent
        {
            Id            = Guid.NewGuid(),
            TenantId      = tenantId,
            AssignmentId  = assignmentId,
            OverlayId     = overlayId,
            EventType     = eventType,
            PreviousState = prev,
            NewState      = next,
            Actor         = actor,
            Reason        = reason,
            CreatedAt     = now,
        });
    }

    // ── Mappers ───────────────────────────────────────────────────────────────

    private static TenantAssignmentDto MapAssignment(SmsGovernanceTenantRulePackAssignment a) =>
        new(a.Id, a.TenantId, a.RulePackId, a.AssignmentState, a.AssignmentMode,
            a.Priority, a.EffectiveFrom, a.EffectiveTo,
            a.RolloutPlanId, a.RolloutStageId, a.ReleasePackageId,
            a.AssignedBy, a.ActivatedAt, a.DeactivatedAt, a.SupersededAt,
            a.DeactivationReason, a.CreatedAt, a.UpdatedAt);

    private static TenantOverlayDto MapOverlay(SmsGovernanceTenantOverlay o) =>
        new(o.Id, o.TenantId, o.RulePackId, o.RuleId, o.OverlayType, o.OverlayState,
            o.OverrideJson, o.Priority, o.Enabled,
            o.EffectiveFrom, o.EffectiveTo,
            o.CreatedAt, o.UpdatedAt, o.CreatedBy, o.UpdatedBy);

    private static AssignmentOperationResult Ok(Guid id)        => new(true, id);
    private static AssignmentOperationResult Fail(string msg, string code) => new(false, null, msg, code);
    private static OverlayOperationResult OkOverlay(Guid id)    => new(true, id);
    private static OverlayOperationResult FailOverlay(string msg, string code) => new(false, null, msg, code);
}
