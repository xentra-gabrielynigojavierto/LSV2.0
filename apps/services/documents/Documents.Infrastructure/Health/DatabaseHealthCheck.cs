using Documents.Infrastructure.Database;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Documents.Infrastructure.Health;

/// <summary>
/// Verifies MySQL connectivity by calling CanConnectAsync on the EF Core DbContext.
/// </summary>
public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly IServiceScopeFactory         _scopes;
    private readonly ILogger<DatabaseHealthCheck> _log;

    public DatabaseHealthCheck(IServiceScopeFactory scopes, ILogger<DatabaseHealthCheck> log)
    {
        _scopes = scopes;
        _log    = log;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken  ct = default)
    {
        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<DocsDbContext>();
            var canConnect = await db.Database.CanConnectAsync(ct);
            return canConnect
                ? HealthCheckResult.Healthy("MySQL reachable")
                : HealthCheckResult.Unhealthy("MySQL: CanConnect returned false");
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Database health check failed");
            return HealthCheckResult.Unhealthy($"MySQL: {ex.Message}");
        }
    }
}
