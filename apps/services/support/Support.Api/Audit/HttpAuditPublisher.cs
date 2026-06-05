using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace Support.Api.Audit;

/// <summary>
/// HTTP adapter that forwards audit events to the Audit Service.
/// Failures are logged but never propagated — Support Service writes
/// must not be rolled back due to audit transport issues.
/// </summary>
public sealed class HttpAuditPublisher : IAuditPublisher
{
    public const string HttpClientName = "support-audit";

    private readonly HttpClient _http;
    private readonly IOptionsMonitor<AuditOptions> _options;
    private readonly ILogger<HttpAuditPublisher> _log;

    public HttpAuditPublisher(
        HttpClient http,
        IOptionsMonitor<AuditOptions> options,
        ILogger<HttpAuditPublisher> log)
    {
        _http = http;
        _options = options;
        _log = log;
    }

    public async Task PublishAsync(SupportAuditEvent auditEvent, CancellationToken ct = default)
    {
        var opts = _options.CurrentValue;
        if (!opts.Enabled)
        {
            _log.LogDebug(
                "Audit disabled; suppressing HTTP dispatch event={EventType} resource={ResourceId}",
                auditEvent.EventType, auditEvent.ResourceId);
            return;
        }

        if (string.IsNullOrWhiteSpace(opts.BaseUrl))
        {
            _log.LogWarning(
                "Audit enabled in Http mode but BaseUrl is unset; skipping event={EventType} resource={ResourceId}",
                auditEvent.EventType, auditEvent.ResourceId);
            return;
        }

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, opts.TimeoutSeconds)));

            var url = CombineUrl(opts.BaseUrl!, "audit-events");
            using var resp = await _http.PostAsJsonAsync(url, auditEvent, cts.Token);
            if (!resp.IsSuccessStatusCode)
            {
                _log.LogWarning(
                    "Audit dispatch returned {Status} event={EventType} resource={ResourceId}",
                    (int)resp.StatusCode, auditEvent.EventType, auditEvent.ResourceId);
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex,
                "Audit dispatch failed event={EventType} resource={ResourceId}",
                auditEvent.EventType, auditEvent.ResourceId);
        }
    }

    private static string CombineUrl(string baseUrl, string path)
    {
        var b = baseUrl.TrimEnd('/');
        var p = path.TrimStart('/');
        return $"{b}/{p}";
    }
}
