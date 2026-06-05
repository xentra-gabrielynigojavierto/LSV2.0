using Microsoft.EntityFrameworkCore;
using Notifications.Application.DTOs;
using Notifications.Application.Interfaces;
using Notifications.Domain;
using Notifications.Infrastructure.Data;

namespace Notifications.Infrastructure.Repositories;

/// <summary>
/// LS-NOTIF-SMS-011: EF Core repository for SMS alert escalation attempt persistence.
///
/// Security:
///   - TargetMasked is the only target field stored and returned — no raw URLs or emails.
///   - MetadataJson must not contain credentials, phone numbers, or raw provider payloads.
/// </summary>
public sealed class SmsAlertEscalationRepository : ISmsOperationalAlertEscalationRepository
{
    private readonly NotificationsDbContext _db;

    public SmsAlertEscalationRepository(NotificationsDbContext db) => _db = db;

    // ── Create ────────────────────────────────────────────────────────────────

    public async Task<SmsOperationalAlertEscalation> CreateAsync(
        SmsOperationalAlertEscalation escalation, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        escalation.CreatedAt = now;
        escalation.UpdatedAt = now;

        _db.SmsAlertEscalations.Add(escalation);
        await _db.SaveChangesAsync(ct);
        return escalation;
    }

    // ── Update ────────────────────────────────────────────────────────────────

    public async Task UpdateAsync(SmsOperationalAlertEscalation escalation, CancellationToken ct = default)
    {
        escalation.UpdatedAt = DateTime.UtcNow;
        _db.SmsAlertEscalations.Update(escalation);
        await _db.SaveChangesAsync(ct);
    }

    // ── Get by ID ─────────────────────────────────────────────────────────────

    public async Task<SmsOperationalAlertEscalation?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.SmsAlertEscalations
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, ct);

    // ── List ──────────────────────────────────────────────────────────────────

    public async Task<SmsAlertEscalationListResult> ListAsync(
        SmsAlertEscalationQuery query, CancellationToken ct = default)
    {
        var limit  = Math.Max(1, Math.Min(query.Limit, 200));
        var offset = Math.Max(0, query.Offset);

        var q = _db.SmsAlertEscalations.AsNoTracking();

        if (query.AlertId.HasValue)
            q = q.Where(e => e.AlertId == query.AlertId.Value);

        if (query.PolicyId.HasValue)
            q = q.Where(e => e.PolicyId == query.PolicyId.Value);

        if (!string.IsNullOrWhiteSpace(query.Status))
            q = q.Where(e => e.Status == query.Status.Trim().ToLowerInvariant());

        if (!string.IsNullOrWhiteSpace(query.ChannelType))
            q = q.Where(e => e.ChannelType == query.ChannelType.Trim());

        if (!string.IsNullOrWhiteSpace(query.Severity))
            q = q.Where(e => e.Severity == query.Severity.Trim().ToLowerInvariant());

        if (query.From.HasValue)
            q = q.Where(e => e.CreatedAt >= query.From.Value.ToUniversalTime());

        if (query.To.HasValue)
            q = q.Where(e => e.CreatedAt <= query.To.Value.ToUniversalTime());

        var total = await q.CountAsync(ct);
        var rows  = await q
            .OrderByDescending(e => e.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(ct);

        return new SmsAlertEscalationListResult
        {
            Items  = rows.Select(MapToDto).ToList(),
            Total  = total,
            Limit  = limit,
            Offset = offset,
        };
    }

    // ── Summary ───────────────────────────────────────────────────────────────

    public async Task<SmsEscalationSummaryDto> SummarizeAsync(
        SmsAlertEscalationQuery query, CancellationToken ct = default)
    {
        var q = _db.SmsAlertEscalations.AsNoTracking();

        if (query.AlertId.HasValue)
            q = q.Where(e => e.AlertId == query.AlertId.Value);
        if (query.From.HasValue)
            q = q.Where(e => e.CreatedAt >= query.From.Value.ToUniversalTime());
        if (query.To.HasValue)
            q = q.Where(e => e.CreatedAt <= query.To.Value.ToUniversalTime());

        var rows = await q
            .Select(e => new { e.Status, e.ChannelType })
            .ToListAsync(ct);

        var byStatus  = new Dictionary<string, int>(StringComparer.Ordinal);
        var byChannel = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        int sent = 0, failed = 0, pending = 0, suppressed = 0, skipped = 0;

        foreach (var r in rows)
        {
            byStatus.TryGetValue(r.Status, out var sc);
            byStatus[r.Status] = sc + 1;

            byChannel.TryGetValue(r.ChannelType, out var cc);
            byChannel[r.ChannelType] = cc + 1;

            switch (r.Status)
            {
                case "sent":       sent++;       break;
                case "failed":     failed++;     break;
                case "pending":    pending++;    break;
                case "suppressed": suppressed++; break;
                case "skipped":    skipped++;    break;
            }
        }

        return new SmsEscalationSummaryDto
        {
            TotalCount      = rows.Count,
            SentCount       = sent,
            FailedCount     = failed,
            PendingCount    = pending,
            SuppressedCount = suppressed,
            SkippedCount    = skipped,
            ByStatus        = byStatus,
            ByChannel       = byChannel,
        };
    }

    // ── Dedup finder ──────────────────────────────────────────────────────────

    public async Task<SmsOperationalAlertEscalation?> FindRecentDuplicateAsync(
        Guid alertId,
        Guid? policyId,
        string? payloadHash,
        int cooldownMinutes,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(payloadHash)) return null;

        var cutoff = DateTime.UtcNow.AddMinutes(-cooldownMinutes);

        var q = _db.SmsAlertEscalations
            .AsNoTracking()
            .Where(e =>
                e.AlertId     == alertId     &&
                e.PayloadHash == payloadHash &&
                e.CreatedAt   >= cutoff      &&
                (e.Status == "sent" || e.Status == "suppressed" || e.Status == "pending"));

        if (policyId.HasValue)
            q = q.Where(e => e.PolicyId == policyId.Value);

        return await q.OrderByDescending(e => e.CreatedAt).FirstOrDefaultAsync(ct);
    }

    // ── Pending retry finder ──────────────────────────────────────────────────

    public async Task<List<SmsOperationalAlertEscalation>> GetPendingRetriesAsync(
        int limit, DateTime now, CancellationToken ct = default)
    {
        var safeLimit = Math.Max(1, Math.Min(limit, 200));

        return await _db.SmsAlertEscalations
            .Where(e =>
                e.Status     == "pending"  &&
                e.NextRetryAt != null       &&
                e.NextRetryAt <= now)
            .OrderBy(e => e.NextRetryAt)
            .Take(safeLimit)
            .ToListAsync(ct);
    }

    // ── Mapper ────────────────────────────────────────────────────────────────

    private static SmsAlertEscalationDto MapToDto(SmsOperationalAlertEscalation e) => new()
    {
        Id             = e.Id,
        AlertId        = e.AlertId,
        PolicyId       = e.PolicyId,
        ChannelType    = e.ChannelType,
        TargetMasked   = e.TargetMasked,
        Severity       = e.Severity,
        Status         = e.Status,
        AttemptCount   = e.AttemptCount,
        LastAttemptAt  = e.LastAttemptAt,
        SentAt         = e.SentAt,
        FailureReason  = e.FailureReason,
        NextRetryAt    = e.NextRetryAt,
        SuppressedUntil = e.SuppressedUntil,
        PayloadHash    = e.PayloadHash,
        CreatedAt      = e.CreatedAt,
        UpdatedAt      = e.UpdatedAt,
    };
}
