using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Monitoring.Application.Scheduling;
using Monitoring.Domain.Monitoring;
using Monitoring.Infrastructure.Persistence;

namespace Monitoring.Infrastructure.Scheduling;

/// <summary>
/// Real <see cref="IMonitoringCycleExecutor"/> that drives the monitoring
/// pipeline from the persisted registry. On each cycle it:
/// <list type="number">
///   <item>loads enabled <c>MonitoredEntity</c> records via
///     <see cref="MonitoringDbContext"/> (no-tracking, server-side filtered)</item>
///   <item>iterates them in stable order (<c>Name</c> ASC, then
///     <c>CreatedAtUtc</c> ASC, then <c>Id</c> ASC)</item>
///   <item>invokes <see cref="IMonitoredEntityExecutor"/> for each entity
///     and collects the returned <see cref="CheckResult"/></item>
///   <item>isolates per-entity failures so one bad entity never blocks the
///     rest of the cycle; unexpected exceptions are translated into an
///     <see cref="CheckOutcome.UnexpectedFailure"/> result so the
///     aggregation list always has one row per loaded entity</item>
///   <item>emits a single cycle summary log line with totals and a
///     per-outcome breakdown</item>
/// </list>
///
/// <para>Lives in Infrastructure because it depends on EF Core and the
/// <see cref="MonitoringDbContext"/>. The hosted scheduler in
/// <see cref="MonitoringSchedulerHostedService"/> stays generic — it knows
/// only about <see cref="IMonitoringCycleExecutor"/>.</para>
///
/// <para><b>Cycle-level failure isolation</b>: if the DB load throws
/// (DB unavailable, etc.), the exception is allowed to escape so the
/// hosted service's existing per-cycle catch logs it once and continues
/// with the next interval. Wrapping the load here would double-log without
/// changing behavior.</para>
///
/// <para><b>Aggregation</b>: results are kept only in memory for the
/// duration of the current cycle and discarded after the summary line
/// is logged. Persistence and history are deliberate later features.</para>
/// </summary>
public sealed class MonitoredEntityRegistryCycleExecutor : IMonitoringCycleExecutor
{
    private readonly MonitoringDbContext _db;
    private readonly IMonitoredEntityExecutor _entityExecutor;
    private readonly ICheckResultWriter _resultWriter;
    private readonly IEntityStatusWriter _statusWriter;
    private readonly IAlertRuleEngine _alertRuleEngine;
    private readonly ILogger<MonitoredEntityRegistryCycleExecutor> _logger;

    public MonitoredEntityRegistryCycleExecutor(
        MonitoringDbContext db,
        IMonitoredEntityExecutor entityExecutor,
        ICheckResultWriter resultWriter,
        IEntityStatusWriter statusWriter,
        IAlertRuleEngine alertRuleEngine,
        ILogger<MonitoredEntityRegistryCycleExecutor> logger)
    {
        _db = db;
        _entityExecutor = entityExecutor;
        _resultWriter = resultWriter;
        _statusWriter = statusWriter;
        _alertRuleEngine = alertRuleEngine;
        _logger = logger;
    }

    public async Task ExecuteCycleAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        // Cycle-level failure isolation: any exception here escapes and is
        // logged once by the hosted service's per-cycle catch.
        var entities = await _db.MonitoredEntities
            .AsNoTracking()
            .Where(e => e.IsEnabled)
            .OrderBy(e => e.Name)
            .ThenBy(e => e.CreatedAtUtc)
            .ThenBy(e => e.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Cycle loaded {EnabledEntityCount} enabled monitored entities.",
            entities.Count);

        // Pre-sized: one CheckResult per loaded entity, always.
        var results = new List<CheckResult>(entities.Count);

        foreach (var entity in entities)
        {
            // Surface shutdown promptly between entities, even if the
            // per-entity executor itself doesn't observe the token in time.
            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogDebug(
                "Executing per-entity hook for entity {EntityId} ({EntityName}).",
                entity.Id, entity.Name);

            CheckResult result;
            try
            {
                result = await _entityExecutor
                    .ExecuteAsync(entity, cancellationToken)
                    .ConfigureAwait(false);

                // Defensive contract enforcement: the interface forbids
                // returning null. A null here is a bug in an implementation,
                // not a per-entity outcome — translate it to UnexpectedFailure
                // rather than letting a NullReferenceException escape.
                if (result is null)
                {
                    _logger.LogError(
                        "Per-entity executor returned null for entity {EntityId} ({EntityName}). " +
                        "This violates the IMonitoredEntityExecutor contract; treating as UnexpectedFailure.",
                        entity.Id, entity.Name);
                    result = new CheckResult(
                        EntityId: entity.Id,
                        EntityName: entity.Name,
                        MonitoringType: entity.MonitoringType,
                        Target: entity.Target,
                        Succeeded: false,
                        Outcome: CheckOutcome.UnexpectedFailure,
                        StatusCode: null,
                        ElapsedMs: 0,
                        CheckedAtUtc: DateTime.UtcNow,
                        Message: "executor returned null (contract violation)",
                        ErrorType: "NullResult");
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Host shutdown — propagate to let the hosted service log
                // the cancellation cleanly rather than counting it as a
                // per-entity failure.
                throw;
            }
            catch (Exception ex)
            {
                // Translate unexpected exceptions into a structured
                // UnexpectedFailure result so aggregation always has a row
                // per loaded entity. The cycle continues with the next
                // entity — exactly the same isolation guarantee as before.
                _logger.LogError(
                    ex,
                    "Per-entity execution failed unexpectedly for entity {EntityId} ({EntityName}). " +
                    "The cycle will continue with the next entity.",
                    entity.Id, entity.Name);

                result = new CheckResult(
                    EntityId: entity.Id,
                    EntityName: entity.Name,
                    MonitoringType: entity.MonitoringType,
                    Target: entity.Target,
                    Succeeded: false,
                    Outcome: CheckOutcome.UnexpectedFailure,
                    StatusCode: null,
                    ElapsedMs: 0,
                    CheckedAtUtc: DateTime.UtcNow,
                    Message: "unexpected executor failure",
                    ErrorType: ex.GetType().Name);
            }

            results.Add(result);

            // Persist this result as a durable history row. Failures are
            // isolated per-row: the in-memory aggregation is unaffected,
            // the cycle continues, and the host stays up. We log at
            // Error level (not Warning) because losing audit history is
            // operationally significant even though it is not fatal.
            try
            {
                await _resultWriter
                    .WriteAsync(result, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Host shutdown mid-write — propagate so the hosted
                // service logs cancellation cleanly.
                throw;
            }
            catch (Exception persistEx)
            {
                _logger.LogError(
                    persistEx,
                    "Failed to persist check result for entity {EntityId} ({EntityName}) " +
                    "with outcome {Outcome}. The cycle will continue with the next entity.",
                    entity.Id, entity.Name, result.Outcome);
            }

            // Evaluate alert rules BEFORE the current-status upsert.
            // The engine reads the prior status from entity_current_status,
            // so it must run before that row is overwritten. Wrapped in
            // its own try/catch so a bad alert evaluation isolates
            // per-entity and never blocks the status upsert that
            // immediately follows or the rest of the cycle. History +
            // current-status are independent of this path.
            try
            {
                await _alertRuleEngine
                    .EvaluateAsync(entity, result, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception alertEx)
            {
                _logger.LogError(
                    alertEx,
                    "Alert rule evaluation failed for entity {EntityId} ({EntityName}) " +
                    "with outcome {Outcome}. The cycle will continue with the next entity; " +
                    "current-status upsert will still run.",
                    entity.Id, entity.Name, result.Outcome);
            }

            // Evaluate current status from the result and upsert the
            // current-state projection. Wrapped in its own try/catch so
            // a status-row failure isolates per-entity and never blocks
            // the rest of the cycle. The history row above is the source
            // of truth and was already written.
            var evaluatedStatus = StatusEvaluator.EvaluateFromOutcome(result.Outcome);
            try
            {
                await _statusWriter
                    .UpsertFromResultAsync(result, cancellationToken)
                    .ConfigureAwait(false);

                _logger.LogDebug(
                    "Current status for entity {EntityId} ({EntityName}) " +
                    "evaluated as {EntityStatus} from outcome {Outcome}.",
                    entity.Id, entity.Name, evaluatedStatus, result.Outcome);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception statusEx)
            {
                _logger.LogError(
                    statusEx,
                    "Failed to upsert current status for entity {EntityId} ({EntityName}) " +
                    "(evaluated {EntityStatus} from outcome {Outcome}). " +
                    "The cycle will continue with the next entity.",
                    entity.Id, entity.Name, evaluatedStatus, result.Outcome);
            }
        }

        stopwatch.Stop();

        var succeeded = results.Count(r => r.Outcome == CheckOutcome.Success);
        var skipped = results.Count(r => r.Outcome == CheckOutcome.Skipped);
        var failed = results.Count - succeeded - skipped;

        _logger.LogInformation(
            "Cycle processed {Loaded} enabled entities in {ElapsedMs} ms. " +
            "Outcomes: {Succeeded} succeeded, {Failed} failed, {Skipped} skipped. " +
            "Breakdown: Success={Success}, NonSuccessStatusCode={NonSuccess}, " +
            "Timeout={Timeout}, InvalidTarget={InvalidTarget}, " +
            "NetworkFailure={NetworkFailure}, Skipped={SkippedBreakdown}, " +
            "UnexpectedFailure={UnexpectedFailure}.",
            entities.Count, stopwatch.ElapsedMilliseconds,
            succeeded, failed, skipped,
            succeeded,
            results.Count(r => r.Outcome == CheckOutcome.NonSuccessStatusCode),
            results.Count(r => r.Outcome == CheckOutcome.Timeout),
            results.Count(r => r.Outcome == CheckOutcome.InvalidTarget),
            results.Count(r => r.Outcome == CheckOutcome.NetworkFailure),
            skipped,
            results.Count(r => r.Outcome == CheckOutcome.UnexpectedFailure));
    }
}
