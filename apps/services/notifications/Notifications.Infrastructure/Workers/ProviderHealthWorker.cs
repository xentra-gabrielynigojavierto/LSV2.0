using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Notifications.Application.Interfaces;

namespace Notifications.Infrastructure.Workers;

public class ProviderHealthWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProviderHealthWorker> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(2);

    public ProviderHealthWorker(IServiceScopeFactory scopeFactory, ILogger<ProviderHealthWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ProviderHealthWorker started, interval={Interval}s", _interval.TotalSeconds);
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var healthRepo = scope.ServiceProvider.GetRequiredService<IProviderHealthRepository>();
                var emailAdapter = scope.ServiceProvider.GetRequiredService<IEmailProviderAdapter>();
                var smsAdapter = scope.ServiceProvider.GetRequiredService<ISmsProviderAdapter>();

                var emailHealth = await emailAdapter.HealthCheckAsync();
                await healthRepo.UpsertAsync(new Domain.ProviderHealth
                {
                    ProviderType = emailAdapter.ProviderType,
                    Channel = "email",
                    OwnershipMode = "platform",
                    HealthStatus = emailHealth.Status,
                    LastLatencyMs = emailHealth.LatencyMs,
                    LastCheckAt = DateTime.UtcNow,
                    ConsecutiveFailures = emailHealth.Status == "healthy" ? 0 : 1,
                    ConsecutiveSuccesses = emailHealth.Status == "healthy" ? 1 : 0
                });

                var smsHealth = await smsAdapter.HealthCheckAsync();
                await healthRepo.UpsertAsync(new Domain.ProviderHealth
                {
                    ProviderType = smsAdapter.ProviderType,
                    Channel = "sms",
                    OwnershipMode = "platform",
                    HealthStatus = smsHealth.Status,
                    LastLatencyMs = smsHealth.LatencyMs,
                    LastCheckAt = DateTime.UtcNow,
                    ConsecutiveFailures = smsHealth.Status == "healthy" ? 0 : 1,
                    ConsecutiveSuccesses = smsHealth.Status == "healthy" ? 1 : 0
                });

                _logger.LogDebug("Provider health check complete: email={EmailStatus}, sms={SmsStatus}", emailHealth.Status, smsHealth.Status);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ProviderHealthWorker error");
            }

            await Task.Delay(_interval, stoppingToken);
        }

        _logger.LogInformation("ProviderHealthWorker stopped");
    }
}
