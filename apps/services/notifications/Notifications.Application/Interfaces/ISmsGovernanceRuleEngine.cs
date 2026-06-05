namespace Notifications.Application.Interfaces;

// ─── Request ─────────────────────────────────────────────────────────────────

/// <summary>
/// LS-NOTIF-SMS-019: Input to the dynamic governance rule engine.
/// Raw phone numbers must NEVER be set on this object.
/// </summary>
public sealed class SmsGovernanceRuleEvaluationRequest
{
    public Guid?   TenantId              { get; set; }
    public Guid?   NotificationId        { get; set; }
    public Guid?   TemplateId            { get; set; }
    public Guid?   TemplateVersionId     { get; set; }

    /// <summary>Pre-rendered SMS body. No raw phones.</summary>
    public string? RenderedBody          { get; set; }

    /// <summary>Raw template body (with {{tokens}}). Used for variable_rule evaluation.</summary>
    public string? TemplateBody          { get; set; }

    /// <summary>Variable substitutions (name → value). No PII phone values.</summary>
    public Dictionary<string, string>? Variables { get; set; }

    /// <summary>Current content classification from LS-018 (e.g. "transactional").</summary>
    public string? ContentClassification { get; set; }

    /// <summary>Evaluation context: "content" | "template" | "escalation" | "simulation"</summary>
    public string  Context               { get; set; } = "content";

    public bool    IsDryRun              { get; set; }
    public DateTime NowUtc               { get; set; } = DateTime.UtcNow;
}

// ─── Result ───────────────────────────────────────────────────────────────────

/// <summary>
/// LS-NOTIF-SMS-019: Dynamic rule match detail within an evaluation result.
/// </summary>
public sealed class SmsGovernanceRuleMatch
{
    public Guid    RuleId             { get; set; }
    public Guid    RulePackId         { get; set; }
    public string  RuleType           { get; set; } = string.Empty;
    public string  RuleName           { get; set; } = string.Empty;
    public string  Severity           { get; set; } = string.Empty;

    /// <summary>Matched pattern — truncated/masked for safety (first 20 chars only).</summary>
    public string? MatchedPatternMasked { get; set; }

    public string? ReasonCode         { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = [];
}

/// <summary>
/// LS-NOTIF-SMS-019: Output of the dynamic rule engine evaluation.
/// </summary>
public sealed class SmsGovernanceRuleEvaluationResult
{
    /// <summary>allow | warn | review_required | block | override_allowed</summary>
    public string  DecisionType              { get; set; } = "allow";

    /// <summary>Machine-readable explanation.</summary>
    public string  ReasonCode                { get; set; } = "no_matching_rule";

    public bool    ShouldProceed             => DecisionType is "allow" or "warn" or "override_allowed";
    public bool    ShouldBlock               => DecisionType is "block" or "review_required";

    /// <summary>All rules that matched (may include multiple).</summary>
    public List<SmsGovernanceRuleMatch> MatchedRules { get; set; } = [];

    /// <summary>Classification after any classification_override rules applied.</summary>
    public string? EffectiveClassification   { get; set; }

    /// <summary>Enforcement mode applied during this evaluation.</summary>
    public string  EnforcementMode           { get; set; } = "standard";

    public Dictionary<string, object> Metadata { get; set; } = [];
}

// ─── Interface ────────────────────────────────────────────────────────────────

/// <summary>
/// LS-NOTIF-SMS-019: Dynamic governance rule engine.
///
/// Evaluates tenant-configurable rule packs against SMS content.
/// No AI/ML, no external APIs, no raw phone persistence.
/// Evaluation failures always degrade safely (fail-open by default).
///
/// Rules are resolved via ISmsGovernanceRuleResolver.
/// Final decision = highest severity across all matched rules.
/// EnforcementMode may upgrade or downgrade the final decision.
/// </summary>
public interface ISmsGovernanceRuleEngine
{
    /// <summary>
    /// Evaluate dynamic governance rules against rendered SMS body content.
    /// Called from SmsTemplateGovernanceService after static LS-018 checks.
    /// </summary>
    Task<SmsGovernanceRuleEvaluationResult> EvaluateContentAsync(
        SmsGovernanceRuleEvaluationRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Evaluate dynamic governance rules against a template body (pre-render).
    /// Used during template approval governance for template-level rules.
    /// </summary>
    Task<SmsGovernanceRuleEvaluationResult> EvaluateTemplateAsync(
        SmsGovernanceRuleEvaluationRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Evaluate dynamic governance rules for escalation payloads.
    /// Applies escalation_rule and delivery_restriction rule types only.
    /// </summary>
    Task<SmsGovernanceRuleEvaluationResult> EvaluateEscalationPayloadAsync(
        SmsGovernanceRuleEvaluationRequest request,
        CancellationToken ct = default);
}
