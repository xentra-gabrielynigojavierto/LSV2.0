namespace Notifications.Application.Interfaces;

/// <summary>
/// LS-NOTIF-SMS-025: Per-channel governance enforcement engine abstraction.
///
/// Engines receive effective rules resolved by IGovernanceTopologyResolver.
/// Engines MUST NOT:
/// - Query the DB directly for rule content (use GovernanceRuleEvaluationHelper)
/// - Call external services
/// - Persist raw payloads, phone numbers, email addresses, or webhook URLs
/// - Mutate governance rules
/// Engines MUST fail safely (return allow on error when fail-open is configured).
/// </summary>
public interface IGovernanceChannelEnforcementEngine
{
    /// <summary>Normalized channel type this engine handles (e.g. "email", "push", "webhook", "sms").</summary>
    string ChannelType { get; }

    /// <summary>True if this engine supports simulation (SimulateAsync).</summary>
    bool SupportsSimulation { get; }

    /// <summary>
    /// Evaluate governance for a channel-specific delivery context.
    /// The topology graph provides pack summaries; engines use GovernanceRuleEvaluationHelper
    /// to load and evaluate actual rule content.
    /// </summary>
    Task<GovernanceExecutionResult> EvaluateAsync(
        GovernanceExecutionContext context,
        GovernanceTopologyGraph topology,
        CancellationToken ct = default);

    /// <summary>
    /// Simulate governance evaluation against operator-provided payload text.
    /// Payload text is transient — must not be persisted by the engine.
    /// </summary>
    Task<GovernanceSimulationResult> SimulateAsync(
        GovernanceSimulationRequest request,
        GovernanceTopologyGraph topology,
        CancellationToken ct = default);
}
