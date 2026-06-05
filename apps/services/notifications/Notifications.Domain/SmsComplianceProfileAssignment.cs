namespace Notifications.Domain;

/// <summary>
/// LS-NOTIF-SMS-019: Tenant-to-compliance-profile assignment.
///
/// Links a specific tenant to an SmsComplianceProfile for a given scope.
/// If no assignment exists for a tenant, the resolver falls back to global packs.
///
/// Scope values:
///   tenant             — applies to all SMS for this tenant
///   provider           — applies to SMS sent via a specific provider
///   template_category  — applies only to a specific content classification
///   escalation         — applies to escalation-channel SMS
/// </summary>
public sealed class SmsComplianceProfileAssignment
{
    public Guid     Id        { get; set; } = Guid.NewGuid();
    public Guid     TenantId  { get; set; }
    public Guid     ProfileId { get; set; }

    /// <summary>tenant | provider | template_category | escalation</summary>
    public string   Scope     { get; set; } = "tenant";

    public bool     Enabled   { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
