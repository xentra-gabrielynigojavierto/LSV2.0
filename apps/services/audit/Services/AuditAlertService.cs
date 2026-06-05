using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PlatformAuditEventService.Data;
using PlatformAuditEventService.DTOs.Alerts;
using PlatformAuditEventService.DTOs.Analytics;
using PlatformAuditEventService.Entities;
using PlatformAuditEventService.Enums;

namespace PlatformAuditEventService.Services;

/// <summary>
/// Alert lifecycle engine that converts anomaly detection results into durable,
/// deduplicated alert records stored in <see cref="AuditEventDbContext.AuditAlerts"/>.
///
/// Alert generation strategy:
///   Explicit, on-demand evaluation via <see cref="EvaluateAsync"/>.
///   The caller (HTTP endpoint + UI button) controls when evaluation runs.
///   This avoids alert storms caused by every page load creating duplicate records.
///
/// Deduplication:
///   Each anomaly is assigned a deterministic SHA-256 fingerprint from its rule key +
///   scope + context identifiers. The fingerprint is used to detect whether an equivalent
///   alert already exists in the database before creating a new one.
///
/// Cooldown after resolution:
///   If a matching alert was resolved within the past hour, a new re-detection is suppressed.
///   After the cooldown expires, a new Open alert is created (rather than reopening the old one,
///   which preserves the full history of the previous alert episode).
///
/// Tenant isolation:
///   All queries scope by TenantId when a tenant scope is active. Platform admin callers
///   may query cross-tenant by leaving TenantId null.
/// </summary>
public sealed class AuditAlertService : IAuditAlertService
{
    private const int  DefaultLimit        = 50;
    private const int  MaxLimit            = 200;
    private static readonly TimeSpan CooldownWindow = TimeSpan.FromHours(1);

    private readonly IDbContextFactory<AuditEventDbContext> _factory;
    private readonly IAuditAnomalyService                   _anomalyService;
    private readonly ILogger<AuditAlertService>             _log;

    public AuditAlertService(
        IDbContextFactory<AuditEventDbContext> factory,
        IAuditAnomalyService                   anomalyService,
        ILogger<AuditAlertService>             log)
    {
        _factory        = factory;
        _anomalyService = anomalyService;
        _log            = log;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // EvaluateAsync — run anomaly detection + upsert alerts
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<AuditEvaluateAlertsResponse> EvaluateAsync(
        AuditAnomalyRequest request,
        string?             callerTenantId,
        bool                isPlatformAdmin,
        CancellationToken   ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        // ── 1. Detect current anomalies ───────────────────────────────────────

        var anomalyResult = await _anomalyService.DetectAsync(
            request, callerTenantId, isPlatformAdmin, ct);

        string? effectiveTenantId = anomalyResult.EffectiveTenantId;

        var response = new AuditEvaluateAlertsResponse
        {
            EvaluatedAt       = now,
            EffectiveTenantId = effectiveTenantId,
            AnomaliesDetected = anomalyResult.TotalAnomalies,
        };

        if (anomalyResult.TotalAnomalies == 0)
            return response;

        // ── 2. Upsert alerts for each firing anomaly ──────────────────────────

        await using var db = await _factory.CreateDbContextAsync(ct);

        foreach (var anomaly in anomalyResult.Anomalies)
        {
            var scopeType   = effectiveTenantId is not null ? "Tenant" : "Platform";
            var fingerprint = ComputeFingerprint(
                anomaly.RuleKey,
                scopeType,
                effectiveTenantId,
                anomaly.AffectedActorId,
                anomaly.AffectedTenantId,
                anomaly.AffectedEventType);

            var contextJson = BuildContextJson(anomaly);

            // ── Query for existing alert with same fingerprint ────────────────

            var existing = await db.AuditAlerts
                .Where(a => a.Fingerprint == fingerprint)
                .OrderByDescending(a => a.FirstDetectedAtUtc)
                .FirstOrDefaultAsync(ct);

            if (existing is null)
            {
                // No prior alert — create a new Open alert
                var newAlert = new AuditAlert
                {
                    AlertId             = Guid.NewGuid(),
                    RuleKey             = anomaly.RuleKey,
                    Fingerprint         = fingerprint,
                    ScopeType           = scopeType,
                    TenantId            = effectiveTenantId ?? anomaly.AffectedTenantId,
                    Severity            = anomaly.Severity,
                    Status              = AlertStatus.Open,
                    Title               = anomaly.Title,
                    Description         = anomaly.Description,
                    ContextJson         = contextJson,
                    DrillDownPath       = anomaly.DrillDownPath,
                    FirstDetectedAtUtc  = now,
                    LastDetectedAtUtc   = now,
                    DetectionCount      = 1,
                };

                db.AuditAlerts.Add(newAlert);
                response.AlertsCreated++;

                _log.LogInformation(
                    "AuditAlert created: Rule={Rule} Scope={Scope} Tenant={Tenant} Severity={Sev}",
                    anomaly.RuleKey, scopeType, effectiveTenantId ?? "(all)", anomaly.Severity);
            }
            else if (existing.Status == AlertStatus.Resolved)
            {
                // Alert was previously resolved — check cooldown
                var cooldownExpiry = (existing.ResolvedAtUtc ?? existing.LastDetectedAtUtc) + CooldownWindow;
                if (now < cooldownExpiry)
                {
                    // Within cooldown — suppress
                    response.AlertsSuppressed++;
                    _log.LogDebug(
                        "AuditAlert suppressed (in cooldown): Rule={Rule} AlertId={Id} CooldownExpiry={Exp:u}",
                        anomaly.RuleKey, existing.AlertId, cooldownExpiry);
                }
                else
                {
                    // Cooldown expired — create a fresh alert
                    var newAlert = new AuditAlert
                    {
                        AlertId             = Guid.NewGuid(),
                        RuleKey             = anomaly.RuleKey,
                        Fingerprint         = fingerprint,
                        ScopeType           = scopeType,
                        TenantId            = effectiveTenantId ?? anomaly.AffectedTenantId,
                        Severity            = anomaly.Severity,
                        Status              = AlertStatus.Open,
                        Title               = anomaly.Title,
                        Description         = anomaly.Description,
                        ContextJson         = contextJson,
                        DrillDownPath       = anomaly.DrillDownPath,
                        FirstDetectedAtUtc  = now,
                        LastDetectedAtUtc   = now,
                        DetectionCount      = 1,
                    };

                    db.AuditAlerts.Add(newAlert);
                    response.AlertsCreated++;

                    _log.LogInformation(
                        "AuditAlert re-opened (post-cooldown): Rule={Rule} Scope={Scope}",
                        anomaly.RuleKey, scopeType);
                }
            }
            else
            {
                // Active alert (Open or Acknowledged) — refresh it
                existing.LastDetectedAtUtc = now;
                existing.DetectionCount++;
                existing.Title         = anomaly.Title;
                existing.Description   = anomaly.Description;
                existing.ContextJson   = contextJson;
                existing.DrillDownPath = anomaly.DrillDownPath;

                response.AlertsRefreshed++;

                _log.LogDebug(
                    "AuditAlert refreshed: Rule={Rule} AlertId={Id} Count={N}",
                    anomaly.RuleKey, existing.AlertId, existing.DetectionCount);
            }
        }

        await db.SaveChangesAsync(ct);

        // ── 3. Fetch current active alerts for the response ───────────────────

        response.ActiveAlerts = await BuildScopedQuery(db, effectiveTenantId, isPlatformAdmin)
            .Where(a => a.Status != AlertStatus.Resolved)
            .OrderBy(a => a.Severity == "High" ? 0 : a.Severity == "Medium" ? 1 : 2)
            .ThenByDescending(a => a.LastDetectedAtUtc)
            .Take(50)
            .Select(a => MapToItem(a))
            .ToListAsync(ct);

        return response;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ListAsync
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<AuditAlertListResponse> ListAsync(
        AuditAlertQueryRequest request,
        string?                callerTenantId,
        bool                   isPlatformAdmin,
        CancellationToken      ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);

        var effectiveTenantId = callerTenantId
            ?? (isPlatformAdmin ? request.TenantId : null);

        var baseQuery = BuildScopedQuery(db, effectiveTenantId, isPlatformAdmin);

        // Status filter
        AlertStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (Enum.TryParse<AlertStatus>(request.Status, ignoreCase: true, out var parsed))
                statusFilter = parsed;
        }

        var filteredQuery = statusFilter.HasValue
            ? baseQuery.Where(a => a.Status == statusFilter.Value)
            : baseQuery;

        // Count summaries (scoped, not status-filtered)
        var openCount  = await baseQuery.CountAsync(a => a.Status == AlertStatus.Open,         ct);
        var ackCount   = await baseQuery.CountAsync(a => a.Status == AlertStatus.Acknowledged,  ct);
        var resCount   = await baseQuery.CountAsync(a => a.Status == AlertStatus.Resolved,      ct);

        var limit = Math.Clamp(request.Limit ?? DefaultLimit, 1, MaxLimit);

        var alerts = await filteredQuery
            .OrderBy(a => a.Status == AlertStatus.Open ? 0 : a.Status == AlertStatus.Acknowledged ? 1 : 2)
            .ThenBy(a => a.Severity == "High" ? 0 : a.Severity == "Medium" ? 1 : 2)
            .ThenByDescending(a => a.LastDetectedAtUtc)
            .Take(limit)
            .Select(a => MapToItem(a))
            .ToListAsync(ct);

        return new AuditAlertListResponse
        {
            StatusFilter      = statusFilter?.ToString(),
            EffectiveTenantId = effectiveTenantId,
            TotalReturned     = alerts.Count,
            OpenCount         = openCount,
            AcknowledgedCount = ackCount,
            ResolvedCount     = resCount,
            Alerts            = alerts,
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GetByIdAsync
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<AuditAlertItem?> GetByIdAsync(
        Guid              alertId,
        string?           callerTenantId,
        bool              isPlatformAdmin,
        CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);

        var effectiveTenantId = callerTenantId;

        var alert = await db.AuditAlerts
            .AsNoTracking()
            .Where(a => a.AlertId == alertId)
            .FirstOrDefaultAsync(ct);

        if (alert is null) return null;

        // Tenant isolation check
        if (!isPlatformAdmin && callerTenantId is not null && alert.TenantId != callerTenantId)
            return null;

        return MapToItem(alert);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // AcknowledgeAsync
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<bool> AcknowledgeAsync(
        Guid              alertId,
        string            acknowledgedBy,
        string?           callerTenantId,
        bool              isPlatformAdmin,
        CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);

        var alert = await db.AuditAlerts
            .Where(a => a.AlertId == alertId)
            .FirstOrDefaultAsync(ct);

        if (alert is null) return false;

        if (!isPlatformAdmin && callerTenantId is not null && alert.TenantId != callerTenantId)
            return false;

        if (alert.Status == AlertStatus.Acknowledged)
            return true; // idempotent

        alert.Status              = AlertStatus.Acknowledged;
        alert.AcknowledgedAtUtc   = DateTimeOffset.UtcNow;
        alert.AcknowledgedBy      = acknowledgedBy;

        await db.SaveChangesAsync(ct);

        _log.LogInformation(
            "AuditAlert acknowledged: AlertId={Id} By={By}",
            alertId, acknowledgedBy);

        return true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ResolveAsync
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<bool> ResolveAsync(
        Guid              alertId,
        string            resolvedBy,
        string?           callerTenantId,
        bool              isPlatformAdmin,
        CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);

        var alert = await db.AuditAlerts
            .Where(a => a.AlertId == alertId)
            .FirstOrDefaultAsync(ct);

        if (alert is null) return false;

        if (!isPlatformAdmin && callerTenantId is not null && alert.TenantId != callerTenantId)
            return false;

        if (alert.Status == AlertStatus.Resolved)
            return true; // idempotent

        alert.Status        = AlertStatus.Resolved;
        alert.ResolvedAtUtc = DateTimeOffset.UtcNow;
        alert.ResolvedBy    = resolvedBy;

        await db.SaveChangesAsync(ct);

        _log.LogInformation(
            "AuditAlert resolved: AlertId={Id} By={By}",
            alertId, resolvedBy);

        return true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Private helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns an IQueryable scoped to the caller's visibility.
    /// - Tenant-scoped callers: filtered to their tenantId.
    /// - Platform admin with effectiveTenantId: filtered to that tenant.
    /// - Platform admin with no tenant: unfiltered (all records).
    /// AsNoTracking is intentionally omitted here — lifecycle mutations need tracking.
    /// </summary>
    private static IQueryable<AuditAlert> BuildScopedQuery(
        AuditEventDbContext db,
        string?             effectiveTenantId,
        bool                isPlatformAdmin)
    {
        var q = db.AuditAlerts.AsQueryable();

        if (effectiveTenantId is not null)
            q = q.Where(a => a.TenantId == effectiveTenantId);
        else if (!isPlatformAdmin)
            q = q.Where(a => a.Id < 0); // no tenant + not PA → empty result set (safety net)

        return q;
    }

    /// <summary>
    /// Computes a deterministic SHA-256 hex fingerprint for an anomaly condition.
    /// Two anomalies with the same fingerprint represent the same "condition"
    /// and should be deduplicated into a single alert record.
    /// </summary>
    private static string ComputeFingerprint(
        string  ruleKey,
        string  scopeType,
        string? tenantId,
        string? affectedActorId,
        string? affectedTenantId,
        string? affectedEventType)
    {
        var raw = $"{ruleKey}|{scopeType}|{tenantId ?? ""}|{affectedActorId ?? ""}|{affectedTenantId ?? ""}|{affectedEventType ?? ""}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Builds a compact JSON context payload from an anomaly item.
    /// Stores only safe, non-PII metrics and context identifiers.
    /// </summary>
    private static string BuildContextJson(DTOs.Analytics.AuditAnomalyItem anomaly)
    {
        var ctx = new Dictionary<string, object?>(8)
        {
            ["recentValue"]       = anomaly.RecentValue,
            ["baselineValue"]     = anomaly.BaselineValue,
            ["threshold"]         = anomaly.Threshold,
            ["actualValue"]       = anomaly.ActualValue,
        };

        if (anomaly.AffectedActorId   is not null) ctx["affectedActorId"]   = anomaly.AffectedActorId;
        if (anomaly.AffectedActorName is not null) ctx["affectedActorName"] = anomaly.AffectedActorName;
        if (anomaly.AffectedTenantId  is not null) ctx["affectedTenantId"]  = anomaly.AffectedTenantId;
        if (anomaly.AffectedEventType is not null) ctx["affectedEventType"] = anomaly.AffectedEventType;

        return JsonSerializer.Serialize(ctx);
    }

    /// <summary>Maps an <see cref="AuditAlert"/> entity to its API DTO.</summary>
    private static AuditAlertItem MapToItem(AuditAlert a) => new()
    {
        AlertId            = a.AlertId,
        RuleKey            = a.RuleKey,
        ScopeType          = a.ScopeType,
        TenantId           = a.TenantId,
        Severity           = a.Severity,
        Status             = a.Status.ToString(),
        Title              = a.Title,
        Description        = a.Description,
        ContextJson        = a.ContextJson,
        DrillDownPath      = a.DrillDownPath,
        FirstDetectedAtUtc = a.FirstDetectedAtUtc,
        LastDetectedAtUtc  = a.LastDetectedAtUtc,
        DetectionCount     = a.DetectionCount,
        AcknowledgedAtUtc  = a.AcknowledgedAtUtc,
        AcknowledgedBy     = a.AcknowledgedBy,
        ResolvedAtUtc      = a.ResolvedAtUtc,
        ResolvedBy         = a.ResolvedBy,
    };
}
