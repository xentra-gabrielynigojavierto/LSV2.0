using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BuildingBlocks.Notifications;
using Liens.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Liens.Infrastructure.Notifications;

public sealed class NotificationPublisher : INotificationPublisher
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<NotificationPublisher> _logger;

    public NotificationPublisher(
        IHttpClientFactory httpClientFactory,
        ILogger<NotificationPublisher> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task PublishAsync(
        string notificationType,
        Guid tenantId,
        Dictionary<string, string> data,
        CancellationToken ct = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("NotificationsService");

            var request = new NotificationsProducerRequest
            {
                Channel      = "event",
                ProductKey   = "liens",
                EventKey     = notificationType,
                SourceSystem = "liens-service",
                TemplateKey  = notificationType,
                TemplateData = data,
                Recipient    = new NotificationsRecipient { TenantId = tenantId.ToString() },
                Message      = new { type = notificationType },
                Metadata     = new Dictionary<string, string>
                {
                    ["notificationType"] = notificationType,
                    ["tenantId"]         = tenantId.ToString(),
                },
            };

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/notifications");
            httpRequest.Headers.Add("X-Tenant-Id", tenantId.ToString());
            httpRequest.Content = JsonContent.Create(request, options: JsonOpts);

            var response = await client.SendAsync(httpRequest, CancellationToken.None);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug(
                    "Notification published: Type={NotificationType} Tenant={TenantId}",
                    notificationType, tenantId);
            }
            else
            {
                var body = string.Empty;
                try { body = await response.Content.ReadAsStringAsync(CancellationToken.None); } catch { }

                _logger.LogWarning(
                    "Notification publish returned {StatusCode}: Type={NotificationType} Tenant={TenantId} Body={Body}",
                    (int)response.StatusCode, notificationType, tenantId, body);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex,
                "Notification publish failed: Type={NotificationType} Tenant={TenantId}",
                notificationType, tenantId);
        }
    }
}
