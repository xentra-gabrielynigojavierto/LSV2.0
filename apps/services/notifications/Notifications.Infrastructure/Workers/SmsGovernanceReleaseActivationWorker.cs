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
/// LS-NOTIF-SMS-021: Optional background worker that activates scheduled governance releases.
///
/// Disabled by default (SmsGovernanceReleaseManagement:ScheduledActivationWorkerEnabled = false).
/// When enabled, polls every ScheduledActivationPollMinutes and activates all scheduled releases
/// whose ScheduledActivationAt is in the past.
///
/// LS-NOTIF-SMS-021-HARDENING:
/// - Respects NextActivationRetryAt backoff window — releases within a backoff window are skipped.
/// - Uses MaxScheduledReleasesPerCycle instead of hard-coded 10.
///
/// Safety guarantees:
/// - Does not block or slow the delivery pipeline.
/// - Does not send SMS.
/// - Does not call external APIs.
/// - Individual activation failures are logged and swallowed — the cycle continues.
/// - Batch-capped at MaxScheduledReleasesPerCycle releases per cycle to bound execution time.
/// - Respects cancellation.
/// </summary>
public sealed class SmsGovernanceReleaseActivationWorker : BackgroundService
{
    private readonly IServiceScopeFactory                              _scopeFactory;
    private readonly SmsGovernanceReleaseManagementOptions             _opts;
    private readonly ILogger<SmsGovernanceReleaseActivationWorker>     _logger;

    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(60);

    public SmsGovernanceReleaseActivationWorker(
        IServiceScopeFactory                                   scopeFactory,
        IOptions<SmsGovernanceReleaseManagementOptions>        opts,
        ILogger<SmsGovernanceReleaseActivationWorker>          logger)
    {
        _scopeFactory = scopeFactory;
        _opts         = opts.Value;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_opts.ScheduledActivationWorkerEnabled)
        {
            _logger.LogInformation(
                "SmsGovernanceReleaseActivationWorker: disabled " +
                "(SmsGovernanceReleaseManagement:ScheduledActivationWorkerEnabled = false) — not starting");
            return;
        }

        _logger.LogInformation(
            "SmsGovernanceReleaseActivationWorker: starting — poll every {PollMin} min, " +
            "batch cap {BatchCap}",
            _opts.ScheduledActivationPollMinutes, _opts.MaxScheduledReleasesPerCycle);

        await Task.Delay(StartupDelay, stoppingToken);

        var interval = TimeSpan.FromMinutes(Math.Max(1, _opts.ScheduledActivationPollMinutes));

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
            using var scope      = _scopeFactory.CreateScope();
            var db               = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
            var releaseService   = scope.ServiceProvider.GetRequiredService<ISmsGovernanceReleaseService>();

            var now  = DateTime.UtcNow;

            // ── LS-NOTIF-SMS-021-HARDENING: filter by NextActivationRetryAt ───
            // Include packages where:
            //   - state is scheduled AND ScheduledActivationAt is due
            //   - AND (NextActivationRetryAt is null OR NextActivationRetryAt <= now)
            // This skips releases that are within their backoff window.
            var due  = await db.SmsGovernanceReleasePackages
                .AsNoTracking()
                .Where(p => p.ReleaseState          == ReleaseStates.Scheduled
                         && p.ScheduledActivationAt != null
                         && p.ScheduledActivationAt <= now
                         && (p.NextActivationRetryAt == null || p.NextActivationRetryAt <= now))
                .OrderBy(p => p.ScheduledActivationAt)
                .Take(_opts.MaxScheduledReleasesPerCycle)
                .Select(p => p.Id)
                .ToListAsync(ct);

            if (due.Count == 0)
            {
                _logger.LogDebug("SmsGovernanceReleaseActivationWorker: no scheduled releases due");
                return;
            }

            _logger.LogInformation(
                "SmsGovernanceReleaseActivationWorker: activating {Count} scheduled release(s)", due.Count);

            foreach (var releaseId in due)
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    var result = await releaseService.ActivateAsync(releaseId, "system:scheduled-worker", ct);
                    if (result.Success)
                        _logger.LogInformation(
                            "SmsGovernanceReleaseActivationWorker: release {Id} activated", releaseId);
                    else
                        _logger.LogWarning(
                            "SmsGovernanceReleaseActivationWorker: release {Id} activation failed: {Msg}",
                            releaseId, result.ErrorMessage);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "SmsGovernanceReleaseActivationWorker: exception activating release {Id}", releaseId);
                }
            }
        }
        catch (OperationCanceledException) { /* host shutting down */ }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SmsGovernanceReleaseActivationWorker: cycle exception");
        }
    }
}
