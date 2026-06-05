using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monitoring.Application.Scheduling;
using Monitoring.Domain.Monitoring;

namespace Monitoring.Infrastructure.Http;

/// <summary>
/// First real <see cref="IMonitoredEntityExecutor"/>: performs an HTTP
/// <c>GET</c> against <see cref="MonitoredEntity.Target"/> for entities whose
/// <see cref="MonitoredEntity.MonitoringType"/> is <see cref="MonitoringType.Http"/>,
/// classifies the outcome (success / non-2xx / timeout / invalid URL /
/// network failure), logs it with a sanitized target, and returns a
/// structured <see cref="CheckResult"/>.
///
/// <para>Non-HTTP entities are skipped at debug level and a
/// <see cref="CheckOutcome.Skipped"/> result is returned; that keeps the
/// cycle aggregation's per-outcome breakdown meaningful while ensuring a
/// non-HTTP entity is not counted as a failure.</para>
///
/// <para>Cancellation: the host's stopping token is propagated through to
/// the HTTP request via a linked <see cref="CancellationTokenSource"/>, so
/// shutdown unwinds promptly even mid-request. Only host-shutdown
/// cancellation is rethrown — timeouts (linked CTS firing) become a
/// <see cref="CheckOutcome.Timeout"/> result.</para>
/// </summary>
public sealed class HttpMonitoredEntityExecutor : IMonitoredEntityExecutor
{
    /// <summary>Name used to resolve the dedicated <see cref="HttpClient"/> from
    /// <see cref="IHttpClientFactory"/>. Keep it private to the executor — no
    /// other component should reuse this client.</summary>
    public const string HttpClientName = "monitoring-http-check";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<HttpCheckOptions> _options;
    private readonly ILogger<HttpMonitoredEntityExecutor> _logger;

    public HttpMonitoredEntityExecutor(
        IHttpClientFactory httpClientFactory,
        IOptions<HttpCheckOptions> options,
        ILogger<HttpMonitoredEntityExecutor> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    public async Task<CheckResult> ExecuteAsync(MonitoredEntity entity, CancellationToken cancellationToken)
    {
        if (entity.MonitoringType != MonitoringType.Http)
        {
            _logger.LogDebug(
                "Skipping non-HTTP entity {EntityId} ({EntityName}); MonitoringType={MonitoringType}.",
                entity.Id, entity.Name, entity.MonitoringType);
            return Build(entity, false, CheckOutcome.Skipped, null, 0,
                $"skipped: non-HTTP ({entity.MonitoringType})");
        }

        if (!Uri.TryCreate(entity.Target, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            // Do NOT log the raw target — a malformed string could still
            // contain credentials or other sensitive material. Length is
            // enough to help an operator correlate to the registry row.
            _logger.LogWarning(
                "HTTP check failed for entity {EntityId} ({EntityName}): " +
                "target is not a valid absolute http(s) URL. TargetLength={TargetLength}.",
                entity.Id, entity.Name, entity.Target?.Length ?? 0);
            return Build(entity, false, CheckOutcome.InvalidTarget, null, 0,
                "invalid http(s) URL");
        }

        // SSRF guard: reject targets that resolve to loopback, link-local,
        // or RFC-1918 private addresses. This prevents the monitoring
        // scheduler from being used as a blind HTTP pivot into internal
        // infrastructure, cloud metadata endpoints, or other services on
        // the host network.
        //
        // Exception: when AllowInternalTargets is explicitly enabled (co-located
        // deployment — e.g. Replit dev, single-host Docker), internal addresses
        // are intentional. A warning is still logged so the config is visible in
        // the audit trail, but the check is not blocked.
        if (IsSsrfProneUri(uri))
        {
            if (!_options.Value.AllowInternalTargets)
            {
                _logger.LogWarning(
                    "HTTP check blocked for entity {EntityId} ({EntityName}): " +
                    "target resolves to a private, loopback, or link-local address. " +
                    "TargetLength={TargetLength}.",
                    entity.Id, entity.Name, entity.Target?.Length ?? 0);
                return Build(entity, false, CheckOutcome.InvalidTarget, null, 0,
                    "target blocked: private or loopback address");
            }

            _logger.LogDebug(
                "HTTP check for entity {EntityId} ({EntityName}) is targeting an internal address. " +
                "AllowInternalTargets=true — proceeding (co-located deployment mode).",
                entity.Id, entity.Name);
        }

        // Build a sanitized log-only representation of the target. Userinfo
        // (basic-auth credentials), query strings, and fragments are
        // dropped — they can carry secrets, tokens, or PII. We keep the
        // operator-useful parts: scheme, host, port (if non-default), path.
        var safeTarget = SanitizeForLog(uri);

        var timeout = TimeSpan.FromSeconds(_options.Value.TimeoutSeconds);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linkedCts.CancelAfter(timeout);

        var client = _httpClientFactory.CreateClient(HttpClientName);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var response = await client
                .GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, linkedCts.Token)
                .ConfigureAwait(false);
            stopwatch.Stop();

            var statusCode = (int)response.StatusCode;

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "HTTP check OK for entity {EntityId} ({EntityName}). " +
                    "Target={Target} StatusCode={StatusCode} ElapsedMs={ElapsedMs}.",
                    entity.Id, entity.Name, safeTarget, statusCode, stopwatch.ElapsedMilliseconds);
                return Build(entity, true, CheckOutcome.Success, statusCode,
                    stopwatch.ElapsedMilliseconds, "OK");
            }

            _logger.LogWarning(
                "HTTP check non-2xx for entity {EntityId} ({EntityName}). " +
                "Target={Target} StatusCode={StatusCode} ElapsedMs={ElapsedMs}.",
                entity.Id, entity.Name, safeTarget, statusCode, stopwatch.ElapsedMilliseconds);
            return Build(entity, false, CheckOutcome.NonSuccessStatusCode, statusCode,
                stopwatch.ElapsedMilliseconds, "non-2xx");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Host shutdown — propagate so the cycle executor handles it as
            // shutdown rather than counting it as a per-entity failure.
            stopwatch.Stop();
            throw;
        }
        catch (OperationCanceledException)
        {
            // The linked CTS fired without the host token — this is a timeout.
            stopwatch.Stop();
            _logger.LogWarning(
                "HTTP check timeout for entity {EntityId} ({EntityName}) after {ElapsedMs} ms. " +
                "Target={Target} TimeoutSeconds={TimeoutSeconds}.",
                entity.Id, entity.Name, stopwatch.ElapsedMilliseconds, safeTarget, _options.Value.TimeoutSeconds);
            return Build(entity, false, CheckOutcome.Timeout, null,
                stopwatch.ElapsedMilliseconds,
                $"timeout after {stopwatch.ElapsedMilliseconds} ms",
                errorType: "OperationCanceled");
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            // Log a stable, enum-based classification (HttpRequestError) plus
            // the inner exception type name. We deliberately do NOT log
            // ex.Message — depending on platform/handler it can carry
            // endpoint detail that we have already encoded safely in
            // safeTarget, and we do not want raw message content to bypass
            // future redaction rules.
            _logger.LogWarning(
                "HTTP check network failure for entity {EntityId} ({EntityName}) after {ElapsedMs} ms. " +
                "Target={Target} HttpRequestError={HttpRequestError} InnerType={InnerType}.",
                entity.Id, entity.Name, stopwatch.ElapsedMilliseconds, safeTarget,
                ex.HttpRequestError,
                ex.InnerException?.GetType().Name ?? "(none)");
            return Build(entity, false, CheckOutcome.NetworkFailure, null,
                stopwatch.ElapsedMilliseconds, "network failure",
                errorType: ex.HttpRequestError.ToString());
        }
    }

    private static CheckResult Build(
        MonitoredEntity entity,
        bool succeeded,
        CheckOutcome outcome,
        int? statusCode,
        long elapsedMs,
        string message,
        string? errorType = null) =>
        new(
            EntityId: entity.Id,
            EntityName: entity.Name,
            MonitoringType: entity.MonitoringType,
            Target: entity.Target,
            Succeeded: succeeded,
            Outcome: outcome,
            StatusCode: statusCode,
            ElapsedMs: elapsedMs,
            CheckedAtUtc: DateTime.UtcNow,
            Message: message,
            ErrorType: errorType);

    /// <summary>
    /// Returns a log-safe rendering of the target URI: scheme + host
    /// (+ explicit port for non-default ports) + path. Userinfo,
    /// query string, and fragment are intentionally dropped because
    /// any of them can carry credentials, tokens, or PII that must
    /// never reach logs.
    /// </summary>
    private static string SanitizeForLog(Uri uri)
    {
        var isDefaultPort = uri.IsDefaultPort;
        var hostPort = isDefaultPort ? uri.Host : $"{uri.Host}:{uri.Port}";
        // AbsolutePath includes the leading '/'.
        return $"{uri.Scheme}://{hostPort}{uri.AbsolutePath}";
    }

    /// <summary>
    /// Returns <c>true</c> if the URI's host is a known SSRF-prone address:
    /// "localhost", a loopback IP (127.x.x.x / ::1), a link-local address
    /// (169.254.x.x / fe80::/10), or an RFC-1918 private range
    /// (10.x.x.x, 172.16-31.x.x, 192.168.x.x). IPv6 unique-local
    /// (fc00::/7) is also blocked.
    ///
    /// <para>When the host is a DNS name that is not "localhost" this method
    /// returns <c>false</c> — DNS-based SSRF via a rebinding-style attack is
    /// a separate concern handled at the network/egress layer. The check here
    /// eliminates the most common and trivially exploitable cases where an
    /// attacker supplies a bare IP or "localhost".</para>
    /// </summary>
    private static bool IsSsrfProneUri(Uri uri)
    {
        var host = uri.Host;

        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (IPAddress.TryParse(host, out var ip))
        {
            return IsPrivateOrReservedAddress(ip);
        }

        return false;
    }

    /// <summary>
    /// Checks whether an <see cref="IPAddress"/> falls in a loopback,
    /// link-local, private, or otherwise reserved range that should never
    /// be reachable from an outbound monitoring probe.
    /// </summary>
    private static bool IsPrivateOrReservedAddress(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip))
        {
            return true;
        }

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = ip.GetAddressBytes();
            // 10.0.0.0/8
            if (b[0] == 10) return true;
            // 172.16.0.0/12
            if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return true;
            // 192.168.0.0/16
            if (b[0] == 192 && b[1] == 168) return true;
            // 169.254.0.0/16 — link-local and AWS/GCP/Azure IMDS
            if (b[0] == 169 && b[1] == 254) return true;
            // 127.0.0.0/8 — additional loopback range beyond IPAddress.IsLoopback
            if (b[0] == 127) return true;
        }
        else if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var b = ip.GetAddressBytes();
            // fc00::/7 — unique local
            if ((b[0] & 0xfe) == 0xfc) return true;
            // fe80::/10 — link-local
            if (b[0] == 0xfe && (b[1] & 0xc0) == 0x80) return true;
        }

        return false;
    }
}
