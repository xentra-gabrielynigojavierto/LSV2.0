namespace Notifications.Domain;

/// <summary>
/// LS-NOTIF-SMS-019: SMS Compliance Profile.
///
/// Groups rule pack assignments and enforcement mode for a tenant or globally.
/// TenantId null = platform default profile.
///
/// EnforcementMode controls how dynamic rule decisions interact with delivery:
///   permissive — block decisions are downgraded to review_required
///   standard   — decisions respected as-is (default)
///   strict     — review_required decisions are upgraded to block
///
/// DefaultRulePackIdsJson: JSON array of SmsGovernanceRulePack IDs to include
/// when this profile is active and no explicit pack is referenced.
/// </summary>
public sealed class SmsComplianceProfile
{
    public Guid     Id                     { get; set; } = Guid.NewGuid();

    /// <summary>null = platform default / global profile</summary>
    public Guid?    TenantId               { get; set; }

    public string   Name                   { get; set; } = string.Empty;
    public string?  Description            { get; set; }
    public bool     Enabled                { get; set; } = true;

    /// <summary>JSON array of SmsGovernanceRulePack IDs. May be null/empty.</summary>
    public string?  DefaultRulePackIdsJson { get; set; }

    /// <summary>permissive | standard | strict</summary>
    public string   EnforcementMode        { get; set; } = "standard";

    public DateTime CreatedAt              { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt              { get; set; } = DateTime.UtcNow;
    public string?  CreatedBy              { get; set; }
    public string?  UpdatedBy             { get; set; }
}
