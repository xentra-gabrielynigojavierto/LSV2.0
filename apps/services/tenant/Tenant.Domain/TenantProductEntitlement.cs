namespace Tenant.Domain;

/// <summary>
/// Represents a product entitlement granted to a tenant.
///
/// Block 4 — product entitlement ownership model.
///
/// Design notes:
///   - ProductKey is normalized (lowercase, trimmed) for stable lookups.
///   - At most one entitlement per tenant may be flagged IsDefault = true.
///   - Default assignment is managed by EntitlementService (auto-demote pattern).
///   - EffectiveFromUtc/EffectiveToUtc support future time-bounded entitlements.
/// </summary>
public class TenantProductEntitlement
{
    // ── Identity ──────────────────────────────────────────────────────────────

    public Guid Id { get; private set; }

    /// <summary>Owning tenant.</summary>
    public Guid TenantId { get; private set; }

    // ── Product identity ──────────────────────────────────────────────────────

    /// <summary>
    /// Stable, normalized product identifier. Lowercase, trimmed.
    /// Examples: "liens", "careconnect", "fund", "control-center", "task".
    /// Keys are data — no domain logic hardcodes specific product names.
    /// </summary>
    public string ProductKey { get; private set; } = string.Empty;

    /// <summary>
    /// Optional display name snapshot for convenience (e.g. "Liens Management").
    /// Not authoritative — product catalog owns canonical names.
    /// </summary>
    public string? ProductDisplayName { get; private set; }

    // ── Flags ─────────────────────────────────────────────────────────────────

    /// <summary>Whether the entitlement is currently active.</summary>
    public bool IsEnabled { get; private set; }

    /// <summary>
    /// Whether this is the tenant's default product.
    /// At most one enabled entitlement per tenant should have IsDefault = true.
    /// EntitlementService enforces this via auto-demotion.
    /// </summary>
    public bool IsDefault { get; private set; }

    // ── Plan / tier ───────────────────────────────────────────────────────────

    /// <summary>Optional plan or tier code (e.g. "starter", "professional", "enterprise").</summary>
    public string? PlanCode { get; private set; }

    // ── Effective window ──────────────────────────────────────────────────────

    /// <summary>UTC timestamp from which the entitlement is active. Null = no start boundary.</summary>
    public DateTime? EffectiveFromUtc { get; private set; }

    /// <summary>UTC timestamp at which the entitlement expires. Null = no expiry.</summary>
    public DateTime? EffectiveToUtc { get; private set; }

    // ── Timestamps ────────────────────────────────────────────────────────────

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    // ── Navigation ────────────────────────────────────────────────────────────

    public Tenant? Tenant { get; private set; }

    private TenantProductEntitlement() { }

    // ── Key normalization ─────────────────────────────────────────────────────

    /// <summary>Normalizes a product key to lowercase trimmed form.</summary>
    public static string NormalizeKey(string key) =>
        (key ?? throw new ArgumentNullException(nameof(key))).Trim().ToLowerInvariant();

    // ── Factory ───────────────────────────────────────────────────────────────

    public static TenantProductEntitlement Create(
        Guid     tenantId,
        string   productKey,
        string?  productDisplayName,
        bool     isEnabled,
        bool     isDefault,
        string?  planCode         = null,
        DateTime? effectiveFromUtc = null,
        DateTime? effectiveToUtc  = null)
    {
        var normalized = NormalizeKey(productKey);
        if (string.IsNullOrWhiteSpace(normalized))
            throw new ArgumentException("ProductKey cannot be empty.", nameof(productKey));

        var now = DateTime.UtcNow;
        return new TenantProductEntitlement
        {
            Id                 = Guid.NewGuid(),
            TenantId           = tenantId,
            ProductKey         = normalized,
            ProductDisplayName = productDisplayName?.Trim(),
            IsEnabled          = isEnabled,
            IsDefault          = isDefault,
            PlanCode           = planCode?.Trim(),
            EffectiveFromUtc   = effectiveFromUtc,
            EffectiveToUtc     = effectiveToUtc,
            CreatedAtUtc       = now,
            UpdatedAtUtc       = now
        };
    }

    // ── Mutators ──────────────────────────────────────────────────────────────

    public void Update(
        string   productKey,
        string?  productDisplayName,
        bool     isEnabled,
        string?  planCode,
        DateTime? effectiveFromUtc,
        DateTime? effectiveToUtc)
    {
        var normalized = NormalizeKey(productKey);
        if (string.IsNullOrWhiteSpace(normalized))
            throw new ArgumentException("ProductKey cannot be empty.", nameof(productKey));

        ProductKey         = normalized;
        ProductDisplayName = productDisplayName?.Trim();
        IsEnabled          = isEnabled;
        PlanCode           = planCode?.Trim();
        EffectiveFromUtc   = effectiveFromUtc;
        EffectiveToUtc     = effectiveToUtc;
        UpdatedAtUtc       = DateTime.UtcNow;
    }

    /// <summary>Mark this entitlement as the tenant's default product.</summary>
    public void SetDefault()
    {
        IsDefault    = true;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Remove the default flag (used during auto-demotion).</summary>
    public void ClearDefault()
    {
        IsDefault    = false;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Enable this entitlement.</summary>
    public void Enable()
    {
        IsEnabled    = true;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Disable this entitlement. Also clears IsDefault.</summary>
    public void Disable()
    {
        IsEnabled    = false;
        IsDefault    = false;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
