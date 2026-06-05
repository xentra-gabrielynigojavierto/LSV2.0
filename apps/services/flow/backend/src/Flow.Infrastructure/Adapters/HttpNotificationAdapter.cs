using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BuildingBlocks.Notifications;
using Flow.Application.Adapters.NotificationAdapter;
using Microsoft.Extensions.Logging;

namespace Flow.Infrastructure.Adapters;

/// <summary>
/// Optional HTTP-backed notification adapter. Activated only when
/// <c>Notifications:BaseUrl</c> is configured.
///
/// Maps <see cref="NotificationMessage"/> to the canonical
/// <see cref="NotificationsProducerRequest"/> contract before posting to
/// <c>POST /v1/notifications</c>.
/// </summary>
public sealed class HttpNotificationAdapter : INotificationAdapter
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _http;
    private readonly INotificationAdapter _fallback;
    private readonly ILogger<HttpNotificationAdapter> _log;

    public HttpNotificationAdapter(HttpClient http, INotificationAdapter fallback, ILogger<HttpNotificationAdapter> log)
    {
        _http = http;
        _fallback = fallback;
        _log = log;
    }

    public async Task SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
    {
        try
        {
            var correlationId = message.Data?.TryGetValue("correlationId", out var cid) == true ? cid : null;
            var requestedBy   = message.Data?.TryGetValue("requestedBy",   out var rb)  == true ? rb  : null;

            var templateData = message.Data?
                .Where(kvp => !string.Equals(kvp.Key, "correlationId", StringComparison.OrdinalIgnoreCase)
                           && !string.Equals(kvp.Key, "requestedBy",   StringComparison.OrdinalIgnoreCase)
                           && kvp.Value != null)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value!);

            var notificationRequest = new NotificationsProducerRequest
            {
                Channel      = message.Channel,
                ProductKey   = "flow",
                EventKey     = message.EventKey,
                SourceSystem = "flow-service",
                CorrelationId = correlationId,
                RequestedBy  = requestedBy,
                Recipient    = BuildRecipient(message),
                Message      = new
                {
                    type    = message.EventKey,
                    subject = message.Subject,
                    body    = message.Body,
                },
                TemplateData = templateData?.Count > 0 ? templateData : null,
                Metadata     = !string.IsNullOrEmpty(message.TenantId)
                    ? new Dictionary<string, string> { ["tenantId"] = message.TenantId }
                    : null,
            };

            // LS-NOTIF-CORE-021: add X-Tenant-Id header so the
            // NotificationsAuthDelegatingHandler can mint a service JWT
            // (when FLOW_SERVICE_TOKEN_SECRET is configured) or the server
            // can fall back to the legacy header path.
            using var notifReq = new HttpRequestMessage(HttpMethod.Post, "v1/notifications")
            {
                Content = JsonContent.Create(notificationRequest, options: JsonOpts),
            };
            if (!string.IsNullOrEmpty(message.TenantId))
                notifReq.Headers.Add("X-Tenant-Id", message.TenantId);

            using var resp = await _http.SendAsync(notifReq, cancellationToken);
            if (!resp.IsSuccessStatusCode)
            {
                _log.LogWarning(
                    "Notifications POST returned {StatusCode} for event {EventKey}; falling back to logging adapter.",
                    (int)resp.StatusCode, message.EventKey);
                await _fallback.SendAsync(message, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Notifications POST failed for event {EventKey}; falling back to logging adapter.", message.EventKey);
            await _fallback.SendAsync(message, cancellationToken);
        }
    }

    private static NotificationsRecipient BuildRecipient(NotificationMessage message)
    {
        var recipient = new NotificationsRecipient
        {
            TenantId = message.TenantId,
        };

        if (!string.IsNullOrEmpty(message.RecipientUserId))
        {
            recipient.UserId = message.RecipientUserId;
        }
        else if (!string.IsNullOrEmpty(message.RecipientRoleKey))
        {
            recipient.RoleKey = message.RecipientRoleKey;
            recipient.Mode    = "Role";
        }

        return recipient;
    }
}
