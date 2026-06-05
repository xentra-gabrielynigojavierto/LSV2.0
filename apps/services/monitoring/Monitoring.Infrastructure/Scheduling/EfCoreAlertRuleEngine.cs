using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Monitoring.Application.Scheduling;
using Monitoring.Domain.Monitoring;
using Monitoring.Infrastructure.Persistence;

namespace Monitoring.Infrastructure.Scheduling;

/// <summary>
/// EF Core <see cref="IAlertRuleEngine"/> implementing the basic
/// transition-into-<see cref="EntityStatus.Down"/> rule with
/// active-row deduplication, plus minimal Down → Up/Unknown
/// resolution.
///
/// <para><b>Ordering inside the cycle</b>: this engine is called after
/// the history-row write but before the current-status upsert, so the
/// row in <c>entity_current_status</c> still reflects the <i>prior</i>
/// status. A <c>null</c> prior status means the entity has never
/// executed before — that case is treated as
/// <c>Unknown → newStatus</c>, so a first-time Down does fire an
/// alert (documented "no prior row" semantic).</para>
///
/// <para><b>Dedup</b>: read-then-insert filtered on
/// <c>(MonitoredEntityId, AlertType, IsActive=true)</c>. MySQL 8 has no
/// filtered unique indexes, and an unconditional unique key would
/// block legitimate inactive history rows for the same entity once
/// resolution lands. The scheduler is single-writer per cycle and
/// runs as a single instance, so the read-then-insert window is
/// closed by the surrounding cycle ordering.</para>
///
/// <para><b>Per-cycle DbContext</b>: shares the same scoped
/// <see cref="MonitoringDbContext"/> as the history and status writers.
/// Tracked entries created/modified here are detached in the
/// <c>finally</c> block to keep the context lean across many entities.</para>
/// </summary>
public sealed class EfCoreAlertRuleEngine : IAlertRuleEngine
{
    private readonly MonitoringDbContext _db;
    private readonly ILogger<EfCoreAlertRuleEngine> _logger;

    public EfCoreAlertRuleEngine(
        MonitoringDbContext db,
        ILogger<EfCoreAlertRuleEngine> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task EvaluateAsync(
        MonitoredEntity entity,
        CheckResult result,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(result);

        // Read prior current status (no-tracking — we never modify this
        // row; the status writer that runs immediately after us owns it).
        var priorRow = await _db.EntityCurrentStatuses
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.MonitoredEntityId == entity.Id, cancellationToken)
            .ConfigureAwait(false);

        EntityStatus? previousStatus = priorRow?.CurrentStatus;
        var newStatus = StatusEvaluator.EvaluateFromOutcome(result.Outcome);

        // Effective prior for rule comparisons: a missing row means the
        // entity has never executed, which is semantically Unknown.
        var effectivePrior = previousStatus ?? EntityStatus.Unknown;

        // Rule 1: transition into Down — fire (with dedup).
        if (effectivePrior != EntityStatus.Down && newStatus == EntityStatus.Down)
        {
            await FireOrSuppressDownAsync(entity, effectivePrior, newStatus, result, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        // Rule 2: still Down — explicit dedup branch (debug-visible).
        if (effectivePrior == EntityStatus.Down && newStatus == EntityStatus.Down)
        {
            _logger.LogDebug(
                "Alert dedup: entity {EntityId} ({EntityName}) is still Down; " +
                "not creating a duplicate {AlertType} alert.",
                entity.Id, entity.Name, AlertType.StatusDown);
            return;
        }

        // Rule 3: recovery — resolve any active StatusDown alert(s).
        if (effectivePrior == EntityStatus.Down &&
            (newStatus == EntityStatus.Up || newStatus == EntityStatus.Unknown))
        {
            await ResolveActiveDownAlertsAsync(entity, newStatus, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        // Anything else is a no-op for this feature.
    }

    private async Task FireOrSuppressDownAsync(
        MonitoredEntity entity,
        EntityStatus previousStatus,
        EntityStatus newStatus,
        CheckResult result,
        CancellationToken cancellationToken)
    {
        var hasActive = await _db.MonitoringAlerts
            .AsNoTracking()
            .AnyAsync(
                a => a.MonitoredEntityId == entity.Id
                  && a.AlertType == AlertType.StatusDown
                  && a.IsActive,
                cancellationToken)
            .ConfigureAwait(false);

        if (hasActive)
        {
            _logger.LogDebug(
                "Alert dedup: active {AlertType} alert already exists for entity " +
                "{EntityId} ({EntityName}); not creating a duplicate.",
                AlertType.StatusDown, entity.Id, entity.Name);
            return;
        }

        var alert = new MonitoringAlert(
            id: Guid.NewGuid(),
            monitoredEntityId: entity.Id,
            entityName: entity.Name,
            scope: entity.Scope,
            impactLevel: entity.ImpactLevel,
            previousStatus: previousStatus,
            currentStatus: newStatus,
            alertType: AlertType.StatusDown,
            triggeredAtUtc: DateTime.UtcNow,
            message: BuildFireMessage(previousStatus, newStatus, result));

        await _db.MonitoringAlerts.AddAsync(alert, cancellationToken).ConfigureAwait(false);
        try
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Alert created: {AlertType} for entity {EntityId} ({EntityName}) — " +
                "transitioned {PreviousStatus} -> {CurrentStatus} (outcome {Outcome}). " +
                "AlertId={AlertId}, ImpactLevel={ImpactLevel}, Scope={Scope}.",
                AlertType.StatusDown, entity.Id, entity.Name,
                previousStatus, newStatus, result.Outcome,
                alert.Id, entity.ImpactLevel, entity.Scope);
        }
        finally
        {
            _db.Entry(alert).State = EntityState.Detached;
        }
    }

    private async Task ResolveActiveDownAlertsAsync(
        MonitoredEntity entity,
        EntityStatus newStatus,
        CancellationToken cancellationToken)
    {
        // Tracked load — we mutate these.
        var active = await _db.MonitoringAlerts
            .Where(a => a.MonitoredEntityId == entity.Id
                     && a.AlertType == AlertType.StatusDown
                     && a.IsActive)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (active.Count == 0)
        {
            // Nothing active — nothing to resolve. This is normal when an
            // entity recovers without ever having had a Down alert (e.g.
            // it was Down before alerting was enabled).
            return;
        }

        var resolvedAt = DateTime.UtcNow;
        foreach (var a in active)
        {
            a.Resolve(resolvedAt);
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            foreach (var a in active)
            {
                _logger.LogInformation(
                    "Alert resolved: {AlertType} for entity {EntityId} ({EntityName}) — " +
                    "now {CurrentStatus}. AlertId={AlertId}, " +
                    "TriggeredAtUtc={TriggeredAtUtc:o}, ResolvedAtUtc={ResolvedAtUtc:o}.",
                    AlertType.StatusDown, entity.Id, entity.Name,
                    newStatus, a.Id, a.TriggeredAtUtc, a.ResolvedAtUtc);
            }
        }
        finally
        {
            foreach (var a in active)
            {
                _db.Entry(a).State = EntityState.Detached;
            }
        }
    }

    private static string BuildFireMessage(
        EntityStatus previousStatus,
        EntityStatus newStatus,
        CheckResult result)
    {
        // Operator-facing summary. Keep short; no targets / payloads.
        var detail = string.IsNullOrWhiteSpace(result.Message)
            ? result.Outcome.ToString()
            : result.Message;

        return $"Status transitioned {previousStatus} -> {newStatus} ({detail}).";
    }
}
