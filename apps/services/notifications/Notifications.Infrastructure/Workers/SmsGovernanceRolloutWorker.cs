using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notifications.Application.Interfaces;
using Notifications.Application.Options;
using Notifications.Domain;
using Notifications.Infrastructure.Data;

namespace Notifications.Infrastructure.Workers;

/// <summary>
/// LS-NOTIF-SMS-022: Optional background worker for progressive rollout stage advancement.
///
/// Disabled by default (SmsGovernanceRollouts:RolloutWorkerEnabled = false).
/// When enabled:
///  - Polls active rollouts every RolloutPollMinutes.
///  - For each active rollout, evaluates the current stage health.
///  - If a threshold breach is detected: pauses or rolls back per AutoPauseOnThresholdBreach
///    and AutoRollbackOnCriticalThresholdBreach options.
///  - If the stage observation window has elapsed and health is acceptable: advances to next stage.
///  - If the final stage completes: completes the rollout.
///
/// Safety guarantees:
///  - Does not block or slow the delivery pipeline.
///  - Does not send SMS.
///  - Does not call external APIs.
///  - Individual rollout processing failures are logged and swallowed.
///  - Batch-capped at MaxRolloutsPerCycle per cycle.
///  - Respects CancellationToken.
///  - No raw phones or credentials in logs.
/// </summary>
public sealed class SmsGovernanceRolloutWorker : BackgroundService
{
    private readonly IServiceScopeFactory                          _scopeFactory;
    private readonly SmsGovernanceRolloutsOptions                  _opts;
    private readonly ILogger<SmsGovernanceRolloutWorker>           _logger;

    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(90);

    public SmsGovernanceRolloutWorker(
        IServiceScopeFactory                                   scopeFactory,
        IOptions<SmsGovernanceRolloutsOptions>                 opts,
        ILogger<SmsGovernanceRolloutWorker>                    logger)
    {
        _scopeFactory = scopeFactory;
        _opts         = opts.Value;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_opts.RolloutWorkerEnabled)
        {
            _logger.LogInformation(
                "SmsGovernanceRolloutWorker: disabled " +
                "(SmsGovernanceRollouts:RolloutWorkerEnabled = false) — not starting");
            return;
        }

        _logger.LogInformation(
            "SmsGovernanceRolloutWorker: starting — poll every {PollMin} min, batch cap {Cap}",
            _opts.RolloutPollMinutes, _opts.MaxRolloutsPerCycle);

        await Task.Delay(StartupDelay, stoppingToken);

        var interval = TimeSpan.FromMinutes(Math.Max(1, _opts.RolloutPollMinutes));

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunCycleAsync(stoppingToken);
            await Task.Delay(interval, stoppingToken);
        }
    }

    private async Task RunCycleAsync(CancellationToken ct)
    {
        try
        {
            using var scope     = _scopeFactory.CreateScope();
            var db              = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
            var rolloutService  = scope.ServiceProvider.GetRequiredService<ISmsGovernanceRolloutService>();
            var evaluator       = scope.ServiceProvider.GetRequiredService<ISmsGovernanceRolloutEvaluator>();

            var activeStates = new[] { RolloutStates.CanaryActive, RolloutStates.StagedRollout };

            var activeRolloutIds = await db.SmsGovernanceRolloutPlans
                .AsNoTracking()
                .Where(p => activeStates.Contains(p.RolloutState))
                .OrderBy(p => p.StartedAt)
                .Take(_opts.MaxRolloutsPerCycle)
                .Select(p => p.Id)
                .ToListAsync(ct);

            if (activeRolloutIds.Count == 0)
            {
                _logger.LogDebug("SmsGovernanceRolloutWorker: no active rollouts to process");
                return;
            }

            _logger.LogInformation(
                "SmsGovernanceRolloutWorker: processing {Count} active rollout(s)", activeRolloutIds.Count);

            foreach (var rolloutId in activeRolloutIds)
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    await ProcessRolloutAsync(rolloutId, rolloutService, evaluator, db, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "SmsGovernanceRolloutWorker: exception processing rollout {Id}", rolloutId);
                }
            }
        }
        catch (OperationCanceledException) { /* host shutting down */ }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SmsGovernanceRolloutWorker: cycle exception");
        }
    }

    private async Task ProcessRolloutAsync(
        Guid                          rolloutId,
        ISmsGovernanceRolloutService  rolloutService,
        ISmsGovernanceRolloutEvaluator evaluator,
        NotificationsDbContext         db,
        CancellationToken              ct)
    {
        // Find the active stage
        var activeStage = await db.SmsGovernanceRolloutStages
            .AsNoTracking()
            .FirstOrDefaultAsync(
                s => s.RolloutPlanId == rolloutId && s.StageState == RolloutStageStates.Active, ct);

        if (activeStage is null)
        {
            _logger.LogDebug("SmsGovernanceRolloutWorker: rollout {Id} has no active stage", rolloutId);
            return;
        }

        // Evaluate health
        var health = await evaluator.EvaluateStageHealthAsync(rolloutId, activeStage.Id, ct);

        if (!health.IsHealthy && !health.InsufficientData)
        {
            _logger.LogWarning(
                "SmsGovernanceRolloutWorker: rollout {Id} stage {StageNum} threshold breach — " +
                "action={Action} reason={Reason}",
                rolloutId, activeStage.StageNumber, health.RecommendedAction, health.BreachReason);

            // Record threshold exceeded event
            db.SmsGovernanceRolloutAuditEvents.Add(new SmsGovernanceRolloutAuditEvent
            {
                Id            = Guid.NewGuid(),
                RolloutPlanId = rolloutId,
                StageId       = activeStage.Id,
                EventType     = RolloutAuditEventTypes.ThresholdExceeded,
                Actor         = "system:rollout-worker",
                Reason        = health.BreachReason,
                CreatedAt     = DateTime.UtcNow,
            });
            await db.SaveChangesAsync(ct);

            if (health.RecommendedAction == "rollback" && _opts.AutoRollbackOnCriticalThresholdBreach)
            {
                var result = await rolloutService.RollbackRolloutAsync(
                    rolloutId, "system:rollout-worker",
                    $"Auto-rollback: {health.BreachReason}", ct);

                _logger.LogWarning(
                    "SmsGovernanceRolloutWorker: rollout {Id} auto-rolled-back: {Success}",
                    rolloutId, result.Success);
            }
            else if (_opts.AutoPauseOnThresholdBreach)
            {
                var result = await rolloutService.PauseRolloutAsync(
                    rolloutId, "system:rollout-worker",
                    $"Auto-pause: {health.BreachReason}", ct);

                _logger.LogWarning(
                    "SmsGovernanceRolloutWorker: rollout {Id} auto-paused: {Success}",
                    rolloutId, result.Success);
            }

            return;
        }

        // Check if observation window elapsed
        if (activeStage.DurationMinutes.HasValue && activeStage.StartedAt.HasValue)
        {
            var elapsed = DateTime.UtcNow - activeStage.StartedAt.Value;
            if (elapsed.TotalMinutes < activeStage.DurationMinutes.Value)
            {
                _logger.LogDebug(
                    "SmsGovernanceRolloutWorker: rollout {Id} stage {Num} observation window " +
                    "not elapsed ({Remaining} min remaining)",
                    rolloutId, activeStage.StageNumber,
                    activeStage.DurationMinutes.Value - (int)elapsed.TotalMinutes);
                return;
            }
        }
        else if (activeStage.DurationMinutes is null)
        {
            // No duration = manual progression only
            _logger.LogDebug(
                "SmsGovernanceRolloutWorker: rollout {Id} stage {Num} requires manual advancement",
                rolloutId, activeStage.StageNumber);
            return;
        }

        // Advance to next stage
        var advanceResult = await rolloutService.AdvanceStageAsync(rolloutId, "system:rollout-worker", ct);
        if (advanceResult.Success)
            _logger.LogInformation(
                "SmsGovernanceRolloutWorker: rollout {Id} stage advanced successfully", rolloutId);
        else
            _logger.LogWarning(
                "SmsGovernanceRolloutWorker: rollout {Id} stage advance failed: {Msg}",
                rolloutId, advanceResult.ErrorMessage);
    }
}
