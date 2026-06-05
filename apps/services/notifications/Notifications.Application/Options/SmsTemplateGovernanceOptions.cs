namespace Notifications.Application.Options;

/// <summary>
/// LS-NOTIF-SMS-018: SMS Template Governance configuration options.
///
/// Bound from the "SmsTemplateGovernance" section in appsettings.json.
/// All defaults are deliberately safe (fail-open, require approved templates).
/// </summary>
public sealed class SmsTemplateGovernanceOptions
{
    public const string SectionName = "SmsTemplateGovernance";

    /// <summary>Master kill-switch. When false the governance block is skipped and delivery continues.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// When true, SMS messages that reference a TemplateKey must have an approved version.
    /// When false, templates in draft/pending_review state are allowed.
    /// </summary>
    public bool RequireApprovedTemplates { get; set; } = true;

    /// <summary>
    /// When true, evaluation exceptions degrade to allow (default).
    /// When false, evaluation exceptions result in block.
    /// </summary>
    public bool FailOpenOnEvaluationError { get; set; } = true;

    /// <summary>Maximum SMS body length in characters (including rendered variables). Default: 1600.</summary>
    public int MaxTemplateLength { get; set; } = 1600;

    /// <summary>Maximum variable token count in a template body. Default: 50.</summary>
    public int MaxVariableCount { get; set; } = 50;

    /// <summary>
    /// When true, messages without a TemplateKey (inline/untemplated body) are allowed.
    /// When false, every SMS must reference a registered template key.
    /// </summary>
    public bool AllowInlineUntemplatedMessages { get; set; } = false;

    /// <summary>
    /// Content classifications that should be blocked or require review.
    /// Default: marketing_restricted and prohibited are blocked.
    /// </summary>
    public List<string> RestrictedCategories { get; set; } =
    [
        "marketing_restricted",
        "prohibited",
    ];
}
