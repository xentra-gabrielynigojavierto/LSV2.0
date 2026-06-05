namespace Notifications.Domain;

/// <summary>
/// LS-NOTIF-SMS-019: Individual dynamic governance rule.
///
/// Belongs to an SmsGovernanceRulePack.
/// Pattern is type-dependent:
///   prohibited_phrase    — literal phrase for case-insensitive matching (max 500 chars)
///   restricted_pattern   — safe regex (validated at creation; max 500 chars)
///   classification_override — source classification label to match
///   variable_rule        — variable name to restrict/require
///   link_rule            — domain or URL pattern to restrict
///   delivery_restriction — context key or category label
///   escalation_rule      — alert type or escalation context
///
/// MetadataJson stores rule-type-specific config (no credentials, no secrets, no phones).
///
/// Severity: allow | warn | review_required | block | override_allowed
/// </summary>
public sealed class SmsGovernanceRule
{
    public Guid     Id            { get; set; } = Guid.NewGuid();
    public Guid     RulePackId    { get; set; }
    public string   Name          { get; set; } = string.Empty;
    public string?  Description   { get; set; }

    /// <summary>
    /// prohibited_phrase | restricted_pattern | classification_override |
    /// variable_rule | link_rule | delivery_restriction | escalation_rule
    /// </summary>
    public string   RuleType      { get; set; } = string.Empty;

    /// <summary>Type-dependent pattern. Max 500 chars. Regex validated for safety.</summary>
    public string?  Pattern       { get; set; }

    /// <summary>allow | warn | review_required | block | override_allowed</summary>
    public string   Severity      { get; set; } = "block";

    public bool     Enabled       { get; set; } = true;

    /// <summary>Lower number = higher priority within the pack. Evaluated ascending.</summary>
    public int      Priority      { get; set; } = 100;

    /// <summary>Type-specific config JSON — no secrets, no phones, no credentials.</summary>
    public string?  MetadataJson  { get; set; }

    public DateTime CreatedAt     { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt     { get; set; } = DateTime.UtcNow;
    public string?  CreatedBy     { get; set; }
    public string?  UpdatedBy     { get; set; }
}
