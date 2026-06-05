using Microsoft.Extensions.Logging;
using Comms.Application.DTOs;
using Comms.Application.Interfaces;
using Comms.Application.Repositories;
using Comms.Domain.Entities;
using Comms.Domain.Enums;

namespace Comms.Application.Services;

public class ConversationService : IConversationService
{
    private readonly IConversationRepository _repo;
    private readonly IParticipantRepository _participantRepo;
    private readonly IMessageRepository _messageRepo;
    private readonly IConversationReadStateRepository _readStateRepo;
    private readonly IMessageAttachmentRepository _attachmentRepo;
    private readonly IOperationalService _operationalService;
    private readonly IConversationTimelineService _timeline;
    private readonly IAuditPublisher _audit;
    private readonly ILogger<ConversationService> _logger;

    public ConversationService(
        IConversationRepository repo,
        IParticipantRepository participantRepo,
        IMessageRepository messageRepo,
        IConversationReadStateRepository readStateRepo,
        IMessageAttachmentRepository attachmentRepo,
        IOperationalService operationalService,
        IConversationTimelineService timeline,
        IAuditPublisher audit,
        ILogger<ConversationService> logger)
    {
        _repo = repo;
        _participantRepo = participantRepo;
        _messageRepo = messageRepo;
        _readStateRepo = readStateRepo;
        _attachmentRepo = attachmentRepo;
        _operationalService = operationalService;
        _timeline = timeline;
        _audit = audit;
        _logger = logger;
    }

    public async Task<ConversationResponse> CreateAsync(
        Guid tenantId, Guid orgId, Guid userId,
        CreateConversationRequest request, CancellationToken ct = default)
    {
        var conversation = Conversation.Create(
            tenantId, orgId,
            request.ProductKey, request.ContextType, request.ContextId,
            request.Subject, request.VisibilityType,
            userId);

        await _repo.AddAsync(conversation, ct);

        _logger.LogInformation("Conversation {ConversationId} created for context {ContextType}/{ContextId}",
            conversation.Id, conversation.ContextType, conversation.ContextId);

        _audit.Publish("ConversationCreated", "Created", $"Conversation created: {request.Subject}",
            tenantId, userId, "Conversation", conversation.Id.ToString(),
            metadata: $"{{\"contextType\":\"{request.ContextType}\",\"contextId\":\"{request.ContextId}\",\"visibility\":\"{request.VisibilityType}\"}}");

        return ToResponse(conversation);
    }

    public async Task<ConversationResponse?> GetByIdAsync(
        Guid tenantId, Guid id, Guid? currentUserId = null, CancellationToken ct = default)
    {
        var conversation = await _repo.GetByIdAsync(tenantId, id, ct);
        if (conversation is null) return null;

        ConversationParticipant? participant = null;
        if (currentUserId.HasValue)
        {
            participant = await _participantRepo.GetActiveByUserIdAsync(tenantId, id, currentUserId.Value, ct);
            if (participant is null)
                throw new UnauthorizedAccessException("You are not a participant in this conversation.");
        }

        bool? isUnread = null;
        DateTime? lastReadAtUtc = null;
        if (currentUserId.HasValue && participant is not null)
        {
            var readState = await _readStateRepo.GetAsync(tenantId, id, currentUserId.Value, ct);
            var allMessages = await _messageRepo.ListByConversationOrderedAsync(tenantId, id, ct);
            var latestVisible = FilterMessagesByVisibility(allMessages, participant).LastOrDefault();
            isUnread = ComputeUnread(readState, latestVisible);
            lastReadAtUtc = readState?.LastReadAtUtc;
        }

        return ToResponse(conversation, isUnread, lastReadAtUtc);
    }

    public async Task<List<ConversationResponse>> ListByContextAsync(
        Guid tenantId, string contextType, string contextId, Guid? currentUserId = null, CancellationToken ct = default)
    {
        if (!ContextType.All.Contains(contextType))
            throw new ArgumentException($"Invalid context type: '{contextType}'.");

        var conversations = await _repo.ListByContextAsync(tenantId, contextType, contextId, ct);
        var results = new List<ConversationResponse>();

        foreach (var c in conversations)
        {
            bool? isUnread = null;
            DateTime? lastReadAtUtc = null;
            if (currentUserId.HasValue)
            {
                var participant = await _participantRepo.GetActiveByUserIdAsync(tenantId, c.Id, currentUserId.Value, ct);
                if (participant is null) continue;

                var readState = await _readStateRepo.GetAsync(tenantId, c.Id, currentUserId.Value, ct);
                var allMessages = await _messageRepo.ListByConversationOrderedAsync(tenantId, c.Id, ct);
                var latestVisible = FilterMessagesByVisibility(allMessages, participant).LastOrDefault();
                isUnread = ComputeUnread(readState, latestVisible);
                lastReadAtUtc = readState?.LastReadAtUtc;
            }
            results.Add(ToResponse(c, isUnread, lastReadAtUtc));
        }

        return results;
    }

    public async Task<ConversationResponse> UpdateStatusAsync(
        Guid tenantId, Guid id, Guid userId,
        UpdateConversationStatusRequest request, CancellationToken ct = default)
    {
        var conversation = await _repo.GetByIdAsync(tenantId, id, ct)
            ?? throw new KeyNotFoundException($"Conversation '{id}' not found.");

        var participant = await _participantRepo.GetActiveByUserIdAsync(tenantId, id, userId, ct)
            ?? throw new UnauthorizedAccessException("You are not an active participant in this conversation.");

        var oldStatus = conversation.Status;
        conversation.UpdateStatus(request.Status, userId);
        await _repo.UpdateAsync(conversation, ct);

        _logger.LogInformation("Conversation {ConversationId} status changed from {OldStatus} to {NewStatus}",
            conversation.Id, oldStatus, request.Status);

        _audit.Publish("ConversationStatusChanged", "StatusChanged",
            $"Status changed from {oldStatus} to {request.Status}",
            tenantId, userId, "Conversation", conversation.Id.ToString(),
            metadata: $"{{\"previousStatus\":\"{oldStatus}\",\"newStatus\":\"{request.Status}\"}}");

        try
        {
            await _timeline.RecordAsync(
                tenantId, id,
                Domain.Constants.TimelineEventTypes.StatusChanged,
                Domain.Constants.TimelineActorType.User,
                $"Status changed from {oldStatus} to {request.Status}",
                Domain.Constants.TimelineVisibility.InternalOnly,
                DateTime.UtcNow,
                actorId: userId,
                metadataJson: $"{{\"previousStatus\":\"{oldStatus}\",\"newStatus\":\"{request.Status}\"}}",
                ct: ct);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to record timeline for status change on {ConversationId}", id); }

        try
        {
            if (request.Status == ConversationStatus.Resolved || request.Status == ConversationStatus.Closed)
            {
                await _operationalService.SatisfyResolutionAsync(
                    tenantId, conversation.Id, DateTime.UtcNow, userId, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update SLA resolution for conversation {ConversationId}", conversation.Id);
        }

        return ToResponse(conversation);
    }

    public async Task<ConversationThreadResponse> GetThreadAsync(
        Guid tenantId, Guid id, Guid userId, CancellationToken ct = default)
    {
        var conversation = await _repo.GetByIdAsync(tenantId, id, ct)
            ?? throw new KeyNotFoundException($"Conversation '{id}' not found.");

        var participant = await _participantRepo.GetActiveByUserIdAsync(tenantId, id, userId, ct)
            ?? throw new UnauthorizedAccessException("You are not an active participant in this conversation.");

        var allMessages = await _messageRepo.ListByConversationOrderedAsync(tenantId, id, ct);
        var visibleMessages = FilterMessagesByVisibility(allMessages, participant);

        var allAttachments = await _attachmentRepo.ListByConversationAsync(tenantId, id, ct);
        var attachmentsByMessage = allAttachments
            .GroupBy(a => a.MessageId)
            .ToDictionary(g => g.Key, g => g.Select(ToAttachmentResponse).ToList());

        var participants = await _participantRepo.ListByConversationAsync(tenantId, id, ct);

        var readState = await _readStateRepo.GetAsync(tenantId, id, userId, ct);
        var latestVisible = visibleMessages.LastOrDefault();
        var isUnread = ComputeUnread(readState, latestVisible);

        var messageResponses = visibleMessages.Select(m =>
        {
            attachmentsByMessage.TryGetValue(m.Id, out var msgAttachments);
            return ToMessageResponse(m, msgAttachments);
        }).ToList();

        return new ConversationThreadResponse(
            conversation.Id, conversation.TenantId, conversation.OrgId,
            conversation.ProductKey, conversation.ContextType, conversation.ContextId,
            conversation.Subject, conversation.Status, conversation.VisibilityType,
            conversation.LastActivityAtUtc, conversation.CreatedAtUtc, conversation.UpdatedAtUtc,
            conversation.CreatedByUserId,
            isUnread, readState?.LastReadAtUtc, readState?.LastReadMessageId,
            messageResponses,
            participants.Select(ToParticipantResponse).ToList());
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

    private static bool ComputeUnread(ConversationReadState? readState, Message? latestMessage)
    {
        if (latestMessage is null) return false;
        if (readState?.LastReadAtUtc is null) return true;
        return latestMessage.SentAtUtc > readState.LastReadAtUtc;
    }

    private static ConversationResponse ToResponse(Conversation c, bool? isUnread = null, DateTime? lastReadAtUtc = null) => new(
        c.Id, c.TenantId, c.OrgId,
        c.ProductKey, c.ContextType, c.ContextId,
        c.Subject, c.Status, c.VisibilityType,
        c.LastActivityAtUtc, c.CreatedAtUtc, c.UpdatedAtUtc, c.CreatedByUserId,
        isUnread, lastReadAtUtc);

    private static MessageResponse ToMessageResponse(Message m, List<AttachmentResponse>? attachments = null) => new(
        m.Id, m.ConversationId,
        m.Channel, m.Direction, m.Body, m.VisibilityType,
        m.SentAtUtc, m.SenderUserId, m.SenderParticipantType,
        m.ExternalSenderName, m.ExternalSenderEmail,
        m.Status, m.CreatedAtUtc, attachments);

    private static AttachmentResponse ToAttachmentResponse(MessageAttachment a) => new(
        a.Id, a.MessageId, a.DocumentId,
        a.FileName, a.ContentType, a.FileSizeBytes,
        a.IsActive, a.CreatedAtUtc, a.CreatedByUserId);

    private static ParticipantResponse ToParticipantResponse(ConversationParticipant p) => new(
        p.Id, p.ConversationId,
        p.ParticipantType, p.UserId, p.ExternalName, p.ExternalEmail,
        p.Role, p.CanReply, p.IsActive, p.JoinedAtUtc, p.CreatedAtUtc);
}
