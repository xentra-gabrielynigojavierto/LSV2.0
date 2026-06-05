using System.Net.Http.Json;
using Flow.Application.Adapters.AuditAdapter;
using Microsoft.Extensions.Logging;

namespace Flow.Infrastructure.Adapters;

/// <summary>
/// Optional HTTP-backed audit adapter. Activated only when
/// <c>Audit:BaseUrl</c> is configured. Decorates a fallback adapter so
/// that transient failures degrade gracefully without breaking the
/// originating request.
/// </summary>
public sealed class HttpAuditAdapter : IAuditAdapter
{
    private readonly HttpClient _http;
    private readonly IAuditAdapter _fallback;
    private readonly AuditAuthHeaderProvider _auth;
    private readonly ILogger<HttpAuditAdapter> _log;

    public HttpAuditAdapter(
        HttpClient http,
        IAuditAdapter fallback,
        AuditAuthHeaderProvider auth,
        ILogger<HttpAuditAdapter> log)
    {
        _http = http;
        _fallback = fallback;
        _auth = auth;
        _log = log;
    }

    public async Task WriteEventAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "audit/events")
            {
                Content = JsonContent.Create(auditEvent),
            };
            // Forward the operator's bearer when present so the audit
            // service applies its per-caller scope rules; fall back to a
            // minted service token (or to anonymous, when the audit
            // service runs in QueryAuth:Mode=None) for background outbox
            // writes that have no HttpContext.
            req.Headers.Authorization = _auth.GetHeader(
                fallbackTenantId: auditEvent.TenantId,
                fallbackUserId:   auditEvent.UserId);

            using var resp = await _http.SendAsync(req, cancellationToken);
            if (!resp.IsSuccessStatusCode)
            {
                _log.LogWarning(
                    "Audit POST returned {StatusCode}; falling back to logging adapter.",
                    (int)resp.StatusCode);
                await _fallback.WriteEventAsync(auditEvent, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Audit POST failed; falling back to logging adapter.");
            await _fallback.WriteEventAsync(auditEvent, cancellationToken);
        }
    }
}
