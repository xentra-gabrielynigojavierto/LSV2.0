namespace Flow.Infrastructure.Outbox;

/// <summary>
/// LS-FLOW-E10.3 — runtime configuration for the SLA / timer evaluator.
/// Bound to the <c>WorkflowSla</c> section of <c>appsettings.json</c> /
/// environment variables. All defaults are sane for local dev and
/// intentionally generous so the loop never fights the operator.
///
/// <para>
/// Threshold semantics (all UTC):
///   • <c>DueSoonThresholdMinutes</c> — minutes before <c>DueAt</c> at
///     which an OnTrack instance is promoted to DueSoon.
///   • <c>EscalationThresholdMinutes</c> — minutes <i>after</i>
///     <c>DueAt</c> required before an Overdue instance is promoted to
///     Escalated. Single escalation level only this phase.
/// </para>
///
/// <para>
/// Idempotency: the evaluator only emits an outbox event when the
/// computed SlaStatus differs from the persisted one (or when
/// EscalationLevel changes), so PollingIntervalSeconds may be tuned
/// freely without producing duplicate notifications.
/// </para>
/// </summary>
public sealed class WorkflowSlaOptions
{
    public const string SectionName = "WorkflowSla";

    /// <summary>Master switch. Disable to suspend SLA evaluation entirely (assignment in StartAsync still happens).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Polling cadence between evaluator ticks.</summary>
    public int PollingIntervalSeconds { get; set; } = 30;

    /// <summary>Max active workflow instances inspected per tick. Bound to keep one tick latency-bounded.</summary>
    public int BatchSize { get; set; } = 200;

    /// <summary>Minutes before <c>DueAt</c> at which an instance flips OnTrack → DueSoon.</summary>
    public int DueSoonThresholdMinutes { get; set; } = 60;

    /// <summary>Minutes past <c>DueAt</c> required before an Overdue instance escalates.</summary>
    public int EscalationThresholdMinutes { get; set; } = 60;
}
