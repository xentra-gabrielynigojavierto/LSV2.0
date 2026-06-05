using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notifications.Application.Interfaces;
using Notifications.Application.Options;
using Notifications.Domain;
using Notifications.Infrastructure.Data;

namespace Notifications.Infrastructure.Services;

/// <summary>
/// LS-NOTIF-SMS-020: Immutable governance rule / rule-pack versioning service.
///
/// Snapshots are taken AFTER the entity is saved to the database so the
/// persisted state is always captured correctly.
/// Rollback restores safe fields and creates a new version (ChangeType=rollback).
/// Version history is never deleted.
/// </summary>
public sealed class SmsGovernanceVersioningService : ISmsGovernanceVersioningService
{
    private static readonly JsonSerializerOptions _json =
        new() { WriteIndented = false };

    private readonly NotificationsDbContext              _db;
    private readonly SmsGovernanceVersioningOptions      _opts;
    private readonly ILogger<SmsGovernanceVersioningService> _logger;

    public SmsGovernanceVersioningService(
        NotificationsDbContext                       db,
        IOptions<SmsGovernanceVersioningOptions>    opts,
        ILogger<SmsGovernanceVersioningService>     logger)
    {
        _db     = db;
        _opts   = opts.Value;
        _logger = logger;
    }

    // ─── Snapshot rule ────────────────────────────────────────────────────────

    public async Task SnapshotRuleAsync(
        Guid ruleId,
        string changeType,
        string? changeReason,
        string? requestedBy,
        CancellationToken ct = default)
    {
        if (!_opts.Enabled) return;

        var rule = await _db.SmsGovernanceRules
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == ruleId, ct);

        if (rule == null)
        {
            _logger.LogWarning("SnapshotRuleAsync: rule {RuleId} not found — snapshot skipped", ruleId);
            return;
        }

        var nextVersion = await _db.SmsGovernanceRuleVersions
            .Where(v => v.RuleId == ruleId)
            .MaxAsync(v => (int?)v.VersionNumber, ct) ?? 0;

        var snapshot = BuildRuleSnapshot(rule);
        var snapshotJson = JsonSerializer.Serialize(snapshot, _json);

        if (System.Text.Encoding.UTF8.GetByteCount(snapshotJson) > _opts.MaxSnapshotJsonBytes)
        {
            _logger.LogWarning(
                "SnapshotRuleAsync: snapshot for rule {RuleId} exceeds MaxSnapshotJsonBytes ({Max}) — truncating MetadataJson",
                ruleId, _opts.MaxSnapshotJsonBytes);
            snapshot = snapshot with { MetadataJson = null };
            snapshotJson = JsonSerializer.Serialize(snapshot, _json);
        }

        _db.SmsGovernanceRuleVersions.Add(new SmsGovernanceRuleVersion
        {
            Id               = Guid.NewGuid(),
            RuleId           = ruleId,
            RulePackId       = rule.RulePackId,
            VersionNumber    = nextVersion + 1,
            RuleSnapshotJson = snapshotJson,
            ChangeType       = changeType,
            ChangeReason     = changeReason,
            CreatedAt        = DateTime.UtcNow,
            CreatedBy        = requestedBy,
        });

        await _db.SaveChangesAsync(ct);
    }

    // ─── Snapshot rule pack ───────────────────────────────────────────────────

    public async Task SnapshotRulePackAsync(
        Guid rulePackId,
        string changeType,
        string? changeReason,
        string? requestedBy,
        bool includeRules = true,
        CancellationToken ct = default)
    {
        if (!_opts.Enabled) return;

        var pack = await _db.SmsGovernanceRulePacks
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == rulePackId, ct);

        if (pack == null)
        {
            _logger.LogWarning("SnapshotRulePackAsync: pack {RulePackId} not found — snapshot skipped", rulePackId);
            return;
        }

        var nextVersion = await _db.SmsGovernanceRulePackVersions
            .Where(v => v.RulePackId == rulePackId)
            .MaxAsync(v => (int?)v.VersionNumber, ct) ?? 0;

        var packSnapshot = BuildPackSnapshot(pack);
        var packJson = JsonSerializer.Serialize(packSnapshot, _json);

        string? rulesJson = null;
        if (includeRules && _opts.IncludeRulesInPackSnapshot)
        {
            var rules = await _db.SmsGovernanceRules
                .AsNoTracking()
                .Where(r => r.RulePackId == rulePackId)
                .OrderBy(r => r.Priority)
                .ToListAsync(ct);

            if (rules.Count > 0)
            {
                var ruleSnapshots = rules.Select(BuildRuleSnapshot).ToList();
                rulesJson = JsonSerializer.Serialize(ruleSnapshots, _json);

                var totalBytes =
                    System.Text.Encoding.UTF8.GetByteCount(packJson) +
                    System.Text.Encoding.UTF8.GetByteCount(rulesJson);

                if (totalBytes > _opts.MaxSnapshotJsonBytes)
                {
                    _logger.LogWarning(
                        "SnapshotRulePackAsync: combined snapshot for pack {RulePackId} exceeds MaxSnapshotJsonBytes — omitting rule snapshots",
                        rulePackId);
                    rulesJson = null;
                }
            }
        }

        _db.SmsGovernanceRulePackVersions.Add(new SmsGovernanceRulePackVersion
        {
            Id                        = Guid.NewGuid(),
            RulePackId                = rulePackId,
            VersionNumber             = nextVersion + 1,
            PackSnapshotJson          = packJson,
            IncludedRulesSnapshotJson = rulesJson,
            ChangeType                = changeType,
            ChangeReason              = changeReason,
            CreatedAt                 = DateTime.UtcNow,
            CreatedBy                 = requestedBy,
        });

        await _db.SaveChangesAsync(ct);
    }

    // ─── Get version history ──────────────────────────────────────────────────

    public async Task<IReadOnlyList<RuleVersionDto>> GetRuleVersionsAsync(
        Guid ruleId,
        CancellationToken ct = default)
    {
        return await _db.SmsGovernanceRuleVersions
            .AsNoTracking()
            .Where(v => v.RuleId == ruleId)
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => new RuleVersionDto
            {
                Id               = v.Id,
                RuleId           = v.RuleId,
                RulePackId       = v.RulePackId,
                VersionNumber    = v.VersionNumber,
                RuleSnapshotJson = v.RuleSnapshotJson,
                ChangeType       = v.ChangeType,
                ChangeReason     = v.ChangeReason,
                CreatedAt        = v.CreatedAt,
                CreatedBy        = v.CreatedBy,
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<RulePackVersionDto>> GetRulePackVersionsAsync(
        Guid rulePackId,
        CancellationToken ct = default)
    {
        return await _db.SmsGovernanceRulePackVersions
            .AsNoTracking()
            .Where(v => v.RulePackId == rulePackId)
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => new RulePackVersionDto
            {
                Id                        = v.Id,
                RulePackId                = v.RulePackId,
                VersionNumber             = v.VersionNumber,
                PackSnapshotJson          = v.PackSnapshotJson,
                IncludedRulesSnapshotJson = v.IncludedRulesSnapshotJson,
                ChangeType                = v.ChangeType,
                ChangeReason              = v.ChangeReason,
                CreatedAt                 = v.CreatedAt,
                CreatedBy                 = v.CreatedBy,
            })
            .ToListAsync(ct);
    }

    // ─── Rollback ─────────────────────────────────────────────────────────────

    public async Task<RollbackResult> RollbackRuleAsync(
        Guid ruleId,
        int versionNumber,
        string? requestedBy,
        string? reason,
        CancellationToken ct = default)
    {
        var targetVersion = await _db.SmsGovernanceRuleVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.RuleId == ruleId && v.VersionNumber == versionNumber, ct);

        if (targetVersion == null)
            return RollbackResult.Fail($"Version {versionNumber} not found for rule {ruleId}");

        var rule = await _db.SmsGovernanceRules.FindAsync([ruleId], ct);
        if (rule == null)
            return RollbackResult.Fail($"Rule {ruleId} not found");

        RuleSnapshotData snapshot;
        try
        {
            snapshot = JsonSerializer.Deserialize<RuleSnapshotData>(
                targetVersion.RuleSnapshotJson, _json)!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RollbackRuleAsync: failed to deserialise snapshot for rule {RuleId} v{Version}", ruleId, versionNumber);
            return RollbackResult.Fail("Snapshot data is malformed and cannot be restored");
        }

        // Restore safe fields only — never restore secrets or PII
        rule.Name         = snapshot.Name        ?? rule.Name;
        rule.Description  = snapshot.Description;
        rule.RuleType     = snapshot.RuleType     ?? rule.RuleType;
        rule.Pattern      = snapshot.Pattern;
        rule.Severity     = snapshot.Severity     ?? rule.Severity;
        rule.Enabled      = snapshot.Enabled;
        rule.Priority     = snapshot.Priority;
        rule.MetadataJson = snapshot.MetadataJson;
        rule.UpdatedAt    = DateTime.UtcNow;
        rule.UpdatedBy    = requestedBy;

        await _db.SaveChangesAsync(ct);

        // Create a new rollback version snapshot
        await SnapshotRuleAsync(ruleId, "rollback",
            reason ?? $"Rolled back to version {versionNumber}", requestedBy, ct);

        var newVersion = await _db.SmsGovernanceRuleVersions
            .Where(v => v.RuleId == ruleId)
            .MaxAsync(v => v.VersionNumber, ct);

        _logger.LogInformation(
            "Rule {RuleId} rolled back to v{Target} by {User}; new version = v{New}",
            ruleId, versionNumber, requestedBy, newVersion);

        return RollbackResult.Ok(versionNumber, newVersion);
    }

    public async Task<RollbackResult> RollbackRulePackAsync(
        Guid rulePackId,
        int versionNumber,
        string? requestedBy,
        string? reason,
        CancellationToken ct = default)
    {
        var targetVersion = await _db.SmsGovernanceRulePackVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.RulePackId == rulePackId && v.VersionNumber == versionNumber, ct);

        if (targetVersion == null)
            return RollbackResult.Fail($"Version {versionNumber} not found for rule pack {rulePackId}");

        var pack = await _db.SmsGovernanceRulePacks.FindAsync([rulePackId], ct);
        if (pack == null)
            return RollbackResult.Fail($"Rule pack {rulePackId} not found");

        PackSnapshotData snapshot;
        try
        {
            snapshot = JsonSerializer.Deserialize<PackSnapshotData>(
                targetVersion.PackSnapshotJson, _json)!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RollbackRulePackAsync: failed to deserialise snapshot for pack {PackId} v{Version}", rulePackId, versionNumber);
            return RollbackResult.Fail("Snapshot data is malformed and cannot be restored");
        }

        // Restore safe pack fields only
        pack.Name            = snapshot.Name            ?? pack.Name;
        pack.Description     = snapshot.Description;
        pack.Status          = snapshot.Status          ?? pack.Status;
        pack.InheritanceMode = snapshot.InheritanceMode ?? pack.InheritanceMode;
        pack.Priority        = snapshot.Priority;
        pack.Enabled         = snapshot.Enabled;
        pack.EffectiveFrom   = snapshot.EffectiveFrom;
        pack.EffectiveTo     = snapshot.EffectiveTo;
        pack.Version++;
        pack.UpdatedAt       = DateTime.UtcNow;
        pack.UpdatedBy       = requestedBy;

        await _db.SaveChangesAsync(ct);

        await SnapshotRulePackAsync(rulePackId, "rollback",
            reason ?? $"Rolled back to version {versionNumber}", requestedBy, includeRules: false, ct);

        var newVersion = await _db.SmsGovernanceRulePackVersions
            .Where(v => v.RulePackId == rulePackId)
            .MaxAsync(v => v.VersionNumber, ct);

        _logger.LogInformation(
            "RulePack {RulePackId} rolled back to v{Target} by {User}; new version = v{New}",
            rulePackId, versionNumber, requestedBy, newVersion);

        return RollbackResult.Ok(versionNumber, newVersion);
    }

    // ─── Snapshot helpers ─────────────────────────────────────────────────────

    private static RuleSnapshotData BuildRuleSnapshot(SmsGovernanceRule r) => new()
    {
        Id           = r.Id,
        RulePackId   = r.RulePackId,
        Name         = r.Name,
        Description  = r.Description,
        RuleType     = r.RuleType,
        Pattern      = r.Pattern,
        Severity     = r.Severity,
        Enabled      = r.Enabled,
        Priority     = r.Priority,
        MetadataJson = r.MetadataJson,
        CreatedAt    = r.CreatedAt,
        CreatedBy    = r.CreatedBy,
    };

    private static PackSnapshotData BuildPackSnapshot(SmsGovernanceRulePack p) => new()
    {
        Id              = p.Id,
        TenantId        = p.TenantId,
        Name            = p.Name,
        Description     = p.Description,
        Version         = p.Version,
        Status          = p.Status,
        Enabled         = p.Enabled,
        InheritanceMode = p.InheritanceMode,
        Priority        = p.Priority,
        EffectiveFrom   = p.EffectiveFrom,
        EffectiveTo     = p.EffectiveTo,
        CreatedAt       = p.CreatedAt,
        CreatedBy       = p.CreatedBy,
    };

    // ─── Internal snapshot data records (safe — no secrets) ──────────────────

    private sealed record RuleSnapshotData
    {
        public Guid    Id           { get; init; }
        public Guid    RulePackId   { get; init; }
        public string? Name         { get; init; }
        public string? Description  { get; init; }
        public string? RuleType     { get; init; }
        public string? Pattern      { get; init; }
        public string? Severity     { get; init; }
        public bool    Enabled      { get; init; }
        public int     Priority     { get; init; }
        public string? MetadataJson { get; init; }
        public DateTime CreatedAt   { get; init; }
        public string? CreatedBy    { get; init; }
    }

    private sealed record PackSnapshotData
    {
        public Guid     Id              { get; init; }
        public Guid?    TenantId        { get; init; }
        public string?  Name            { get; init; }
        public string?  Description     { get; init; }
        public int      Version         { get; init; }
        public string?  Status          { get; init; }
        public bool     Enabled         { get; init; }
        public string?  InheritanceMode { get; init; }
        public int      Priority        { get; init; }
        public DateTime? EffectiveFrom  { get; init; }
        public DateTime? EffectiveTo    { get; init; }
        public DateTime CreatedAt       { get; init; }
        public string?  CreatedBy       { get; init; }
    }
}
