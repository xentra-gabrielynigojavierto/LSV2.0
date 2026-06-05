namespace Notifications.Domain;

/// <summary>
/// LS-NOTIF-SMS-018: Persisted template governance decision.
///
/// Records all non-trivial content-governance evaluation outcomes.
/// Raw phone numbers are NEVER stored in this table.
///
/// DecisionType: allow | warn | block | review_required
/// ReasonCode:
///   template_not_found / template_not_approved / prohibited_content /
///   invalid_variables / classification_mismatch / marketing_restricted /
///   unsafe_payload / governance_evaluation_error
/// </summary>
public sealed class SmsTemplateGovernanceDecision
{
    public Guid     Id                       { get; set; } = Guid.NewGuid();
    public Guid?    NotificationId           { get; set; }
    public Guid?    AttemptId                { get; set; }
    public Guid?    TemplateId               { get; set; }
    public Guid?    TemplateVersionId        { get; set; }
    public Guid?    TenantId                 { get; set; }

    // DecisionType: allow | warn | block | review_required
    public string   DecisionType             { get; set; } = "allow";

    // ReasonCode: machine-readable explanation
    public string   ReasonCode               { get; set; } = string.Empty;

    public string?  ContentClassification    { get; set; }
    public bool     VariableValidationPassed { get; set; } = true;
    public string?  DecisionMetadataJson     { get; set; }

    public DateTime CreatedAt                { get; set; } = DateTime.UtcNow;
}
