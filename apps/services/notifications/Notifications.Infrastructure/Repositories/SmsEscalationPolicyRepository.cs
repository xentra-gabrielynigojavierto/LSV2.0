using Microsoft.EntityFrameworkCore;
using Notifications.Application.DTOs;
using Notifications.Application.Interfaces;
using Notifications.Domain;
using Notifications.Infrastructure.Data;

namespace Notifications.Infrastructure.Repositories;

/// <summary>
/// LS-NOTIF-SMS-011: EF Core repository for SMS escalation policy CRUD.
///
/// Security:
///   - The raw Target field is never projected into DTOs — only TargetMasked is returned.
///   - Policies are soft-disabled (Enabled=false) rather than physically deleted.
/// </summary>
public sealed class SmsEscalationPolicyRepository : ISmsOperationalEscalationPolicyRepository
{
    private readonly NotificationsDbContext _db;

    public SmsEscalationPolicyRepository(NotificationsDbContext db) => _db = db;

    // ── List ──────────────────────────────────────────────────────────────────

    public async Task<SmsEscalationPolicyListResult> ListAsync(
        SmsEscalationPolicyQuery query, CancellationToken ct = default)
    {
        var limit  = Math.Max(1, Math.Min(query.Limit, 200));
        var offset = Math.Max(0, query.Offset);

        var q = _db.SmsEscalationPolicies.AsNoTracking();

        if (query.Enabled.HasValue)
            q = q.Where(p => p.Enabled == query.Enabled.Value);

        if (!string.IsNullOrWhiteSpace(query.ChannelType))
            q = q.Where(p => p.ChannelType == query.ChannelType.Trim());

        if (!string.IsNullOrWhiteSpace(query.AlertType))
            q = q.Where(p => p.AlertType == query.AlertType.Trim() || p.AlertType == null);

        if (!string.IsNullOrWhiteSpace(query.Severity))
            q = q.Where(p => p.Severity == query.Severity.Trim().ToLowerInvariant() || p.Severity == null);

        var total = await q.CountAsync(ct);
        var rows  = await q
            .OrderBy(p => p.Name)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(ct);

        return new SmsEscalationPolicyListResult
        {
            Items  = rows.Select(MapToDto).ToList(),
            Total  = total,
            Limit  = limit,
            Offset = offset,
        };
    }

    // ── Get by ID ─────────────────────────────────────────────────────────────

    public async Task<SmsOperationalEscalationPolicy?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.SmsEscalationPolicies
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    // ── Matching policies for alert ───────────────────────────────────────────

    public async Task<List<SmsOperationalEscalationPolicy>> GetEnabledMatchingPoliciesAsync(
        SmsOperationalAlert alert, CancellationToken ct = default)
    {
        // Load all enabled policies — filter in memory for nullable wildcard matching.
        // The table is small (operational config) so in-memory filtering is safe.
        var policies = await _db.SmsEscalationPolicies
            .AsNoTracking()
            .Where(p => p.Enabled)
            .ToListAsync(ct);

        return policies.Where(p =>
            (p.AlertType == null || p.AlertType == alert.AlertType) &&
            (p.Severity  == null || p.Severity  == alert.Severity)  &&
            (p.TenantId  == null || p.TenantId  == alert.TenantId)  &&
            (p.Provider  == null || p.Provider  == alert.Provider)   &&
            (p.ProviderConfigId == null || p.ProviderConfigId == alert.ProviderConfigId)
        ).ToList();
    }

    // ── Create ────────────────────────────────────────────────────────────────

    public async Task<SmsOperationalEscalationPolicy> CreateAsync(
        SmsOperationalEscalationPolicy policy, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        policy.CreatedAt = now;
        policy.UpdatedAt = now;

        _db.SmsEscalationPolicies.Add(policy);
        await _db.SaveChangesAsync(ct);
        return policy;
    }

    // ── Update ────────────────────────────────────────────────────────────────

    public async Task UpdateAsync(SmsOperationalEscalationPolicy policy, CancellationToken ct = default)
    {
        policy.UpdatedAt = DateTime.UtcNow;
        _db.SmsEscalationPolicies.Update(policy);
        await _db.SaveChangesAsync(ct);
    }

    // ── Disable (soft delete) ─────────────────────────────────────────────────

    public async Task<bool> DisableAsync(Guid id, string? updatedBy, CancellationToken ct = default)
    {
        var policy = await _db.SmsEscalationPolicies.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (policy is null) return false;

        policy.Enabled   = false;
        policy.UpdatedBy = updatedBy;
        policy.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ── Mapper ────────────────────────────────────────────────────────────────

    public static SmsEscalationPolicyDto MapToDto(SmsOperationalEscalationPolicy p) => new()
    {
        Id              = p.Id,
        Name            = p.Name,
        Enabled         = p.Enabled,
        AlertType       = p.AlertType,
        Severity        = p.Severity,
        TenantId        = p.TenantId,
        Provider        = p.Provider,
        ProviderConfigId = p.ProviderConfigId,
        ChannelType     = p.ChannelType,
        TargetMasked    = MaskTarget(p.Target, p.ChannelType),
        TargetDisplay   = p.TargetDisplay,
        CooldownMinutes = p.CooldownMinutes,
        RetryEnabled    = p.RetryEnabled,
        MaxRetryCount   = p.MaxRetryCount,
        CreatedAt       = p.CreatedAt,
        UpdatedAt       = p.UpdatedAt,
        CreatedBy       = p.CreatedBy,
        UpdatedBy       = p.UpdatedBy,
    };

    /// <summary>
    /// Returns a safe masked representation of a raw target.
    /// Email: a***@domain | Webhook URL: https://host/***
    /// Never logs or returns the full raw target.
    /// </summary>
    public static string MaskTarget(string target, string channelType)
    {
        if (string.IsNullOrWhiteSpace(target)) return "(not configured)";

        if (channelType is "email" or "internal_notification")
        {
            var atIdx = target.IndexOf('@');
            if (atIdx > 0)
                return target[0] + "***" + target[atIdx..];
            return target.Length > 3 ? target[..2] + "***" : "***";
        }

        if (Uri.TryCreate(target, UriKind.Absolute, out var uri))
            return $"{uri.Scheme}://{uri.Host}/***";

        return "***";
    }
}
