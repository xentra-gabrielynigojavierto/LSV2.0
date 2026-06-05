namespace Flow.Infrastructure.Outbox;

/// <summary>
/// LS-FLOW-E10.3 (task slice) — runtime configuration for the
/// task-level SLA / timer evaluator and its companion clock policy.
/// Bound to the <c>WorkflowTaskSla</c> section of <c>appsettings.json</c>
/// / environment variables.
///
/// <para>
/// Distinct from <see cref="WorkflowSlaOptions"/> on purpose: the two
/// surfaces (workflow vs task) have independent cadences and may want
/// independent enable flags during rollout. They share the
/// <c>WorkflowSlaStatus</c> string vocabulary but nothing else.
/// </para>
///
/// <para>
/// Threshold semantics (all UTC):
///   • <c>DueSoonThresholdMinutes</c> — minutes before <c>DueAt</c> at
///     which an OnTrack task is promoted to DueSoon (UI labels it
///     "At Risk").
///   • <c>Durations.*Minutes</c> — per-priority defaults applied at
///     task creation. Changing these values does NOT retroactively
///     re-stamp existing tasks.
/// </para>
/// </summary>
public sealed class WorkflowTaskSlaOptions
{
    public const string SectionName = "WorkflowTaskSla";

    /// <summary>Master switch for the evaluator and the DueAt-stamping clock. Defaults to true.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Polling cadence between evaluator ticks.</summary>
    public int PollingIntervalSeconds { get; set; } = 30;

    /// <summary>Max active tasks inspected per tick.</summary>
    public int BatchSize { get; set; } = 200;

    /// <summary>Minutes before <c>DueAt</c> at which a task flips OnTrack → DueSoon (= "At Risk").</summary>
    public int DueSoonThresholdMinutes { get; set; } = 60;

    /// <summary>Per-priority default durations applied at task creation.</summary>
    public WorkflowTaskSlaDurations Durations { get; set; } = new();
}

/// <summary>
/// Per-priority default SLA durations in minutes. Sane defaults map
/// loosely to "answer this within one business day" (Normal) and scale
/// from there.
/// </summary>
public sealed class WorkflowTaskSlaDurations
{
    public int UrgentMinutes { get; set; } = 240;     //  4h
    public int HighMinutes   { get; set; } = 720;     // 12h
    public int NormalMinutes { get; set; } = 1440;    // 24h
    public int LowMinutes    { get; set; } = 4320;    // 72h
}
