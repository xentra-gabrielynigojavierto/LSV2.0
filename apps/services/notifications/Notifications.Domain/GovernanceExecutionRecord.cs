namespace Notifications.Domain;

/// <summary>
/// LS-NOTIF-SMS-025: Cross-channel governance execution telemetry record.
///
/// Persists safe aggregate data only — no raw phone numbers, email addresses,
/// message bodies, webhook URLs, provider payloads, or credentials.
/// Only IDs, decision metadata, and bounded safe metadata JSON are stored.
/// </summary>
public sealed class GovernanceExecutionRecord
{
    public Guid    Id                      { get; set; } = Guid.NewGuid();

    /// <summary>Originating notification ID — nullable for simulations.</summary>
    public Guid?   NotificationId          { get; set; }

    /// <summary>Originating attempt ID — nullable for simulations and pre-attempt evaluations.</summary>
    public Guid?   AttemptId               { get; set; }

    /// <summary>Tenant ID — nullable for platform-level simulations.</summary>
    public Guid?   TenantId                { get; set; }

    /// <summary>Normalized channel type: email | push | webhook | sms | in-app.</summary>
    public string  ChannelType             { get; set; } = string.Empty;

    /// <summary>allow | warn | review_required | block | suppress</summary>
    public string  DecisionType            { get; set; } = string.Empty;

    /// <summary>
    /// no_applicable_rules | rule_match | restricted_content | prohibited_content |
    /// unsafe_payload | topology_resolution_failed | channel_engine_failed |
    /// insufficient_context | evaluation_error | fail_open
    /// </summary>
    public string  ReasonCode              { get; set; } = string.Empty;

    /// <summary>JSON array of matched rule GUIDs — safe aggregate reference only.</summary>
    public string? MatchedRuleIdsJson      { get; set; }

    /// <summary>JSON array of matched rule pack GUIDs.</summary>
    public string? MatchedRulePackIdsJson  { get; set; }

    /// <summary>JSON array of applied overlay GUIDs.</summary>
    public string? AppliedOverlayIdsJson   { get; set; }

    /// <summary>Content classification label if derived — no raw content.</summary>
    public string? ContentClassification   { get; set; }

    /// <summary>ok | not_found | federation_disabled | error</summary>
    public string? TopologyResolutionStatus { get; set; }

    /// <summary>ok | fail_open | no_engine | error</summary>
    public string? EngineStatus            { get; set; }

    /// <summary>
    /// Bounded safe metadata JSON — no phones, emails, bodies, URLs, credentials.
    /// Max 2000 chars.
    /// </summary>
    public string? SafeMetadataJson        { get; set; }

    /// <summary>True if this record was created by a simulation run, not a live send.</summary>
    public bool    IsSimulation            { get; set; }

    public DateTime CreatedAt              { get; set; } = DateTime.UtcNow;
}
