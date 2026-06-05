using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CareConnect.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CareConnect.Infrastructure.Notifications;

/// <summary>
/// LS-NOTIF-CORE-023: Submits outbound notifications to the platform Notifications
/// service using the canonical POST /v1/notifications producer contract.
///
/// Producer identity:
///   productKey   = "careconnect"
///   sourceSystem = "careconnect-service"
///
/// Authentication: X-Tenant-Id header (legacy ServiceSubmission transition path).
/// The Notifications service accepts this with a structured [LEGACY SUBMISSION]
/// warning until full service-JWT auth is wired up.
///
/// Delivery retry: owned by the Notifications service (SendGrid retry / backoff).
/// Submission retry: owned by CareConnect's ReferralEmailRetryWorker on failure here.
///
/// Configuration key: NotificationsService:BaseUrl (default: http://localhost:5008)
/// HTTP client name:  "NotificationsService" (already registered in DI)
/// </summary>
public sealed class NotificationsProducerClient : INotificationsProducer
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy         = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition       = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _http;
    private readonly string     _baseUrl;
    private readonly ILogger<NotificationsProducerClient> _logger;

    public NotificationsProducerClient(
        IHttpClientFactory                    httpClientFactory,
        IConfiguration                        configuration,
        ILogger<NotificationsProducerClient>  logger)
    {
        _http    = httpClientFactory.CreateClient("NotificationsService");
        _baseUrl = (configuration["NotificationsService:BaseUrl"] ?? "http://localhost:5008").TrimEnd('/');
        _logger  = logger;
    }

    public async Task SubmitAsync(
        Guid              tenantId,
        string            eventKey,
        string            toAddress,
        string            subject,
        string            htmlBody,
        string?           idempotencyKey = null,
        string?           correlationId  = null,
        CancellationToken ct             = default)
    {
        var url = $"{_baseUrl}/v1/notifications";

        var payload = new
        {
            channel      = "email",
            recipient    = new { email = toAddress },
            message      = new
            {
                type    = eventKey,
                subject,
                html    = htmlBody,
            },
            productKey     = "careconnect",
            eventKey,
            sourceSystem   = "careconnect-service",
            idempotencyKey,
            correlationId,
        };

        _logger.LogInformation(
            "Submitting notification event={EventKey} tenant={TenantId} recipient={Recipient} idempotencyKey={IdempotencyKey}",
            eventKey, tenantId, toAddress, idempotencyKey);

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload, options: JsonOpts),
        };
        request.Headers.Add("X-Tenant-Id", tenantId.ToString());

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Notifications service unreachable — submission failed for event={EventKey} tenant={TenantId} recipient={Recipient}.",
                eventKey, tenantId, toAddress);
            throw new InvalidOperationException(
                "Notification submission failed — Notifications service is unreachable.", ex);
        }

        if (response.IsSuccessStatusCode)
        {
            _logger.LogInformation(
                "Notification submitted successfully event={EventKey} tenant={TenantId} recipient={Recipient}.",
                eventKey, tenantId, toAddress);
            return;
        }

        string responseBody = string.Empty;
        try { responseBody = await response.Content.ReadAsStringAsync(ct); } catch { }

        _logger.LogWarning(
            "Notifications service returned {StatusCode} for event={EventKey} tenant={TenantId} recipient={Recipient}. Body: {Body}",
            (int)response.StatusCode, eventKey, tenantId, toAddress, responseBody);

        throw new InvalidOperationException(
            $"Notification submission failed — Notifications service returned {(int)response.StatusCode}: {responseBody}");
    }
}
