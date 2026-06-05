namespace Notifications.Domain;

/// <summary>
/// Channel-aware governance overlay. Applies to a specific channel and optional tenant
/// without ever mutating base governance rules. Applied in-memory during topology resolution.
/// OverlayJson must be safe metadata only — no credentials, phones, or provider payloads.
/// </summary>
public sealed class GovernanceFederationOverlay
{
    public Guid    Id           { get; set; }
    public Guid?   TenantId     { get; set; }
    public string  ChannelType  { get; set; } = string.Empty;
    public Guid?   RulePackId   { get; set; }
    public Guid?   RuleId       { get; set; }
    public string  OverlayType  { get; set; } = string.Empty;
    public string  OverlayState { get; set; } = OverlayStates.Draft;
    public string? OverlayJson  { get; set; }
    public int     Priority     { get; set; } = 100;
    public bool    Enabled      { get; set; } = true;
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo   { get; set; }
    public DateTime  CreatedAt  { get; set; }
    public DateTime  UpdatedAt  { get; set; }
    public string?   CreatedBy  { get; set; }
    public string?   UpdatedBy  { get; set; }

    public static class OverlayTypes
    {
        public const string AddRule              = "add_rule";
        public const string DisableRule          = "disable_rule";
        public const string SuppressRule         = "suppress_rule";
        public const string OverrideSeverity     = "override_severity";
        public const string OverridePattern      = "override_pattern";
        public const string OverrideMetadata     = "override_metadata";
        public const string OverrideClassification = "override_classification";
        public const string ChannelOverride      = "channel_override";
        public const string TenantChannelOverride = "tenant_channel_override";

        public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            AddRule, DisableRule, SuppressRule, OverrideSeverity, OverridePattern,
            OverrideMetadata, OverrideClassification, ChannelOverride, TenantChannelOverride
        };

        public static bool IsValid(string t) => All.Contains(t);
    }

    public static class OverlayStates
    {
        public const string Draft      = "draft";
        public const string Active     = "active";
        public const string Inactive   = "inactive";
        public const string Expired    = "expired";
        public const string Superseded = "superseded";

        public static readonly IReadOnlySet<string> Terminal = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Inactive, Expired, Superseded
        };
    }

    private static readonly IReadOnlySet<string> _sensitiveKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "password", "secret", "token", "apikey", "api_key", "credenti", "webhook", "bearer", "private"
    };

    public static bool HasSensitiveContent(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        var lower = json.ToLowerInvariant();
        foreach (var kw in _sensitiveKeywords)
            if (lower.Contains(kw)) return true;
        return false;
    }

    public bool IsEffective(DateTime nowUtc) =>
        Enabled &&
        OverlayState == OverlayStates.Active &&
        (EffectiveFrom == null || EffectiveFrom <= nowUtc) &&
        (EffectiveTo   == null || EffectiveTo   >  nowUtc);
}
