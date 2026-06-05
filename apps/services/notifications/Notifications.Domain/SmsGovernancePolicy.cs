namespace Notifications.Domain;

/// <summary>
/// LS-NOTIF-SMS-017: SMS governance policy definition.
/// Stores operational controls for quiet hours, geographic restriction,
/// rate limiting, provider governance, retry governance, and escalation guardrails.
///
/// TenantId = null means platform-wide / global policy.
/// Tenant-specific policies take precedence over global policies of the same type.
///
/// PolicyJson contains structured operational configuration — never credentials or secrets.
/// Raw phone numbers are never stored in governance tables.
/// </summary>
public sealed class SmsGovernancePolicy
{
    public Guid   Id       { get; set; } = Guid.NewGuid();

    /// <summary>null = platform-wide global policy.</summary>
    public Guid?  TenantId { get; set; }

    public string Name       { get; set; } = string.Empty;

    /// <summary>
    /// quiet_hours | geographic_restriction | rate_limit |
    /// provider_governance | retry_governance | escalation_guardrail
    /// </summary>
    public string PolicyType { get; set; } = string.Empty;

    public bool   Enabled    { get; set; } = true;

    /// <summary>Lower number = higher priority. Evaluated ascending.</summary>
    public int    Priority   { get; set; } = 100;

    /// <summary>Structured policy config JSON — no credentials, no secrets, no phone numbers.</summary>
    public string PolicyJson { get; set; } = "{}";

    /// <summary>When true, an emergency override can bypass this policy.</summary>
    public bool   EmergencyOverrideAllowed { get; set; } = false;

    public DateTime  CreatedAt  { get; set; } = DateTime.UtcNow;
    public DateTime  UpdatedAt  { get; set; } = DateTime.UtcNow;
    public string?   CreatedBy  { get; set; }
    public string?   UpdatedBy  { get; set; }
}
