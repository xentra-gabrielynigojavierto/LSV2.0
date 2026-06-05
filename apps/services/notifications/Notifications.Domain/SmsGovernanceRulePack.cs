namespace Notifications.Domain;

/// <summary>
/// LS-NOTIF-SMS-019: SMS Governance Rule Pack.
///
/// Groups related governance rules for a tenant or globally.
/// TenantId null = platform-wide / global rule pack.
/// Multiple global packs may coexist, evaluated by ascending Priority.
///
/// Status: draft | active | inactive | archived
/// InheritanceMode:
///   merge       — global + tenant rules both apply (default safe mode)
///   override    — tenant pack replaces global rules of same RuleType
///   append_only — global evaluated first, tenant appended after
///
/// Expired packs (EffectiveTo in past) are excluded by resolver.
/// </summary>
public sealed class SmsGovernanceRulePack
{
    public Guid     Id              { get; set; } = Guid.NewGuid();

    /// <summary>null = platform-global rule pack</summary>
    public Guid?    TenantId        { get; set; }

    public string   Name            { get; set; } = string.Empty;
    public string?  Description     { get; set; }
    public int      Version         { get; set; } = 1;

    /// <summary>draft | active | inactive | archived</summary>
    public string   Status          { get; set; } = "draft";

    public bool     Enabled         { get; set; } = true;

    /// <summary>merge | override | append_only</summary>
    public string   InheritanceMode { get; set; } = "merge";

    /// <summary>Lower number = higher priority. Evaluated ascending.</summary>
    public int      Priority        { get; set; } = 100;

    public DateTime? EffectiveFrom  { get; set; }
    public DateTime? EffectiveTo    { get; set; }

    public DateTime CreatedAt       { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt       { get; set; } = DateTime.UtcNow;
    public string?  CreatedBy       { get; set; }
    public string?  UpdatedBy       { get; set; }
}
