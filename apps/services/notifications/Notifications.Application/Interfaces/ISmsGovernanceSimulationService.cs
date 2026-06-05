namespace Notifications.Application.Interfaces;

// ─── Request / Response ───────────────────────────────────────────────────────

public sealed class SmsGovernanceSimulationRequest
{
    /// <summary>Tenant to simulate for. null = global packs only.</summary>
    public Guid?   TenantId              { get; set; }

    /// <summary>Pre-rendered SMS body. No raw phone numbers.</summary>
    public string  RenderedBody          { get; set; } = string.Empty;

    public string? TemplateKey           { get; set; }
    public string? TemplateBody          { get; set; }
    public Dictionary<string, string>?  Variables { get; set; }
    public string? ContentClassification { get; set; }

    /// <summary>Evaluation context: "content" | "template" | "escalation"</summary>
    public string  Context               { get; set; } = "content";

    /// <summary>When true, returns full rule evaluation trace in response.</summary>
    public bool    IncludeRuleTrace      { get; set; }

    /// <summary>When true, persists the simulation decision for audit trail.</summary>
    public bool    PersistDecision       { get; set; }
}

public sealed class SmsGovernanceSimulationMatchedRule
{
    public Guid    RuleId               { get; set; }
    public Guid    RulePackId           { get; set; }
    public string  RuleName             { get; set; } = string.Empty;
    public string  RuleType             { get; set; } = string.Empty;
    public string  Severity             { get; set; } = string.Empty;
    public string? MatchedPatternMasked { get; set; }
    public string? ReasonCode           { get; set; }
}

public sealed class SmsGovernanceSimulationTrace
{
    public string Step         { get; set; } = string.Empty;
    public string DecisionType { get; set; } = string.Empty;
    public string ReasonCode   { get; set; } = string.Empty;
    public bool   Blocked      { get; set; }
}

public sealed class SmsGovernanceSimulationResponse
{
    /// <summary>Final combined decision: allow | warn | review_required | block</summary>
    public string  FinalDecision         { get; set; } = "allow";
    public string  FinalReasonCode       { get; set; } = string.Empty;
    public string? ContentClassification { get; set; }
    public bool    WouldBlock            { get; set; }

    /// <summary>Result from LS-018 static classification/variable/prohibited checks.</summary>
    public string  StaticDecision        { get; set; } = "allow";
    public string  StaticReasonCode      { get; set; } = string.Empty;

    /// <summary>Result from LS-019 dynamic rule engine.</summary>
    public string  DynamicDecision       { get; set; } = "allow";
    public string  DynamicReasonCode     { get; set; } = string.Empty;

    public List<SmsGovernanceSimulationMatchedRule> MatchedRules { get; set; } = [];
    public List<SmsGovernanceSimulationTrace>       RuleTrace    { get; set; } = [];
    public List<string>                              Warnings     { get; set; } = [];

    public string  EnforcementMode       { get; set; } = "standard";
    public bool    ProfileAssigned       { get; set; }
    public DateTime SimulatedAt          { get; set; } = DateTime.UtcNow;
}

// ─── Interface ────────────────────────────────────────────────────────────────

/// <summary>
/// LS-NOTIF-SMS-019: SMS Governance Simulation Service.
///
/// Performs a complete dry-run governance evaluation without sending SMS.
/// Combines LS-018 static classification + LS-019 dynamic rule engine.
/// Returns full decision trace when IncludeRuleTrace = true.
///
/// MUST NOT send SMS.
/// MUST NOT persist live decisions unless PersistDecision = true.
/// </summary>
public interface ISmsGovernanceSimulationService
{
    Task<SmsGovernanceSimulationResponse> SimulateAsync(
        SmsGovernanceSimulationRequest request,
        CancellationToken ct = default);
}
