namespace Tenant.Domain;

/// <summary>
/// Supported value types for tenant settings.
/// </summary>
public enum SettingValueType
{
    String,
    Boolean,
    Number,
    Json
}

/// <summary>
/// Represents a namespaced key/value setting owned by a tenant.
///
/// Block 4 — tenant settings store.
///
/// Design notes:
///   - SettingKey must be dot-namespaced (e.g. "platform.default-product", "portal.locale.default").
///   - ValueType declares the semantic type; SettingValue is always stored as a string.
///   - ProductKey (nullable) scopes the setting to a specific product entitlement key.
///   - Uniqueness: (TenantId, SettingKey, ProductKey) must be unique — enforced by service layer.
///
/// Examples:
///   "platform.default-product"   String   "liens"
///   "portal.locale.default"      String   "en-US"
///   "portal.timezone.default"    String   "America/New_York"
///   "portal.readonly"            Boolean  "true"
///   "branding.colors"            Json     "{\"primary\":\"#005ea2\"}"
/// </summary>
public class TenantSetting
{
    // ── Identity ──────────────────────────────────────────────────────────────

    public Guid Id { get; private set; }

    /// <summary>Owning tenant.</summary>
    public Guid TenantId { get; private set; }

    // ── Setting ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Normalized, dot-namespaced setting key. Lowercase, trimmed.
    /// Must contain at least one dot (e.g. "platform.default-product").
    /// </summary>
    public string SettingKey { get; private set; } = string.Empty;

    /// <summary>String-serialized value. Caller must match ValueType semantics.</summary>
    public string SettingValue { get; private set; } = string.Empty;

    /// <summary>Semantic type of the value.</summary>
    public SettingValueType ValueType { get; private set; }

    /// <summary>
    /// Optional product key scope. Null = platform-wide setting.
    /// When set, the setting applies only in the context of that product.
    /// </summary>
    public string? ProductKey { get; private set; }

    // ── Timestamps ────────────────────────────────────────────────────────────

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    // ── Navigation ────────────────────────────────────────────────────────────

    public Tenant? Tenant { get; private set; }

    private TenantSetting() { }

    // ── Key normalization ─────────────────────────────────────────────────────

    /// <summary>Normalizes a setting key to lowercase trimmed form.</summary>
    public static string NormalizeKey(string key) =>
        (key ?? throw new ArgumentNullException(nameof(key))).Trim().ToLowerInvariant();

    /// <summary>
    /// Returns true if the normalized key has at least one dot (is namespaced).
    /// </summary>
    public static bool IsValidKey(string normalizedKey) =>
        !string.IsNullOrWhiteSpace(normalizedKey) && normalizedKey.Contains('.');

    // ── Factory ───────────────────────────────────────────────────────────────

    public static TenantSetting Create(
        Guid            tenantId,
        string          settingKey,
        string          settingValue,
        SettingValueType valueType,
        string?         productKey = null)
    {
        var normalizedKey = NormalizeKey(settingKey);
        if (!IsValidKey(normalizedKey))
            throw new ArgumentException(
                "SettingKey must be dot-namespaced (e.g. 'platform.default-product').",
                nameof(settingKey));

        var normalizedProductKey = productKey is null
            ? null
            : (string.IsNullOrWhiteSpace(productKey)
                ? null
                : productKey.Trim().ToLowerInvariant());

        var now = DateTime.UtcNow;
        return new TenantSetting
        {
            Id           = Guid.NewGuid(),
            TenantId     = tenantId,
            SettingKey   = normalizedKey,
            SettingValue = settingValue ?? string.Empty,
            ValueType    = valueType,
            ProductKey   = normalizedProductKey,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    // ── Mutators ──────────────────────────────────────────────────────────────

    public void UpdateValue(string settingValue, SettingValueType valueType)
    {
        SettingValue = settingValue ?? string.Empty;
        ValueType    = valueType;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
