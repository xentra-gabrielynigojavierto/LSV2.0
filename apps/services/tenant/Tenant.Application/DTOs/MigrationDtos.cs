namespace Tenant.Application.DTOs;

// ── Migration utility DTOs ────────────────────────────────────────────────────

// ── Block 5 — Execute request ─────────────────────────────────────────────────

/// <summary>
/// Request body for POST /api/admin/migration/execute.
/// </summary>
public record MigrationExecuteRequest(
    /// <summary>"all" or "single"</summary>
    string  Scope        = "all",
    Guid?   TenantId     = null,
    string? TenantCode   = null,
    bool    AllowUpdates = true,
    bool    AllowCreates = true);

// ── Block 5 — Per-tenant execute result ──────────────────────────────────────

/// <summary>
/// Result of executing migration for a single Identity tenant.
/// </summary>
public record MigrationTenantResult(
    Guid    IdentityTenantId,
    string  Code,
    string  Name,
    /// <summary>"Created" | "Updated" | "Skipped" | "Conflict" | "Failed"</summary>
    string  ActionTaken,
    bool    TenantUpserted,
    bool    BrandingUpserted,
    bool    DomainUpserted,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Errors);

// ── Block 5 — Full execution result ──────────────────────────────────────────

/// <summary>
/// Structured result of a migration execute call.
/// Includes per-tenant results and post-run reconciliation summary.
/// </summary>
public record MigrationExecutionResult(
    Guid     RunId,
    DateTime GeneratedAtUtc,
    /// <summary>"DryRun" | "Execute"</summary>
    string   Mode,
    string   Scope,
    bool     IdentityAccessible,
    string?  IdentityAccessError,
    bool     TenantAccessible,
    int      TotalIdentityTenantsScanned,
    int      TenantsCreated,
    int      TenantsUpdated,
    int      TenantsSkipped,
    int      ConflictsDetected,
    int      ErrorsDetected,
    long     DurationMs,
    IReadOnlyList<MigrationTenantResult> TenantResults,
    MigrationDryRunReport? PostRunReconciliation);

// ── Block 5 — History list item ───────────────────────────────────────────────

/// <summary>Summary row used in the history list endpoint.</summary>
public record MigrationRunSummary(
    Guid     RunId,
    string   Mode,
    string   Scope,
    bool     IdentityAccessible,
    int      TotalScanned,
    int      Created,
    int      Updated,
    int      Skipped,
    int      Conflicts,
    int      Errors,
    long     DurationMs,
    DateTime StartedAtUtc,
    DateTime CompletedAtUtc);


/// <summary>
/// Summary of a dry-run reconciliation between Identity tenant data and Tenant service data.
/// Block 4 — foundation only; no writes performed.
/// </summary>
public record MigrationDryRunReport(
    DateTime  GeneratedAtUtc,
    bool      IdentityAccessible,
    string?   IdentityAccessError,
    int       IdentityTenantCount,
    int       TenantServiceCount,
    int       MissingInTenantService,
    int       CodeMismatches,
    int       NameMismatches,
    int       StatusMismatches,
    int       SubdomainGaps,
    int       LogoGaps,
    IReadOnlyList<MigrationTenantDiff> Differences);

/// <summary>Per-tenant difference record from the reconciliation.</summary>
public record MigrationTenantDiff(
    string   IdentityTenantId,
    string   IdentityCode,
    string   IdentityName,
    string   IdentityStatus,
    string?  IdentitySubdomain,
    bool     IdentityHasLogo,
    string?  TenantServiceId,
    string?  TenantServiceCode,
    string?  TenantServiceName,
    string?  TenantServiceStatus,
    bool     IsMissing,
    bool     HasCodeMismatch,
    bool     HasNameMismatch,
    bool     HasStatusMismatch,
    bool     HasSubdomainGap,
    bool     HasLogoGap);
