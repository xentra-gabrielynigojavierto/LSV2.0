namespace Notifications.Domain;

/// <summary>
/// LS-NOTIF-SMS-022: Tenant cohort targeting record for a governance rollout.
/// Associates a specific tenant (by opaque TenantId) with a rollout plan and optionally
/// a specific rollout stage.
///
/// TenantId is stored as an opaque identifier only — no tenant secrets, no raw phones,
/// no message content, no credentials are stored here.
///
/// TenantId must be unique per (RolloutPlanId, StageId?) to prevent duplicate targeting.
/// </summary>
public sealed class SmsGovernanceTenantCohort
{
    public Guid   Id             { get; set; } = Guid.NewGuid();
    public Guid   RolloutPlanId  { get; set; }

    /// <summary>Null = cohort applies to all stages of the rollout; non-null = stage-specific targeting.</summary>
    public Guid?  StageId        { get; set; }

    /// <summary>Opaque tenant identifier. Never store raw phones or tenant secrets alongside this.</summary>
    public Guid   TenantId       { get; set; }

    public string CohortName     { get; set; } = string.Empty;

    /// <summary>When false, this cohort is excluded from the rollout (soft disable).</summary>
    public bool   Enabled        { get; set; } = true;

    /// <summary>Set when the cohort tenant has been activated in the rollout.</summary>
    public DateTime? ActivatedAt   { get; set; }

    /// <summary>Set when the cohort tenant has been rolled back.</summary>
    public DateTime? RolledBackAt  { get; set; }

    public DateTime  CreatedAt     { get; set; }
    public DateTime  UpdatedAt     { get; set; }
}
