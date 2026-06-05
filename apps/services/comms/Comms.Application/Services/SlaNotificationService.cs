using Microsoft.Extensions.Logging;
using Comms.Application.DTOs;
using Comms.Application.Interfaces;
using Comms.Application.Repositories;
using Comms.Domain.Constants;
using Comms.Domain.Entities;
using Comms.Domain.Enums;

namespace Comms.Application.Services;

public class SlaNotificationService : ISlaNotificationService
{
    private readonly IConversationSlaStateRepository _slaRepo;
    private readonly IConversationSlaTriggerStateRepository _triggerRepo;
    private readonly IConversationRepository _conversationRepo;
    private readonly IEscalationTargetResolver _targetResolver;
    private readonly INotificationsServiceClient _notificationsClient;
    private readonly IConversationTimelineService _timeline;
    private readonly IAuditPublisher _audit;
    private readonly ILogger<SlaNotificationService> _logger;

    public SlaNotificationService(
        IConversationSlaStateRepository slaRepo,
        IConversationSlaTriggerStateRepository triggerRepo,
        IConversationRepository conversationRepo,
        IEscalationTargetResolver targetResolver,
        INotificationsServiceClient notificationsClient,
        IConversationTimelineService timeline,
        IAuditPublisher audit,
        ILogger<SlaNotificationService> logger)
    {
        _slaRepo = slaRepo;
        _triggerRepo = triggerRepo;
        _conversationRepo = conversationRepo;
        _targetResolver = targetResolver;
        _notificationsClient = notificationsClient;
        _timeline = timeline;
        _audit = audit;
        _logger = logger;
    }

    public async Task<SlaTriggerEvaluationResponse> EvaluateAllAsync(
        Guid tenantId, Guid systemUserId, CancellationToken ct = default)
    {
        var nowUtc = DateTime.UtcNow;
        int evaluated = 0, warnings = 0, breaches = 0, skipped = 0;

        var conversations = await _conversationRepo.ListByTenantAsync(tenantId, ct);

        foreach (var conv in conversations)
        {
            if (conv.Status is "Resolved" or "Closed")
            {
                skipped++;
                continue;
            }

            var sla = await _slaRepo.GetByConversationAsync(tenantId, conv.Id, ct);
            if (sla is null)
            {
                skipped++;
                continue;
            }

            sla.EvaluateBreaches(nowUtc);
            await _slaRepo.UpdateAsync(sla, ct);

            var triggerState = await _triggerRepo.GetByConversationAsync(tenantId, conv.Id, ct);
            if (triggerState is null)
            {
                triggerState = ConversationSlaTriggerState.Create(tenantId, conv.Id, systemUserId);
                await _triggerRepo.AddAsync(triggerState, ct);
            }

            var result = await EvaluateConversationTriggersAsync(
                tenantId, conv, sla, triggerState, nowUtc, ct);

            warnings += result.Warnings;
            breaches += result.Breaches;
            skipped += result.Skipped;
            evaluated++;

            triggerState.RecordEvaluation(nowUtc);
            if (!await _triggerRepo.TryUpdateAsync(triggerState, ct))
            {
                _logger.LogWarning(
                    "Concurrency conflict updating trigger state for conversation {ConversationId} — another evaluation won the race",
                    conv.Id);
            }
        }

        _audit.Publish("SlaTriggerEvaluationRun", "Evaluated",
            $"SLA trigger evaluation completed: {evaluated} evaluated, {warnings} warnings, {breaches} breaches, {skipped} skipped",
            tenantId, systemUserId, entityType: "Tenant", entityId: tenantId.ToString(),
            metadata: $"{{\"evaluated\":{evaluated},\"warnings\":{warnings},\"breaches\":{breaches},\"skipped\":{skipped}}}");

        return new SlaTriggerEvaluationResponse(evaluated, warnings, breaches, skipped, nowUtc);
    }

    public async Task<ConversationSlaTriggerStateResponse?> GetTriggerStateAsync(
        Guid tenantId, Guid conversationId, CancellationToken ct = default)
    {
        var ts = await _triggerRepo.GetByConversationAsync(tenantId, conversationId, ct);
        if (ts is null) return null;

        return ToResponse(ts);
    }

    private async Task<(int Warnings, int Breaches, int Skipped)> EvaluateConversationTriggersAsync(
        Guid tenantId, Conversation conv, ConversationSlaState sla,
        ConversationSlaTriggerState triggerState, DateTime nowUtc, CancellationToken ct)
    {
        int warnings = 0, breaches = 0, skipped = 0;

        if (sla.FirstResponseAtUtc is null && sla.FirstResponseDueAtUtc.HasValue)
        {
            if (!triggerState.HasFirstResponseBreachSent &&
                SlaWarningThresholds.IsBreached(sla.FirstResponseDueAtUtc.Value, nowUtc))
            {
                var sent = await TrySendTriggerAsync(tenantId, conv, sla,
                    SlaTriggerType.FirstResponseBreach, sla.FirstResponseDueAtUtc.Value, triggerState, nowUtc, ct);
                if (sent) breaches++;
                else skipped++;
            }
            else if (!triggerState.HasFirstResponseWarningSent &&
                     SlaWarningThresholds.IsFirstResponseWarningDue(sla.FirstResponseDueAtUtc.Value, sla.SlaStartedAtUtc, nowUtc))
            {
                var sent = await TrySendTriggerAsync(tenantId, conv, sla,
                    SlaTriggerType.FirstResponseWarning, sla.FirstResponseDueAtUtc.Value, triggerState, nowUtc, ct);
                if (sent) warnings++;
                else skipped++;
            }
        }

        if (sla.ResolvedAtUtc is null && sla.ResolutionDueAtUtc.HasValue)
        {
            if (!triggerState.HasResolutionBreachSent &&
                SlaWarningThresholds.IsBreached(sla.ResolutionDueAtUtc.Value, nowUtc))
            {
                var sent = await TrySendTriggerAsync(tenantId, conv, sla,
                    SlaTriggerType.ResolutionBreach, sla.ResolutionDueAtUtc.Value, triggerState, nowUtc, ct);
                if (sent) breaches++;
                else skipped++;
            }
            else if (!triggerState.HasResolutionWarningSent &&
                     SlaWarningThresholds.IsResolutionWarningDue(sla.ResolutionDueAtUtc.Value, sla.SlaStartedAtUtc, nowUtc))
            {
                var sent = await TrySendTriggerAsync(tenantId, conv, sla,
                    SlaTriggerType.ResolutionWarning, sla.ResolutionDueAtUtc.Value, triggerState, nowUtc, ct);
                if (sent) warnings++;
                else skipped++;
            }
        }

        return (warnings, breaches, skipped);
    }

    private async Task<bool> TrySendTriggerAsync(
        Guid tenantId, Conversation conv, ConversationSlaState sla,
        string triggerType, DateTime dueAtUtc,
        ConversationSlaTriggerState triggerState, DateTime nowUtc, CancellationToken ct)
    {
        var target = await _targetResolver.ResolveAsync(tenantId, conv.Id, ct);
        if (target is null)
        {
            _audit.Publish("SlaTriggerSkippedNoTarget", "Skipped",
                $"SLA trigger {triggerType} skipped: no escalation target",
                tenantId, entityType: "Conversation", entityId: conv.Id.ToString(),
                metadata: $"{{\"conversationId\":\"{conv.Id}\",\"triggerType\":\"{triggerType}\"}}");

            _logger.LogWarning("SLA trigger {TriggerType} skipped for conversation {ConversationId}: no escalation target",
                triggerType, conv.Id);
            return false;
        }

        var idempotencyKey = $"sla-trigger-{conv.Id}-{triggerType}-{dueAtUtc:yyyyMMddHHmm}";

        var payload = new OperationalAlertPayload(
            tenantId, conv.Id, target.QueueId, sla.Priority,
            triggerType, target.UserId, dueAtUtc,
            conv.Subject, idempotencyKey);

        var result = await _notificationsClient.SendOperationalAlertAsync(payload, ct);

        if (result.Success)
        {
            MarkTriggerSent(triggerState, triggerType, target.UserId, target.QueueId, nowUtc);

            var auditEventType = triggerType switch
            {
                SlaTriggerType.FirstResponseWarning => "FirstResponseWarningTriggered",
                SlaTriggerType.FirstResponseBreach => "FirstResponseBreached",
                SlaTriggerType.ResolutionWarning => "ResolutionWarningTriggered",
                SlaTriggerType.ResolutionBreach => "ResolutionBreached",
                _ => "SlaTriggerFired"
            };

            _audit.Publish(auditEventType, "Triggered",
                $"{triggerType} notification sent to user {target.UserId}",
                tenantId, entityType: "Conversation", entityId: conv.Id.ToString(),
                metadata: $"{{\"conversationId\":\"{conv.Id}\",\"triggerType\":\"{triggerType}\",\"targetUserId\":\"{target.UserId}\",\"queueId\":\"{target.QueueId}\",\"priority\":\"{sla.Priority}\",\"dueAtUtc\":\"{dueAtUtc:O}\",\"notificationRequestId\":\"{result.NotificationsRequestId}\"}}");

            _logger.LogInformation(
                "SLA trigger {TriggerType} sent for conversation {ConversationId} to user {UserId}",
                triggerType, conv.Id, target.UserId);

            var timelineEventType = triggerType switch
            {
                SlaTriggerType.FirstResponseWarning => TimelineEventTypes.FirstResponseWarning,
                SlaTriggerType.FirstResponseBreach => TimelineEventTypes.FirstResponseBreach,
                SlaTriggerType.ResolutionWarning => TimelineEventTypes.ResolutionWarning,
                SlaTriggerType.ResolutionBreach => TimelineEventTypes.ResolutionBreach,
                _ => TimelineEventTypes.EscalationTriggered
            };

            try
            {
                await _timeline.RecordAsync(
                    tenantId, conv.Id,
                    timelineEventType,
                    TimelineActorType.System,
                    $"{triggerType} — escalated to user {target.UserId}",
                    TimelineVisibility.InternalOnly,
                    nowUtc,
                    relatedSlaId: sla.Id,
                    metadataJson: $"{{\"triggerType\":\"{triggerType}\",\"targetUserId\":\"{target.UserId}\",\"queueId\":\"{target.QueueId}\",\"priority\":\"{sla.Priority}\",\"dueAtUtc\":\"{dueAtUtc:O}\"}}",
                    ct: ct);
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to record timeline for SLA trigger {TriggerType} on {ConversationId}", triggerType, conv.Id); }

            return true;
        }

        _logger.LogWarning(
            "SLA trigger {TriggerType} notification failed for conversation {ConversationId}: {Error}",
            triggerType, conv.Id, result.ErrorMessage);

        return false;
    }

    private static void MarkTriggerSent(
        ConversationSlaTriggerState state, string triggerType,
        Guid targetUserId, Guid? queueId, DateTime nowUtc)
    {
        switch (triggerType)
        {
            case SlaTriggerType.FirstResponseWarning:
                state.MarkFirstResponseWarningSent(targetUserId, queueId, nowUtc);
                break;
            case SlaTriggerType.FirstResponseBreach:
                state.MarkFirstResponseBreachSent(targetUserId, queueId, nowUtc);
                break;
            case SlaTriggerType.ResolutionWarning:
                state.MarkResolutionWarningSent(targetUserId, queueId, nowUtc);
                break;
            case SlaTriggerType.ResolutionBreach:
                state.MarkResolutionBreachSent(targetUserId, queueId, nowUtc);
                break;
        }
    }

    private static ConversationSlaTriggerStateResponse ToResponse(ConversationSlaTriggerState ts) => new(
        ts.Id, ts.TenantId, ts.ConversationId,
        ts.FirstResponseWarningSentAtUtc, ts.FirstResponseBreachSentAtUtc,
        ts.ResolutionWarningSentAtUtc, ts.ResolutionBreachSentAtUtc,
        ts.LastEvaluatedAtUtc, ts.LastEscalatedToUserId, ts.LastEscalatedQueueId,
        ts.WarningThresholdSnapshotMinutes, ts.EvaluationVersion,
        ts.CreatedAtUtc, ts.UpdatedAtUtc);
}
