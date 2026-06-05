using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Notifications.Application.Interfaces;
using Notifications.Application.Options;
using Notifications.Domain;
using Notifications.Infrastructure.Data;

namespace Notifications.Infrastructure.Services;

/// <summary>
/// LS-NOTIF-SMS-023: Read-only validator for tenant governance isolation invariants.
/// Returns safe diagnostics only. No raw phones or secrets.
/// </summary>
public sealed class SmsGovernanceTenantIsolationValidator : ISmsGovernanceTenantIsolationValidator
{
    private readonly NotificationsDbContext              _db;
    private readonly SmsGovernanceTenantScopingOptions   _opts;

    public SmsGovernanceTenantIsolationValidator(
        NotificationsDbContext                      db,
        IOptions<SmsGovernanceTenantScopingOptions> options)
    {
        _db   = db;
        _opts = options.Value;
    }

    public async Task<IsolationValidationResult> ValidateTenantIsolationAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        var checks   = new List<CheckResult>();
        var errors   = new List<string>();
        var warnings = new List<string>();

        var assignments = await _db.SmsGovernanceTenantRulePackAssignments
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId)
            .ToListAsync(ct);

        var overlays = await _db.SmsGovernanceTenantOverlays
            .AsNoTracking()
            .Where(o => o.TenantId == tenantId)
            .ToListAsync(ct);

        // Check 1: assignment count within limit
        var activeCount = assignments.Count(a =>
            a.AssignmentState == SmsGovernanceTenantRulePackAssignment.AssignmentStates.Active);
        var check1 = activeCount <= _opts.MaxAssignmentsPerTenant;
        checks.Add(new("assignment_count_within_limit", check1,
            $"Active assignments: {activeCount} / limit: {_opts.MaxAssignmentsPerTenant}"));
        if (!check1) errors.Add($"Tenant exceeds MaxAssignmentsPerTenant ({_opts.MaxAssignmentsPerTenant}).");

        // Check 2: overlay count within limit
        var activeOverlayCount = overlays.Count(o =>
            o.Enabled && o.OverlayState == SmsGovernanceTenantOverlay.OverlayStates.Active);
        var check2 = activeOverlayCount <= _opts.MaxOverlaysPerTenant;
        checks.Add(new("overlay_count_within_limit", check2,
            $"Active overlays: {activeOverlayCount} / limit: {_opts.MaxOverlaysPerTenant}"));
        if (!check2) errors.Add($"Tenant exceeds MaxOverlaysPerTenant ({_opts.MaxOverlaysPerTenant}).");

        // Check 3: effective windows valid
        var windowViolations = assignments.Where(a =>
            a.EffectiveFrom.HasValue && a.EffectiveTo.HasValue &&
            a.EffectiveFrom >= a.EffectiveTo).ToList();
        checks.Add(new("effective_windows_valid", windowViolations.Count == 0,
            windowViolations.Count == 0 ? "All effective windows valid" : $"{windowViolations.Count} invalid window(s)"));
        if (windowViolations.Count > 0) warnings.Add("Some assignments have invalid effective windows (From >= To).");

        // Check 4: no duplicate active assignment for same pack
        var activePacks = assignments
            .Where(a => a.AssignmentState == SmsGovernanceTenantRulePackAssignment.AssignmentStates.Active)
            .GroupBy(a => a.RulePackId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key).ToList();
        checks.Add(new("no_duplicate_active_pack", activePacks.Count == 0,
            activePacks.Count == 0 ? "No duplicate active pack assignments" : $"{activePacks.Count} pack(s) have duplicate active assignments"));
        if (activePacks.Count > 0) warnings.Add("Some rule packs have duplicate active assignments for this tenant.");

        // Check 5: isolated mode assignments reference valid packs
        var isolatedAssignments = assignments.Where(a =>
            a.AssignmentMode == SmsGovernanceTenantRulePackAssignment.AssignmentModes.Isolated).ToList();
        checks.Add(new("isolated_mode_check", true,
            isolatedAssignments.Count == 0
                ? "No isolated-mode assignments"
                : $"{isolatedAssignments.Count} isolated-mode assignment(s) — global packs excluded for this tenant"));

        var valid = errors.Count == 0;
        return new IsolationValidationResult(valid, errors, warnings, checks);
    }

    public async Task<IsolationValidationResult> ValidateAssignmentAsync(
        AssignRulePackRequest request, CancellationToken ct = default)
    {
        var checks   = new List<CheckResult>();
        var errors   = new List<string>();
        var warnings = new List<string>();

        // Check 1: rule pack exists and is active
        var packExists = await _db.SmsGovernanceRulePacks
            .AsNoTracking()
            .AnyAsync(p => p.Id == request.RulePackId && p.Enabled && p.Status == "active", ct);
        checks.Add(new("rule_pack_exists_and_active", packExists,
            packExists ? "Rule pack found and active" : $"Rule pack {request.RulePackId} not found or not active"));
        if (!packExists) errors.Add($"Rule pack {request.RulePackId} does not exist or is not active.");

        // Check 2: max assignment limit not exceeded
        var activeCount = await _db.SmsGovernanceTenantRulePackAssignments
            .AsNoTracking()
            .CountAsync(a => a.TenantId == request.TenantId &&
                             a.AssignmentState == SmsGovernanceTenantRulePackAssignment.AssignmentStates.Active, ct);
        var withinLimit = activeCount < _opts.MaxAssignmentsPerTenant;
        checks.Add(new("within_assignment_limit", withinLimit,
            $"Current active assignments: {activeCount} / limit: {_opts.MaxAssignmentsPerTenant}"));
        if (!withinLimit) errors.Add($"Tenant already has {activeCount} active assignments (limit: {_opts.MaxAssignmentsPerTenant}).");

        // Check 3: no conflicting active assignment for same pack
        var duplicate = await _db.SmsGovernanceTenantRulePackAssignments
            .AsNoTracking()
            .AnyAsync(a => a.TenantId == request.TenantId && a.RulePackId == request.RulePackId &&
                           a.AssignmentState == SmsGovernanceTenantRulePackAssignment.AssignmentStates.Active, ct);
        checks.Add(new("no_duplicate_active", !duplicate,
            duplicate ? "Duplicate active assignment exists for this pack" : "No conflicting active assignment"));
        if (duplicate) warnings.Add("A duplicate active assignment for this pack already exists; consider deactivating the existing one first.");

        // Check 4: effective window valid
        if (request.EffectiveFrom.HasValue && request.EffectiveTo.HasValue)
        {
            var windowValid = request.EffectiveFrom < request.EffectiveTo;
            checks.Add(new("effective_window_valid", windowValid,
                windowValid ? "Effective window valid" : "EffectiveFrom must be before EffectiveTo"));
            if (!windowValid) errors.Add("EffectiveFrom must be before EffectiveTo.");
        }

        // Check 5: isolated mode warning
        if (request.AssignmentMode == SmsGovernanceTenantRulePackAssignment.AssignmentModes.Isolated)
            warnings.Add("Isolated mode: global rule packs will not apply to this tenant while this assignment is active.");

        var valid = errors.Count == 0;
        return new IsolationValidationResult(valid, errors, warnings, checks);
    }

    public async Task<IsolationValidationResult> ValidateOverlayAsync(
        CreateTenantOverlayRequest request, CancellationToken ct = default)
    {
        var checks   = new List<CheckResult>();
        var errors   = new List<string>();
        var warnings = new List<string>();

        // Check 1: max overlay limit
        var activeCount = await _db.SmsGovernanceTenantOverlays
            .AsNoTracking()
            .CountAsync(o => o.TenantId == request.TenantId && o.Enabled &&
                             o.OverlayState == SmsGovernanceTenantOverlay.OverlayStates.Active, ct);
        var withinLimit = activeCount < _opts.MaxOverlaysPerTenant;
        checks.Add(new("within_overlay_limit", withinLimit,
            $"Active overlays: {activeCount} / limit: {_opts.MaxOverlaysPerTenant}"));
        if (!withinLimit) errors.Add($"Tenant already has {activeCount} active overlays (limit: {_opts.MaxOverlaysPerTenant}).");

        // Check 2: target rule exists (when specified)
        if (request.RuleId.HasValue)
        {
            var ruleExists = await _db.SmsGovernanceRules
                .AsNoTracking()
                .AnyAsync(r => r.Id == request.RuleId.Value && r.Enabled, ct);
            checks.Add(new("target_rule_exists", ruleExists,
                ruleExists ? "Target rule found" : $"Rule {request.RuleId} not found or disabled"));
            if (!ruleExists) errors.Add($"Target rule {request.RuleId} does not exist or is disabled.");
        }

        // Check 3: target pack exists (when specified)
        if (request.RulePackId.HasValue)
        {
            var packExists = await _db.SmsGovernanceRulePacks
                .AsNoTracking()
                .AnyAsync(p => p.Id == request.RulePackId.Value && p.Enabled, ct);
            checks.Add(new("target_pack_exists", packExists,
                packExists ? "Target pack found" : $"Pack {request.RulePackId} not found or disabled"));
            if (!packExists) errors.Add($"Target pack {request.RulePackId} does not exist or is disabled.");
        }

        // Check 4: OverrideJson must not be excessively large or contain known sensitive patterns
        if (!string.IsNullOrEmpty(request.OverrideJson))
        {
            var jsonSafe = request.OverrideJson.Length <= 4000 && !ContainsSensitivePattern(request.OverrideJson);
            checks.Add(new("override_json_safe", jsonSafe,
                jsonSafe ? "OverrideJson within limits and safe" : "OverrideJson too large or contains disallowed patterns"));
            if (!jsonSafe) errors.Add("OverrideJson exceeds 4000 chars or contains disallowed content.");
        }

        // Check 5: effective window
        if (request.EffectiveFrom.HasValue && request.EffectiveTo.HasValue)
        {
            var windowValid = request.EffectiveFrom < request.EffectiveTo;
            checks.Add(new("effective_window_valid", windowValid,
                windowValid ? "Window valid" : "EffectiveFrom must be before EffectiveTo"));
            if (!windowValid) errors.Add("EffectiveFrom must be before EffectiveTo.");
        }

        var valid = errors.Count == 0;
        return new IsolationValidationResult(valid, errors, warnings, checks);
    }

    private static bool ContainsSensitivePattern(string json)
    {
        // Reject patterns that might indicate credential or PII content
        var lower = json.ToLowerInvariant();
        return lower.Contains("password") || lower.Contains("secret") ||
               lower.Contains("token")    || lower.Contains("apikey") ||
               lower.Contains("credenti") || lower.Contains("webhook");
    }
}
