using Microsoft.EntityFrameworkCore;
using Notifications.Application.DTOs;
using Notifications.Application.Interfaces;
using Notifications.Domain;
using Notifications.Infrastructure.Data;

namespace Notifications.Infrastructure.Repositories;

/// <summary>
/// LS-NOTIF-SMS-010: EF Core repository for SMS operational alert persistence.
///
/// All queries:
///  - Never project CredentialsJson, SettingsJson, RecipientJson, or phone numbers.
///  - Never trigger SMS sends, retries, reconciliation, or provider calls.
///  - Deduplicate active alerts by (AlertType, TenantId, Provider, ProviderConfigId).
/// </summary>
public sealed class SmsOperationalAlertRepository : ISmsOperationalAlertRepository
{
    private readonly NotificationsDbContext _db;

    public SmsOperationalAlertRepository(NotificationsDbContext db) => _db = db;

    // ── List ──────────────────────────────────────────────────────────────────

    public async Task<SmsAlertListResult> ListAsync(SmsAlertQuery query, CancellationToken ct = default)
    {
        var limit  = Math.Max(1, Math.Min(query.Limit,  200));
        var offset = Math.Max(0, query.Offset);

        var q = _db.SmsOperationalAlerts.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Status))
            q = q.Where(a => a.Status == query.Status.Trim().ToLowerInvariant());

        if (!string.IsNullOrWhiteSpace(query.Severity))
            q = q.Where(a => a.Severity == query.Severity.Trim().ToLowerInvariant());

        if (!string.IsNullOrWhiteSpace(query.AlertType))
            q = q.Where(a => a.AlertType == query.AlertType.Trim());

        if (query.TenantId.HasValue)
            q = q.Where(a => a.TenantId == query.TenantId.Value);

        if (!string.IsNullOrWhiteSpace(query.Provider))
        {
            var prov = query.Provider.Trim().ToLowerInvariant();
            q = q.Where(a => a.Provider == prov);
        }

        if (query.ProviderConfigId.HasValue)
            q = q.Where(a => a.ProviderConfigId == query.ProviderConfigId.Value);

        if (query.From.HasValue)
            q = q.Where(a => a.CreatedAt >= query.From.Value.ToUniversalTime());

        if (query.To.HasValue)
            q = q.Where(a => a.CreatedAt <= query.To.Value.ToUniversalTime());

        var total = await q.CountAsync(ct);

        // Order: active first (alphabetical status ASC puts "active" before "resolved"/"suppressed"),
        // then most recently observed.
        var rows = await q
            .OrderBy(a => a.Status)
            .ThenByDescending(a => a.LastObservedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(ct);

        return new SmsAlertListResult
        {
            Items  = rows.Select(MapToDto).ToList(),
            Total  = total,
            Limit  = limit,
            Offset = offset,
        };
    }

    // ── Summary ───────────────────────────────────────────────────────────────

    public async Task<SmsAlertSummaryDto> GetSummaryAsync(CancellationToken ct = default)
    {
        var rows = await _db.SmsOperationalAlerts
            .AsNoTracking()
            .Select(a => new { a.Status, a.Severity, a.AlertType })
            .ToListAsync(ct);

        int active = 0, resolved = 0, suppressed = 0;
        int criticalActive = 0, warningActive = 0;
        var byType = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var r in rows)
        {
            switch (r.Status)
            {
                case "active":
                    active++;
                    if (r.Severity == "critical") criticalActive++;
                    else warningActive++;
                    byType.TryGetValue(r.AlertType, out var cnt);
                    byType[r.AlertType] = cnt + 1;
                    break;
                case "resolved":    resolved++;    break;
                case "suppressed":  suppressed++;  break;
            }
        }

        return new SmsAlertSummaryDto
        {
            ActiveCount         = active,
            ResolvedCount       = resolved,
            SuppressedCount     = suppressed,
            TotalCount          = rows.Count,
            CriticalActiveCount = criticalActive,
            WarningActiveCount  = warningActive,
            ActiveByType        = byType,
        };
    }

    // ── Get by ID ─────────────────────────────────────────────────────────────

    public async Task<SmsOperationalAlert?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.SmsOperationalAlerts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    // ── Deduplication finders ─────────────────────────────────────────────────

    public async Task<SmsOperationalAlert?> FindActiveAlertAsync(
        string alertType,
        Guid? tenantId,
        string? provider,
        Guid? providerConfigId,
        CancellationToken ct = default)
    {
        var q = _db.SmsOperationalAlerts
            .Where(a => a.Status == "active" && a.AlertType == alertType);

        q = tenantId.HasValue
            ? q.Where(a => a.TenantId == tenantId.Value)
            : q.Where(a => a.TenantId == null);

        q = string.IsNullOrEmpty(provider)
            ? q.Where(a => a.Provider == null)
            : q.Where(a => a.Provider == provider);

        q = providerConfigId.HasValue
            ? q.Where(a => a.ProviderConfigId == providerConfigId.Value)
            : q.Where(a => a.ProviderConfigId == null);

        return await q.FirstOrDefaultAsync(ct);
    }

    public async Task<SmsOperationalAlert?> FindRecentlyResolvedAlertAsync(
        string alertType,
        Guid? tenantId,
        string? provider,
        Guid? providerConfigId,
        int cooldownMinutes,
        CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-cooldownMinutes);

        var q = _db.SmsOperationalAlerts
            .Where(a =>
                a.AlertType == alertType &&
                a.Status    == "resolved" &&
                a.ResolvedAt >= cutoff);

        q = tenantId.HasValue
            ? q.Where(a => a.TenantId == tenantId.Value)
            : q.Where(a => a.TenantId == null);

        q = string.IsNullOrEmpty(provider)
            ? q.Where(a => a.Provider == null)
            : q.Where(a => a.Provider == provider);

        q = providerConfigId.HasValue
            ? q.Where(a => a.ProviderConfigId == providerConfigId.Value)
            : q.Where(a => a.ProviderConfigId == null);

        return await q.OrderByDescending(a => a.ResolvedAt).FirstOrDefaultAsync(ct);
    }

    // ── Create / Update ───────────────────────────────────────────────────────

    public async Task<SmsOperationalAlert> CreateAsync(SmsOperationalAlert alert, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        alert.CreatedAt       = now;
        alert.UpdatedAt       = now;
        alert.FirstObservedAt = now;
        alert.LastObservedAt  = now;

        _db.SmsOperationalAlerts.Add(alert);
        await _db.SaveChangesAsync(ct);
        return alert;
    }

    public async Task UpdateAsync(SmsOperationalAlert alert, CancellationToken ct = default)
    {
        alert.UpdatedAt = DateTime.UtcNow;
        _db.SmsOperationalAlerts.Update(alert);
        await _db.SaveChangesAsync(ct);
    }

    // ── Resolve / Suppress ────────────────────────────────────────────────────

    public async Task<bool> ResolveAsync(
        Guid id,
        string? resolvedBy,
        string? resolutionNote,
        CancellationToken ct = default)
    {
        var alert = await _db.SmsOperationalAlerts.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (alert is null || alert.Status == "resolved")
            return false;

        var now = DateTime.UtcNow;
        alert.Status         = "resolved";
        alert.ResolvedAt     = now;
        alert.ResolvedBy     = resolvedBy;
        alert.ResolutionNote = string.IsNullOrWhiteSpace(resolutionNote)
            ? null
            : resolutionNote.Trim()[..Math.Min(resolutionNote.Trim().Length, 1000)];
        alert.UpdatedAt      = now;

        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> SuppressAsync(
        Guid id,
        DateTime suppressedUntil,
        CancellationToken ct = default)
    {
        var alert = await _db.SmsOperationalAlerts.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (alert is null)
            return false;

        alert.SuppressedUntil = suppressedUntil.ToUniversalTime();
        if (alert.Status == "active")
            alert.Status = "suppressed";
        alert.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ── Mapper ────────────────────────────────────────────────────────────────

    private static SmsAlertDto MapToDto(SmsOperationalAlert a) => new()
    {
        Id                    = a.Id,
        AlertType             = a.AlertType,
        Severity              = a.Severity,
        TenantId              = a.TenantId,
        Provider              = a.Provider,
        ProviderConfigId      = a.ProviderConfigId,
        MetricValue           = a.MetricValue,
        ThresholdValue        = a.ThresholdValue,
        Message               = a.Message,
        EvaluationWindowStart = a.EvaluationWindowStart,
        EvaluationWindowEnd   = a.EvaluationWindowEnd,
        Status                = a.Status,
        OccurrenceCount       = a.OccurrenceCount,
        FirstObservedAt       = a.FirstObservedAt,
        LastObservedAt        = a.LastObservedAt,
        ResolvedAt            = a.ResolvedAt,
        ResolvedBy            = a.ResolvedBy,
        ResolutionNote        = a.ResolutionNote,
        SuppressedUntil       = a.SuppressedUntil,
        CreatedAt             = a.CreatedAt,
        UpdatedAt             = a.UpdatedAt,
    };
}
