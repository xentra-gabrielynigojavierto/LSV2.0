using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using Tenant.Application.DTOs;
using Tenant.Application.Interfaces;
using Tenant.Domain;
using Tenant.Infrastructure.Data;

namespace Tenant.Infrastructure.Services;

/// <summary>
/// Block 4 + Block 5 migration utility.
///
/// Block 4: RunDryRunAsync — read-only reconciliation between Identity and Tenant service.
/// Block 5: ExecuteAsync   — write-capable, idempotent migration with rollback-safe audit persistence.
///
/// Identity is accessed via a separate read-only MySqlConnection using "IdentityDb" connection string.
/// If absent or unreachable, all methods fail gracefully with structured error responses.
///
/// Block 4 SQL bug fixes applied in Block 5:
///   - Table name corrected from `tenants` to `idt_Tenants`
///   - Column name corrected from `DisplayName` to `Name`
/// </summary>
public class MigrationUtilityService : IMigrationUtilityService
{
    private readonly TenantDbContext _db;
    private readonly IConfiguration  _configuration;
    private readonly ILogger<MigrationUtilityService> _logger;

    public MigrationUtilityService(
        TenantDbContext  db,
        IConfiguration   configuration,
        ILogger<MigrationUtilityService> logger)
    {
        _db            = db;
        _configuration = configuration;
        _logger        = logger;
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  DRY-RUN (Block 4, preserved and corrected)
    // ══════════════════════════════════════════════════════════════════════════

    public async Task<MigrationDryRunReport> RunDryRunAsync(CancellationToken ct = default)
    {
        var generatedAt = DateTime.UtcNow;
        _logger.LogInformation("Migration dry-run started at {Time}", generatedAt);

        var tenantRows = await LoadTenantServiceRowsAsync(ct);
        _logger.LogInformation("Tenant service has {Count} tenants", tenantRows.Count);

        var identityCs = _configuration.GetConnectionString("IdentityDb");
        if (string.IsNullOrWhiteSpace(identityCs))
        {
            _logger.LogWarning("IdentityDb connection string not configured — dry-run reports as unavailable.");
            return BuildUnavailableReport(generatedAt, tenantRows.Count,
                "ConnectionStrings:IdentityDb is not configured.");
        }

        List<IdentityTenantRow> identityTenants;
        try
        {
            identityTenants = await LoadIdentityTenantsAsync(identityCs, ct);
            _logger.LogInformation("Identity database has {Count} tenants", identityTenants.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read Identity tenant data for dry-run.");
            return BuildUnavailableReport(generatedAt, tenantRows.Count, ex.Message);
        }

        return Reconcile(generatedAt, identityTenants, tenantRows);
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  EXECUTE (Block 5)
    // ══════════════════════════════════════════════════════════════════════════

    public async Task<MigrationExecutionResult> ExecuteAsync(
        MigrationExecuteRequest request,
        CancellationToken       ct = default)
    {
        var sw          = Stopwatch.StartNew();
        var startedAt   = DateTime.UtcNow;
        var scope       = string.Equals(request.Scope, "single", StringComparison.OrdinalIgnoreCase)
            ? "single" : "all";

        _logger.LogInformation(
            "Migration execute started. Scope={Scope} AllowCreates={Creates} AllowUpdates={Updates}",
            scope, request.AllowCreates, request.AllowUpdates);

        // Create audit run record immediately so we have a RunId to return.
        var run = MigrationRun.Create("Execute", scope, request.AllowCreates, request.AllowUpdates);
        _db.MigrationRuns.Add(run);
        await _db.SaveChangesAsync(ct);

        // ── Pre-validation: Identity accessible? ──────────────────────────────

        var identityCs = _configuration.GetConnectionString("IdentityDb");
        if (string.IsNullOrWhiteSpace(identityCs))
        {
            var errMsg = "ConnectionStrings:IdentityDb is not configured. Migration aborted.";
            _logger.LogWarning(errMsg);
            run.Complete(false, true, 0, 0, 0, 0, 0, 1, sw.ElapsedMilliseconds, errMsg);
            await _db.SaveChangesAsync(ct);
            return FailedResult(run.Id, startedAt, scope, identityCs, errMsg);
        }

        // ── Load Identity tenants ─────────────────────────────────────────────

        List<IdentityTenantRow> identityTenants;
        try
        {
            identityTenants = await LoadIdentityTenantsAsync(identityCs, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cannot load Identity tenants for migration execute.");
            run.Complete(false, true, 0, 0, 0, 0, 0, 1, sw.ElapsedMilliseconds, ex.Message);
            await _db.SaveChangesAsync(ct);
            return FailedResult(run.Id, startedAt, scope, identityCs, ex.Message);
        }

        _logger.LogInformation("Loaded {Count} Identity tenants for migration.", identityTenants.Count);

        // ── Apply scope filter (single tenant by Id or Code) ──────────────────

        if (scope == "single")
        {
            if (request.TenantId.HasValue)
            {
                var idStr = request.TenantId.Value.ToString();
                identityTenants = identityTenants
                    .Where(t => string.Equals(t.Id, idStr, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
            else if (!string.IsNullOrWhiteSpace(request.TenantCode))
            {
                var code = request.TenantCode.Trim().ToLowerInvariant();
                identityTenants = identityTenants
                    .Where(t => string.Equals(t.Code.ToLowerInvariant(), code, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            if (identityTenants.Count == 0)
            {
                var errMsg = "No matching Identity tenant found for the specified scope filter.";
                run.Complete(true, true, 0, 0, 0, 0, 0, 1, sw.ElapsedMilliseconds, errMsg);
                await _db.SaveChangesAsync(ct);
                return FailedResult(run.Id, startedAt, scope, identityCs, errMsg);
            }
        }

        // ── Pre-validation: duplicate Identity codes ───────────────────────────

        var duplicateCodes = identityTenants
            .GroupBy(t => t.Code.ToLowerInvariant())
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateCodes.Count > 0)
        {
            var errMsg = $"Identity source has duplicate tenant codes: {string.Join(", ", duplicateCodes)}. Migration aborted.";
            _logger.LogError(errMsg);
            run.Complete(true, true, identityTenants.Count, 0, 0, 0, duplicateCodes.Count, 1,
                sw.ElapsedMilliseconds, errMsg);
            await _db.SaveChangesAsync(ct);
            return FailedResult(run.Id, startedAt, scope, identityCs, errMsg);
        }

        // ── Load current Tenant service state ─────────────────────────────────

        var existingTenantsById = await _db.Tenants
            .AsNoTracking()
            .ToDictionaryAsync(t => t.Id, ct);

        var existingTenantsByCode = existingTenantsById.Values
            .GroupBy(t => t.Code.ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.First());

        var existingBrandingByTenantId = await _db.Brandings
            .AsNoTracking()
            .ToDictionaryAsync(b => b.TenantId, ct);

        var existingDomainsByTenantId = await _db.Domains
            .AsNoTracking()
            .Where(d => d.DomainType == TenantDomainType.Subdomain)
            .GroupBy(d => d.TenantId)
            .ToDictionaryAsync(g => g.Key, g => g.ToList(), ct);

        // ── Process each Identity tenant ──────────────────────────────────────

        var tenantResults = new List<MigrationTenantResult>();
        int created = 0, updated = 0, skipped = 0, conflicts = 0, errors = 0;

        foreach (var identity in identityTenants)
        {
            var result = await ProcessTenantAsync(
                identity,
                request,
                existingTenantsById,
                existingTenantsByCode,
                existingBrandingByTenantId,
                existingDomainsByTenantId,
                run.Id,
                ct);

            tenantResults.Add(result);

            switch (result.ActionTaken)
            {
                case "Created":  created++;   break;
                case "Updated":  updated++;   break;
                case "Skipped":  skipped++;   break;
                case "Conflict": conflicts++; break;
                case "Failed":   errors++;    break;
            }
        }

        sw.Stop();

        // ── Persist run completion ─────────────────────────────────────────────

        run.Complete(
            identityAccessible: true,
            tenantAccessible:   true,
            totalScanned:       identityTenants.Count,
            created:            created,
            updated:            updated,
            skipped:            skipped,
            conflicts:          conflicts,
            errors:             errors,
            durationMs:         sw.ElapsedMilliseconds);

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Migration execute complete in {Ms}ms. Created={C} Updated={U} Skipped={S} Conflicts={X} Errors={E}",
            sw.ElapsedMilliseconds, created, updated, skipped, conflicts, errors);

        // ── Post-run reconciliation ────────────────────────────────────────────

        MigrationDryRunReport? postRun = null;
        try
        {
            postRun = await RunDryRunAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Post-run reconciliation failed — result will be null.");
        }

        return new MigrationExecutionResult(
            RunId:                       run.Id,
            GeneratedAtUtc:              startedAt,
            Mode:                        "Execute",
            Scope:                       scope,
            IdentityAccessible:          true,
            IdentityAccessError:         null,
            TenantAccessible:            true,
            TotalIdentityTenantsScanned: identityTenants.Count,
            TenantsCreated:              created,
            TenantsUpdated:              updated,
            TenantsSkipped:              skipped,
            ConflictsDetected:           conflicts,
            ErrorsDetected:              errors,
            DurationMs:                  sw.ElapsedMilliseconds,
            TenantResults:               tenantResults.AsReadOnly(),
            PostRunReconciliation:       postRun);
    }

    // ── Per-tenant upsert ─────────────────────────────────────────────────────

    private async Task<MigrationTenantResult> ProcessTenantAsync(
        IdentityTenantRow                                  identity,
        MigrationExecuteRequest                            request,
        Dictionary<Guid, Domain.Tenant>                   existingById,
        Dictionary<string, Domain.Tenant>                  existingByCode,
        Dictionary<Guid, TenantBranding>                   existingBrandings,
        Dictionary<Guid, List<TenantDomain>>               existingDomains,
        Guid                                               runId,
        CancellationToken                                  ct)
    {
        var warnings = new List<string>();
        var errMsgs  = new List<string>();

        if (!Guid.TryParse(identity.Id, out var tenantId))
        {
            var err = $"Identity tenant '{identity.Code}' has unparseable Id '{identity.Id}'.";
            _logger.LogError(err);
            errMsgs.Add(err);
            _db.MigrationRunItems.Add(MigrationRunItem.Create(
                runId, Guid.Empty, identity.Code, MigrationTenantAction.Failed,
                errors: string.Join("; ", errMsgs)));
            await _db.SaveChangesAsync(ct);
            return ToDto(identity, Guid.Empty, "Failed", false, false, false, warnings, errMsgs);
        }

        var codeKey  = identity.Code.ToLowerInvariant();
        var status   = MapStatus(identity.IsActive, identity.ProvisioningStatus);
        bool tenantUpserted   = false;
        bool brandingUpserted = false;
        bool domainUpserted   = false;
        string action;

        try
        {
            // ── Conflict detection ─────────────────────────────────────────────

            // A different tenant in Tenant service has the same code but a different Id.
            if (existingByCode.TryGetValue(codeKey, out var codeMatch) && codeMatch.Id != tenantId)
            {
                var w = $"Code conflict: Tenant service has '{codeKey}' with Id={codeMatch.Id}, " +
                        $"but Identity has Id={tenantId}. Skipping.";
                warnings.Add(w);
                _logger.LogWarning(w);
                _db.MigrationRunItems.Add(MigrationRunItem.Create(
                    runId, tenantId, identity.Code, MigrationTenantAction.Conflict,
                    warnings: string.Join("; ", warnings)));
                await _db.SaveChangesAsync(ct);
                return ToDto(identity, tenantId, "Conflict", false, false, false, warnings, errMsgs);
            }

            // ── Tenant core upsert ─────────────────────────────────────────────

            if (existingById.TryGetValue(tenantId, out var existing))
            {
                // UPDATE path
                if (!request.AllowUpdates)
                {
                    action = "Skipped";
                    warnings.Add("AllowUpdates=false — skipping existing tenant.");
                }
                else
                {
                    // Re-attach for tracking.
                    var tracked = await _db.Tenants.FindAsync([tenantId], ct)
                        ?? throw new InvalidOperationException($"Tenant {tenantId} not found during update.");

                    tracked.UpdateProfile(
                        displayName:  identity.Name,
                        legalName:    null,
                        description:  null,
                        websiteUrl:   null,
                        timeZone:     null,
                        locale:       null,
                        supportEmail: null,
                        supportPhone: null);

                    tracked.SetStatus(status);

                    if (!string.IsNullOrWhiteSpace(identity.Subdomain))
                        tracked.SetSubdomain(identity.Subdomain);

                    tracked.SetLogo(identity.LogoDocumentId);
                    tracked.SetLogoWhite(identity.LogoWhiteDocumentId);

                    tenantUpserted = true;
                    action = "Updated";
                }
            }
            else
            {
                // CREATE path
                if (!request.AllowCreates)
                {
                    action = "Skipped";
                    warnings.Add("AllowCreates=false — skipping new tenant.");
                }
                else
                {
                    var newTenant = Domain.Tenant.Rehydrate(
                        id:                 tenantId,
                        code:               identity.Code,
                        displayName:        identity.Name,
                        status:             status,
                        subdomain:          identity.Subdomain,
                        logoDocumentId:     identity.LogoDocumentId,
                        logoWhiteDocumentId: identity.LogoWhiteDocumentId,
                        createdAtUtc:       identity.CreatedAtUtc,
                        updatedAtUtc:       identity.UpdatedAtUtc);

                    _db.Tenants.Add(newTenant);
                    tenantUpserted = true;
                    action = "Created";
                }
            }

            await _db.SaveChangesAsync(ct);

            // ── Branding upsert ────────────────────────────────────────────────

            if (tenantUpserted && (identity.LogoDocumentId.HasValue || identity.LogoWhiteDocumentId.HasValue))
            {
                TenantBranding branding;

                if (existingBrandings.TryGetValue(tenantId, out _))
                {
                    branding = await _db.Brandings.FindAsync([tenantId], ct)
                        ?? throw new InvalidOperationException($"Branding for {tenantId} not found.");

                    // Only update logo fields; preserve other branding data.
                    branding.Update(
                        logoDocumentId:      identity.LogoDocumentId,
                        logoWhiteDocumentId: identity.LogoWhiteDocumentId);
                }
                else
                {
                    branding = TenantBranding.Create(tenantId);
                    branding.Update(
                        logoDocumentId:      identity.LogoDocumentId,
                        logoWhiteDocumentId: identity.LogoWhiteDocumentId);
                    _db.Brandings.Add(branding);
                }

                brandingUpserted = true;
                await _db.SaveChangesAsync(ct);
            }

            // ── Domain compat upsert ───────────────────────────────────────────

            if (tenantUpserted && !string.IsNullOrWhiteSpace(identity.Subdomain))
            {
                var normalized = TenantDomain.NormalizeHost(identity.Subdomain);

                if (!TenantDomain.IsValidHost(normalized))
                {
                    warnings.Add(
                        $"Subdomain '{identity.Subdomain}' is not a valid hostname — skipping domain record creation. " +
                        "Tenant.Subdomain still preserved.");
                }
                else
                {
                    // Check for existing Subdomain-type record with the same host.
                    var existingDomainList = existingDomains.GetValueOrDefault(tenantId);
                    var alreadyExists      = existingDomainList?.Any(d =>
                        string.Equals(d.Host, normalized, StringComparison.OrdinalIgnoreCase)) ?? false;

                    if (!alreadyExists)
                    {
                        var domain = TenantDomain.Create(
                            tenantId:   tenantId,
                            host:       normalized,
                            domainType: TenantDomainType.Subdomain,
                            isPrimary:  true,
                            status:     TenantDomainStatus.Active);

                        _db.Domains.Add(domain);
                        domainUpserted = true;
                        await _db.SaveChangesAsync(ct);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Identity tenant {Code} ({Id})", identity.Code, identity.Id);
            errMsgs.Add(ex.Message);
            action = "Failed";
            try { await _db.SaveChangesAsync(ct); } catch { /* best effort */ }
        }

        // ── Persist audit item ─────────────────────────────────────────────────

        _db.MigrationRunItems.Add(MigrationRunItem.Create(
            runId:            runId,
            identityTenantId: tenantId,
            code:             identity.Code,
            action:           Enum.Parse<MigrationTenantAction>(action),
            tenantUpserted:   tenantUpserted,
            brandingUpserted: brandingUpserted,
            domainUpserted:   domainUpserted,
            warnings:         warnings.Count > 0 ? string.Join("; ", warnings) : null,
            errors:           errMsgs.Count  > 0 ? string.Join("; ", errMsgs)  : null));

        await _db.SaveChangesAsync(ct);

        return ToDto(identity, tenantId, action, tenantUpserted, brandingUpserted, domainUpserted, warnings, errMsgs);
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  HISTORY (Block 5)
    // ══════════════════════════════════════════════════════════════════════════

    public async Task<IReadOnlyList<MigrationRunSummary>> GetHistoryAsync(
        int               limit = 20,
        CancellationToken ct    = default)
    {
        var runs = await _db.MigrationRuns
            .AsNoTracking()
            .OrderByDescending(r => r.StartedAtUtc)
            .Take(Math.Clamp(limit, 1, 100))
            .ToListAsync(ct);

        return runs.Select(r => new MigrationRunSummary(
            RunId:              r.Id,
            Mode:               r.Mode,
            Scope:              r.Scope,
            IdentityAccessible: r.IdentityAccessible,
            TotalScanned:       r.TotalScanned,
            Created:            r.TenantsCreated,
            Updated:            r.TenantsUpdated,
            Skipped:            r.TenantsSkipped,
            Conflicts:          r.Conflicts,
            Errors:             r.Errors,
            DurationMs:         r.DurationMs,
            StartedAtUtc:       r.StartedAtUtc,
            CompletedAtUtc:     r.CompletedAtUtc)).ToList().AsReadOnly();
    }

    public async Task<MigrationExecutionResult?> GetRunAsync(
        Guid              runId,
        CancellationToken ct = default)
    {
        var run = await _db.MigrationRuns
            .AsNoTracking()
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.Id == runId, ct);

        if (run is null) return null;

        var tenantResults = run.Items.Select(i => new MigrationTenantResult(
            IdentityTenantId: i.IdentityTenantId,
            Code:             i.Code,
            Name:             i.Code,
            ActionTaken:      i.ActionTaken.ToString(),
            TenantUpserted:   i.TenantUpserted,
            BrandingUpserted: i.BrandingUpserted,
            DomainUpserted:   i.DomainUpserted,
            Warnings:         SplitNullable(i.Warnings),
            Errors:           SplitNullable(i.Errors))).ToList().AsReadOnly();

        return new MigrationExecutionResult(
            RunId:                       run.Id,
            GeneratedAtUtc:              run.StartedAtUtc,
            Mode:                        run.Mode,
            Scope:                       run.Scope,
            IdentityAccessible:          run.IdentityAccessible,
            IdentityAccessError:         run.ErrorMessage,
            TenantAccessible:            run.TenantAccessible,
            TotalIdentityTenantsScanned: run.TotalScanned,
            TenantsCreated:              run.TenantsCreated,
            TenantsUpdated:              run.TenantsUpdated,
            TenantsSkipped:              run.TenantsSkipped,
            ConflictsDetected:           run.Conflicts,
            ErrorsDetected:              run.Errors,
            DurationMs:                  run.DurationMs,
            TenantResults:               tenantResults,
            PostRunReconciliation:       null);
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  RECONCILIATION HELPERS (shared by dry-run and post-execute)
    // ══════════════════════════════════════════════════════════════════════════

    private async Task<List<TenantServiceRow>> LoadTenantServiceRowsAsync(CancellationToken ct)
    {
        var raw = await _db.Tenants.AsNoTracking()
            .Select(t => new { t.Id, t.Code, t.DisplayName, t.Status, t.Subdomain })
            .ToListAsync(ct);

        return raw.Select(t => new TenantServiceRow(
            t.Id, t.Code, t.DisplayName, t.Status.ToString(), t.Subdomain)).ToList();
    }

    private MigrationDryRunReport Reconcile(
        DateTime                generatedAt,
        List<IdentityTenantRow> identityTenants,
        List<TenantServiceRow>  tenantRows)
    {
        var tenantByCode = tenantRows
            .GroupBy(t => t.Code.ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.First());

        var differences = new List<MigrationTenantDiff>();

        foreach (var identity in identityTenants)
        {
            var key = identity.Code.ToLowerInvariant();
            tenantByCode.TryGetValue(key, out var matched);

            var isMissing         = matched is null;
            var hasCodeMismatch   = matched is not null
                && !string.Equals(identity.Code, matched.Code, StringComparison.OrdinalIgnoreCase);
            var hasNameMismatch   = matched is not null
                && !string.Equals(identity.Name, matched.DisplayName, StringComparison.OrdinalIgnoreCase);
            var hasStatusMismatch = matched is not null
                && !string.Equals(
                    NormalizeStatus(identity.IsActive, identity.ProvisioningStatus).ToString(),
                    matched.Status,
                    StringComparison.OrdinalIgnoreCase);
            var hasSubdomainGap   = matched is not null
                && !string.IsNullOrWhiteSpace(identity.Subdomain)
                && string.IsNullOrWhiteSpace(matched.Subdomain);
            var hasLogoGap        = matched is not null
                && identity.HasLogo
                && !matched.HasLogo;

            if (isMissing || hasCodeMismatch || hasNameMismatch ||
                hasStatusMismatch || hasSubdomainGap || hasLogoGap)
            {
                differences.Add(new MigrationTenantDiff(
                    IdentityTenantId:    identity.Id,
                    IdentityCode:        identity.Code,
                    IdentityName:        identity.Name,
                    IdentityStatus:      identity.ProvisioningStatus,
                    IdentitySubdomain:   identity.Subdomain,
                    IdentityHasLogo:     identity.HasLogo,
                    TenantServiceId:     matched?.Id.ToString(),
                    TenantServiceCode:   matched?.Code,
                    TenantServiceName:   matched?.DisplayName,
                    TenantServiceStatus: matched?.Status,
                    IsMissing:           isMissing,
                    HasCodeMismatch:     hasCodeMismatch,
                    HasNameMismatch:     hasNameMismatch,
                    HasStatusMismatch:   hasStatusMismatch,
                    HasSubdomainGap:     hasSubdomainGap,
                    HasLogoGap:          hasLogoGap));
            }
        }

        var report = new MigrationDryRunReport(
            GeneratedAtUtc:         generatedAt,
            IdentityAccessible:     true,
            IdentityAccessError:    null,
            IdentityTenantCount:    identityTenants.Count,
            TenantServiceCount:     tenantRows.Count,
            MissingInTenantService: differences.Count(d => d.IsMissing),
            CodeMismatches:         differences.Count(d => d.HasCodeMismatch),
            NameMismatches:         differences.Count(d => d.HasNameMismatch),
            StatusMismatches:       differences.Count(d => d.HasStatusMismatch),
            SubdomainGaps:          differences.Count(d => d.HasSubdomainGap),
            LogoGaps:               differences.Count(d => d.HasLogoGap),
            Differences:            differences.AsReadOnly());

        _logger.LogInformation(
            "Reconciliation: Missing={M} CodeMismatch={C} NameMismatch={N}",
            report.MissingInTenantService, report.CodeMismatches, report.NameMismatches);

        return report;
    }

    private MigrationDryRunReport BuildUnavailableReport(
        DateTime generatedAt, int tenantServiceCount, string error) =>
        new(
            GeneratedAtUtc:         generatedAt,
            IdentityAccessible:     false,
            IdentityAccessError:    error,
            IdentityTenantCount:    0,
            TenantServiceCount:     tenantServiceCount,
            MissingInTenantService: 0,
            CodeMismatches:         0,
            NameMismatches:         0,
            StatusMismatches:       0,
            SubdomainGaps:          0,
            LogoGaps:               0,
            Differences:            Array.Empty<MigrationTenantDiff>());

    // ══════════════════════════════════════════════════════════════════════════
    //  IDENTITY DATA ACCESS (read-only, raw SQL)
    // ══════════════════════════════════════════════════════════════════════════

    private static async Task<List<IdentityTenantRow>> LoadIdentityTenantsAsync(
        string            connectionString,
        CancellationToken ct)
    {
        // B04 bugs fixed: table is idt_Tenants, column is Name (not DisplayName).
        // Also loads IsActive, ProvisioningStatus, LogoWhiteDocumentId, and timestamps
        // for the B05 write/status-mapping paths.
        const string sql = """
            SELECT
                CAST(Id              AS CHAR) AS Id,
                Code,
                Name,
                IsActive,
                ProvisioningStatus,
                Subdomain,
                CAST(LogoDocumentId      AS CHAR) AS LogoDocumentId,
                CAST(LogoWhiteDocumentId AS CHAR) AS LogoWhiteDocumentId,
                CreatedAtUtc,
                UpdatedAtUtc
            FROM idt_Tenants
            ORDER BY Code;
            """;

        await using var conn   = new MySqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var cmd    = new MySqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var rows = new List<IdentityTenantRow>();
        while (await reader.ReadAsync(ct))
        {
            var logoRaw  = reader.IsDBNull(reader.GetOrdinal("LogoDocumentId"))
                ? null : reader.GetString("LogoDocumentId");
            var logoWRaw = reader.IsDBNull(reader.GetOrdinal("LogoWhiteDocumentId"))
                ? null : reader.GetString("LogoWhiteDocumentId");

            rows.Add(new IdentityTenantRow(
                Id:                 reader.GetString("Id"),
                Code:               reader.GetString("Code"),
                Name:               reader.GetString("Name"),
                IsActive:           reader.GetBoolean("IsActive"),
                ProvisioningStatus: reader.GetString("ProvisioningStatus"),
                Subdomain:          reader.IsDBNull(reader.GetOrdinal("Subdomain"))
                    ? null : reader.GetString("Subdomain"),
                LogoDocumentId:     logoRaw  is not null && Guid.TryParse(logoRaw,  out var lg) ? lg : null,
                LogoWhiteDocumentId: logoWRaw is not null && Guid.TryParse(logoWRaw, out var lgw) ? lgw : null,
                HasLogo:            logoRaw  is not null,
                CreatedAtUtc:       reader.GetDateTime("CreatedAtUtc"),
                UpdatedAtUtc:       reader.GetDateTime("UpdatedAtUtc")));
        }

        return rows;
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  STATUS MAPPING
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Maps Identity lifecycle state to TenantStatus.
    ///
    /// Identity.IsActive=true  AND ProvisioningStatus=Active    → Active
    /// Identity.IsActive=false                                   → Inactive
    /// ProvisioningStatus ∈ {Pending, InProgress, Verifying}     → Pending
    /// ProvisioningStatus = Failed                               → Inactive
    /// Default                                                   → Active
    /// </summary>
    private static TenantStatus MapStatus(bool isActive, string provisioningStatus)
    {
        if (!isActive) return TenantStatus.Inactive;

        return provisioningStatus?.Trim() switch
        {
            "Active"     => TenantStatus.Active,
            "Pending"    => TenantStatus.Pending,
            "InProgress" => TenantStatus.Pending,
            "Verifying"  => TenantStatus.Pending,
            "Failed"     => TenantStatus.Inactive,
            _            => TenantStatus.Active
        };
    }

    /// <summary>B04 compat — maps raw status string (for reconciliation display only).</summary>
    private static TenantStatus NormalizeStatus(bool isActive, string provisioningStatus) =>
        MapStatus(isActive, provisioningStatus);

    // ══════════════════════════════════════════════════════════════════════════
    //  HELPERS
    // ══════════════════════════════════════════════════════════════════════════

    private static MigrationExecutionResult FailedResult(
        Guid    runId,
        DateTime startedAt,
        string  scope,
        string? identityCs,
        string  error) =>
        new(
            RunId:                       runId,
            GeneratedAtUtc:              startedAt,
            Mode:                        "Execute",
            Scope:                       scope,
            IdentityAccessible:          !string.IsNullOrWhiteSpace(identityCs),
            IdentityAccessError:         error,
            TenantAccessible:            true,
            TotalIdentityTenantsScanned: 0,
            TenantsCreated:              0,
            TenantsUpdated:              0,
            TenantsSkipped:              0,
            ConflictsDetected:           0,
            ErrorsDetected:              1,
            DurationMs:                  0,
            TenantResults:               Array.Empty<MigrationTenantResult>(),
            PostRunReconciliation:       null);

    private static MigrationTenantResult ToDto(
        IdentityTenantRow     identity,
        Guid                  tenantId,
        string                action,
        bool                  tenantUpserted,
        bool                  brandingUpserted,
        bool                  domainUpserted,
        IReadOnlyList<string> warnings,
        IReadOnlyList<string> errors) =>
        new(
            IdentityTenantId: tenantId,
            Code:             identity.Code,
            Name:             identity.Name,
            ActionTaken:      action,
            TenantUpserted:   tenantUpserted,
            BrandingUpserted: brandingUpserted,
            DomainUpserted:   domainUpserted,
            Warnings:         warnings,
            Errors:           errors);

    private static IReadOnlyList<string> SplitNullable(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? Array.Empty<string>()
            : value.Split("; ", StringSplitOptions.RemoveEmptyEntries);

    // ── Internal projection types ──────────────────────────────────────────────

    private sealed record IdentityTenantRow(
        string  Id,
        string  Code,
        string  Name,
        bool    IsActive,
        string  ProvisioningStatus,
        string? Subdomain,
        Guid?   LogoDocumentId,
        Guid?   LogoWhiteDocumentId,
        bool    HasLogo,
        DateTime CreatedAtUtc,
        DateTime UpdatedAtUtc);

    private sealed record TenantServiceRow(
        Guid    Id,
        string  Code,
        string  DisplayName,
        string  Status,
        string? Subdomain)
    {
        public bool HasLogo => false; // Proxy — full logo check deferred to branding join
    }
}
