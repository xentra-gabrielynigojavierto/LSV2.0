namespace Notifications.Domain;

/// <summary>
/// LS-NOTIF-SMS-018: SMS Template version — immutable once approved.
///
/// TemplateBody is the raw body template (may contain {{variable}} tokens).
/// VariableSchemaJson is an optional JSON array of variable descriptors:
///   [{ "name": "recipientName", "required": true, "description": "..." }, ...]
///
/// ApprovalStatus: draft | pending_review | approved | rejected
/// Once approved, TemplateBody must NOT be mutated.
/// </summary>
public sealed class SmsTemplateVersion
{
    public Guid     Id                   { get; set; } = Guid.NewGuid();
    public Guid     TemplateId           { get; set; }
    public int      VersionNumber        { get; set; } = 1;
    public string   TemplateBody         { get; set; } = string.Empty;
    public string?  VariableSchemaJson   { get; set; }
    public string   ContentClassification { get; set; } = "transactional";

    // ApprovalStatus: draft | pending_review | approved | rejected
    public string   ApprovalStatus       { get; set; } = "draft";
    public string?  ApprovedBy           { get; set; }
    public DateTime? ApprovedAt          { get; set; }
    public string?  RejectionReason      { get; set; }

    public DateTime CreatedAt            { get; set; } = DateTime.UtcNow;
    public string?  CreatedBy            { get; set; }
}
