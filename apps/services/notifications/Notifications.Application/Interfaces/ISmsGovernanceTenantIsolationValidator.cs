namespace Notifications.Application.Interfaces;

/// <summary>
/// LS-NOTIF-SMS-023: Read-only validator that checks tenant governance isolation
/// invariants before assignment/overlay creation. Returns safe diagnostics — no secrets.
/// </summary>
public interface ISmsGovernanceTenantIsolationValidator
{
    /// <summary>
    /// Validates the overall isolation health of a tenant's governance configuration.
    /// Checks: no cross-tenant leakage, isolated mode excludes global, valid effective windows, max limits.
    /// </summary>
    Task<IsolationValidationResult> ValidateTenantIsolationAsync(
        Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Validates an assignment request before it is persisted.
    /// Checks: rule pack exists, tenant does not exceed limit, no conflicting active assignment,
    /// effective window is valid, isolated mode does not accidentally inherit global packs.
    /// </summary>
    Task<IsolationValidationResult> ValidateAssignmentAsync(
        AssignRulePackRequest request, CancellationToken ct = default);

    /// <summary>
    /// Validates an overlay request before it is persisted.
    /// Checks: target rule/pack exists (when specified), overlay JSON is safe,
    /// tenant does not exceed overlay limit, effective window is valid.
    /// </summary>
    Task<IsolationValidationResult> ValidateOverlayAsync(
        CreateTenantOverlayRequest request, CancellationToken ct = default);
}

// ── Validation result ─────────────────────────────────────────────────────────

public record IsolationValidationResult(
    bool                        IsValid,
    IReadOnlyList<string>       Errors,
    IReadOnlyList<string>       Warnings,
    IReadOnlyList<CheckResult>  Checks);

public record CheckResult(
    string CheckName,
    bool   Passed,
    string Details);
