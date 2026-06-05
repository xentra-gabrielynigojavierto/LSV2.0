using Microsoft.Extensions.Options;
using PlatformAuditEventService.Configuration;
using PlatformAuditEventService.Entities;
using PlatformAuditEventService.Enums;
using PlatformAuditEventService.Repositories;
using PlatformAuditEventService.Services;
using PlatformAuditEventService.Services.Export;

namespace PlatformAuditEventService.Jobs;

/// <summary>
/// Periodic <see cref="BackgroundService"/> that drives async export job processing.
///
/// The export controller creates <see cref="AuditExportJob"/> records with Status=Pending.
/// This background service polls for Pending and stale Processing jobs, processes them
/// via <see cref="IAuditExportService"/>, and updates the job status accordingly.
///
/// Behaviour:
///   1. Poll interval: Export:ProcessingIntervalSeconds (default: 30s).
///   2. Pick up to Export:MaxConcurrentExports (default: 4) jobs per tick.
///   3. Process each job sequentially in a fresh DI scope (export is already I/O-bound).
///   4. Mark jobs Completed or Failed depending on the outcome.
///   5. Errors are logged but do NOT crash the service.
///
/// Idempotency: if the service crashes mid-export, the job remains Processing.
/// A stale-job recovery window (Export:StalledJobTimeoutMinutes, default: 30) re-queues
/// jobs that have been Processing longer than the threshold back to Pending.
///
/// No-overlap: PeriodicTimer ensures the next tick waits for the previous one to finish.
/// </summary>
public sealed class ExportProcessingJob : BackgroundService
{
    private readonly IServiceScopeFactory            _scopeFactory;
    private readonly ExportOptions                   _opts;
    private readonly ILogger<ExportProcessingJob>    _logger;

    public ExportProcessingJob(
        IServiceScopeFactory         scopeFactory,
        IOptions<ExportOptions>      opts,
        ILogger<ExportProcessingJob> logger)
    {
        _scopeFactory = scopeFactory;
        _opts         = opts.Value;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_opts.Provider.Equals("None", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation(
                "ExportProcessingJob: disabled — Export:Provider=None. " +
                "Set Provider=Local to enable export processing.");
            return;
        }

        var interval = TimeSpan.FromSeconds(
            _opts.ProcessingIntervalSeconds > 0 ? _opts.ProcessingIntervalSeconds : 30);

        _logger.LogInformation(
            "ExportProcessingJob: starting. Poll interval={Interval:g}", interval);

        using var timer = new PeriodicTimer(interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await ProcessBatchAsync(stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var exportRepo = scope.ServiceProvider.GetRequiredService<IAuditExportJobRepository>();
            var exportSvc  = scope.ServiceProvider.GetRequiredService<IAuditExportService>();

            // Recover stalled jobs first
            await RecoverStalledJobsAsync(exportRepo, ct);

            var activeJobs = await exportRepo.ListActiveAsync(ct);

            if (activeJobs.Count == 0) return;

            _logger.LogDebug(
                "ExportProcessingJob: found {Count} active jobs to process.", activeJobs.Count);

            var maxConcurrent = _opts.MaxConcurrentExports > 0 ? _opts.MaxConcurrentExports : 4;
            var toProcess     = activeJobs.Take(maxConcurrent).ToList();

            foreach (var job in toProcess)
            {
                if (ct.IsCancellationRequested) break;
                await ProcessOneAsync(job, exportRepo, exportSvc, ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ExportProcessingJob: unexpected error in batch processing loop.");
        }
    }

    private async Task ProcessOneAsync(
        AuditExportJob       job,
        IAuditExportJobRepository repo,
        IAuditExportService  exportSvc,
        CancellationToken    ct)
    {
        _logger.LogInformation(
            "ExportProcessingJob: processing ExportId={ExportId} Status={Status}",
            job.ExportId, job.Status);

        // Mark as Processing to prevent other instances from picking it up
        job.Status = ExportStatus.Processing;
        await repo.UpdateAsync(job, ct);

        try
        {
            await exportSvc.ProcessJobAsync(job.ExportId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "ExportProcessingJob: failed to process ExportId={ExportId}", job.ExportId);

            job.Status       = ExportStatus.Failed;
            job.ErrorMessage = ex.Message;
            await repo.UpdateAsync(job, ct);
        }
    }

    /// <summary>
    /// Resets jobs that have been stuck in Processing longer than the stall timeout back to Pending
    /// so a fresh worker can pick them up. Protects against crashes mid-export.
    /// </summary>
    private async Task RecoverStalledJobsAsync(
        IAuditExportJobRepository repo,
        CancellationToken         ct)
    {
        var stalledJobs = await repo.ListByStatusAsync(
            [ExportStatus.Processing], page: 1, pageSize: 50, ct: ct);

        var stallTimeout = TimeSpan.FromMinutes(
            _opts.StalledJobTimeoutMinutes > 0 ? _opts.StalledJobTimeoutMinutes : 30);

        var cutoff = DateTimeOffset.UtcNow - stallTimeout;

        foreach (var job in stalledJobs.Items)
        {
            if (job.CreatedAtUtc < cutoff)
            {
                _logger.LogWarning(
                    "ExportProcessingJob: resetting stalled job ExportId={ExportId} " +
                    "CreatedAt={Created:u} back to Pending.",
                    job.ExportId, job.CreatedAtUtc);

                job.Status = ExportStatus.Pending;
                await repo.UpdateAsync(job, ct);
            }
        }
    }
}
