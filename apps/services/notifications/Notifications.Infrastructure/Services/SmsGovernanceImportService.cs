using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notifications.Application.Interfaces;
using Notifications.Application.Options;
using Notifications.Domain;
using Notifications.Infrastructure.Data;

namespace Notifications.Infrastructure.Services;

/// <summary>
/// LS-NOTIF-SMS-020: Bulk governance rule import and export service.
///
/// Import validates the entire payload before writing anything.
/// All bundles are persisted in a single DB transaction.
/// Invalid imports are rejected entirely — no partial state.
/// Exported data never contains secrets, credentials, or raw phone numbers.
/// </summary>
public sealed class SmsGovernanceImportService : ISmsGovernanceImportService
{
    private static readonly string[] ValidRuleTypes =
    [
        "prohibited_phrase", "restricted_pattern", "classification_override",
        "variable_rule", "link_rule", "delivery_restriction", "escalation_rule",
    ];

    private static readonly string[] ValidSeverities =
        ["allow", "warn", "review_required", "block", "override_allowed"];

    private static readonly string[] ValidStatuses =
        ["draft", "active", "inactive", "archived"];

    private static readonly string[] ValidInheritanceModes =
        ["merge", "override", "append_only"];

    // Catastrophic backtracking detection — same as rule engine
    private static readonly Regex CatastrophicPattern =
        new(@"(\(.*\+.*\)\+)|(\(.*\*.*\)\+)|(\(.*\+.*\)\*)|(\(.*\{.*\}.*\)\+)",
            RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));

    private readonly NotificationsDbContext             _db;
    private readonly ISmsGovernanceVersioningService   _versioning;
    private readonly SmsGovernanceDynamicOptions       _dynamicOpts;
    private readonly ILogger<SmsGovernanceImportService> _logger;

    public SmsGovernanceImportService(
        NotificationsDbContext                         db,
        ISmsGovernanceVersioningService               versioning,
        IOptions<SmsGovernanceDynamicOptions>         dynamicOpts,
        ILogger<SmsGovernanceImportService>           logger)
    {
        _db          = db;
        _versioning  = versioning;
        _dynamicOpts = dynamicOpts.Value;
        _logger      = logger;
    }

    // ─── Validate ─────────────────────────────────────────────────────────────

    public Task<GovernanceImportResult> ValidateImportAsync(
        GovernanceImportRequest request,
        CancellationToken ct = default)
    {
        var errors = Validate(request);
        return Task.FromResult(errors.Count == 0
            ? new GovernanceImportResult { IsValid = true, Persisted = false }
            : GovernanceImportResult.ValidationFailed(errors));
    }

    // ─── Import ───────────────────────────────────────────────────────────────

    public async Task<GovernanceImportResult> ImportAsync(
        GovernanceImportRequest request,
        CancellationToken ct = default)
    {
        // Phase 1: full validation — no DB writes
        var errors = Validate(request);
        if (errors.Count > 0)
            return GovernanceImportResult.ValidationFailed(errors);

        if (request.DryRun)
            return new GovernanceImportResult { IsValid = true, Persisted = false };

        // Phase 2: persist all bundles in a single transaction
        var bundlesImported = 0;
        var rulesImported   = 0;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            foreach (var bundle in request.Bundles)
            {
                var pack = new SmsGovernanceRulePack
                {
                    Id              = Guid.NewGuid(),
                    TenantId        = bundle.RulePack.TenantId,
                    Name            = bundle.RulePack.Name.Trim(),
                    Description     = bundle.RulePack.Description,
                    Version         = 1,
                    Status          = bundle.RulePack.Status,
                    Enabled         = bundle.RulePack.Enabled,
                    InheritanceMode = bundle.RulePack.InheritanceMode,
                    Priority        = bundle.RulePack.Priority,
                    EffectiveFrom   = bundle.RulePack.EffectiveFrom,
                    EffectiveTo     = bundle.RulePack.EffectiveTo,
                    CreatedAt       = DateTime.UtcNow,
                    UpdatedAt       = DateTime.UtcNow,
                    CreatedBy       = request.RequestedBy,
                    UpdatedBy       = request.RequestedBy,
                };
                _db.SmsGovernanceRulePacks.Add(pack);
                await _db.SaveChangesAsync(ct);

                foreach (var entry in bundle.Rules)
                {
                    var rule = new SmsGovernanceRule
                    {
                        Id           = Guid.NewGuid(),
                        RulePackId   = pack.Id,
                        Name         = entry.Name.Trim(),
                        Description  = entry.Description,
                        RuleType     = entry.RuleType,
                        Pattern      = entry.Pattern,
                        Severity     = entry.Severity,
                        Enabled      = entry.Enabled,
                        Priority     = entry.Priority,
                        MetadataJson = entry.MetadataJson,
                        CreatedAt    = DateTime.UtcNow,
                        UpdatedAt    = DateTime.UtcNow,
                        CreatedBy    = request.RequestedBy,
                        UpdatedBy    = request.RequestedBy,
                    };
                    _db.SmsGovernanceRules.Add(rule);
                    await _db.SaveChangesAsync(ct);

                    // Rule version snapshot (outside the write transaction for
                    // snapshot table — uses a fresh SaveChangesAsync internally)
                    await _versioning.SnapshotRuleAsync(
                        rule.Id, "imported", $"Bulk import by {request.RequestedBy}",
                        request.RequestedBy, ct);

                    rulesImported++;
                }

                // Pack version snapshot (with rules included)
                await _versioning.SnapshotRulePackAsync(
                    pack.Id, "imported", $"Bulk import by {request.RequestedBy}",
                    request.RequestedBy, includeRules: true, ct);

                bundlesImported++;
            }

            await tx.CommitAsync(ct);

            _logger.LogInformation(
                "Governance bulk import completed: {Bundles} packs, {Rules} rules by {User}",
                bundlesImported, rulesImported, request.RequestedBy);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            _logger.LogError(ex, "Governance bulk import failed — transaction rolled back");
            throw;
        }

        return new GovernanceImportResult
        {
            IsValid         = true,
            Persisted       = true,
            BundlesImported = bundlesImported,
            RulesImported   = rulesImported,
        };
    }

    // ─── Export ───────────────────────────────────────────────────────────────

    public async Task<object> ExportAsync(
        GovernanceExportQuery query,
        CancellationToken ct = default)
    {
        var packQuery = _db.SmsGovernanceRulePacks.AsNoTracking().AsQueryable();
        if (query.TenantId.HasValue)              packQuery = packQuery.Where(p => p.TenantId == query.TenantId);
        if (query.RulePackId.HasValue)            packQuery = packQuery.Where(p => p.Id == query.RulePackId);
        if (!string.IsNullOrEmpty(query.Status))  packQuery = packQuery.Where(p => p.Status == query.Status);

        var packs = await packQuery
            .OrderBy(p => p.Priority).ThenBy(p => p.Name)
            .Select(p => new
            {
                p.Id, p.TenantId, p.Name, p.Description, p.Version,
                p.Status, p.Enabled, p.InheritanceMode, p.Priority,
                p.EffectiveFrom, p.EffectiveTo, p.CreatedAt, p.CreatedBy,
            })
            .ToListAsync(ct);

        var packIds = packs.Select(p => p.Id).ToList();

        var ruleQuery = _db.SmsGovernanceRules.AsNoTracking()
            .Where(r => packIds.Contains(r.RulePackId));

        if (!string.IsNullOrEmpty(query.RuleType)) ruleQuery = ruleQuery.Where(r => r.RuleType == query.RuleType);
        if (!string.IsNullOrEmpty(query.Severity)) ruleQuery = ruleQuery.Where(r => r.Severity == query.Severity);

        var rules = await ruleQuery
            .OrderBy(r => r.RulePackId).ThenBy(r => r.Priority)
            .Select(r => new
            {
                r.Id, r.RulePackId, r.Name, r.Description, r.RuleType,
                r.Pattern, r.Severity, r.Enabled, r.Priority,
                r.MetadataJson, r.CreatedAt, r.CreatedBy,
            })
            .ToListAsync(ct);

        object? profiles = null;
        if (query.IncludeProfiles)
        {
            profiles = await _db.SmsComplianceProfiles.AsNoTracking()
                .Select(p => new
                {
                    p.Id, p.Name, p.Description, p.EnforcementMode, p.Enabled,
                    p.CreatedAt,
                })
                .OrderBy(p => p.Name)
                .ToListAsync(ct);
        }

        return new
        {
            exportedAt = DateTime.UtcNow,
            filters    = new { query.TenantId, query.RulePackId, query.Status, query.RuleType, query.Severity },
            packCount  = packs.Count,
            ruleCount  = rules.Count,
            rulePacks  = packs,
            rules,
            profiles,
        };
    }

    // ─── Validation ───────────────────────────────────────────────────────────

    private List<ImportValidationError> Validate(GovernanceImportRequest request)
    {
        var errors = new List<ImportValidationError>();

        if (request.Bundles is null || request.Bundles.Count == 0)
        {
            errors.Add(new ImportValidationError
            {
                BundleIndex = -1, RuleIndex = -1,
                Field = "bundles", Message = "At least one bundle is required",
            });
            return errors;
        }

        for (var bi = 0; bi < request.Bundles.Count; bi++)
        {
            var bundle = request.Bundles[bi];

            // Validate pack
            if (string.IsNullOrWhiteSpace(bundle.RulePack?.Name))
                errors.Add(Err(bi, -1, "rulePack.name", "name is required"));

            if (!string.IsNullOrEmpty(bundle.RulePack?.Status) &&
                !ValidStatuses.Contains(bundle.RulePack.Status))
                errors.Add(Err(bi, -1, "rulePack.status",
                    $"Invalid status. Allowed: {string.Join(", ", ValidStatuses)}"));

            if (!string.IsNullOrEmpty(bundle.RulePack?.InheritanceMode) &&
                !ValidInheritanceModes.Contains(bundle.RulePack.InheritanceMode))
                errors.Add(Err(bi, -1, "rulePack.inheritanceMode",
                    $"Invalid inheritanceMode. Allowed: {string.Join(", ", ValidInheritanceModes)}"));

            // Validate rules
            if (bundle.Rules == null) continue;

            for (var ri = 0; ri < bundle.Rules.Count; ri++)
            {
                var rule = bundle.Rules[ri];

                if (string.IsNullOrWhiteSpace(rule.Name))
                    errors.Add(Err(bi, ri, "name", "name is required"));

                if (string.IsNullOrWhiteSpace(rule.RuleType) || !ValidRuleTypes.Contains(rule.RuleType))
                    errors.Add(Err(bi, ri, "ruleType",
                        $"Invalid ruleType. Allowed: {string.Join(", ", ValidRuleTypes)}"));

                if (string.IsNullOrWhiteSpace(rule.Severity) || !ValidSeverities.Contains(rule.Severity))
                    errors.Add(Err(bi, ri, "severity",
                        $"Invalid severity. Allowed: {string.Join(", ", ValidSeverities)}"));

                if (!string.IsNullOrEmpty(rule.Pattern) &&
                    rule.Pattern.Length > _dynamicOpts.MaxPatternLength)
                    errors.Add(Err(bi, ri, "pattern",
                        $"Pattern exceeds max length of {_dynamicOpts.MaxPatternLength}"));

                if (rule.RuleType == "restricted_pattern")
                {
                    if (!_dynamicOpts.AllowRegexRules)
                        errors.Add(Err(bi, ri, "ruleType", "Regex rules are disabled by platform configuration"));
                    else if (string.IsNullOrEmpty(rule.Pattern))
                        errors.Add(Err(bi, ri, "pattern", "pattern is required for restricted_pattern rules"));
                    else
                    {
                        var (ok, msg) = ValidateRegexSafety(rule.Pattern);
                        if (!ok)
                            errors.Add(Err(bi, ri, "pattern", $"Unsafe or invalid regex: {msg}"));
                    }
                }

                if (!string.IsNullOrEmpty(rule.MetadataJson))
                {
                    try { JsonDocument.Parse(rule.MetadataJson); }
                    catch { errors.Add(Err(bi, ri, "metadataJson", "metadataJson must be valid JSON")); }
                }
            }
        }

        return errors;
    }

    private static (bool Ok, string? Error) ValidateRegexSafety(string pattern)
    {
        try
        {
            if (CatastrophicPattern.IsMatch(pattern))
                return (false, "Pattern contains potentially catastrophic backtracking constructs");

            _ = new Regex(pattern, RegexOptions.None, TimeSpan.FromMilliseconds(200));
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private static ImportValidationError Err(int bi, int ri, string field, string message) =>
        new() { BundleIndex = bi, RuleIndex = ri, Field = field, Message = message };
}
