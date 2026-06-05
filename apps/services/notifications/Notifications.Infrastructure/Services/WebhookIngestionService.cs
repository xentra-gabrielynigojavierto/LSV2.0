using System.Text.Json;
using Microsoft.Extensions.Logging;
using Notifications.Application.Interfaces;
using Notifications.Domain;
using Notifications.Infrastructure.Webhooks.Normalizers;
using Notifications.Infrastructure.Webhooks.Verifiers;
using LegalSynq.AuditClient;
using LegalSynq.AuditClient.DTOs;

namespace Notifications.Infrastructure.Services;

public class WebhookIngestionServiceImpl : IWebhookIngestionService
{
    private readonly IWebhookLogRepository _webhookLogRepo;
    private readonly INotificationEventRepository _eventRepo;
    private readonly INotificationAttemptRepository _attemptRepo;
    private readonly IDeliveryStatusService _deliveryStatusSvc;
    private readonly IRecipientContactHealthService _contactHealthSvc;
    private readonly IDeliveryIssueService _deliveryIssueSvc;
    private readonly IContactSuppressionRepository _suppressionRepo;
    private readonly ISmsPreferenceService _smsPreferenceSvc;
    private readonly IInboundSmsResolverService _inboundSmsResolver;
    private readonly SendGridVerifier _sendGridVerifier;
    private readonly TwilioVerifier _twilioVerifier;
    private readonly IAuditEventClient _auditClient;
    private readonly ILogger<WebhookIngestionServiceImpl> _logger;

    private static readonly HashSet<string> DeliveryFinalEvents = new() { "delivered", "bounced", "failed", "undeliverable", "rejected", "complained", "unsubscribed" };
    private static readonly HashSet<string> InboundDirections = new(StringComparer.OrdinalIgnoreCase) { "inbound", "inbound-api" };
    private static readonly Dictionary<string, string> SgSuppressionMap = new() { ["bounced"] = "bounce", ["complained"] = "complaint", ["unsubscribed"] = "unsubscribe" };
    private static readonly Dictionary<string, string> TwilioSuppressionMap = new() { ["bounced"] = "bounce", ["complained"] = "complaint", ["unsubscribed"] = "unsubscribe", ["carrier_rejected"] = "carrier_rejection" };

    public WebhookIngestionServiceImpl(
        IWebhookLogRepository webhookLogRepo, INotificationEventRepository eventRepo, INotificationAttemptRepository attemptRepo,
        IDeliveryStatusService deliveryStatusSvc, IRecipientContactHealthService contactHealthSvc, IDeliveryIssueService deliveryIssueSvc,
        IContactSuppressionRepository suppressionRepo, ISmsPreferenceService smsPreferenceSvc,
        IInboundSmsResolverService inboundSmsResolver,
        SendGridVerifier sendGridVerifier, TwilioVerifier twilioVerifier,
        IAuditEventClient auditClient, ILogger<WebhookIngestionServiceImpl> logger)
    {
        _webhookLogRepo = webhookLogRepo; _eventRepo = eventRepo; _attemptRepo = attemptRepo;
        _deliveryStatusSvc = deliveryStatusSvc; _contactHealthSvc = contactHealthSvc; _deliveryIssueSvc = deliveryIssueSvc;
        _suppressionRepo = suppressionRepo; _smsPreferenceSvc = smsPreferenceSvc;
        _inboundSmsResolver = inboundSmsResolver;
        _sendGridVerifier = sendGridVerifier; _twilioVerifier = twilioVerifier;
        _auditClient = auditClient; _logger = logger;
    }

    public async Task<WebhookResult> HandleSendGridAsync(string rawBody, Dictionary<string, string?> headers)
    {
        var signature = headers.GetValueOrDefault("x-twilio-email-event-webhook-signature");
        var timestamp = headers.GetValueOrDefault("x-twilio-email-event-webhook-timestamp");
        var (verified, skipped, reason) = _sendGridVerifier.Verify(rawBody, signature, timestamp);

        try { await _auditClient.IngestAsync(new IngestAuditEventRequest { EventType = "webhook.received", Action = "webhook.received", SourceSystem = "notifications", Description = "SendGrid webhook received" }); } catch { }

        if (!verified && !skipped)
        {
            _logger.LogWarning("SendGrid webhook rejected: {Reason}", reason);
            return new WebhookResult { Accepted = false, RejectedReason = "signature_verification_failed" };
        }

        var rawLog = await _webhookLogRepo.CreateAsync(new ProviderWebhookLog
        {
            Provider = "sendgrid", Channel = "email",
            RequestHeadersJson = JsonSerializer.Serialize(headers),
            PayloadJson = rawBody, SignatureVerified = verified,
            ProcessingStatus = "received", ReceivedAt = DateTime.UtcNow
        });

        var events = SendGridNormalizer.ParseEvents(rawBody);
        foreach (var rawEvent in events)
        {
            try { await ProcessSendGridEventAsync(rawEvent); } catch (Exception ex) { _logger.LogError(ex, "Error processing SendGrid event"); }
        }

        await _webhookLogRepo.UpdateStatusAsync(rawLog.Id, "processed");
        return new WebhookResult { Accepted = true };
    }

    public async Task<WebhookResult> HandleTwilioAsync(string rawBody, Dictionary<string, string?> headers, string requestUrl, Dictionary<string, string> formParams)
    {
        var signature = headers.GetValueOrDefault("x-twilio-signature");
        var (verified, skipped, reason) = _twilioVerifier.Verify(requestUrl, formParams, signature);

        try { await _auditClient.IngestAsync(new IngestAuditEventRequest { EventType = "webhook.received", Action = "webhook.received", SourceSystem = "notifications", Description = "Twilio webhook received" }); } catch { }

        if (!verified && !skipped)
        {
            _logger.LogWarning("Twilio webhook rejected: {Reason}", reason);
            return new WebhookResult { Accepted = false, RejectedReason = "signature_verification_failed" };
        }

        var rawLog = await _webhookLogRepo.CreateAsync(new ProviderWebhookLog
        {
            Provider = "twilio", Channel = "sms",
            RequestHeadersJson = JsonSerializer.Serialize(headers),
            PayloadJson = JsonSerializer.Serialize(formParams), SignatureVerified = verified,
            ProcessingStatus = "received", ReceivedAt = DateTime.UtcNow
        });

        try { await ProcessTwilioEventAsync(formParams); } catch (Exception ex) { _logger.LogError(ex, "Error processing Twilio event"); }

        await _webhookLogRepo.UpdateStatusAsync(rawLog.Id, "processed");
        return new WebhookResult { Accepted = true };
    }

    private async Task ProcessSendGridEventAsync(SendGridEventItem rawEvent)
    {
        var normalized = SendGridNormalizer.Normalize(rawEvent);
        var dedupKey = normalized.ProviderMessageId != null ? $"sendgrid:{normalized.ProviderMessageId}:{normalized.RawEventType}:{normalized.EventTimestamp.Ticks}" : null;

        if (dedupKey != null && await _eventRepo.FindByDedupKeyAsync(dedupKey) != null) return;

        NotificationAttempt? attempt = null;
        if (normalized.ProviderMessageId != null)
            attempt = await _attemptRepo.FindByProviderMessageIdAsync(normalized.ProviderMessageId);

        var notificationId = attempt?.NotificationId;
        var tenantId = attempt?.TenantId;

        await _eventRepo.CreateAsync(new NotificationEvent
        {
            TenantId = tenantId, NotificationId = notificationId, NotificationAttemptId = attempt?.Id,
            Provider = "sendgrid", Channel = "email", RawEventType = normalized.RawEventType,
            NormalizedEventType = normalized.NormalizedEventType, EventTimestamp = normalized.EventTimestamp,
            ProviderMessageId = normalized.ProviderMessageId, DedupKey = dedupKey
        });

        if (attempt != null)
        {
            await _deliveryStatusSvc.UpdateAttemptFromEventAsync(attempt.Id, normalized.NormalizedEventType);
            if (notificationId.HasValue)
                await _deliveryStatusSvc.UpdateNotificationFromEventAsync(notificationId.Value, normalized.NormalizedEventType);
        }

        if (tenantId.HasValue && normalized.RecipientEmail != null)
            await _contactHealthSvc.ProcessEventAsync(tenantId.Value, "email", normalized.RecipientEmail, normalized.NormalizedEventType, normalized.RawEventType);

        if (tenantId.HasValue && notificationId.HasValue && DeliveryFinalEvents.Contains(normalized.NormalizedEventType))
            await _deliveryIssueSvc.ProcessEventAsync(new DeliveryIssueContext { TenantId = tenantId.Value, NotificationId = notificationId.Value, NotificationAttemptId = attempt?.Id, Channel = "email", Provider = "sendgrid", NormalizedEventType = normalized.NormalizedEventType, RawEventType = normalized.RawEventType, RecipientContact = normalized.RecipientEmail });

        if (tenantId.HasValue && normalized.RecipientEmail != null && SgSuppressionMap.TryGetValue(normalized.NormalizedEventType, out var suppressionType))
        {
            try
            {
                await _suppressionRepo.UpsertFromEventAsync(new ContactSuppression
                {
                    TenantId = tenantId.Value, Channel = "email", ContactValue = normalized.RecipientEmail.Trim().ToLowerInvariant(),
                    SuppressionType = suppressionType, Reason = $"Auto-suppressed via SendGrid webhook: {normalized.RawEventType}",
                    Source = "provider_webhook", Notes = $"providerMessageId: {normalized.ProviderMessageId ?? "unknown"}"
                });
            }
            catch (Exception ex) { _logger.LogError(ex, "Failed to upsert suppression from SendGrid event"); }
        }
    }

    private async Task ProcessTwilioEventAsync(Dictionary<string, string> formParams)
    {
        // ── LS-NOTIF-SMS-003: Inbound SMS processing with tenant resolution ──────
        // Twilio sends inbound messages to the same webhook URL with Direction=inbound or inbound-api.
        // These are NOT outbound status callbacks — detect, resolve tenant, then handle separately.
        var direction = formParams.GetValueOrDefault("Direction", "");
        if (InboundDirections.Contains(direction))
        {
            var fromPhone  = formParams.GetValueOrDefault("From", "");
            var toPhone    = formParams.GetValueOrDefault("To", "");
            var body       = formParams.GetValueOrDefault("Body", "");
            var messageSid = formParams.GetValueOrDefault("MessageSid") ?? formParams.GetValueOrDefault("SmsSid");

            _logger.LogInformation("Twilio inbound SMS from {From} to {To}: SID={Sid}",
                MaskPhone(fromPhone), MaskPhone(toPhone), messageSid);

            var keyword = _smsPreferenceSvc.ClassifyKeyword(body);

            // Resolve inbound `To` number to tenant/provider config.
            // This must happen BEFORE any preference mutation to ensure correct tenant scoping.
            InboundSmsResolutionResult resolution;
            try
            {
                resolution = await _inboundSmsResolver.ResolveAsync(toPhone);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "InboundSmsResolver threw unexpectedly for To={To}", MaskPhone(toPhone));
                resolution = InboundSmsResolutionResult.Unresolved(toPhone);
            }

            if (keyword != null)
            {
                if (resolution.Resolved)
                {
                    // Resolved tenant — process keyword with full provider context.
                    try
                    {
                        await _smsPreferenceSvc.ProcessInboundKeywordWithContextAsync(new InboundSmsKeywordContext
                        {
                            TenantId          = resolution.TenantId,
                            FromPhone         = fromPhone,
                            ToPhone           = toPhone,
                            Keyword           = keyword,
                            RawKeyword        = body.Trim(),
                            ProviderMessageId = messageSid,
                            ProviderConfigId  = resolution.ProviderConfigId,
                            Provider          = resolution.Provider ?? "twilio",
                            TenantResolved    = true,
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to process inbound SMS keyword for TenantId={Tid}", resolution.TenantId);
                    }
                }
                else
                {
                    // Unresolved — do NOT mutate any tenant-scoped preference state.
                    // Emit unresolved audit event and structured log only.
                    try
                    {
                        await _smsPreferenceSvc.AuditUnresolvedInboundAsync(fromPhone, toPhone, keyword, body.Trim(), messageSid);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to audit unresolved inbound SMS from {From}", MaskPhone(fromPhone));
                    }
                }
            }
            else
            {
                _logger.LogDebug("Twilio inbound SMS body is not a compliance keyword — ignoring. SID={Sid}", messageSid);
            }

            // Inbound messages are not outbound status events — return without further processing.
            return;
        }

        // ── Outbound status callback (existing behavior) ──────────────────────────
        var normalized = TwilioNormalizer.Normalize(formParams);
        var dedupKey = normalized.ProviderMessageId != null ? $"twilio:{normalized.ProviderMessageId}:{normalized.RawEventType}" : null;

        if (dedupKey != null && await _eventRepo.FindByDedupKeyAsync(dedupKey) != null) return;

        NotificationAttempt? attempt = null;
        if (normalized.ProviderMessageId != null)
            attempt = await _attemptRepo.FindByProviderMessageIdAsync(normalized.ProviderMessageId);

        var notificationId = attempt?.NotificationId;
        var tenantId = attempt?.TenantId;

        await _eventRepo.CreateAsync(new NotificationEvent
        {
            TenantId = tenantId, NotificationId = notificationId, NotificationAttemptId = attempt?.Id,
            Provider = "twilio", Channel = "sms", RawEventType = normalized.RawEventType,
            NormalizedEventType = normalized.NormalizedEventType, EventTimestamp = normalized.EventTimestamp,
            ProviderMessageId = normalized.ProviderMessageId, DedupKey = dedupKey
        });

        if (attempt != null)
        {
            await _deliveryStatusSvc.UpdateAttemptFromEventAsync(attempt.Id, normalized.NormalizedEventType);
            if (notificationId.HasValue)
                await _deliveryStatusSvc.UpdateNotificationFromEventAsync(notificationId.Value, normalized.NormalizedEventType);
        }

        if (tenantId.HasValue && normalized.RecipientPhone != null)
            await _contactHealthSvc.ProcessEventAsync(tenantId.Value, "sms", normalized.RecipientPhone, normalized.NormalizedEventType, normalized.RawEventType);

        if (tenantId.HasValue && notificationId.HasValue && DeliveryFinalEvents.Contains(normalized.NormalizedEventType))
            await _deliveryIssueSvc.ProcessEventAsync(new DeliveryIssueContext { TenantId = tenantId.Value, NotificationId = notificationId.Value, NotificationAttemptId = attempt?.Id, Channel = "sms", Provider = "twilio", NormalizedEventType = normalized.NormalizedEventType, RawEventType = normalized.RawEventType, RecipientContact = normalized.RecipientPhone, ErrorCode = normalized.ErrorCode, ErrorMessage = normalized.ErrorMessage });

        if (tenantId.HasValue && normalized.RecipientPhone != null && TwilioSuppressionMap.TryGetValue(normalized.NormalizedEventType, out var suppressionType))
        {
            try
            {
                var normalizedPhone = System.Text.RegularExpressions.Regex.Replace(normalized.RecipientPhone.Trim(), @"[^\d+]", "");
                await _suppressionRepo.UpsertFromEventAsync(new ContactSuppression
                {
                    TenantId = tenantId.Value, Channel = "sms", ContactValue = normalizedPhone,
                    SuppressionType = suppressionType, Reason = $"Auto-suppressed via Twilio webhook: {normalized.RawEventType}",
                    Source = "provider_webhook", Notes = $"providerMessageId: {normalized.ProviderMessageId ?? "unknown"}"
                });
            }
            catch (Exception ex) { _logger.LogError(ex, "Failed to upsert suppression from Twilio event"); }
        }
    }

    private static string MaskPhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return "***";
        var digits = System.Text.RegularExpressions.Regex.Replace(phone.Trim(), @"[^\d+]", "");
        return digits.Length > 3 ? digits[..3] + "***" : "***";
    }
}
