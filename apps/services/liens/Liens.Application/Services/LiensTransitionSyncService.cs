using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Application.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Liens.Application.Services;

/// <summary>
/// TASK-MIG-04 — Background service that periodically pushes all Liens stage transitions
/// to the Task service (write-through catch-up).
///
/// Runs once at startup (after a short delay) and then on a configurable interval.
/// Uses batch-replace semantics: the full active transition set per (TenantId, Product)
/// is sent on each sync. Idempotent — running multiple times is safe.
/// Failures are fully isolated — the Liens DB remains authoritative at all times.
/// </summary>
public sealed class LiensTransitionSyncService : BackgroundService
{
    private const string ProductCode = "SYNQ_LIENS";

    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan SyncInterval = TimeSpan.FromMinutes(60);

    private readonly IServiceScopeFactory          _scopeFactory;
    private readonly ILogger<LiensTransitionSyncService> _logger;

    public LiensTransitionSyncService(
        IServiceScopeFactory               scopeFactory,
        ILogger<LiensTransitionSyncService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("LiensTransitionSyncService started. Initial delay {Delay}.", InitialDelay);

        try { await Task.Delay(InitialDelay, stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunSyncCycleAsync(stoppingToken);

            try { await Task.Delay(SyncInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }

        _logger.LogInformation("LiensTransitionSyncService stopping.");
    }

    private async Task RunSyncCycleAsync(CancellationToken ct)
    {
        _logger.LogInformation("transition_sync_cycle=start");
        int synced = 0, failed = 0;

        try
        {
            using var scope     = _scopeFactory.CreateScope();
            var workflowRepo    = scope.ServiceProvider.GetRequiredService<ILienWorkflowConfigRepository>();
            var taskClient      = scope.ServiceProvider.GetRequiredService<ILiensTaskServiceClient>();

            var configs = await workflowRepo.GetAllConfigsAsync(ct);

            foreach (var config in configs)
            {
                try
                {
                    var activeTransitions = config.Transitions
                        .Where(t => t.IsActive)
                        .ToList();

                    var payload = new TaskServiceTransitionsUpsertRequest
                    {
                        SourceProductCode = ProductCode,
                        Transitions = activeTransitions.Select(t =>
                            new TaskServiceTransitionsUpsertRequest.TransitionEntryDto
                            {
                                FromStageId = t.FromStageId,
                                ToStageId   = t.ToStageId,
                                SortOrder   = t.SortOrder,
                            }).ToList(),
                    };

                    var actorId = config.CreatedByUserId ?? Guid.Empty;
                    await taskClient.UpsertTransitionsFromSourceAsync(config.TenantId, actorId, payload, ct);
                    synced++;
                }
                catch (Exception ex)
                {
                    failed++;
                    _logger.LogWarning(ex,
                        "transition_sync_cycle=config_failed TenantId={TenantId} WorkflowId={Id}",
                        config.TenantId, config.Id);
                }
            }

            _logger.LogInformation(
                "transition_sync_cycle=complete Synced={Synced} Failed={Failed}", synced, failed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "transition_sync_cycle=error");
        }
    }
}
