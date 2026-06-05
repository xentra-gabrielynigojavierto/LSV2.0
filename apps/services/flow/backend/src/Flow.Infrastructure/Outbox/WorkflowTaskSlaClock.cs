using Flow.Application.Interfaces;
using Flow.Domain.Common;
using Microsoft.Extensions.Options;

namespace Flow.Infrastructure.Outbox;

/// <summary>
/// LS-FLOW-E10.3 (task slice) — production implementation of
/// <see cref="IWorkflowTaskSlaClock"/>. Picks the per-priority duration
/// from <see cref="WorkflowTaskSlaOptions"/> and adds it to the supplied
/// <c>createdAt</c>. Returns <c>null</c> when the SLA engine is
/// disabled or the priority is unknown — the factory persists null,
/// and the evaluator + UI both skip the row cleanly.
/// </summary>
public sealed class WorkflowTaskSlaClock : IWorkflowTaskSlaClock
{
    private readonly IOptionsMonitor<WorkflowTaskSlaOptions> _options;

    public WorkflowTaskSlaClock(IOptionsMonitor<WorkflowTaskSlaOptions> options)
    {
        _options = options;
    }

    public System.DateTime? ComputeDueAt(System.DateTime createdAt, string priority)
    {
        var opts = _options.CurrentValue;
        if (!opts.Enabled) return null;

        var durations = opts.Durations;
        var minutes = priority switch
        {
            WorkflowTaskPriority.Urgent => durations.UrgentMinutes,
            WorkflowTaskPriority.High   => durations.HighMinutes,
            WorkflowTaskPriority.Normal => durations.NormalMinutes,
            WorkflowTaskPriority.Low    => durations.LowMinutes,
            _                           => 0, // unknown priority → no SLA
        };

        if (minutes <= 0) return null;

        return createdAt.AddMinutes(minutes);
    }
}
