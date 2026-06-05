using Microsoft.Extensions.Logging;
using Comms.Application.DTOs;
using Comms.Application.Interfaces;
using Comms.Application.Repositories;
using Comms.Domain.Entities;

namespace Comms.Application.Services;

public class MentionService : IMentionService
{
    private readonly IMessageMentionRepository _mentionRepo;
    private readonly IParticipantRepository _participantRepo;
    private readonly IConversationTimelineService _timeline;
    private readonly INotificationsServiceClient _notifications;
    private readonly ILogger<MentionService> _logger;

    public MentionService(
        IMessageMentionRepository mentionRepo,
        IParticipantRepository participantRepo,
        IConversationTimelineService timeline,
        INotificationsServiceClient notifications,
        ILogger<MentionService> logger)
    {
        _mentionRepo = mentionRepo;
        _participantRepo = participantRepo;
        _timeline = timeline;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task ProcessMentionsAsync(
        Guid tenantId, Guid conversationId, Guid messageId,
        Guid senderUserId, string messageBody,
        CancellationToken ct = default)
    {
        var mentionedUserIds = MentionParser.ExtractMentionedUserIds(messageBody);
        if (mentionedUserIds.Count == 0)
            return;

        mentionedUserIds.Remove(senderUserId);
        if (mentionedUserIds.Count == 0)
            return;

        var participants = await _participantRepo.ListByConversationAsync(tenantId, conversationId, ct);
        var participantUserIds = new HashSet<Guid>(
            participants
                .Where(p => p.UserId.HasValue && p.IsActive)
                .Select(p => p.UserId!.Value));

        var mentions = new List<MessageMention>();
        foreach (var userId in mentionedUserIds)
        {
            var isParticipant = participantUserIds.Contains(userId);
            mentions.Add(MessageMention.Create(
                tenantId, conversationId, messageId,
                userId, senderUserId, isParticipant));
        }

        await _mentionRepo.AddRangeAsync(mentions, ct);

        _logger.LogInformation(
            "Processed {Count} mentions in message {MessageId} for conversation {ConversationId}",
            mentions.Count, messageId, conversationId);

        var snippet = messageBody.Length > 120 ? messageBody[..120] + "..." : messageBody;

        foreach (var mention in mentions)
        {
            try
            {
                await _timeline.RecordAsync(
                    tenantId, conversationId,
                    Domain.Constants.TimelineEventTypes.Mentioned,
                    Domain.Constants.TimelineActorType.User,
                    $"User mentioned in a message",
                    Domain.Constants.TimelineVisibility.InternalOnly,
                    mention.CreatedAtUtc,
                    actorId: senderUserId,
                    relatedMessageId: messageId,
                    metadataJson: $"{{\"mentionedUserId\":\"{mention.MentionedUserId}\",\"isParticipant\":{mention.IsMentionedUserParticipant.ToString().ToLower()}}}",
                    ct: ct);
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to record timeline entry for mention in message {MessageId}", messageId); }

            if (!mention.IsMentionedUserParticipant)
            {
                _logger.LogInformation(
                    "Skipping notification for non-participant mention {UserId} in message {MessageId}",
                    mention.MentionedUserId, messageId);
                continue;
            }

            try
            {
                var payload = new OperationalAlertPayload(
                    TenantId: tenantId,
                    ConversationId: conversationId,
                    QueueId: null,
                    Priority: "Normal",
                    TriggerType: "comms_internal_mention",
                    TargetUserId: mention.MentionedUserId,
                    DueAtUtc: DateTime.UtcNow,
                    ConversationSubject: snippet,
                    IdempotencyKey: $"mention-{messageId}-{mention.MentionedUserId}");

                await _notifications.SendOperationalAlertAsync(payload, ct);
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to send mention notification for user {UserId} in message {MessageId}", mention.MentionedUserId, messageId); }
        }
    }

    public async Task<List<MentionResponse>> GetMentionsByMessageAsync(
        Guid tenantId, Guid messageId, CancellationToken ct = default)
    {
        var mentions = await _mentionRepo.ListByMessageAsync(tenantId, messageId, ct);
        return mentions.Select(m => new MentionResponse(
            m.Id, m.MentionedUserId, m.MentionedByUserId,
            m.IsMentionedUserParticipant, m.CreatedAtUtc)).ToList();
    }
}
