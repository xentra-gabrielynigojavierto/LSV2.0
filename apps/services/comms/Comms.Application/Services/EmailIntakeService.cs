using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Comms.Application.DTOs;
using Comms.Application.Interfaces;
using Comms.Application.Repositories;
using Comms.Domain.Entities;
using Comms.Domain.Enums;

namespace Comms.Application.Services;

public partial class EmailIntakeService : IEmailIntakeService
{
    private readonly IEmailMessageReferenceRepository _emailRefRepo;
    private readonly IExternalParticipantIdentityRepository _identityRepo;
    private readonly IConversationRepository _conversationRepo;
    private readonly IMessageRepository _messageRepo;
    private readonly IParticipantRepository _participantRepo;
    private readonly IMessageAttachmentRepository _attachmentRepo;
    private readonly IEmailRecipientRecordRepository _recipientRepo;
    private readonly IDocumentServiceClient _documentClient;
    private readonly IOperationalService _operationalService;
    private readonly IConversationQueueRepository _queueRepo;
    private readonly IConversationAssignmentRepository _assignmentRepo;
    private readonly IConversationTimelineService _timeline;
    private readonly IAuditPublisher _audit;
    private readonly ILogger<EmailIntakeService> _logger;

    private static readonly Guid SystemUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Regex ConversationTokenRegex = MyConversationTokenRegex();

    public EmailIntakeService(
        IEmailMessageReferenceRepository emailRefRepo,
        IExternalParticipantIdentityRepository identityRepo,
        IConversationRepository conversationRepo,
        IMessageRepository messageRepo,
        IParticipantRepository participantRepo,
        IMessageAttachmentRepository attachmentRepo,
        IEmailRecipientRecordRepository recipientRepo,
        IDocumentServiceClient documentClient,
        IOperationalService operationalService,
        IConversationQueueRepository queueRepo,
        IConversationAssignmentRepository assignmentRepo,
        IConversationTimelineService timeline,
        IAuditPublisher audit,
        ILogger<EmailIntakeService> logger)
    {
        _emailRefRepo = emailRefRepo;
        _identityRepo = identityRepo;
        _conversationRepo = conversationRepo;
        _messageRepo = messageRepo;
        _participantRepo = participantRepo;
        _attachmentRepo = attachmentRepo;
        _recipientRepo = recipientRepo;
        _documentClient = documentClient;
        _operationalService = operationalService;
        _queueRepo = queueRepo;
        _assignmentRepo = assignmentRepo;
        _timeline = timeline;
        _audit = audit;
        _logger = logger;
    }

    public async Task<InboundEmailIntakeResponse> ProcessInboundAsync(
        InboundEmailIntakeRequest request, CancellationToken ct = default)
    {
        if (request.TenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required for email intake.");
        if (request.OrgId == Guid.Empty)
            throw new ArgumentException("OrgId is required for email intake.");

        var normalizedFrom = EmailMessageReference.NormalizeEmail(request.FromEmail);

        var existing = await _emailRefRepo.FindByInternetMessageIdAsync(request.TenantId, request.InternetMessageId, ct);
        if (existing is not null)
        {
            _logger.LogWarning("Duplicate inbound email ignored: InternetMessageId={Id}", request.InternetMessageId);
            return new InboundEmailIntakeResponse(
                existing.ConversationId, false, false,
                existing.MessageId ?? Guid.Empty, "Duplicate", 0, existing.Id);
        }

        var (conversationId, matchStrategy) = await ResolveConversationAsync(request, ct);

        bool createdNewConversation = false;
        bool createdNewParticipant = false;
        Conversation conversation;

        if (conversationId.HasValue)
        {
            conversation = await _conversationRepo.GetByIdAsync(request.TenantId, conversationId.Value, ct)
                ?? throw new KeyNotFoundException($"Matched conversation '{conversationId}' not found.");
        }
        else
        {
            conversation = Conversation.Create(
                request.TenantId, request.OrgId, "SYNQ_COMMS",
                ContextType.General, $"email-{Guid.NewGuid():N}",
                request.Subject, VisibilityType.SharedExternal,
                SystemUserId);
            await _conversationRepo.AddAsync(conversation, ct);
            createdNewConversation = true;
            matchStrategy = MatchStrategy.NewConversation;

            _logger.LogInformation(
                "New conversation {ConversationId} created from inbound email {InternetMessageId}",
                conversation.Id, request.InternetMessageId);

            _audit.Publish("InboundEmailNewConversation", "Created",
                $"New conversation created from inbound email: {request.Subject}",
                request.TenantId, null, "Conversation", conversation.Id.ToString(),
                metadata: $"{{\"internetMessageId\":\"{request.InternetMessageId}\",\"fromEmail\":\"{normalizedFrom}\"}}");
        }

        var effectiveOrgId = createdNewConversation ? request.OrgId : conversation.OrgId;

        var (participantId, isNewParticipant) = await ResolveExternalParticipantAsync(
            request.TenantId, effectiveOrgId, conversation.Id,
            normalizedFrom, request.FromDisplayName, ct);
        createdNewParticipant = isNewParticipant;

        var messageBody = request.TextBody ?? request.HtmlBody ?? request.Subject;
        if (string.IsNullOrWhiteSpace(messageBody))
            messageBody = "(empty email body)";

        var message = Message.Create(
            conversation.Id, request.TenantId, effectiveOrgId,
            Channel.Email, Direction.Inbound,
            messageBody, VisibilityType.SharedExternal,
            SystemUserId,
            senderUserId: null,
            senderParticipantType: ParticipantType.ExternalContact,
            externalSenderName: request.FromDisplayName,
            externalSenderEmail: normalizedFrom);

        await _messageRepo.AddAsync(message, ct);

        conversation.TouchActivity();
        conversation.AutoTransitionToOpen(SystemUserId);
        if (conversation.Status == ConversationStatus.Closed)
            conversation.ReopenFromClosed(SystemUserId);
        await _conversationRepo.UpdateAsync(conversation, ct);

        var emailRef = EmailMessageReference.Create(
            request.TenantId, conversation.Id, message.Id,
            request.InternetMessageId, EmailDirection.Inbound,
            normalizedFrom, request.ToAddresses, request.Subject,
            null,
            providerMessageId: request.ProviderMessageId,
            inReplyToMessageId: request.InReplyToMessageId,
            referencesHeader: request.ReferencesHeader,
            providerThreadId: request.ProviderThreadId,
            fromDisplayName: request.FromDisplayName,
            ccAddresses: request.CcAddresses,
            receivedAtUtc: request.ReceivedAtUtc);

        await _emailRefRepo.AddAsync(emailRef, ct);

        var recipientExpansionCount = await ProcessInboundRecipientsAsync(
            request, emailRef, conversation, effectiveOrgId, normalizedFrom, ct);

        int attachmentCount = 0;
        if (request.Attachments is { Count: > 0 })
        {
            foreach (var att in request.Attachments)
            {
                if (att.DocumentId.HasValue && att.DocumentId.Value != Guid.Empty)
                {
                    var validation = await _documentClient.ValidateDocumentAsync(
                        att.DocumentId.Value, request.TenantId, ct);
                    if (!validation.Exists || validation.TenantId != request.TenantId)
                    {
                        _logger.LogWarning(
                            "Skipping attachment {DocumentId} — document validation failed for tenant {TenantId}",
                            att.DocumentId.Value, request.TenantId);
                        continue;
                    }

                    var attachment = MessageAttachment.Create(
                        request.TenantId, conversation.Id, message.Id,
                        att.DocumentId.Value, att.FileName, att.ContentType,
                        att.FileSizeBytes, SystemUserId);
                    await _attachmentRepo.AddAsync(attachment, ct);
                    attachmentCount++;
                }
            }
        }

        _audit.Publish("InboundEmailReceived", "Created",
            $"Inbound email processed: {request.Subject}",
            request.TenantId, null, "EmailMessageReference", emailRef.Id.ToString(),
            metadata: $"{{\"internetMessageId\":\"{request.InternetMessageId}\",\"fromEmail\":\"{normalizedFrom}\",\"conversationId\":\"{conversation.Id}\",\"messageId\":\"{message.Id}\",\"matchedBy\":\"{matchStrategy}\",\"createdNewConversation\":{createdNewConversation.ToString().ToLower()},\"attachmentCount\":{attachmentCount},\"recipientExpansionCount\":{recipientExpansionCount}}}");

        if (!createdNewConversation)
        {
            _audit.Publish("InboundEmailMatched", "Updated",
                $"Inbound email matched to conversation {conversation.Id} via {matchStrategy}",
                request.TenantId, null, "Conversation", conversation.Id.ToString(),
                metadata: $"{{\"internetMessageId\":\"{request.InternetMessageId}\",\"matchedBy\":\"{matchStrategy}\"}}");
        }

        if (createdNewParticipant)
        {
            _audit.Publish("ExternalParticipantCreated", "Created",
                $"External participant created from email: {normalizedFrom}",
                request.TenantId, null, "ConversationParticipant", participantId.ToString(),
                metadata: $"{{\"normalizedEmail\":\"{normalizedFrom}\",\"conversationId\":\"{conversation.Id}\"}}");
        }

        _logger.LogInformation(
            "Inbound email processed: InternetMessageId={InternetMessageId} ConversationId={ConversationId} MessageId={MessageId} MatchedBy={MatchedBy}",
            request.InternetMessageId, conversation.Id, message.Id, matchStrategy);

        try
        {
            await _timeline.RecordAsync(
                request.TenantId, conversation.Id,
                Domain.Constants.TimelineEventTypes.EmailReceived,
                Domain.Constants.TimelineActorType.System,
                $"Email received from {request.FromDisplayName ?? normalizedFrom}",
                Domain.Constants.TimelineVisibility.SharedExternalSafe,
                request.ReceivedAtUtc,
                actorDisplayName: request.FromDisplayName,
                relatedMessageId: message.Id,
                metadataJson: $"{{\"fromEmail\":\"{normalizedFrom}\",\"subject\":\"{request.Subject}\",\"matchedBy\":\"{matchStrategy}\",\"attachmentCount\":{attachmentCount}}}",
                ct: ct);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to record timeline for inbound email on {ConversationId}", conversation.Id); }

        try
        {
            await InitializeOperationalStateAsync(request.TenantId, conversation, createdNewConversation, ct);
            await _operationalService.UpdateWaitingStateAsync(
                request.TenantId, conversation.Id, WaitingState.WaitingInternal, SystemUserId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize operational state for conversation {ConversationId}", conversation.Id);
        }

        return new InboundEmailIntakeResponse(
            conversation.Id, createdNewConversation, createdNewParticipant,
            message.Id, matchStrategy, attachmentCount, emailRef.Id);
    }

    private async Task InitializeOperationalStateAsync(
        Guid tenantId, Conversation conversation, bool isNew, CancellationToken ct)
    {
        await _operationalService.InitializeSlaAsync(
            tenantId, conversation.Id, ConversationPriority.Normal,
            DateTime.UtcNow, SystemUserId, ct);

        if (isNew)
        {
            var defaultQueue = await _queueRepo.GetDefaultAsync(tenantId, ct);
            if (defaultQueue is not null)
            {
                var existingAssignment = await _assignmentRepo.GetByConversationAsync(tenantId, conversation.Id, ct);
                if (existingAssignment is null)
                {
                    var assignment = ConversationAssignment.Create(
                        tenantId, conversation.Id, defaultQueue.Id, null, null, SystemUserId);
                    await _assignmentRepo.AddAsync(assignment, ct);

                    _logger.LogInformation(
                        "Conversation {ConversationId} auto-assigned to default queue {QueueId}",
                        conversation.Id, defaultQueue.Id);
                }
            }
        }
    }

    public async Task<List<EmailReferenceResponse>> ListEmailReferencesAsync(
        Guid tenantId, Guid conversationId, Guid userId, CancellationToken ct = default)
    {
        var participant = await _participantRepo.GetActiveByUserIdAsync(tenantId, conversationId, userId, ct)
            ?? throw new UnauthorizedAccessException("You are not an active participant in this conversation.");

        var refs = await _emailRefRepo.ListByConversationAsync(tenantId, conversationId, ct);
        return refs.Select(ToResponse).ToList();
    }

    private async Task<int> ProcessInboundRecipientsAsync(
        InboundEmailIntakeRequest request,
        EmailMessageReference emailRef,
        Conversation conversation,
        Guid effectiveOrgId,
        string normalizedFrom,
        CancellationToken ct)
    {
        var recipientRecords = new List<EmailRecipientRecord>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        seen.Add(normalizedFrom);

        if (!string.IsNullOrWhiteSpace(request.ToAddresses))
        {
            foreach (var raw in request.ToAddresses.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var email = raw.Trim();
                if (string.IsNullOrWhiteSpace(email)) continue;
                var normalized = EmailMessageReference.NormalizeEmail(email);
                if (!seen.Add(normalized)) continue;

                recipientRecords.Add(EmailRecipientRecord.Create(
                    request.TenantId, conversation.Id, emailRef.Id,
                    normalized, RecipientType.To, null,
                    SystemUserId, "INBOUND_TO"));
            }
        }

        int participantExpansionCount = 0;

        if (!string.IsNullOrWhiteSpace(request.CcAddresses))
        {
            foreach (var raw in request.CcAddresses.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var email = raw.Trim();
                if (string.IsNullOrWhiteSpace(email)) continue;
                var normalized = EmailMessageReference.NormalizeEmail(email);
                if (!seen.Add(normalized)) continue;

                var record = EmailRecipientRecord.Create(
                    request.TenantId, conversation.Id, emailRef.Id,
                    normalized, RecipientType.Cc, null,
                    SystemUserId, "INBOUND_CC");

                Guid participantId;
                bool isNew;
                try
                {
                    (participantId, isNew) = await ResolveExternalParticipantAsync(
                        request.TenantId, effectiveOrgId, conversation.Id,
                        normalized, null, ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex,
                        "CC participant resolution failed for {Email} in conversation {ConversationId}, skipping expansion",
                        normalized, conversation.Id);
                    recipientRecords.Add(record);
                    continue;
                }

                record.LinkParticipant(participantId, SystemUserId);

                if (isNew)
                {
                    participantExpansionCount++;
                    _audit.Publish("ExternalParticipantExpandedFromCc", "Created",
                        $"External participant auto-created from CC: {normalized}",
                        request.TenantId, null, "ConversationParticipant", participantId.ToString(),
                        metadata: $"{{\"normalizedEmail\":\"{normalized}\",\"conversationId\":\"{conversation.Id}\",\"source\":\"INBOUND_CC\"}}");
                }
                else
                {
                    _audit.Publish("ExternalIdentityReusedFromCc", "Updated",
                        $"External identity reused from CC: {normalized}",
                        request.TenantId, null, "ConversationParticipant", participantId.ToString(),
                        metadata: $"{{\"normalizedEmail\":\"{normalized}\",\"conversationId\":\"{conversation.Id}\",\"source\":\"INBOUND_CC\"}}");
                }

                recipientRecords.Add(record);
            }
        }

        if (recipientRecords.Count > 0)
            await _recipientRepo.AddRangeAsync(recipientRecords, ct);

        if (recipientRecords.Count > 0)
        {
            _audit.Publish("InboundRecipientRecordsCreated", "Created",
                $"Inbound recipient records created: {recipientRecords.Count}",
                request.TenantId, null, "EmailMessageReference", emailRef.Id.ToString(),
                metadata: $"{{\"conversationId\":\"{conversation.Id}\",\"toCount\":{recipientRecords.Count(r => r.RecipientType == RecipientType.To)},\"ccCount\":{recipientRecords.Count(r => r.RecipientType == RecipientType.Cc)},\"participantExpansionCount\":{participantExpansionCount}}}");
        }

        return participantExpansionCount;
    }

    private async Task<(Guid? ConversationId, string MatchStrategy)> ResolveConversationAsync(
        InboundEmailIntakeRequest request, CancellationToken ct)
    {
        var tokenMatch = ExtractConversationToken(request.Subject);
        if (tokenMatch.HasValue)
        {
            var conv = await _conversationRepo.GetByIdAsync(request.TenantId, tokenMatch.Value, ct);
            if (conv is not null)
            {
                _logger.LogInformation("Matched by conversation token: {ConversationId}", conv.Id);
                return (conv.Id, MatchStrategy.ConversationToken);
            }
        }

        if (!string.IsNullOrWhiteSpace(request.InReplyToMessageId))
        {
            var replyMatches = await _emailRefRepo.FindByInReplyToAsync(
                request.TenantId, request.InReplyToMessageId.Trim(), ct);
            if (replyMatches.Count > 0)
            {
                _logger.LogInformation("Matched by In-Reply-To: {ConversationId}", replyMatches[0].ConversationId);
                return (replyMatches[0].ConversationId, MatchStrategy.InReplyTo);
            }
        }

        if (!string.IsNullOrWhiteSpace(request.ReferencesHeader))
        {
            var refIds = ParseReferencesHeader(request.ReferencesHeader);
            if (refIds.Count > 0)
            {
                var refMatch = await _emailRefRepo.FindConversationByReferencesAsync(request.TenantId, refIds, ct);
                if (refMatch is not null)
                {
                    _logger.LogInformation("Matched by References header: {ConversationId}", refMatch.ConversationId);
                    return (refMatch.ConversationId, MatchStrategy.References);
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(request.ProviderThreadId))
        {
            var threadMatches = await _emailRefRepo.FindByProviderThreadIdAsync(
                request.TenantId, request.ProviderThreadId.Trim(), ct);
            if (threadMatches.Count > 0)
            {
                _logger.LogInformation("Matched by provider thread: {ConversationId}", threadMatches[0].ConversationId);
                return (threadMatches[0].ConversationId, MatchStrategy.ProviderThread);
            }
        }

        return (null, MatchStrategy.NewConversation);
    }

    private async Task<(Guid ParticipantId, bool IsNew)> ResolveExternalParticipantAsync(
        Guid tenantId, Guid orgId, Guid conversationId,
        string normalizedEmail, string? displayName, CancellationToken ct)
    {
        var existingParticipant = await _participantRepo.FindActiveAsync(
            tenantId, conversationId, null, normalizedEmail, ct);

        if (existingParticipant is not null)
            return (existingParticipant.Id, false);

        var identity = await _identityRepo.FindByEmailAsync(tenantId, normalizedEmail, ct);
        if (identity is null)
        {
            identity = ExternalParticipantIdentity.Create(
                tenantId, normalizedEmail, null, displayName);
            await _identityRepo.AddAsync(identity, ct);
        }
        else if (!string.IsNullOrWhiteSpace(displayName) && identity.DisplayName != displayName)
        {
            identity.UpdateDisplayName(displayName, null);
            await _identityRepo.UpdateAsync(identity, ct);
        }

        var participant = ConversationParticipant.Create(
            conversationId, tenantId, orgId,
            ParticipantType.ExternalContact,
            ParticipantRole.Participant,
            canReply: true,
            createdByUserId: SystemUserId,
            externalName: displayName,
            externalEmail: normalizedEmail);

        await _participantRepo.AddAsync(participant, ct);

        identity.LinkParticipant(participant.Id, null);
        await _identityRepo.UpdateAsync(identity, ct);

        return (participant.Id, true);
    }

    private static Guid? ExtractConversationToken(string subject)
    {
        var match = ConversationTokenRegex.Match(subject);
        if (match.Success && Guid.TryParse(match.Groups[1].Value, out var conversationId))
            return conversationId;
        return null;
    }

    private static List<string> ParseReferencesHeader(string referencesHeader)
    {
        var results = new List<string>();
        var parts = referencesHeader.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (!string.IsNullOrWhiteSpace(trimmed))
                results.Add(trimmed);
        }
        return results;
    }

    private static EmailReferenceResponse ToResponse(EmailMessageReference e) => new(
        e.Id, e.ConversationId, e.MessageId,
        e.InternetMessageId, e.ProviderMessageId, e.ProviderThreadId,
        e.InReplyToMessageId, e.EmailDirection,
        e.FromEmail, e.FromDisplayName,
        e.ToAddresses, e.CcAddresses, e.Subject,
        e.ReceivedAtUtc, e.SentAtUtc, e.CreatedAtUtc);

    [GeneratedRegex(@"\[COMMS-([0-9a-fA-F\-]{36})\]", RegexOptions.Compiled)]
    private static partial Regex MyConversationTokenRegex();
}
