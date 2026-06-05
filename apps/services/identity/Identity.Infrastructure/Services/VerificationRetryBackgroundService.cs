using Identity.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Infrastructure.Services;

public sealed class VerificationRetryBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly VerificationRetryOptions _opts;
    private readonly ILogger<VerificationRetryBackgroundService> _log;

    public VerificationRetryBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<VerificationRetryOptions> opts,
        ILogger<VerificationRetryBackgroundService> log)
    {
        _scopeFactory = scopeFactory;
        _opts = opts.Value;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation(
            "Verification retry background worker started (polling every {Interval}s)",
            _opts.InitialDelaySeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            var delaySeconds = Math.Max(15, _opts.InitialDelaySeconds);
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var retryService = scope.ServiceProvider.GetRequiredService<IVerificationRetryService>();
                await retryService.ProcessPendingRetriesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Verification retry background worker encountered an error");
            }
        }

        _log.LogInformation("Verification retry background worker stopped");
    }
}
