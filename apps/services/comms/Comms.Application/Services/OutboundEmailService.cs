using Microsoft.Extensions.Logging;
using Comms.Application.DTOs;
using Comms.Application.Interfaces;
using Comms.Application.Repositories;
using Comms.Domain.Entities;
using Comms.Domain.Enums;

namespace Comms.Application.Services;

public class OutboundEmailService : IOutboundEmailService
{
    private readonly IConversationRepository _conversationRepo;
    private readonly IMessageRepository _messageRepo;
    private readonly IParticipantRepository _participantRepo;
    private readonly IMessageAttachmentRepository _attachmentRepo;
    private readonly IEmailMessageReferenceRepository _emailRefRepo;
    private readonly IEmailDeliveryStateRepository _deliveryRepo;
    private readonly IEmailRecipientRecordRepository _recipientRepo;
    private readonly ITenantEmailSenderConfigRepository _senderConfigRepo;
    private readonly IEmailTemplateConfigRepository _templateConfigRepo;
    private readonly INotificationsServiceClient _notificationsClient;
    private readonly IOperationalService _operationalService;
    private readonly IConversationTimelineService _timeline;
    private readonly IAuditPublisher _audit;
    private readonly ILogger<OutboundEmailService> _logger;

    private static readonly Guid SystemUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public OutboundEmailService(
        IConversationRepository conversationRepo,
        IMessageRepository messageRepo,
        IParticipantRepository participantRepo,
        IMessageAttachmentRepository attachmentRepo,
        IEmailMessageReferenceRepository emailRefRepo,
        IEmailDeliveryStateRepository deliveryRepo,
        IEmailRecipientRecordRepository recipientRepo,
        ITenantEmailSenderConfigRepository senderConfigRepo,
        IEmailTemplateConfigRepository templateConfigRepo,
        INotificationsServiceClient notificationsClient,
        IOperationalService operationalService,
        IConversationTimelineService timeline,
        IAuditPublisher audit,
        ILogger<OutboundEmailService> logger)
    {
        _conversationRepo = conversationRepo;
        _messageRepo = messageRepo;
        _participantRepo = participantRepo;
        _attachmentRepo = attachmentRepo;
        _emailRefRepo = emailRefRepo;
        _deliveryRepo = deliveryRepo;
        _recipientRepo = recipientRepo;
        _senderConfigRepo = senderConfigRepo;
        _templateConfigRepo = templateConfigRepo;
        _notificationsClient = notificationsClient;
        _operationalService = operationalService;
        _timeline = timeline;
        _audit = audit;
        _logger = logger;
    }

    public async Task<SendOutboundEmailResponse> SendOutboundAsync(
        SendOutboundEmailRequest request, Guid tenantId, Guid orgId, Guid userId,
        CancellationToken ct = default)
    {
        var conversation = await _conversationRepo.GetByIdAsync(tenantId, request.ConversationId, ct)
            ?? throw new KeyNotFoundException($"Conversation '{request.ConversationId}' not found.");

        var participant = await _participantRepo.GetActiveByUserIdAsync(tenantId, request.ConversationId, userId, ct)
            ?? throw new UnauthorizedAccessException("You are not an active participant in this conversation.");

        if (participant.ParticipantType != ParticipantType.InternalUser)
            throw new UnauthorizedAccessException("Only internal users can send outbound email.");

        if (!participant.CanReply)
            throw new UnauthorizedAccessException("You do not have reply permissions in this conversation.");

        var message = await _messageRepo.GetByIdAsync(tenantId, request.ConversationId, request.MessageId, ct)
            ?? throw new KeyNotFoundException($"Message '{request.MessageId}' not found.");

        if (message.VisibilityType != VisibilityType.SharedExternal)
        {
            _audit.Publish("OutboundEmailRejected", "Rejected",
                $"Outbound email rejected: message visibility is {message.VisibilityType}, not SharedExternal",
                tenantId, userId, "Message", message.Id.ToString(),
                metadata: $"{{\"reason\":\"visibility_mismatch\",\"visibilityType\":\"{message.VisibilityType}\"}}");

            throw new InvalidOperationException(
                $"Only messages with SharedExternal visibility can be sent as outbound email. Current: {message.VisibilityType}");
        }

        if (message.Channel == Channel.SystemNote)
        {
            _audit.Publish("OutboundEmailRejected", "Rejected",
                "Outbound email rejected: SystemNote messages cannot be sent externally",
                tenantId, userId, "Message", message.Id.ToString());

            throw new InvalidOperationException("SystemNote messages cannot be sent as outbound email.");
        }

        var existingOutbound = await _emailRefRepo.FindByMessageIdAsync(tenantId, message.Id, ct);
        if (existingOutbound is not null && existingOutbound.EmailDirection == EmailDirection.Outbound)
            throw new InvalidOperationException("An outbound email has already been sent for this message.");

        var internetMessageId = EmailMessageReference.GenerateInternetMessageId(conversation.Id);

        var (fromEmail, fromDisplayName, replyToEmail, senderConfigId, senderConfigEmail) =
            await ResolveSenderAsync(tenantId, userId, request.SenderConfigId, ct);

        var (subject, bodyText, bodyHtml, templateConfigId, templateKey, compositionMode) =
            await ResolveCompositionAsync(
                tenantId, userId, request, conversation.Subject,
                message.BodyPlainText ?? message.Body, message.Body, ct);

        string? inReplyToMessageId = null;
        string? referencesHeader = null;
        Guid? matchedReplyReferenceId = null;

        if (request.ReplyToEmailReferenceId.HasValue)
        {
            var replyRef = await _emailRefRepo.GetByIdAsync(tenantId, request.ReplyToEmailReferenceId.Value, ct);
            if (replyRef is not null && replyRef.ConversationId == conversation.Id)
            {
                inReplyToMessageId = replyRef.InternetMessageId;
                matchedReplyReferenceId = replyRef.Id;

                var chainRefs = new List<string>();
                if (!string.IsNullOrWhiteSpace(replyRef.ReferencesHeader))
                    chainRefs.AddRange(replyRef.ReferencesHeader.Split(' ', StringSplitOptions.RemoveEmptyEntries));
                if (!string.IsNullOrWhiteSpace(replyRef.InternetMessageId))
                    chainRefs.Add(replyRef.InternetMessageId);
                referencesHeader = string.Join(" ", chainRefs.Distinct());
            }
        }
        else
        {
            var latestRef = await _emailRefRepo.GetLatestByConversationAsync(tenantId, conversation.Id, ct);
            if (latestRef is not null)
            {
                inReplyToMessageId = latestRef.InternetMessageId;
                matchedReplyReferenceId = latestRef.Id;

                var chainRefs = new List<string>();
                if (!string.IsNullOrWhiteSpace(latestRef.ReferencesHeader))
                    chainRefs.AddRange(latestRef.ReferencesHeader.Split(' ', StringSplitOptions.RemoveEmptyEntries));
                if (!string.IsNullOrWhiteSpace(latestRef.InternetMessageId))
                    chainRefs.Add(latestRef.InternetMessageId);
                referencesHeader = string.Join(" ", chainRefs.Distinct());
            }
        }

        var attachments = new List<OutboundAttachmentDescriptor>();
        if (request.AttachmentDocumentIds is { Count: > 0 })
        {
            var messageAttachments = await _attachmentRepo.ListByMessageAsync(tenantId, message.Id, ct);
            foreach (var docId in request.AttachmentDocumentIds)
            {
                var att = messageAttachments.FirstOrDefault(a => a.DocumentId == docId && a.IsActive);
                if (att is not null)
                {
                    attachments.Add(new OutboundAttachmentDescriptor(
                        att.DocumentId, att.FileName, att.ContentType, att.FileSizeBytes));
                }
            }
        }

        var sendAttemptId = Guid.NewGuid();
        var idempotencyKey = $"comms-outbound-{sendAttemptId}";
        var payload = new OutboundEmailPayload(
            TenantId: tenantId,
            FromEmail: fromEmail,
            FromDisplayName: fromDisplayName,
            ToAddresses: request.ToAddresses,
            CcAddresses: request.CcAddresses,
            BccAddresses: request.BccAddresses,
            Subject: subject,
            BodyText: bodyText,
            BodyHtml: bodyHtml,
            InternetMessageId: internetMessageId,
            InReplyToMessageId: inReplyToMessageId,
            ReferencesHeader: referencesHeader,
            IdempotencyKey: idempotencyKey,
            Attachments: attachments.Count > 0 ? attachments : null,
            ReplyToEmail: request.ReplyToOverride ?? replyToEmail,
            TemplateKey: templateKey,
            TemplateData: request.TemplateVariables);

        var sendResult = await _notificationsClient.SendEmailAsync(payload, ct);

        if (!sendResult.Success)
        {
            _audit.Publish("OutboundEmailFailed", "Failed",
                $"Outbound email failed: {subject}",
                tenantId, userId, "Message", message.Id.ToString(),
                metadata: $"{{\"internetMessageId\":\"{internetMessageId}\",\"conversationId\":\"{conversation.Id}\",\"messageId\":\"{message.Id}\",\"toAddresses\":\"{request.ToAddresses}\",\"errorMessage\":\"{sendResult.ErrorMessage}\"}}");

            _logger.LogWarning(
                "Outbound email failed: ConversationId={ConversationId} MessageId={MessageId} Error={Error}",
                conversation.Id, message.Id, sendResult.ErrorMessage);

            throw new InvalidOperationException(
                $"Failed to submit outbound email to Notifications service: {sendResult.ErrorMessage}");
        }

        var emailRef = EmailMessageReference.Create(
            tenantId, conversation.Id, message.Id,
            internetMessageId, EmailDirection.Outbound,
            fromEmail, request.ToAddresses, subject,
            userId,
            inReplyToMessageId: inReplyToMessageId,
            referencesHeader: referencesHeader,
            ccAddresses: request.CcAddresses,
            fromDisplayName: fromDisplayName);

        emailRef.SetCompositionMetadata(
            senderConfigId, senderConfigEmail,
            templateConfigId, templateKey,
            compositionMode, userId);

        await _emailRefRepo.AddAsync(emailRef, ct);

        var recipientRecords = BuildRecipientRecords(
            tenantId, conversation.Id, emailRef.Id,
            request.ToAddresses, request.CcAddresses, request.BccAddresses,
            userId);
        if (recipientRecords.Count > 0)
        {
            await _recipientRepo.AddRangeAsync(recipientRecords, ct);

            var visibleCount = recipientRecords.Count(r => r.RecipientVisibility == RecipientVisibility.Visible);
            var hiddenCount = recipientRecords.Count(r => r.RecipientVisibility == RecipientVisibility.Hidden);

            _audit.Publish("OutboundRecipientRecordsCreated", "Created",
                $"Outbound recipient records created: {visibleCount} visible, {hiddenCount} hidden",
                tenantId, userId, "EmailMessageReference", emailRef.Id.ToString(),
                metadata: $"{{\"conversationId\":\"{conversation.Id}\",\"visibleCount\":{visibleCount},\"hiddenCount\":{hiddenCount},\"toCount\":{recipientRecords.Count(r => r.RecipientType == RecipientType.To)},\"ccCount\":{recipientRecords.Count(r => r.RecipientType == RecipientType.Cc)},\"bccCount\":{recipientRecords.Count(r => r.RecipientType == RecipientType.Bcc)}}}");
        }

        var deliveryState = EmailDeliveryState.Create(
            tenantId, conversation.Id, message.Id, emailRef.Id,
            sendResult.NotificationsRequestId?.ToString(),
            sendResult.ProviderUsed,
            sendResult.ProviderMessageId,
            DeliveryStatus.Queued,
            userId);

        await _deliveryRepo.AddAsync(deliveryState, ct);

        conversation.TouchActivity();
        conversation.AutoTransitionToOpen(userId);
        if (conversation.Status == ConversationStatus.Closed)
        {
            conversation.ReopenFromClosed(userId);
            _audit.Publish("ConversationReopened", "Updated",
                "Conversation reopened due to outbound email",
                tenantId, userId, "Conversation", conversation.Id.ToString());
        }
        await _conversationRepo.UpdateAsync(conversation, ct);

        _audit.Publish("OutboundEmailQueued", "Created",
            $"Outbound email queued: {subject}",
            tenantId, userId, "EmailMessageReference", emailRef.Id.ToString(),
            metadata: $"{{\"internetMessageId\":\"{internetMessageId}\",\"conversationId\":\"{conversation.Id}\",\"messageId\":\"{message.Id}\",\"toAddresses\":\"{request.ToAddresses}\",\"attachmentCount\":{attachments.Count},\"deliveryStatus\":\"{DeliveryStatus.Queued}\",\"notificationsRequestId\":\"{sendResult.NotificationsRequestId}\"}}");

        _logger.LogInformation(
            "Outbound email queued: ConversationId={ConversationId} MessageId={MessageId} InternetMessageId={InternetMessageId}",
            conversation.Id, message.Id, internetMessageId);

        try
        {
            await _timeline.RecordAsync(
                tenantId, conversation.Id,
                Domain.Constants.TimelineEventTypes.EmailSent,
                Domain.Constants.TimelineActorType.User,
                $"Email sent to {request.ToAddresses}",
                Domain.Constants.TimelineVisibility.SharedExternalSafe,
                DateTime.UtcNow,
                actorId: userId,
                relatedMessageId: message.Id,
                metadataJson: $"{{\"toAddresses\":\"{request.ToAddresses}\",\"subject\":\"{subject}\",\"attachmentCount\":{attachments.Count}}}",
                ct: ct);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to record timeline for outbound email on {ConversationId}", conversation.Id); }

        try
        {
            if (message.VisibilityType == VisibilityType.SharedExternal)
            {
                await _operationalService.SatisfyFirstResponseAsync(
                    tenantId, conversation.Id, DateTime.UtcNow, userId, ct);
                await _operationalService.UpdateWaitingStateAsync(
                    tenantId, conversation.Id, WaitingState.WaitingExternal, userId, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update operational state after outbound email for conversation {ConversationId}", conversation.Id);
        }

        return new SendOutboundEmailResponse(
            conversation.Id, message.Id, emailRef.Id,
            DeliveryStatus.Queued,
            sendResult.NotificationsRequestId,
            internetMessageId,
            matchedReplyReferenceId,
            attachments.Count,
            senderConfigId,
            senderConfigEmail ?? fromEmail,
            templateKey,
            templateConfigId,
            subject,
            compositionMode);
    }

    public async Task<bool> ProcessDeliveryStatusAsync(
        DeliveryStatusUpdateRequest request, Guid tenantId,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Processing delivery status callback: Provider={Provider} Status={Status} ProviderMessageId={ProviderMessageId} InternetMessageId={InternetMessageId} NotificationsRequestId={NotificationsRequestId}",
            request.Provider, request.Status, request.ProviderMessageId, request.InternetMessageId, request.NotificationsRequestId);

        EmailDeliveryState? deliveryState = null;
        string correlatedBy = "none";

        if (!string.IsNullOrWhiteSpace(request.ProviderMessageId))
        {
            deliveryState = await _deliveryRepo.FindByProviderMessageIdAsync(tenantId, request.ProviderMessageId, ct);
            if (deliveryState is not null) correlatedBy = "providerMessageId";
        }

        if (deliveryState is null && !string.IsNullOrWhiteSpace(request.NotificationsRequestId)
            && Guid.TryParse(request.NotificationsRequestId, out var notifReqId))
        {
            deliveryState = await _deliveryRepo.FindByNotificationsRequestIdAsync(tenantId, notifReqId, ct);
            if (deliveryState is not null) correlatedBy = "notificationsRequestId";
        }

        if (deliveryState is null && !string.IsNullOrWhiteSpace(request.InternetMessageId))
        {
            var emailRef = await _emailRefRepo.FindByInternetMessageIdAsync(tenantId, request.InternetMessageId, ct);
            if (emailRef is not null)
            {
                deliveryState = await _deliveryRepo.FindByEmailReferenceIdAsync(tenantId, emailRef.Id, ct);
                if (deliveryState is not null) correlatedBy = "internetMessageId";
            }
        }

        if (deliveryState is null)
        {
            _logger.LogWarning(
                "Delivery status callback unmatched: Provider={Provider} ProviderMessageId={ProviderMessageId} InternetMessageId={InternetMessageId} NotificationsRequestId={NotificationsRequestId}",
                request.Provider, request.ProviderMessageId, request.InternetMessageId, request.NotificationsRequestId);

            _audit.Publish("DeliveryCallbackUnmatched", "Rejected",
                "Delivery status callback could not be matched to any known delivery record",
                tenantId, null, "EmailDeliveryState", null,
                metadata: $"{{\"provider\":\"{request.Provider}\",\"providerMessageId\":\"{request.ProviderMessageId}\",\"internetMessageId\":\"{request.InternetMessageId}\",\"notificationsRequestId\":\"{request.NotificationsRequestId}\",\"status\":\"{request.Status}\"}}");

            return false;
        }

        var normalizedStatus = NormalizeDeliveryStatus(request.Status);
        var updated = deliveryState.UpdateStatus(
            normalizedStatus, request.StatusAtUtc,
            request.ErrorCode, request.ErrorMessage,
            request.RetryCount, request.ProviderMessageId,
            SystemUserId);

        if (!updated)
        {
            _logger.LogInformation(
                "Delivery status callback ignored (terminal or stale): DeliveryStateId={Id} CurrentStatus={CurrentStatus} IncomingStatus={IncomingStatus} CorrelatedBy={CorrelatedBy}",
                deliveryState.Id, deliveryState.DeliveryStatus, normalizedStatus, correlatedBy);

            _audit.Publish("DeliveryCallbackIgnored", "Ignored",
                $"Delivery callback ignored: current status is {deliveryState.DeliveryStatus} (terminal or stale timestamp)",
                tenantId, null, "EmailDeliveryState", deliveryState.Id.ToString(),
                metadata: $"{{\"currentStatus\":\"{deliveryState.DeliveryStatus}\",\"incomingStatus\":\"{normalizedStatus}\",\"correlatedBy\":\"{correlatedBy}\",\"provider\":\"{request.Provider}\"}}");

            return true;
        }

        await _deliveryRepo.UpdateAsync(deliveryState, ct);

        if (normalizedStatus == DeliveryStatus.Sent || normalizedStatus == DeliveryStatus.Delivered)
        {
            var emailRef = await _emailRefRepo.GetByIdAsync(tenantId, deliveryState.EmailMessageReferenceId, ct);
            if (emailRef is not null && emailRef.SentAtUtc is null)
            {
                emailRef.SetSentAtUtc(request.StatusAtUtc, SystemUserId);
                await _emailRefRepo.UpdateAsync(emailRef, ct);
            }
        }

        _audit.Publish("OutboundEmailDeliveryUpdate", "Updated",
            $"Delivery status updated to {normalizedStatus}",
            tenantId, null, "EmailDeliveryState", deliveryState.Id.ToString(),
            metadata: $"{{\"deliveryStatus\":\"{normalizedStatus}\",\"providerMessageId\":\"{request.ProviderMessageId}\",\"provider\":\"{request.Provider}\",\"emailMessageReferenceId\":\"{deliveryState.EmailMessageReferenceId}\",\"correlatedBy\":\"{correlatedBy}\"}}");

        _logger.LogInformation(
            "Delivery status updated: DeliveryStateId={Id} Status={Status} Provider={Provider} CorrelatedBy={CorrelatedBy}",
            deliveryState.Id, normalizedStatus, request.Provider, correlatedBy);

        return true;
    }

    public async Task<List<EmailDeliveryStateResponse>> ListDeliveryStatesAsync(
        Guid tenantId, Guid conversationId, Guid userId,
        CancellationToken ct = default)
    {
        var participant = await _participantRepo.GetActiveByUserIdAsync(tenantId, conversationId, userId, ct)
            ?? throw new UnauthorizedAccessException("You are not an active participant in this conversation.");

        var states = await _deliveryRepo.ListByConversationAsync(tenantId, conversationId, ct);
        return states.Select(ToResponse).ToList();
    }

    public async Task<ReplyAllPreviewResponse> GetReplyAllPreviewAsync(
        Guid tenantId, Guid conversationId, Guid userId,
        CancellationToken ct = default)
    {
        var participant = await _participantRepo.GetActiveByUserIdAsync(tenantId, conversationId, userId, ct)
            ?? throw new UnauthorizedAccessException("You are not an active participant in this conversation.");

        var conversation = await _conversationRepo.GetByIdAsync(tenantId, conversationId, ct)
            ?? throw new KeyNotFoundException($"Conversation '{conversationId}' not found.");

        var latestRef = await _emailRefRepo.GetLatestByConversationAsync(tenantId, conversationId, ct);
        if (latestRef is null)
        {
            return new ReplyAllPreviewResponse(conversationId, null,
                new List<ReplyAllRecipient>(), new List<ReplyAllRecipient>(),
                conversation.Subject);
        }

        var visibleRecipients = await _recipientRepo.ListVisibleByEmailReferenceAsync(
            tenantId, latestRef.Id, ct);

        var currentSenderEmail = latestRef.FromEmail?.Trim().ToLowerInvariant();

        var toRecipients = new List<ReplyAllRecipient>();
        var ccRecipients = new List<ReplyAllRecipient>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(currentSenderEmail))
            seen.Add(currentSenderEmail);

        if (!string.IsNullOrWhiteSpace(latestRef.FromEmail) &&
            !IsSystemSenderAddress(currentSenderEmail))
        {
            toRecipients.Add(new ReplyAllRecipient(
                EmailMessageReference.NormalizeEmail(latestRef.FromEmail),
                latestRef.FromDisplayName));
        }

        foreach (var r in visibleRecipients)
        {
            if (seen.Contains(r.NormalizedEmail))
                continue;
            seen.Add(r.NormalizedEmail);

            if (r.RecipientType == RecipientType.To)
                toRecipients.Add(new ReplyAllRecipient(r.NormalizedEmail, r.DisplayName));
            else if (r.RecipientType == RecipientType.Cc)
                ccRecipients.Add(new ReplyAllRecipient(r.NormalizedEmail, r.DisplayName));
        }

        _audit.Publish("ReplyAllRecipientsResolved", "Read",
            $"Reply-all recipients resolved: {toRecipients.Count} to, {ccRecipients.Count} cc",
            tenantId, userId, "Conversation", conversationId.ToString(),
            metadata: $"{{\"sourceEmailReferenceId\":\"{latestRef.Id}\",\"toCount\":{toRecipients.Count},\"ccCount\":{ccRecipients.Count}}}");

        return new ReplyAllPreviewResponse(
            conversationId, latestRef.Id, toRecipients, ccRecipients,
            conversation.Subject);
    }

    private static List<EmailRecipientRecord> BuildRecipientRecords(
        Guid tenantId, Guid conversationId, Guid emailRefId,
        string? toAddresses, string? ccAddresses, string? bccAddresses,
        Guid? createdByUserId)
    {
        var records = new List<EmailRecipientRecord>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddRecipients(records, seen, tenantId, conversationId, emailRefId,
            toAddresses, RecipientType.To, "OUTBOUND_TO", createdByUserId);
        AddRecipients(records, seen, tenantId, conversationId, emailRefId,
            ccAddresses, RecipientType.Cc, "OUTBOUND_CC", createdByUserId);
        AddRecipients(records, seen, tenantId, conversationId, emailRefId,
            bccAddresses, RecipientType.Bcc, "OUTBOUND_BCC", createdByUserId);

        return records;
    }

    private static void AddRecipients(
        List<EmailRecipientRecord> records,
        HashSet<string> seen,
        Guid tenantId, Guid conversationId, Guid emailRefId,
        string? addresses, string recipientType, string source,
        Guid? createdByUserId)
    {
        if (string.IsNullOrWhiteSpace(addresses)) return;

        foreach (var raw in addresses.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var email = raw.Trim();
            if (string.IsNullOrWhiteSpace(email)) continue;

            var normalized = EmailMessageReference.NormalizeEmail(email);
            if (!seen.Add(normalized)) continue;

            records.Add(EmailRecipientRecord.Create(
                tenantId, conversationId, emailRefId,
                normalized, recipientType, null,
                createdByUserId, source));
        }
    }

    private async Task<(string fromEmail, string fromDisplayName, string? replyToEmail, Guid? senderConfigId, string? senderConfigEmail)>
        ResolveSenderAsync(Guid tenantId, Guid userId, Guid? requestedSenderConfigId, CancellationToken ct)
    {
        TenantEmailSenderConfig? senderConfig = null;

        if (requestedSenderConfigId.HasValue)
        {
            senderConfig = await _senderConfigRepo.GetByIdAsync(tenantId, requestedSenderConfigId.Value, ct);
            if (senderConfig is null)
            {
                _audit.Publish("OutboundEmailRejected", "Rejected",
                    $"Sender config {requestedSenderConfigId.Value} not found",
                    tenantId, userId, "TenantEmailSenderConfig", requestedSenderConfigId.Value.ToString(),
                    metadata: $"{{\"reason\":\"sender_config_not_found\"}}");
                throw new InvalidOperationException($"Sender configuration '{requestedSenderConfigId.Value}' not found for this tenant.");
            }
        }
        else
        {
            senderConfig = await _senderConfigRepo.GetDefaultAsync(tenantId, ct);
        }

        if (senderConfig is not null)
        {
            if (!senderConfig.CanSend())
            {
                _audit.Publish("OutboundEmailRejected", "Rejected",
                    $"Sender config {senderConfig.Id} is not usable (active={senderConfig.IsActive}, verification={senderConfig.VerificationStatus})",
                    tenantId, userId, "TenantEmailSenderConfig", senderConfig.Id.ToString(),
                    metadata: $"{{\"reason\":\"sender_config_unusable\",\"isActive\":{senderConfig.IsActive.ToString().ToLower()},\"verificationStatus\":\"{senderConfig.VerificationStatus}\"}}");
                throw new InvalidOperationException(
                    $"Sender configuration '{senderConfig.Id}' cannot be used: it must be active and verified.");
            }

            _audit.Publish("OutboundEmailSenderResolved", "Resolved",
                $"Sender resolved: {senderConfig.DisplayName} <{senderConfig.FromEmail}>",
                tenantId, userId, "TenantEmailSenderConfig", senderConfig.Id.ToString(),
                metadata: $"{{\"fromEmail\":\"{senderConfig.FromEmail}\",\"senderType\":\"{senderConfig.SenderType}\"}}");

            return (senderConfig.FromEmail, senderConfig.DisplayName,
                senderConfig.ReplyToEmail, senderConfig.Id, senderConfig.FromEmail);
        }

        return ("noreply@legalsynq.com", "LegalSynq Communications", null, null, null);
    }

    private async Task<(string subject, string bodyText, string bodyHtml, Guid? templateConfigId, string? templateKey, string compositionMode)>
        ResolveCompositionAsync(
            Guid tenantId, Guid userId, SendOutboundEmailRequest request,
            string conversationSubject, string messageBodyText, string messageBodyHtml,
            CancellationToken ct)
    {
        EmailTemplateConfig? template = null;
        string? resolvedTemplateKey = null;
        Guid? resolvedTemplateId = null;

        if (request.TemplateConfigId.HasValue)
        {
            template = await _templateConfigRepo.GetByIdAsync(request.TemplateConfigId.Value, ct);
            if (template is null)
            {
                _audit.Publish("OutboundEmailRejected", "Rejected",
                    $"Template config {request.TemplateConfigId.Value} not found",
                    tenantId, userId, "EmailTemplateConfig", request.TemplateConfigId.Value.ToString());
                throw new InvalidOperationException($"Email template '{request.TemplateConfigId.Value}' not found.");
            }
        }
        else if (!string.IsNullOrWhiteSpace(request.TemplateKey))
        {
            template = await _templateConfigRepo.GetByKeyAsync(tenantId, request.TemplateKey, ct);
            if (template is null)
                template = await _templateConfigRepo.GetGlobalByKeyAsync(request.TemplateKey, ct);

            if (template is null)
            {
                _audit.Publish("OutboundEmailRejected", "Rejected",
                    $"Template key '{request.TemplateKey}' not found",
                    tenantId, userId, "EmailTemplateConfig", request.TemplateKey);
                throw new InvalidOperationException($"Email template with key '{request.TemplateKey}' not found.");
            }
        }

        if (template is not null)
        {
            if (!template.IsActive)
            {
                _audit.Publish("OutboundEmailRejected", "Rejected",
                    $"Template {template.Id} is inactive",
                    tenantId, userId, "EmailTemplateConfig", template.Id.ToString());
                throw new InvalidOperationException($"Email template '{template.Id}' is inactive.");
            }

            if (template.TenantId.HasValue && template.TenantId.Value != tenantId)
            {
                throw new UnauthorizedAccessException("Cannot use templates belonging to another tenant.");
            }

            resolvedTemplateKey = template.TemplateKey;
            resolvedTemplateId = template.Id;

            _audit.Publish("OutboundEmailTemplateResolved", "Resolved",
                $"Template resolved: {template.DisplayName} (key: {template.TemplateKey}, v{template.Version})",
                tenantId, userId, "EmailTemplateConfig", template.Id.ToString(),
                metadata: $"{{\"templateKey\":\"{template.TemplateKey}\",\"version\":{template.Version},\"templateScope\":\"{template.TemplateScope}\"}}");
        }

        if (request.SubjectOverride is not null || request.BodyTextOverride is not null || request.BodyHtmlOverride is not null)
        {
            var subject = request.SubjectOverride ?? conversationSubject;
            var bodyText = request.BodyTextOverride ?? messageBodyText;
            var bodyHtml = request.BodyHtmlOverride ?? messageBodyHtml;
            return (subject, bodyText, bodyHtml, resolvedTemplateId, resolvedTemplateKey, "EXPLICIT_OVERRIDE");
        }

        if (template is not null)
        {
            var renderedSubject = template.RenderSubject(request.TemplateVariables);
            var renderedBodyText = template.RenderBodyText(request.TemplateVariables);
            var renderedBodyHtml = template.RenderBodyHtml(request.TemplateVariables);

            var subject = !string.IsNullOrWhiteSpace(renderedSubject) ? renderedSubject : conversationSubject;
            var bodyText = !string.IsNullOrWhiteSpace(renderedBodyText) ? renderedBodyText : messageBodyText;
            var bodyHtml = !string.IsNullOrWhiteSpace(renderedBodyHtml) ? renderedBodyHtml : messageBodyHtml;

            return (subject, bodyText, bodyHtml, resolvedTemplateId, resolvedTemplateKey, "TEMPLATE");
        }

        return (conversationSubject, messageBodyText, messageBodyHtml, null, null, "MESSAGE_CONTENT");
    }

    private static bool IsSystemSenderAddress(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        var normalized = email.Trim().ToLowerInvariant();
        return normalized.EndsWith("@legalsynq.com") ||
               normalized.StartsWith("noreply@");
    }

    private static string NormalizeDeliveryStatus(string status)
    {
        return status?.Trim().ToLowerInvariant() switch
        {
            "pending" => DeliveryStatus.Pending,
            "queued" => DeliveryStatus.Queued,
            "sent" => DeliveryStatus.Sent,
            "delivered" => DeliveryStatus.Delivered,
            "failed" => DeliveryStatus.Failed,
            "bounced" or "bounce" => DeliveryStatus.Bounced,
            "deferred" => DeliveryStatus.Deferred,
            "suppressed" => DeliveryStatus.Suppressed,
            _ => DeliveryStatus.Unknown,
        };
    }

    private static EmailDeliveryStateResponse ToResponse(EmailDeliveryState e) => new(
        e.Id, e.ConversationId, e.MessageId, e.EmailMessageReferenceId,
        e.DeliveryStatus, e.ProviderName, e.ProviderMessageId,
        e.NotificationsRequestId, e.LastStatusAtUtc,
        e.LastErrorCode, e.LastErrorMessage, e.RetryCount, e.CreatedAtUtc);
}
