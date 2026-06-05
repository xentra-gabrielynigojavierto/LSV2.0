using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BuildingBlocks.Notifications;
using Microsoft.Extensions.Options;

namespace Support.Api.Notifications;

/// <summary>
/// LS-SUP-INT-05 — HTTP adapter that forwards notification requests to the
/// platform Notifications Service at <c>POST /v1/notifications</c>.
///
/// <para>
/// One <see cref="NotificationsProducerRequest"/> is sent per recipient so
/// the Notifications Service can apply per-user template substitution.
/// </para>
///
/// <para>
/// The <c>X-Tenant-Id</c> header is added to every outbound request so that
/// the <see cref="NotificationsAuthDelegatingHandler"/> registered in the
/// HttpClient pipeline can mint a short-lived service JWT carrying the
/// tenant claim.  If the signing secret is not configured the handler is a
/// no-op and requests are forwarded without a Bearer token (Notifications
/// Service will log a warning on its side; Support writes are unaffected).
/// </para>
///
/// Failures are logged but never propagated — Support Service writes must
/// not be rolled back due to notification transport issues.
/// </summary>
public sealed class HttpNotificationPublisher : INotificationPublisher
{
    public const string HttpClientName = "support-notifications";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy   = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _http;
    private readonly IOptionsMonitor<NotificationOptions> _options;
    private readonly ITenantSlugResolver _slugResolver;
    private readonly IPlatformSettingStore _platformSettings;
    private readonly ILogger<HttpNotificationPublisher> _log;

    public HttpNotificationPublisher(
        HttpClient http,
        IOptionsMonitor<NotificationOptions> options,
        ITenantSlugResolver slugResolver,
        IPlatformSettingStore platformSettings,
        ILogger<HttpNotificationPublisher> log)
    {
        _http             = http;
        _options          = options;
        _slugResolver     = slugResolver;
        _platformSettings = platformSettings;
        _log              = log;
    }

    public async Task PublishAsync(SupportNotification notification, CancellationToken ct = default)
    {
        var opts = _options.CurrentValue;
        if (!opts.Enabled)
        {
            _log.LogDebug(
                "Notifications disabled; suppressing HTTP dispatch event={EventType} ticket={TicketNumber}",
                notification.EventType, notification.TicketNumber);
            return;
        }

        if (string.IsNullOrWhiteSpace(opts.BaseUrl))
        {
            _log.LogWarning(
                "Notifications enabled in Http mode but BaseUrl is unset; skipping event={EventType} ticket={TicketNumber}",
                notification.EventType, notification.TicketNumber);
            return;
        }

        var url = CombineUrl(opts.BaseUrl!, "v1/notifications");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, opts.TimeoutSeconds)));

        // Resolve the tenant slug once per notification (cached after first call).
        var tenantSlug = await _slugResolver.ResolveAsync(notification.TenantId, cts.Token);

        // Resolve portal base domain (Control Center DB → env-var fallback).
        var portalBaseDomain =
            await _platformSettings.GetAsync("platform.portalBaseDomain", cts.Token)
            ?? opts.PortalBaseDomain;

        // Resolve the Control Center base URL for admin deeplinks.
        // Precedence: live DB value (set via CC Settings page) → NotificationOptions fallback (env var).
        var ccBaseUrl =
            await _platformSettings.GetAsync("platform.controlCenterBaseUrl", cts.Token)
            ?? opts.ControlCenterBaseUrl;

        // Precompute both deeplink variants so we don't re-derive them per recipient.
        var ticketId       = notification.Payload.TryGetValue("ticket_id", out var tid) ? tid?.ToString() : null;
        var tenantDeeplink = BuildTenantDeeplink(ticketId, tenantSlug, portalBaseDomain, opts);
        var adminDeeplink  = BuildAdminDeeplink (ticketId, ccBaseUrl)
                             ?? tenantDeeplink;   // fall back to tenant URL if CC URL not configured

        foreach (var recipient in notification.Recipients)
        {
            var recipientObj = MapRecipient(recipient);
            if (recipientObj is null)
            {
                _log.LogDebug(
                    "Skipping unresolvable recipient kind={Kind} event={EventType} ticket={TicketNumber}",
                    recipient.Kind, notification.EventType, notification.TicketNumber);
                continue;
            }

            // Admin recipients (control-centre users) get a direct link to the ticket
            // in the Control Center rather than the tenant-facing portal URL.
            var deeplink = recipient.IsAdminRecipient ? adminDeeplink : tenantDeeplink;

            var request = BuildProducerRequest(notification, recipientObj, deeplink);
            await SendOneAsync(url, notification.TenantId, request, notification.EventType, notification.TicketNumber, cts.Token);
        }
    }

    /// <summary>Returns the tenant-portal deeplink for a ticket, or null when prerequisites are missing.</summary>
    private static string? BuildTenantDeeplink(string? ticketId, string? tenantSlug, string? portalBaseDomain, NotificationOptions opts)
    {
        if (string.IsNullOrEmpty(ticketId)) return null;

        if (!string.IsNullOrWhiteSpace(tenantSlug) && !string.IsNullOrWhiteSpace(portalBaseDomain))
            return $"https://{tenantSlug}.{portalBaseDomain.TrimEnd('/')}/support/{ticketId}";

        if (!string.IsNullOrWhiteSpace(opts.PortalBaseUrl))
            return $"{opts.PortalBaseUrl.TrimEnd('/')}/support/{ticketId}";

        return null;
    }

    /// <summary>Returns the Control Center deeplink for a ticket, or null when <paramref name="ccBaseUrl"/> is blank.</summary>
    private static string? BuildAdminDeeplink(string? ticketId, string? ccBaseUrl)
    {
        if (string.IsNullOrEmpty(ticketId) || string.IsNullOrWhiteSpace(ccBaseUrl)) return null;
        return $"{ccBaseUrl.TrimEnd('/')}/support/{ticketId}";
    }

    private async Task SendOneAsync(
        string url,
        string tenantId,
        NotificationsProducerRequest request,
        string eventType,
        string ticketNumber,
        CancellationToken ct)
    {
        try
        {
            using var httpReq = new HttpRequestMessage(HttpMethod.Post, url);
            httpReq.Headers.TryAddWithoutValidation("X-Tenant-Id", tenantId);
            httpReq.Content = JsonContent.Create(request, options: JsonOpts);

            using var resp = await _http.SendAsync(httpReq, ct);
            if (!resp.IsSuccessStatusCode)
            {
                _log.LogWarning(
                    "Notifications dispatch returned {Status} event={EventType} ticket={TicketNumber}",
                    (int)resp.StatusCode, eventType, ticketNumber);
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex,
                "Notifications dispatch failed event={EventType} ticket={TicketNumber}",
                eventType, ticketNumber);
        }
    }

    private static NotificationsProducerRequest BuildProducerRequest(
        SupportNotification notification,
        NotificationsRecipient recipient,
        string? deeplink)
    {
        var templateData = notification.Payload
            .Where(kv => kv.Value is not null)
            .ToDictionary(kv => kv.Key, kv => kv.Value!.ToString() ?? string.Empty);

        if (!string.IsNullOrWhiteSpace(deeplink))
            templateData["deeplink_url"] = deeplink;

        var templateKey = MapTemplateKey(notification.EventType);

        return new NotificationsProducerRequest
        {
            Channel          = NotificationTaxonomy.Channels.Email,
            ProductKey       = NotificationTaxonomy.Support.ProductKey,
            EventKey         = notification.EventType,
            TemplateKey      = templateKey,
            SourceSystem     = NotificationTaxonomy.Support.SourceSystem,
            Recipient        = recipient,
            TemplateData     = templateData.Count > 0 ? templateData : null,
            BrandedRendering = true,
            Metadata         = new
            {
                ticketId     = notification.TicketId,
                ticketNumber = notification.TicketNumber,
            },
        };
    }

    /// <summary>
    /// Maps a support event type to its canonical email template key.
    /// New event types must be registered in
    /// <see cref="NotificationTaxonomy.Support.Templates"/> before use.
    /// </summary>
    private static string? MapTemplateKey(string eventType) => eventType switch
    {
        SupportNotificationEventTypes.TicketCreated       => NotificationTaxonomy.Support.Templates.TicketCreatedEmail,
        SupportNotificationEventTypes.TicketAssigned      => NotificationTaxonomy.Support.Templates.TicketAssignedEmail,
        SupportNotificationEventTypes.TicketUpdated       => NotificationTaxonomy.Support.Templates.TicketUpdatedEmail,
        SupportNotificationEventTypes.TicketStatusChanged => NotificationTaxonomy.Support.Templates.TicketStatusChangedEmail,
        SupportNotificationEventTypes.TicketCommentAdded  => NotificationTaxonomy.Support.Templates.TicketCommentAddedEmail,
        _                                                  => null,
    };

    private static NotificationsRecipient? MapRecipient(NotificationRecipient r)
        => r.Kind switch
        {
            NotificationRecipientKind.User when !string.IsNullOrWhiteSpace(r.UserId)
                => new NotificationsRecipient { UserId = r.UserId },

            NotificationRecipientKind.Email when !string.IsNullOrWhiteSpace(r.Email)
                => new NotificationsRecipient { Email = r.Email },

            _ => null,
        };

    private static string CombineUrl(string baseUrl, string path)
    {
        var b = baseUrl.TrimEnd('/');
        var p = path.TrimStart('/');
        return $"{b}/{p}";
    }
}
