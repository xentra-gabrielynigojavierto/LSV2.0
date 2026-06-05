namespace Tenant.Domain;

/// <summary>
/// Represents a fine-grained capability flag for a tenant.
///
/// Block 4 — capability flags model.
///
/// A capability is either:
///   - tenant-global (ProductEntitlementId = null): applies across all products.
///   - product-scoped (ProductEntitlementId set): applies only within a specific entitlement.
///
/// CapabilityKey is normalized (lowercase, trimmed). Examples of keys as data:
///   "branding.advanced", "domain.custom", "portal.readonly",
///   "notifications.custom-reply-to", "product.case-management".
///
/// Uniqueness invariant: (TenantId, CapabilityKey, ProductEntitlementId) must be unique.
/// Enforced at service layer (MySQL does not support filtered unique indexes over nullable columns).
/// </summary>
public class TenantCapability
{
    // ── Identity ──────────────────────────────────────────────────────────────

    public Guid Id { get; private set; }

    /// <summary>Owning tenant.</summary>
    public Guid TenantId { get; private set; }

    /// <summary>
    /// Optional product entitlement scope. Null = tenant-global capability.
    /// </summary>
    public Guid? ProductEntitlementId { get; private set; }

    // ── Capability ────────────────────────────────────────────────────────────

    /// <summary>
    /// Stable, normalized capability identifier. Lowercase, trimmed.
    /// Must use dot-namespacing (e.g. "branding.advanced").
    /// </summary>
    public string CapabilityKey { get; private set; } = string.Empty;

    /// <summary>Whether this capability is currently enabled for the tenant.</summary>
    public bool IsEnabled { get; private set; }

    // ── Timestamps ────────────────────────────────────────────────────────────

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    // ── Navigation ────────────────────────────────────────────────────────────

    public Tenant? Tenant { get; private set; }
    public TenantProductEntitlement? ProductEntitlement { get; private set; }

    private TenantCapability() { }

    // ── Key normalization ─────────────────────────────────────────────────────

    /// <summary>Normalizes a capability key to lowercase trimmed form.</summary>
    public static string NormalizeKey(string key) =>
        (key ?? throw new ArgumentNullException(nameof(key))).Trim().ToLowerInvariant();

    // ── Factory ───────────────────────────────────────────────────────────────

    public static TenantCapability Create(
        Guid  tenantId,
        string capabilityKey,
        bool  isEnabled,
        Guid? productEntitlementId = null)
    {
        var normalized = NormalizeKey(capabilityKey);
        if (string.IsNullOrWhiteSpace(normalized))
            throw new ArgumentException("CapabilityKey cannot be empty.", nameof(capabilityKey));

        var now = DateTime.UtcNow;
        return new TenantCapability
        {
            Id                   = Guid.NewGuid(),
            TenantId             = tenantId,
            ProductEntitlementId = productEntitlementId,
            CapabilityKey        = normalized,
            IsEnabled            = isEnabled,
            CreatedAtUtc         = now,
            UpdatedAtUtc         = now
        };
    }

    // ── Mutators ──────────────────────────────────────────────────────────────

    public void Update(string capabilityKey, bool isEnabled, Guid? productEntitlementId)
    {
        var normalized = NormalizeKey(capabilityKey);
        if (string.IsNullOrWhiteSpace(normalized))
            throw new ArgumentException("CapabilityKey cannot be empty.", nameof(capabilityKey));

        CapabilityKey        = normalized;
        IsEnabled            = isEnabled;
        ProductEntitlementId = productEntitlementId;
        UpdatedAtUtc         = DateTime.UtcNow;
    }

    public void SetEnabled(bool enabled)
    {
        IsEnabled    = enabled;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
