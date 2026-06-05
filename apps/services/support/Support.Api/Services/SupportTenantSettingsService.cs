using Microsoft.EntityFrameworkCore;
using Support.Api.Auth;
using Support.Api.Audit;
using Support.Api.Data;
using Support.Api.Domain;

namespace Support.Api.Services;

// ── DTOs ─────────────────────────────────────────────────────────────────────

public record TenantSettingsResponse(
    string TenantId,
    string SupportMode,
    bool CustomerPortalEnabled,
    bool EffectiveCustomerSupportEnabled);

public record UpdateTenantSettingsRequest
{
    public string SupportMode { get; init; } = default!;
    public bool CustomerPortalEnabled { get; init; }
}

// ── Interface ─────────────────────────────────────────────────────────────────

public interface ISupportTenantSettingsService
{
    /// <summary>
    /// Returns the effective settings for the tenant.
    /// If no row exists, returns the safe default (InternalOnly, portal disabled).
    /// </summary>
    Task<TenantSettingsResponse> GetEffectiveSettingsAsync(
        string tenantId,
        CancellationToken ct = default);

    /// <summary>
    /// Creates or updates the tenant support mode settings.
    /// Internal/admin use only — ExternalCustomer must not call this.
    /// Emits an audit event on change.
    /// </summary>
    Task<TenantSettingsResponse> SetSupportModeAsync(
        string tenantId,
        SupportTenantMode mode,
        bool customerPortalEnabled,
        string? actorUserId,
        CancellationToken ct = default);

    /// <summary>
    /// Returns true only when SupportMode=TenantCustomerSupport AND CustomerPortalEnabled=true.
    /// If no settings row exists, returns false (InternalOnly default).
    /// Used by customer endpoints to gate access per tenant.
    /// </summary>
    Task<bool> IsCustomerSupportEnabledAsync(
        string tenantId,
        CancellationToken ct = default);
}

// ── Implementation ────────────────────────────────────────────────────────────

public class SupportTenantSettingsService : ISupportTenantSettingsService
{
    private readonly SupportDbContext   _db;
    private readonly IAuditPublisher    _audit;
    private readonly IActorAccessor     _actor;
    private readonly ILogger<SupportTenantSettingsService> _log;

    public SupportTenantSettingsService(
        SupportDbContext db,
        IAuditPublisher audit,
        IActorAccessor actor,
        ILogger<SupportTenantSettingsService> log)
    {
        _db    = db;
        _audit = audit;
        _actor = actor;
        _log   = log;
    }

    // ── Default response (no row exists) ──────────────────────────────────────

    private static TenantSettingsResponse DefaultResponse(string tenantId) =>
        new(tenantId, SupportTenantMode.InternalOnly.ToString(), false, false);

    // ── Public API ────────────────────────────────────────────────────────────

    public async Task<TenantSettingsResponse> GetEffectiveSettingsAsync(
        string tenantId,
        CancellationToken ct = default)
    {
        var row = await _db.TenantSettings.FindAsync([tenantId], ct);
        return row is null ? DefaultResponse(tenantId) : ToResponse(row);
    }

    public async Task<TenantSettingsResponse> SetSupportModeAsync(
        string tenantId,
        SupportTenantMode mode,
        bool customerPortalEnabled,
        string? actorUserId,
        CancellationToken ct = default)
    {
        var row = await _db.TenantSettings.FindAsync([tenantId], ct);

        // Capture old values for audit
        var oldMode    = row?.SupportMode.ToString()       ?? SupportTenantMode.InternalOnly.ToString();
        var oldEnabled = row?.CustomerPortalEnabled        ?? false;

        if (row is null)
        {
            row = new SupportTenantSettings
            {
                TenantId              = tenantId,
                SupportMode           = mode,
                CustomerPortalEnabled = customerPortalEnabled,
                CreatedAt             = DateTime.UtcNow,
            };
            _db.TenantSettings.Add(row);
        }
        else
        {
            row.SupportMode           = mode;
            row.CustomerPortalEnabled = customerPortalEnabled;
            row.UpdatedAt             = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        _log.LogInformation(
            "Tenant {TenantId} support mode set to {Mode} customerPortalEnabled={Enabled} by actor={Actor}",
            tenantId, mode, customerPortalEnabled, actorUserId);

        var response = ToResponse(row);

        try
        {
            var actor = _actor.Actor;
            var req   = _actor.Request;
            var evt = new SupportAuditEvent(
                EventType:      SupportAuditEventTypes.TenantSettingsChanged,
                TenantId:       tenantId,
                ActorUserId:    actor.UserId ?? actorUserId,
                ActorEmail:     actor.Email,
                ActorRoles:     actor.Roles,
                ResourceType:   SupportAuditResourceTypes.SupportTenantSettings,
                ResourceId:     tenantId,
                ResourceNumber: null,
                Action:         SupportAuditActions.SettingsUpdate,
                Outcome:        SupportAuditOutcomes.Success,
                OccurredAt:     DateTime.UtcNow,
                CorrelationId:  req.CorrelationId,
                IpAddress:      req.IpAddress,
                UserAgent:      req.UserAgent,
                Metadata: new Dictionary<string, object?>
                {
                    ["old_support_mode"]           = oldMode,
                    ["new_support_mode"]           = mode.ToString(),
                    ["old_customer_portal_enabled"] = oldEnabled,
                    ["new_customer_portal_enabled"] = customerPortalEnabled,
                    ["actor_user_id"]              = actorUserId,
                });
            await _audit.PublishAsync(evt, ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex,
                "Audit dispatch failed for event={EventType} tenant={TenantId}",
                SupportAuditEventTypes.TenantSettingsChanged, tenantId);
        }

        return response;
    }

    public async Task<bool> IsCustomerSupportEnabledAsync(
        string tenantId,
        CancellationToken ct = default)
    {
        var row = await _db.TenantSettings.FindAsync([tenantId], ct);
        if (row is null) return false;
        return row.SupportMode == SupportTenantMode.TenantCustomerSupport
            && row.CustomerPortalEnabled;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static TenantSettingsResponse ToResponse(SupportTenantSettings row)
    {
        var effective = row.SupportMode == SupportTenantMode.TenantCustomerSupport
                     && row.CustomerPortalEnabled;
        return new TenantSettingsResponse(
            row.TenantId,
            row.SupportMode.ToString(),
            row.CustomerPortalEnabled,
            effective);
    }
}
