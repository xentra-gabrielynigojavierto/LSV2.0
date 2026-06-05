using Tenant.Application.DTOs;

namespace Tenant.Application.Interfaces;

/// <summary>
/// Migration utility service — Block 4 dry-run + Block 5 execution.
///
/// Block 4: RunDryRunAsync — read-only reconciliation.
/// Block 5: ExecuteAsync — write-capable migration with idempotency, rollback-safe
///          audit persistence, and post-run reconciliation.
///
/// Identity remains the runtime source of truth.
/// No read switch or Identity ownership removal is performed in this block.
/// </summary>
public interface IMigrationUtilityService
{
    /// <summary>
    /// Runs a dry-run reconciliation between Identity and Tenant service.
    /// Returns a structured report. Never writes to either database.
    /// </summary>
    Task<MigrationDryRunReport> RunDryRunAsync(CancellationToken ct = default);

    /// <summary>
    /// Block 5 — Executes migration from Identity into the Tenant service.
    ///
    /// Requirements:
    /// - Explicit call only; never triggered automatically.
    /// - Idempotent: re-running converges to the same result.
    /// - Preserves original TenantId exactly.
    /// - Persists a MigrationRun + MigrationRunItems audit record.
    /// - Runs post-execute reconciliation and includes it in the result.
    /// </summary>
    Task<MigrationExecutionResult> ExecuteAsync(
        MigrationExecuteRequest request,
        CancellationToken       ct = default);

    /// <summary>Returns the most recent migration run summaries, newest first.</summary>
    Task<IReadOnlyList<MigrationRunSummary>> GetHistoryAsync(
        int               limit = 20,
        CancellationToken ct    = default);

    /// <summary>Returns the full execution result for a specific run, or null if not found.</summary>
    Task<MigrationExecutionResult?> GetRunAsync(
        Guid              runId,
        CancellationToken ct = default);
}
