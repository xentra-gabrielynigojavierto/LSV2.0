using Liens.Application.Events;

namespace Liens.Application.Interfaces;

/// <summary>
/// LS-LIENS-FLOW-009 — dispatches validated Flow events into the appropriate sync path.
/// Implementations must be idempotent and must not throw for no-op or unknown-instance cases.
/// </summary>
public interface IFlowEventHandler
{
    /// <summary>
    /// Processes a Flow <c>workflow.step.changed</c> event.
    /// Finds all tasks linked to the workflow instance, syncs their step key via
    /// <see cref="ITaskFlowSyncService"/>, and emits audit events for actual changes.
    /// </summary>
    Task<FlowEventHandleResult> HandleStepChangedAsync(
        FlowStepChangedEvent evt,
        CancellationToken ct = default);
}
