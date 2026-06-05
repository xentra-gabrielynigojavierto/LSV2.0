using Liens.Domain.Entities;

namespace Liens.Application.Interfaces;

/// <summary>
/// LS-LIENS-FLOW-009 — centralised, reusable sync logic for keeping a task's
/// Flow-derived fields aligned with the current Flow workflow state.
///
/// Designed so that FLOW-008 (stage mapping) and FLOW-009 (event-driven sync)
/// both call this service rather than duplicating update logic.
/// </summary>
public interface ITaskFlowSyncService
{
    /// <summary>
    /// Evaluates whether <paramref name="task"/> needs to be updated for
    /// <paramref name="newStepKey"/> and, if so, applies the change and persists it.
    /// Idempotent: returns <see cref="SyncOutcome.NoOp"/> when already aligned.
    /// </summary>
    Task<SyncOutcome> SyncAsync(
        LienTask task,
        string   newStepKey,
        CancellationToken ct = default);
}

/// <summary>Result of a single task sync attempt.</summary>
public enum SyncOutcome
{
    /// <summary>The task was updated and persisted.</summary>
    Synced,

    /// <summary>The task was already aligned — no write was issued.</summary>
    NoOp,
}
