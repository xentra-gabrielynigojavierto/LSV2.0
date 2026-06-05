using Microsoft.Extensions.Logging;
using Comms.Application.DTOs;
using Comms.Application.Interfaces;
using Comms.Application.Repositories;
using Comms.Domain.Entities;
using Comms.Domain.Enums;

namespace Comms.Application.Services;

public class MessageService : IMessageService
{
    private readonly IMessageRepository _messageRepo;
    private readonly IConversationRepository _conversationRepo;
    private readonly IParticipantRepository _participantRepo;
    private readonly IConversationTimelineService _timeline;
    private readonly IMentionService _mentions;
    private readonly IAuditPublisher _audit;
    private readonly ILogger<MessageService> _logger;

    public MessageService(
        IMessageRepository messageRepo,
        IConversationRepository conversationRepo,
        IParticipantRepository participantRepo,
        IConversationTimelineService timeline,
        IMentionService mentions,
        IAuditPublisher audit,
        ILogger<MessageService> logger)
    {
        _messageRepo = messageRepo;
        _conversationRepo = conversationRepo;
        _participantRepo = participantRepo;
        _timeline = timeline;
        _mentions = mentions;
        _audit = audit;
        _logger = logger;
    }

    public async Task<MessageResponse> AddAsync(
        Guid tenantId, Guid orgId, Guid userId, Guid conversationId,
        AddMessageRequest request, CancellationToken ct = default)
    {
        var conversation = await _conversationRepo.GetByIdAsync(tenantId, conversationId, ct)
            ?? throw new KeyNotFoundException($"Conversation '{conversationId}' not found.");

        var participant = await _participantRepo.GetActiveByUserIdAsync(tenantId, conversationId, userId, ct)
            ?? throw new UnauthorizedAccessException("You are not an active participant in this conversation.");

        if (!participant.CanReply)
            throw new UnauthorizedAccessException("You do not have reply permission in this conversation.");

        if (participant.ParticipantType == ParticipantType.ExternalContact)
            throw new UnauthorizedAccessException("External contacts cannot post in-app messages in this version.");

        if (participant.ParticipantType == ParticipantType.System)
            throw new UnauthorizedAccessException("System participants cannot post interactive user messages.");

        if (request.Channel == Channel.SystemNote && request.VisibilityType == VisibilityType.SharedExternal)
            throw new InvalidOperationException("System notes cannot have SharedExternal visibility.");

        var message = Message.Create(
            conversationId, tenantId, orgId,
            request.Channel, request.Direction,
            request.Body, request.VisibilityType,
            userId,
            senderUserId: userId,
            senderParticipantType: participant.ParticipantType);

        await _messageRepo.AddAsync(message, ct);

        conversation.TouchActivity();
        conversation.AutoTransitionToOpen(userId);

        if (conversation.Status == ConversationStatus.Closed)
            conversation.ReopenFromClosed(userId);

        await _conversationRepo.UpdateAsync(conversation, ct);

        _logger.LogInformation("Message {MessageId} added to conversation {ConversationId}",
            message.Id, conversationId);

        _audit.Publish("MessageAdded", "Created", $"Message added to conversation {conversationId}",
            tenantId, userId, "Message", message.Id.ToString());

        var visibility = message.VisibilityType == VisibilityType.SharedExternal
            ? Domain.Constants.TimelineVisibility.SharedExternalSafe
            : Domain.Constants.TimelineVisibility.InternalOnly;

        try
        {
            await _timeline.RecordAsync(
                tenantId, conversationId,
                Domain.Constants.TimelineEventTypes.MessageSent,
                Domain.Constants.TimelineActorType.User,
                "Message sent",
                visibility,
                message.SentAtUtc,
                actorId: userId,
                relatedMessageId: message.Id,
                metadataJson: $"{{\"channel\":\"{message.Channel}\",\"direction\":\"{message.Direction}\",\"visibility\":\"{message.VisibilityType}\"}}",
                ct: ct);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to record timeline entry for message {MessageId}", message.Id); }

        try
        {
            await _mentions.ProcessMentionsAsync(tenantId, conversationId, message.Id, userId, request.Body, ct);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to process mentions for message {MessageId}", message.Id); }

        return ToResponse(message);
    }

    public async Task<List<MessageResponse>> ListByConversationAsync(
        Guid tenantId, Guid conversationId, Guid userId, CancellationToken ct = default)
    {
        var participant = await _participantRepo.GetActiveByUserIdAsync(tenantId, conversationId, userId, ct)
            ?? throw new UnauthorizedAccessException("You are not an active participant in this conversation.");

        var messages = await _messageRepo.ListByConversationOrderedAsync(tenantId, conversationId, ct);

        if (participant.ParticipantType != ParticipantType.InternalUser)
        {
            messages = messages
                .Where(m => m.VisibilityType == VisibilityType.SharedExternal)
                .ToList();
        }

        return messages.Select(ToResponse).ToList();
    }

    private static MessageResponse ToResponse(Message m)
    {
        var mentions = MentionParser.ExtractMentionedUserIds(m.Body);
        return new MessageResponse(
            m.Id, m.ConversationId,
            m.Channel, m.Direction, m.Body, m.VisibilityType,
            m.SentAtUtc, m.SenderUserId, m.SenderParticipantType,
            m.ExternalSenderName, m.ExternalSenderEmail,
            m.Status, m.CreatedAtUtc,
            Mentions: mentions.Count > 0 ? mentions : null);
    }
}
