namespace Tenant.Domain;

/// <summary>
/// Action taken on a single Identity tenant during a migration execution run.
/// </summary>
public enum MigrationTenantAction
{
    Created,
    Updated,
    Skipped,
    Conflict,
    Failed
}

/// <summary>
/// Header record for a single migration execution.
///
/// Block 5 — rollback-safe audit persistence.
/// One row per POST /api/admin/migration/execute call.
/// Never deleted — provides a durable history of all migration activity.
/// </summary>
public class MigrationRun
{
    public Guid     Id              { get; private set; }

    /// <summary>"DryRun" or "Execute"</summary>
    public string   Mode            { get; private set; } = string.Empty;

    /// <summary>"all" | "single"</summary>
    public string   Scope           { get; private set; } = string.Empty;

    public bool     AllowCreates    { get; private set; }
    public bool     AllowUpdates    { get; private set; }

    public bool     IdentityAccessible { get; private set; }
    public bool     TenantAccessible   { get; private set; }

    public int      TotalScanned    { get; private set; }
    public int      TenantsCreated  { get; private set; }
    public int      TenantsUpdated  { get; private set; }
    public int      TenantsSkipped  { get; private set; }
    public int      Conflicts       { get; private set; }
    public int      Errors          { get; private set; }
    public long     DurationMs      { get; private set; }

    public string?  ErrorMessage    { get; private set; }

    public DateTime StartedAtUtc    { get; private set; }
    public DateTime CompletedAtUtc  { get; private set; }

    public ICollection<MigrationRunItem> Items { get; private set; } = [];

    private MigrationRun() { }

    public static MigrationRun Create(
        string mode,
        string scope,
        bool   allowCreates,
        bool   allowUpdates)
    {
        var now = DateTime.UtcNow;
        return new MigrationRun
        {
            Id            = Guid.NewGuid(),
            Mode          = mode,
            Scope         = scope,
            AllowCreates  = allowCreates,
            AllowUpdates  = allowUpdates,
            StartedAtUtc  = now,
            CompletedAtUtc = now
        };
    }

    public void Complete(
        bool   identityAccessible,
        bool   tenantAccessible,
        int    totalScanned,
        int    created,
        int    updated,
        int    skipped,
        int    conflicts,
        int    errors,
        long   durationMs,
        string? errorMessage = null)
    {
        IdentityAccessible = identityAccessible;
        TenantAccessible   = tenantAccessible;
        TotalScanned       = totalScanned;
        TenantsCreated     = created;
        TenantsUpdated     = updated;
        TenantsSkipped     = skipped;
        Conflicts          = conflicts;
        Errors             = errors;
        DurationMs         = durationMs;
        ErrorMessage       = errorMessage;
        CompletedAtUtc     = DateTime.UtcNow;
    }
}

/// <summary>
/// Per-tenant audit item within a migration run.
///
/// Block 5 — one row per Identity tenant processed.
/// </summary>
public class MigrationRunItem
{
    public Guid                  Id               { get; private set; }
    public Guid                  RunId            { get; private set; }
    public Guid                  IdentityTenantId { get; private set; }
    public string                Code             { get; private set; } = string.Empty;
    public MigrationTenantAction ActionTaken      { get; private set; }
    public bool                  TenantUpserted   { get; private set; }
    public bool                  BrandingUpserted { get; private set; }
    public bool                  DomainUpserted   { get; private set; }
    public string?               Warnings         { get; private set; }
    public string?               Errors           { get; private set; }
    public DateTime              CreatedAtUtc     { get; private set; }

    public MigrationRun? Run { get; private set; }

    private MigrationRunItem() { }

    public static MigrationRunItem Create(
        Guid                  runId,
        Guid                  identityTenantId,
        string                code,
        MigrationTenantAction action,
        bool                  tenantUpserted   = false,
        bool                  brandingUpserted = false,
        bool                  domainUpserted   = false,
        string?               warnings         = null,
        string?               errors           = null)
    {
        return new MigrationRunItem
        {
            Id               = Guid.NewGuid(),
            RunId            = runId,
            IdentityTenantId = identityTenantId,
            Code             = code,
            ActionTaken      = action,
            TenantUpserted   = tenantUpserted,
            BrandingUpserted = brandingUpserted,
            DomainUpserted   = domainUpserted,
            Warnings         = warnings,
            Errors           = errors,
            CreatedAtUtc     = DateTime.UtcNow
        };
    }
}
