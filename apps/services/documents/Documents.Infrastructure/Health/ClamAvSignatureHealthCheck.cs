using Documents.Infrastructure.Scanner;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Documents.Infrastructure.Health;

/// <summary>
/// ASP.NET Core health check that reports the freshness of the ClamAV virus-definition database.
///
/// Uses <see cref="ClamAvSignatureFreshnessMonitor"/> to obtain the database date from the
/// ClamAV VERSION command. Reports Degraded when the database age exceeds
/// <see cref="ClamAvOptions.SignatureMaxAgeHours"/>.
///
/// This is a pure observability check — it never affects scan behavior.
/// </summary>
public sealed class ClamAvSignatureHealthCheck : IHealthCheck
{
    private readonly ClamAvOptions                         _opts;
    private readonly ClamAvSignatureFreshnessMonitor       _monitor;
    private readonly ILogger<ClamAvSignatureHealthCheck>   _log;

    public ClamAvSignatureHealthCheck(
        IOptions<ClamAvOptions>                    opts,
        ClamAvSignatureFreshnessMonitor            monitor,
        ILogger<ClamAvSignatureHealthCheck>        log)
    {
        _opts    = opts.Value;
        _monitor = monitor;
        _log     = log;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken  ct = default)
    {
        var info = await _monitor.GetAsync(ct);

        var data = new Dictionary<string, object>
        {
            ["raw_version"]        = info.RawVersion,
            ["db_version"]         = info.DbVersion,
            ["db_date"]            = info.DbDate?.ToString("o") ?? "unknown",
            ["age_seconds"]        = info.AgeSeconds.HasValue ? (object)Math.Round(info.AgeSeconds.Value) : "unknown",
            ["max_age_hours"]      = _opts.SignatureMaxAgeHours,
            ["queried_at"]         = info.QueriedAt.ToString("o"),
        };

        if (!info.Success)
        {
            _log.LogWarning(
                "ClamAV signature freshness check failed — signature age unknown: {Error}",
                info.ErrorMessage);

            data["error"] = info.ErrorMessage ?? "unknown";
            return HealthCheckResult.Degraded(
                "ClamAV signature freshness unavailable — cannot verify definition age",
                data: data);
        }

        if (!info.AgeSeconds.HasValue)
        {
            return HealthCheckResult.Degraded(
                "ClamAV VERSION returned but signature date could not be parsed",
                data: data);
        }

        var maxAgeSeconds = _opts.SignatureMaxAgeHours * 3600.0;
        var ageHours      = info.AgeSeconds.Value / 3600.0;

        if (info.AgeSeconds.Value > maxAgeSeconds)
        {
            _log.LogWarning(
                "ClamAV signatures are stale — age {AgeHours:F1}h exceeds threshold {MaxAgeHours}h",
                ageHours, _opts.SignatureMaxAgeHours);

            data["stale"] = true;
            return HealthCheckResult.Degraded(
                $"ClamAV signatures stale — age {ageHours:F1}h exceeds threshold {_opts.SignatureMaxAgeHours}h",
                data: data);
        }

        data["stale"] = false;
        return HealthCheckResult.Healthy(
            $"ClamAV signatures current — age {ageHours:F1}h, threshold {_opts.SignatureMaxAgeHours}h",
            data: data);
    }
}
