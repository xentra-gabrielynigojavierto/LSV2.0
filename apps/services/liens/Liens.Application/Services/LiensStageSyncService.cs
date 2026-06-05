using Liens.Application.Interfaces;
using Liens.Application.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Liens.Application.Services;

/// <summary>
/// TASK-MIG-03 — Background service that periodically pushes all Liens stage configs
/// to the Task service (write-through catch-up).
///
/// Runs once at startup (after a short delay) and then on a configurable interval.
/// Failures are fully isolated — the Liens DB remains authoritative at all times.
/// </summary>
public sealed class LiensStageSyncService : BackgroundService
{
    private const string ProductCode = "SYNQ_LIENS";

    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan SyncInterval = TimeSpan.FromMinutes(60);

    private readonly IServiceScopeFactory         _scopeFactory;
    private readonly ILogger<LiensStageSyncService> _logger;

    public LiensStageSyncService(
        IServiceScopeFactory          scopeFactory,
        ILogger<LiensStageSyncService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("LiensStageSyncService started. Initial delay {Delay}.", InitialDelay);

        try { await Task.Delay(InitialDelay, stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunSyncCycleAsync(stoppingToken);

            try { await Task.Delay(SyncInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }

        _logger.LogInformation("LiensStageSyncService stopping.");
    }

    private async Task RunSyncCycleAsync(CancellationToken ct)
    {
        _logger.LogInformation("stage_sync_cycle=start");
        int synced = 0, failed = 0;

        try
        {
            using var scope    = _scopeFactory.CreateScope();
            var workflowRepo   = scope.ServiceProvider.GetRequiredService<ILienWorkflowConfigRepository>();
            var taskClient     = scope.ServiceProvider.GetRequiredService<ILiensTaskServiceClient>();

            var configs = await workflowRepo.GetAllConfigsAsync(ct);

            foreach (var config in configs)
            {
                foreach (var stage in config.Stages)
                {
                    try
                    {
                        var payload = LienWorkflowConfigService.BuildStageUpsertPayload(stage);
                        await taskClient.UpsertStageFromSourceAsync(
                            config.TenantId, config.CreatedByUserId ?? Guid.Empty, payload, ct);
                        synced++;
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        _logger.LogWarning(ex,
                            "stage_sync_cycle=stage_failed StageId={StageId} TenantId={TenantId}",
                            stage.Id, config.TenantId);
                    }
                }
            }

            _logger.LogInformation(
                "stage_sync_cycle=complete Synced={Synced} Failed={Failed}", synced, failed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "stage_sync_cycle=error");
        }
    }
}
