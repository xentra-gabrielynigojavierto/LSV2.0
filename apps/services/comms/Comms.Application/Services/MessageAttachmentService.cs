using Microsoft.Extensions.Logging;
using Comms.Application.DTOs;
using Comms.Application.Interfaces;
using Comms.Application.Repositories;
using Comms.Domain.Entities;
using Comms.Domain.Enums;

namespace Comms.Application.Services;

public class MessageAttachmentService : IMessageAttachmentService
{
    private readonly IMessageAttachmentRepository _attachmentRepo;
    private readonly IMessageRepository _messageRepo;
    private readonly IConversationRepository _conversationRepo;
    private readonly IParticipantRepository _participantRepo;
    private readonly IDocumentServiceClient _documentClient;
    private readonly IAuditPublisher _audit;
    private readonly ILogger<MessageAttachmentService> _logger;

    public MessageAttachmentService(
        IMessageAttachmentRepository attachmentRepo,
        IMessageRepository messageRepo,
        IConversationRepository conversationRepo,
        IParticipantRepository participantRepo,
        IDocumentServiceClient documentClient,
        IAuditPublisher audit,
        ILogger<MessageAttachmentService> logger)
    {
        _attachmentRepo = attachmentRepo;
        _messageRepo = messageRepo;
        _conversationRepo = conversationRepo;
        _participantRepo = participantRepo;
        _documentClient = documentClient;
        _audit = audit;
        _logger = logger;
    }

    public async Task<AttachmentResponse> LinkAttachmentAsync(
        Guid tenantId, Guid userId, Guid conversationId, Guid messageId,
        AddMessageAttachmentRequest request, CancellationToken ct = default)
    {
        var conversation = await _conversationRepo.GetByIdAsync(tenantId, conversationId, ct)
            ?? throw new KeyNotFoundException($"Conversation '{conversationId}' not found.");

        var participant = await _participantRepo.GetActiveByUserIdAsync(tenantId, conversationId, userId, ct)
            ?? throw new UnauthorizedAccessException("You are not an active participant in this conversation.");

        if (!participant.CanReply)
            throw new UnauthorizedAccessException("You do not have reply permission in this conversation.");

        var messages = await _messageRepo.ListByConversationOrderedAsync(tenantId, conversationId, ct);
        var message = messages.FirstOrDefault(m => m.Id == messageId)
            ?? throw new KeyNotFoundException($"Message '{messageId}' not found in conversation '{conversationId}'.");

        if (message.VisibilityType == VisibilityType.InternalOnly &&
            participant.ParticipantType != ParticipantType.InternalUser)
            throw new UnauthorizedAccessException("You cannot attach documents to internal-only messages.");

        var validation = await _documentClient.ValidateDocumentAsync(request.DocumentId, tenantId, ct);
        if (!validation.Exists)
            throw new KeyNotFoundException($"Document '{request.DocumentId}' not found or not accessible.");
        if (!validation.TenantId.HasValue)
            throw new InvalidOperationException($"Document '{request.DocumentId}' could not be verified for tenant ownership.");
        if (validation.TenantId.Value != tenantId)
            throw new UnauthorizedAccessException("Document does not belong to this tenant.");

        var attachment = MessageAttachment.Create(
            tenantId, conversationId, messageId,
            request.DocumentId, request.FileName, request.ContentType,
            request.FileSizeBytes, userId);

        await _attachmentRepo.AddAsync(attachment, ct);

        conversation.TouchActivity();
        await _conversationRepo.UpdateAsync(conversation, ct);

        _logger.LogInformation(
            "Attachment {AttachmentId} linked to message {MessageId} in conversation {ConversationId}",
            attachment.Id, messageId, conversationId);

        _audit.Publish("DocumentLinked", "Created",
            $"Document {request.DocumentId} linked to message {messageId}",
            tenantId, userId, "MessageAttachment", attachment.Id.ToString(),
            metadata: $"{{\"documentId\":\"{request.DocumentId}\",\"conversationId\":\"{conversationId}\",\"messageId\":\"{messageId}\",\"fileName\":\"{request.FileName}\"}}");

        return ToResponse(attachment);
    }

    public async Task<List<AttachmentResponse>> ListByMessageAsync(
        Guid tenantId, Guid userId, Guid conversationId, Guid messageId,
        CancellationToken ct = default)
    {
        var participant = await _participantRepo.GetActiveByUserIdAsync(tenantId, conversationId, userId, ct)
            ?? throw new UnauthorizedAccessException("You are not an active participant in this conversation.");

        var messages = await _messageRepo.ListByConversationOrderedAsync(tenantId, conversationId, ct);
        var message = messages.FirstOrDefault(m => m.Id == messageId)
            ?? throw new KeyNotFoundException($"Message '{messageId}' not found in conversation '{conversationId}'.");

        if (message.VisibilityType == VisibilityType.InternalOnly &&
            participant.ParticipantType != ParticipantType.InternalUser)
            return new List<AttachmentResponse>();

        var attachments = await _attachmentRepo.ListByMessageAsync(tenantId, messageId, ct);
        return attachments.Select(ToResponse).ToList();
    }

    public async Task RemoveAttachmentAsync(
        Guid tenantId, Guid userId, Guid conversationId, Guid messageId, Guid attachmentId,
        CancellationToken ct = default)
    {
        var participant = await _participantRepo.GetActiveByUserIdAsync(tenantId, conversationId, userId, ct)
            ?? throw new UnauthorizedAccessException("You are not an active participant in this conversation.");

        var messages = await _messageRepo.ListByConversationOrderedAsync(tenantId, conversationId, ct);
        var message = messages.FirstOrDefault(m => m.Id == messageId)
            ?? throw new KeyNotFoundException($"Message '{messageId}' not found in conversation '{conversationId}'.");

        if (message.VisibilityType == VisibilityType.InternalOnly &&
            participant.ParticipantType != ParticipantType.InternalUser)
            throw new UnauthorizedAccessException("You cannot modify attachments on internal-only messages.");

        var attachment = await _attachmentRepo.GetByIdAsync(tenantId, attachmentId, ct)
            ?? throw new KeyNotFoundException($"Attachment '{attachmentId}' not found.");

        if (attachment.MessageId != messageId || attachment.ConversationId != conversationId)
            throw new KeyNotFoundException($"Attachment '{attachmentId}' does not belong to the specified message/conversation.");

        attachment.Deactivate(userId);
        await _attachmentRepo.UpdateAsync(attachment, ct);

        _logger.LogInformation(
            "Attachment {AttachmentId} removed from message {MessageId} in conversation {ConversationId}",
            attachmentId, messageId, conversationId);

        _audit.Publish("DocumentUnlinked", "Deleted",
            $"Document attachment {attachmentId} removed from message {messageId}",
            tenantId, userId, "MessageAttachment", attachmentId.ToString(),
            metadata: $"{{\"documentId\":\"{attachment.DocumentId}\",\"conversationId\":\"{conversationId}\",\"messageId\":\"{messageId}\"}}");
    }

    private static AttachmentResponse ToResponse(MessageAttachment a) => new(
        a.Id, a.MessageId, a.DocumentId,
        a.FileName, a.ContentType, a.FileSizeBytes,
        a.IsActive, a.CreatedAtUtc, a.CreatedByUserId);
}
