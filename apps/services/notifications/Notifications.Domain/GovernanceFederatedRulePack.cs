namespace Notifications.Domain;

/// <summary>
/// Maps an existing SmsGovernanceRulePack to a channel/federation scope.
/// TenantId = null means global/channel-level mapping (applies to all tenants on that channel).
/// TenantId set means tenant-specific federated mapping for that channel.
/// RulePackId must reference an existing SmsGovernanceRulePack.
/// No credentials, raw phones, message bodies, or provider payloads are stored here.
/// </summary>
public sealed class GovernanceFederatedRulePack
{
    public Guid    Id              { get; set; }
    public Guid    RulePackId      { get; set; }
    public string  ChannelType     { get; set; } = string.Empty;
    public string? FederationGroup { get; set; }
    public Guid?   TenantId        { get; set; }
    public bool    Enabled         { get; set; } = true;
    public int     Priority        { get; set; } = 100;
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo   { get; set; }
    public DateTime  CreatedAt     { get; set; }
    public DateTime  UpdatedAt     { get; set; }
    public string?   CreatedBy     { get; set; }
    public string?   UpdatedBy     { get; set; }

    public bool IsEffective(DateTime nowUtc) =>
        Enabled &&
        (EffectiveFrom == null || EffectiveFrom <= nowUtc) &&
        (EffectiveTo   == null || EffectiveTo   >  nowUtc);
}
