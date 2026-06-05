namespace Notifications.Domain;

/// <summary>
/// LS-NOTIF-SMS-018: SMS Template registry.
///
/// Tracks the governance lifecycle for each SMS template key.
/// TenantId null = platform-global template.
/// TemplateKey must be unique per (TenantId, TemplateKey) scope.
///
/// Status lifecycle: draft → pending_review → approved/rejected → archived
/// ContentClassification: transactional / operational / escalation / compliance /
///                        marketing_restricted / prohibited
/// </summary>
public sealed class SmsTemplate
{
    public Guid     Id                     { get; set; } = Guid.NewGuid();
    public Guid?    TenantId               { get; set; }
    public string   TemplateKey            { get; set; } = string.Empty;
    public string   Name                   { get; set; } = string.Empty;
    public string?  Description            { get; set; }
    public string?  Category               { get; set; }

    // Status: draft | pending_review | approved | rejected | archived
    public string   Status                 { get; set; } = "draft";

    public int      CurrentVersion         { get; set; } = 0;
    public int?     LatestApprovedVersion  { get; set; }

    // ContentClassification: transactional | operational | escalation |
    //                        compliance | marketing_restricted | prohibited
    public string   ContentClassification  { get; set; } = "transactional";

    public bool     RequiresApproval       { get; set; } = true;
    public bool     Enabled                { get; set; } = true;

    public DateTime CreatedAt              { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt              { get; set; } = DateTime.UtcNow;
    public string?  CreatedBy              { get; set; }
    public string?  UpdatedBy             { get; set; }
}
