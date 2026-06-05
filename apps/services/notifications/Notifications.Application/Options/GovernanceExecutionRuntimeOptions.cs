namespace Notifications.Application.Options;

/// <summary>
/// LS-NOTIF-SMS-025: Unified governance execution runtime configuration.
/// Bound from the "GovernanceExecutionRuntime" section in appsettings.json.
/// </summary>
public sealed class GovernanceExecutionRuntimeOptions
{
    /// <summary>Master switch. When false, all channel runtime evaluation is bypassed.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// When true, runtime errors (topology failure, engine error) result in allow
    /// rather than block. Default true (safe default).
    /// </summary>
    public bool FailOpenOnRuntimeError { get; set; } = true;

    /// <summary>Activate Email governance enforcement engine.</summary>
    public bool EnableEmailEnforcement { get; set; } = true;

    /// <summary>Activate Push governance enforcement engine (reserved — push pipeline not yet active).</summary>
    public bool EnablePushEnforcement { get; set; } = true;

    /// <summary>Activate Webhook governance enforcement engine (reserved — general webhook pipeline not yet active).</summary>
    public bool EnableWebhookEnforcement { get; set; } = true;

    /// <summary>
    /// Expose SMS channel via compatibility adapter for simulation and runtime status.
    /// Does NOT duplicate SMS governance execution. Default false.
    /// </summary>
    public bool EnableSmsCompatibilityRuntime { get; set; } = false;

    /// <summary>
    /// Persist allow decisions in GovernanceExecutionRecords.
    /// When false, only warn/block/review/suppress/error decisions are persisted.
    /// Default false to reduce write volume.
    /// </summary>
    public bool PersistAllowDecisions { get; set; } = false;

    /// <summary>Max characters of payload text passed to rule evaluation. Excess is truncated.</summary>
    public int MaxEvaluationTextLength { get; set; } = 5000;

    /// <summary>Timeout for regex evaluation per rule in milliseconds.</summary>
    public int RegexTimeoutMs { get; set; } = 200;
}
