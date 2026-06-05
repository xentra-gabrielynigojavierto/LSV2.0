namespace Notifications.Application.Interfaces;

// ─── Import request / result types ───────────────────────────────────────────

/// <summary>LS-NOTIF-SMS-020: A single rule entry within a bulk import payload.</summary>
public sealed class ImportRuleEntry
{
    public string  RuleType     { get; init; } = string.Empty;
    public string  Name         { get; init; } = string.Empty;
    public string? Description  { get; init; }
    public string? Pattern      { get; init; }
    public string  Severity     { get; init; } = "block";
    public int     Priority     { get; init; } = 100;
    public bool    Enabled      { get; init; } = true;
    public string? MetadataJson { get; init; }
}

/// <summary>LS-NOTIF-SMS-020: A single rule-pack entry within a bulk import payload.</summary>
public sealed class ImportRulePackEntry
{
    public Guid?   TenantId        { get; init; }
    public string  Name            { get; init; } = string.Empty;
    public string? Description     { get; init; }
    public string  InheritanceMode { get; init; } = "merge";
    public string  Status          { get; init; } = "draft";
    public int     Priority        { get; init; } = 100;
    public bool    Enabled         { get; init; } = true;
    public DateTime? EffectiveFrom { get; init; }
    public DateTime? EffectiveTo   { get; init; }
}

/// <summary>LS-NOTIF-SMS-020: A single import bundle (one pack + its rules).</summary>
public sealed class GovernanceImportBundle
{
    public ImportRulePackEntry         RulePack { get; init; } = new();
    public IReadOnlyList<ImportRuleEntry> Rules { get; init; } = [];
}

/// <summary>LS-NOTIF-SMS-020: Full bulk import request (one or more bundles).</summary>
public sealed class GovernanceImportRequest
{
    public IReadOnlyList<GovernanceImportBundle> Bundles { get; init; } = [];

    /// <summary>
    /// When true, validates the entire import but writes nothing to the database.
    /// Same validation logic as a real import; result will always have Persisted=false.
    /// </summary>
    public bool DryRun      { get; init; }
    public string? RequestedBy { get; init; }
}

/// <summary>LS-NOTIF-SMS-020: Row-level validation error for a single import entry.</summary>
public sealed class ImportValidationError
{
    /// <summary>Zero-based bundle index.</summary>
    public int     BundleIndex { get; init; }

    /// <summary>-1 = pack-level error; 0+ = zero-based rule index within the bundle.</summary>
    public int     RuleIndex   { get; init; } = -1;

    public string  Field       { get; init; } = string.Empty;
    public string  Message     { get; init; } = string.Empty;
}

/// <summary>LS-NOTIF-SMS-020: Result of a validate or import operation.</summary>
public sealed class GovernanceImportResult
{
    public bool    IsValid         { get; init; }
    public bool    Persisted       { get; init; }
    public int     BundlesImported { get; init; }
    public int     RulesImported   { get; init; }

    public IReadOnlyList<ImportValidationError> Errors { get; init; } = [];

    public static GovernanceImportResult ValidationFailed(IReadOnlyList<ImportValidationError> errors) =>
        new() { IsValid = false, Persisted = false, Errors = errors };
}

// ─── Export query / result types ─────────────────────────────────────────────

/// <summary>LS-NOTIF-SMS-020: Filter parameters for governance export.</summary>
public sealed class GovernanceExportQuery
{
    public Guid?   TenantId   { get; init; }
    public Guid?   RulePackId { get; init; }
    public string? Status     { get; init; }
    public string? RuleType   { get; init; }
    public string? Severity   { get; init; }

    /// <summary>When true, include compliance profiles in the export.</summary>
    public bool    IncludeProfiles { get; init; }
}

// ─── Interface ────────────────────────────────────────────────────────────────

/// <summary>
/// LS-NOTIF-SMS-020: Governance rule bulk import and export service.
///
/// Import is transactional — all bundles are validated fully before any DB write.
/// Invalid imports are rejected entirely; no partial state is persisted.
/// Exported data never contains secrets, credentials, or raw phone numbers.
/// </summary>
public interface ISmsGovernanceImportService
{
    /// <summary>
    /// Validate the import request without writing anything to the database.
    /// Returns row-level errors for every invalid entry.
    /// Equivalent to calling ImportAsync with DryRun=true.
    /// </summary>
    Task<GovernanceImportResult> ValidateImportAsync(
        GovernanceImportRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Validate and (if DryRun=false) persist all bundles in a single transaction.
    /// Creates version snapshots (ChangeType=imported) for every persisted entity.
    /// Rolls back the entire transaction if any validation error is found.
    /// </summary>
    Task<GovernanceImportResult> ImportAsync(
        GovernanceImportRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Export rule packs, rules, and optionally profiles as a structured JSON payload.
    /// Applies the provided filters. Never exports secrets or raw phone numbers.
    /// </summary>
    Task<object> ExportAsync(
        GovernanceExportQuery query,
        CancellationToken ct = default);
}
