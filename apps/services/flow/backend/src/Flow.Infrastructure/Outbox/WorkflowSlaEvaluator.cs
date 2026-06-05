using Flow.Application.Engines.WorkflowEngine;
using Flow.Application.Outbox;
using Flow.Domain.Common;
using Flow.Domain.Entities;
using Flow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Flow.Infrastructure.Outbox;

/// <summary>
/// LS-FLOW-E10.3 — background worker that evaluates workflow-level SLA /
/// timer state and emits durable outbox events on transition.
///
/// <para>
/// Lifecycle (per tick):
///   1. Open a fresh DI scope (so <see cref="FlowDbContext"/> is fresh
///      and the request-scoped tenant provider resolves null — see
///      entity configuration: workflow_instances has a tenant query
///      filter that we explicitly opt out of via IgnoreQueryFilters).
///   2. Pull a bounded batch of Active instances with <c>DueAt</c>
///      assigned, ordered by oldest <c>LastSlaEvaluatedAt</c> first so
///      the workload is fair under continuous churn.
///   3. For each row, compute the <see cref="WorkflowSlaStatus"/> from
///      <c>(now, DueAt, OverdueSince, EscalationLevel, thresholds)</c>.
///      If the computed value differs from persisted, mutate the row
///      and enqueue exactly one outbox event in the SAME SaveChanges
///      transaction (atomic with the state change).
///   4. Stamp <c>LastSlaEvaluatedAt = now</c> on every visited row,
///      whether or not its status changed, so the ordering window
///      naturally rotates.
/// </para>
///
/// <para>
/// Idempotency model: a re-evaluation of an unchanged row is a no-op
/// from the outbox's perspective (the gate is "persisted != computed"
/// or "EscalationLevel changed"). This phase is single-replica by
/// design; multi-replica would require a SKIP LOCKED claim phase like
/// <see cref="OutboxProcessor"/>.
/// </para>
///
/// <para>
/// Tenant assignment on the outbox row: this worker has no tenant
/// context, so it must populate <c>OutboxMessage.TenantId</c> from the
/// owning <see cref="WorkflowInstance.TenantId"/> BEFORE
/// SaveChangesAsync — the FlowDbContext save hook will not invent one
/// for a background save and would otherwise throw.
/// </para>
/// </summary>
public sealed class WorkflowSlaEvaluator : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IOptionsMonitor<WorkflowSlaOptions> _options;
    private readonly ILogger<WorkflowSlaEvaluator> _log;

    public WorkflowSlaEvaluator(
        IServiceScopeFactory scopes,
        IOptionsMonitor<WorkflowSlaOptions> options,
        ILogger<WorkflowSlaEvaluator> log)
    {
        _scopes = scopes;
        _options = options;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = _options.CurrentValue;
        _log.LogInformation(
            "WorkflowSlaEvaluator starting. Enabled={Enabled} pollSeconds={Poll} batchSize={Batch} dueSoonMinutes={DueSoon} escalationMinutes={Escalation}",
            opts.Enabled, opts.PollingIntervalSeconds, opts.BatchSize,
            opts.DueSoonThresholdMinutes, opts.EscalationThresholdMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "WorkflowSlaEvaluator tick threw — sleeping then retrying.");
            }

            try
            {
                var delay = TimeSpan.FromSeconds(Math.Max(1, _options.CurrentValue.PollingIntervalSeconds));
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _log.LogInformation("WorkflowSlaEvaluator stopped.");
    }

    private async Task TickAsync(CancellationToken ct)
    {
        var opts = _options.CurrentValue;
        if (!opts.Enabled) return;

        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FlowDbContext>();

        var now = DateTime.UtcNow;
        var dueSoonHorizon = now.AddMinutes(Math.Max(0, opts.DueSoonThresholdMinutes));

        // Pull active instances with a DueAt that is either:
        //   • inside the dueSoon horizon (might transition OnTrack → DueSoon), or
        //   • already past (might transition further to Overdue / Escalated), or
        //   • flagged with a non-OnTrack status (might need re-evaluation).
        // IgnoreQueryFilters() because this worker has no tenant context.
        var batch = await db.WorkflowInstances
            .IgnoreQueryFilters()
            .Where(w => w.Status == WorkflowEngine.StatusActive
                        && w.DueAt != null
                        && (w.DueAt <= dueSoonHorizon || w.SlaStatus != WorkflowSlaStatus.OnTrack))
            .OrderBy(w => w.LastSlaEvaluatedAt ?? DateTime.MinValue)
            .ThenBy(w => w.DueAt)
            .Take(Math.Max(1, opts.BatchSize))
            .ToListAsync(ct);

        if (batch.Count == 0) return;

        var transitions = 0;
        foreach (var instance in batch)
        {
            if (ct.IsCancellationRequested) break;
            if (TryEvaluateAndStage(instance, now, opts, scope.ServiceProvider, db))
            {
                transitions++;
            }
            instance.LastSlaEvaluatedAt = now;
        }

        await db.SaveChangesAsync(ct);

        if (transitions > 0)
        {
            _log.LogInformation(
                "WorkflowSlaEvaluator tick: visited={Visited} transitioned={Transitioned}",
                batch.Count, transitions);
        }
    }

    /// <summary>
    /// Compute the new SLA status / escalation level for one instance.
    /// Mutates <paramref name="instance"/> and stages an outbox row
    /// only when state changes. Returns true when an outbox row was
    /// staged.
    /// </summary>
    private static bool TryEvaluateAndStage(
        WorkflowInstance instance,
        DateTime now,
        WorkflowSlaOptions opts,
        IServiceProvider sp,
        FlowDbContext db)
    {
        if (instance.DueAt is not DateTime dueAt) return false;

        var dueSoonAt = dueAt.AddMinutes(-Math.Max(0, opts.DueSoonThresholdMinutes));
        var (computedStatus, computedOverdueSince, computedEscalationLevel) = Compute(
            now, dueAt, dueSoonAt,
            instance.OverdueSince, instance.EscalationLevel,
            opts.EscalationThresholdMinutes);

        var statusChanged     = !string.Equals(computedStatus, instance.SlaStatus, StringComparison.Ordinal);
        var escalationChanged = computedEscalationLevel != instance.EscalationLevel;

        // Always sync the supporting fields so the persisted row is the
        // source of truth even on visits that do not emit an event
        // (e.g. OverdueSince landing on a row that was already Overdue).
        instance.OverdueSince    = computedOverdueSince;
        instance.EscalationLevel = computedEscalationLevel;

        if (!statusChanged && !escalationChanged) return false;

        var previousStatus = instance.SlaStatus;
        instance.SlaStatus = computedStatus;

        // Choose the event type that matches the *new* status. The
        // dispatcher is responsible for the audit/notification fan-out.
        var eventType = computedStatus switch
        {
            WorkflowSlaStatus.DueSoon   => OutboxEventTypes.WorkflowSlaDueSoon,
            WorkflowSlaStatus.Overdue   => OutboxEventTypes.WorkflowSlaOverdue,
            WorkflowSlaStatus.Escalated => OutboxEventTypes.WorkflowSlaEscalated,
            _                           => null,  // OnTrack — no event (defensive; we never compute back to OnTrack here)
        };
        if (eventType is null) return false;

        long? overdueSeconds = computedOverdueSince is DateTime since
            ? (long)Math.Max(0, (now - since).TotalSeconds)
            : null;

        var payload = new WorkflowSlaTransitionPayload(
            WorkflowInstanceId:     instance.Id,
            ProductKey:             instance.ProductKey,
            CurrentStepKey:         instance.CurrentStepKey,
            DueAt:                  dueAt,
            PreviousSlaStatus:      previousStatus,
            NewSlaStatus:           computedStatus,
            EscalationLevel:        computedEscalationLevel,
            OverdueDurationSeconds: overdueSeconds,
            AssignedToUserId:       instance.AssignedToUserId,
            OccurredAtUtc:          now);

        // Stage the outbox row directly via DbContext (not OutboxWriter),
        // so we can populate TenantId from the instance — the writer
        // relies on the tenant save-hook which is intentionally
        // unavailable in background scopes.
        var outbox = new OutboxMessage
        {
            Id                 = Guid.NewGuid(),
            EventType          = eventType,
            WorkflowInstanceId = instance.Id,
            TenantId           = instance.TenantId,
            PayloadJson        = System.Text.Json.JsonSerializer.Serialize(payload, payload.GetType(),
                                    new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)),
            Status             = OutboxStatus.Pending,
            AttemptCount       = 0,
            NextAttemptAt      = now,
        };
        db.OutboxMessages.Add(outbox);

        return true;
    }

    /// <summary>
    /// Pure decision function: given the current clock and persisted
    /// SLA fields, returns what the new SlaStatus / OverdueSince /
    /// EscalationLevel SHOULD be. Kept pure so it can be unit-tested.
    /// </summary>
    internal static (string status, DateTime? overdueSince, int escalationLevel) Compute(
        DateTime now,
        DateTime dueAt,
        DateTime dueSoonAt,
        DateTime? currentOverdueSince,
        int currentEscalationLevel,
        int escalationThresholdMinutes)
    {
        if (now > dueAt)
        {
            // Persist the moment we first observed overdue so escalation
            // duration is measured from the actual breach, not from the
            // evaluator's current pickup time.
            var overdueSince = currentOverdueSince ?? dueAt;
            var overdueFor   = now - overdueSince;

            if (overdueFor.TotalMinutes >= Math.Max(0, escalationThresholdMinutes) && currentEscalationLevel < 1)
            {
                return (WorkflowSlaStatus.Escalated, overdueSince, 1);
            }

            // If already escalated and still overdue, hold escalated
            // status (no second escalation level this phase).
            if (currentEscalationLevel >= 1)
            {
                return (WorkflowSlaStatus.Escalated, overdueSince, currentEscalationLevel);
            }

            return (WorkflowSlaStatus.Overdue, overdueSince, currentEscalationLevel);
        }

        if (now >= dueSoonAt)
        {
            // Within dueSoon window but not yet breached. Clear any
            // stale OverdueSince (defensive — should not happen since
            // we only set it on overdue, but a clock skew or operator
            // mutation could leave it dangling).
            return (WorkflowSlaStatus.DueSoon, null, currentEscalationLevel);
        }

        // Comfortably ahead of the deadline.
        return (WorkflowSlaStatus.OnTrack, null, currentEscalationLevel);
    }
}
