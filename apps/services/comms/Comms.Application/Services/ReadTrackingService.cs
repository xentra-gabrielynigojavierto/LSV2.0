using Microsoft.Extensions.Logging;
using Comms.Application.DTOs;
using Comms.Application.Interfaces;
using Comms.Application.Repositories;
using Comms.Domain.Entities;
using Comms.Domain.Enums;

namespace Comms.Application.Services;

public class ReadTrackingService : IReadTrackingService
{
    private readonly IConversationReadStateRepository _readStateRepo;
    private readonly IConversationRepository _conversationRepo;
    private readonly IParticipantRepository _participantRepo;
    private readonly IMessageRepository _messageRepo;
    private readonly IAuditPublisher _audit;
    private readonly ILogger<ReadTrackingService> _logger;

    public ReadTrackingService(
        IConversationReadStateRepository readStateRepo,
        IConversationRepository conversationRepo,
        IParticipantRepository participantRepo,
        IMessageRepository messageRepo,
        IAuditPublisher audit,
        ILogger<ReadTrackingService> logger)
    {
        _readStateRepo = readStateRepo;
        _conversationRepo = conversationRepo;
        _participantRepo = participantRepo;
        _messageRepo = messageRepo;
        _audit = audit;
        _logger = logger;
    }

    public async Task<ReadStateResponse> MarkReadAsync(
        Guid tenantId, Guid conversationId, Guid userId, CancellationToken ct = default)
    {
        _ = await _conversationRepo.GetByIdAsync(tenantId, conversationId, ct)
            ?? throw new KeyNotFoundException($"Conversation '{conversationId}' not found.");

        var participant = await _participantRepo.GetActiveByUserIdAsync(tenantId, conversationId, userId, ct)
            ?? throw new UnauthorizedAccessException("You are not an active participant in this conversation.");

        var allMessages = await _messageRepo.ListByConversationOrderedAsync(tenantId, conversationId, ct);
        var visibleMessages = FilterMessagesByVisibility(allMessages, participant);
        var latestVisible = visibleMessages.LastOrDefault();

        var readState = await _readStateRepo.GetAsync(tenantId, conversationId, userId, ct);

        if (readState is null)
        {
            readState = ConversationReadState.Create(
                tenantId, conversationId, userId,
                latestVisible?.Id, userId);
            await _readStateRepo.AddAsync(readState, ct);
        }
        else
        {
            if (latestVisible is not null)
                readState.MarkRead(latestVisible.Id, userId);
            else
                readState.MarkRead(readState.LastReadMessageId ?? Guid.Empty, userId);
            await _readStateRepo.UpdateAsync(readState, ct);
        }

        _logger.LogInformation("Conversation {ConversationId} marked as read by user {UserId}",
            conversationId, userId);

        _audit.Publish("ConversationMarkedRead", "Read",
            $"Conversation {conversationId} marked as read",
            tenantId, userId, "Conversation", conversationId.ToString());

        return new ReadStateResponse(
            conversationId, userId, false,
            readState.LastReadMessageId, readState.LastReadAtUtc);
    }

    public async Task<ReadStateResponse> MarkUnreadAsync(
        Guid tenantId, Guid conversationId, Guid userId, CancellationToken ct = default)
    {
        _ = await _conversationRepo.GetByIdAsync(tenantId, conversationId, ct)
            ?? throw new KeyNotFoundException($"Conversation '{conversationId}' not found.");

        _ = await _participantRepo.GetActiveByUserIdAsync(tenantId, conversationId, userId, ct)
            ?? throw new UnauthorizedAccessException("You are not an active participant in this conversation.");

        var readState = await _readStateRepo.GetAsync(tenantId, conversationId, userId, ct);

        if (readState is null)
        {
            return new ReadStateResponse(conversationId, userId, true, null, null);
        }

        readState.ClearReadState(userId);
        await _readStateRepo.UpdateAsync(readState, ct);

        _logger.LogInformation("Conversation {ConversationId} marked as unread by user {UserId}",
            conversationId, userId);

        return new ReadStateResponse(conversationId, userId, true, null, null);
    }

    public async Task<ReadStateResponse> GetReadStateAsync(
        Guid tenantId, Guid conversationId, Guid userId, CancellationToken ct = default)
    {
        _ = await _conversationRepo.GetByIdAsync(tenantId, conversationId, ct)
            ?? throw new KeyNotFoundException($"Conversation '{conversationId}' not found.");

        var participant = await _participantRepo.GetActiveByUserIdAsync(tenantId, conversationId, userId, ct)
            ?? throw new UnauthorizedAccessException("You are not an active participant in this conversation.");

        var readState = await _readStateRepo.GetAsync(tenantId, conversationId, userId, ct);
        var allMessages = await _messageRepo.ListByConversationOrderedAsync(tenantId, conversationId, ct);
        var visibleMessages = FilterMessagesByVisibility(allMessages, participant);
        var latestVisible = visibleMessages.LastOrDefault();

        bool isUnread;
        if (latestVisible is null)
            isUnread = false;
        else if (readState?.LastReadAtUtc is null)
            isUnread = true;
        else
            isUnread = latestVisible.SentAtUtc > readState.LastReadAtUtc;

        return new ReadStateResponse(
            conversationId, userId, isUnread,
            readState?.LastReadMessageId, readState?.LastReadAtUtc);
    }

    private static List<Message> FilterMessagesByVisibility(
        List<Message> messages, ConversationParticipant participant)
    {
        if (participant.ParticipantType == ParticipantType.InternalUser)
            return messages;

        return messages
            .Where(m => m.VisibilityType == VisibilityType.SharedExternal)
            .ToList();
    }
}
