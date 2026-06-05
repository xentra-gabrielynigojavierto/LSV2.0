namespace Notifications.Application.Interfaces;

// ---------------------------------------------------------------------------
// Decision type constants
// ---------------------------------------------------------------------------

public static class GovernanceDecisionTypes
{
    public const string Allow          = "allow";
    public const string Warn           = "warn";
    public const string ReviewRequired = "review_required";
    public const string Block          = "block";
    public const string Suppress       = "suppress";
}

// ---------------------------------------------------------------------------
// Reason code constants
// ---------------------------------------------------------------------------

public static class GovernanceReasonCodes
{
    public const string NoApplicableRules       = "no_applicable_rules";
    public const string RuleMatch               = "rule_match";
    public const string RestrictedContent       = "restricted_content";
    public const string ProhibitedContent       = "prohibited_content";
    public const string UnsafePayload           = "unsafe_payload";
    public const string TopologyResolutionFailed = "topology_resolution_failed";
    public const string ChannelEngineFailed      = "channel_engine_failed";
    public const string InsufficientContext      = "insufficient_context";
    public const string EvaluationError         = "evaluation_error";
    public const string FailOpen                = "fail_open";
    public const string NoEngineRegistered      = "no_engine_registered";
    public const string SmsEnforced             = "sms_enforced";
    public const string SimulationOnly          = "simulation_only";
}

// ---------------------------------------------------------------------------
// Execution context — transient only; PayloadTextForEvaluation never persisted
// ---------------------------------------------------------------------------

/// <summary>
/// Governance execution context passed to the runtime and channel engines.
/// PayloadTextForEvaluation is TRANSIENT — never persisted.
/// </summary>
public sealed class GovernanceExecutionContext
{
    public Guid?   NotificationId           { get; set; }
    public Guid?   AttemptId               { get; set; }
    public Guid?   TenantId                { get; set; }
    public string  ChannelType             { get; set; } = string.Empty;
    public Guid?   TemplateId              { get; set; }
    public string? TemplateKey             { get; set; }
    public Guid?   RolloutPlanId           { get; set; }
    public Guid?   ReleasePackageId        { get; set; }

    /// <summary>Safe subject/metadata hint — no PII, no raw recipient.</summary>
    public string? SubjectMetadata         { get; set; }

    /// <summary>TRANSIENT ONLY — evaluated in-memory, never persisted.</summary>
    public string? PayloadTextForEvaluation { get; set; }

    /// <summary>Safe payload metadata JSON — no phones, emails, URLs, credentials.</summary>
    public string? PayloadMetadataJson     { get; set; }

    /// <summary>Evaluation context label (content, template, escalation).</summary>
    public string? EvaluationContext       { get; set; }

    public DateTime ExecutedAtUtc          { get; set; } = DateTime.UtcNow;
}

// ---------------------------------------------------------------------------
// Execution result
// ---------------------------------------------------------------------------

public sealed class GovernanceExecutionResult
{
    public string  DecisionType           { get; set; } = GovernanceDecisionTypes.Allow;
    public string  ReasonCode             { get; set; } = GovernanceReasonCodes.NoApplicableRules;
    public string  ChannelType            { get; set; } = string.Empty;
    public Guid?   TenantId               { get; set; }

    public IReadOnlyList<Guid> MatchedRuleIds     { get; set; } = Array.Empty<Guid>();
    public IReadOnlyList<Guid> MatchedRulePackIds { get; set; } = Array.Empty<Guid>();
    public IReadOnlyList<Guid> AppliedOverlayIds  { get; set; } = Array.Empty<Guid>();

    public string? ContentClassification  { get; set; }
    public bool    ShouldProceed          { get; set; } = true;
    public bool    ShouldWarn             { get; set; }
    public bool    ShouldBlock            { get; set; }
    public bool    RequiresReview         { get; set; }

    /// <summary>Safe key-value metadata — no PII, no raw payloads.</summary>
    public Dictionary<string, string> SafeMetadata { get; set; } = new();

    public string? TopologyResolutionStatus { get; set; }
    public string? EngineStatus             { get; set; }
}

// ---------------------------------------------------------------------------
// Simulation
// ---------------------------------------------------------------------------

public sealed class GovernanceSimulationRequest
{
    public string  ChannelType            { get; set; } = string.Empty;
    public Guid?   TenantId               { get; set; }
    public Guid?   TemplateId             { get; set; }
    public string? TemplateKey            { get; set; }
    public Guid?   RolloutPlanId          { get; set; }
    public Guid?   ReleasePackageId       { get; set; }

    /// <summary>Operator-provided test payload text — transient, never persisted.</summary>
    public string? SimulationPayloadText  { get; set; }

    public string? SubjectText            { get; set; }
    public string? EvaluationContext      { get; set; }
}

public sealed class GovernanceSimulationResult
{
    public GovernanceExecutionResult Execution    { get; set; } = new();
    public TopologyExplanation?      Explanation  { get; set; }
    public int                       RulesEvaluated { get; set; }
    public TimeSpan                  EvaluationDuration { get; set; }
    public IReadOnlyList<string>     SimulationWarnings { get; set; } = Array.Empty<string>();
}

// ---------------------------------------------------------------------------
// Channel runtime status
// ---------------------------------------------------------------------------

public sealed class GovernanceChannelRuntimeStatus
{
    public string  ChannelType    { get; set; } = string.Empty;
    public bool    EngineRegistered { get; set; }
    public bool    SupportsSimulation { get; set; }
    public bool    EnforcementEnabled { get; set; }
    public string  EnforcementMode { get; set; } = "passive";
    public string? Notes           { get; set; }
}

// ---------------------------------------------------------------------------
// Interface
// ---------------------------------------------------------------------------

public interface IGovernanceExecutionRuntime
{
    /// <summary>
    /// Evaluate governance for a notification channel send.
    /// Fails open when FailOpenOnRuntimeError is true.
    /// Never persists PayloadTextForEvaluation.
    /// </summary>
    Task<GovernanceExecutionResult> EvaluateAsync(
        GovernanceExecutionContext context,
        CancellationToken ct = default);

    /// <summary>
    /// Simulate governance evaluation transiently — persists a simulation=true telemetry record.
    /// Raw payload text from SimulationPayloadText is never persisted.
    /// </summary>
    Task<GovernanceSimulationResult> SimulateAsync(
        GovernanceSimulationRequest request,
        CancellationToken ct = default);

    /// <summary>Returns per-channel runtime status for all registered engines.</summary>
    Task<IReadOnlyList<GovernanceChannelRuntimeStatus>> GetChannelRuntimeStatusAsync(
        CancellationToken ct = default);
}
