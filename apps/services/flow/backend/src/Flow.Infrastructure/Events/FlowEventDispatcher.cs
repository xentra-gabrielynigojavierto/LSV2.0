using Contracts.Notifications;
using Flow.Application.Adapters.AuditAdapter;
using Flow.Application.Adapters.NotificationAdapter;
using Flow.Application.Events;
using Microsoft.Extensions.Logging;

namespace Flow.Infrastructure.Events;

/// <summary>
/// In-process dispatcher: fans out a Flow event to the audit + notification
/// adapter seams. Failures are swallowed and logged so adapter outages cannot
/// break the originating workflow/task operation.
///
/// <para>
/// E12.2 — every notification produced here is built from the canonical
/// <see cref="NotificationEnvelope"/> and translated to the legacy
/// <see cref="NotificationMessage"/> shape via
/// <see cref="NotificationContractTranslator.ToLegacySubmit"/>. This
/// matches the pattern used by <c>OutboxDispatcher</c> for the durable
/// pipe so producers everywhere now share a single template-keyed
/// contract. The legacy <c>NotificationMessage.EventKey</c> still
/// carries the original <see cref="IFlowEvent.EventKey"/> so the wire
/// payload is bit-identical to pre-E12.2 sends.
/// </para>
/// </summary>
public sealed class FlowEventDispatcher : IFlowEventDispatcher
{
    private readonly IAuditAdapter _audit;
    private readonly INotificationAdapter _notifications;
    private readonly ILogger<FlowEventDispatcher> _log;

    public FlowEventDispatcher(
        IAuditAdapter audit,
        INotificationAdapter notifications,
        ILogger<FlowEventDispatcher> log)
    {
        _audit = audit;
        _notifications = notifications;
        _log = log;
    }

    public async Task PublishAsync(IFlowEvent flowEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            await _audit.WriteEventAsync(MapToAudit(flowEvent), cancellationToken);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Audit dispatch failed for {EventKey}", flowEvent.EventKey);
        }

        var envelope = MapToNotificationEnvelope(flowEvent);
        if (envelope is not null)
        {
            try
            {
                await _notifications.SendAsync(
                    ToNotificationMessage(envelope, flowEvent.EventKey), cancellationToken);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Notification dispatch failed for {EventKey}", flowEvent.EventKey);
            }
        }
    }

    private static AuditEvent MapToAudit(IFlowEvent e) => e switch
    {
        WorkflowCreatedEvent w => new AuditEvent(
            "workflow.created", "Workflow", w.WorkflowId.ToString(),
            w.TenantId, w.UserId, $"Workflow '{w.Name}' created (productKey={w.ProductKey})",
            OccurredAtUtc: w.OccurredAtUtc),

        WorkflowStateChangedEvent w => new AuditEvent(
            "workflow.state_changed", "Workflow", w.WorkflowId.ToString(),
            w.TenantId, w.UserId, $"Workflow state {w.FromState} → {w.ToState}",
            OccurredAtUtc: w.OccurredAtUtc),

        WorkflowCompletedEvent w => new AuditEvent(
            "workflow.completed", "Workflow", w.WorkflowId.ToString(),
            w.TenantId, w.UserId, "Workflow completed",
            OccurredAtUtc: w.OccurredAtUtc),

        TaskAssignedEvent t => new AuditEvent(
            "task.assigned", "Task", t.TaskId.ToString(),
            t.TenantId, t.AssignedByUserId,
            $"Task assigned to user={t.AssignedToUserId} role={t.AssignedToRoleKey} org={t.AssignedToOrgId}",
            OccurredAtUtc: t.OccurredAtUtc),

        TaskCompletedEvent t => new AuditEvent(
            "task.completed", "Task", t.TaskId.ToString(),
            t.TenantId, t.CompletedByUserId, "Task completed",
            OccurredAtUtc: t.OccurredAtUtc),

        _ => new AuditEvent(e.EventKey, "Unknown", string.Empty, e.TenantId, null, null,
            OccurredAtUtc: e.OccurredAtUtc),
    };

    // ------------------------------------------------------------------
    // E12.2 — canonical envelope mapping. Returns null when the event
    // cannot be addressed to a recipient (e.g. role/org-only assign
    // without a user id and the legacy code already handled it via the
    // separate notification service path).
    // ------------------------------------------------------------------
    private static NotificationEnvelope? MapToNotificationEnvelope(IFlowEvent e) => e switch
    {
        TaskAssignedEvent t when HasAnyRecipient(t) => new NotificationEnvelope
        {
            TemplateKey   = NotificationTemplateKeys.TaskAssigned,
            TenantId      = t.TenantId,
            EntityType    = "Task",
            EntityId      = t.TaskId.ToString(),
            Recipient     = ResolveRecipient(t),
            Subject       = $"Task assigned: {t.TaskTitle ?? t.TaskId.ToString()}",
            Body          = $"Task '{t.TaskTitle ?? t.TaskId.ToString()}' has been assigned to you.",
            BodyVariables = new Dictionary<string, string?>
            {
                ["taskId"]            = t.TaskId.ToString(),
                ["taskTitle"]         = t.TaskTitle ?? t.TaskId.ToString(),
                ["assignedToUserId"]  = t.AssignedToUserId,
                ["assignedToRoleKey"] = t.AssignedToRoleKey,
                ["assignedToOrgId"]   = t.AssignedToOrgId,
                ["assignedByUserId"]  = t.AssignedByUserId,
            },
            Severity      = NotificationSeverity.Info,
            Category      = NotificationCategory.Task,
            CorrelationId = t.TaskId.ToString(),
            // E12.2 — deterministic idempotency key for the in-process pipe
            // (no outbox row id is available here). Re-using the OutboxId
            // slot lets HttpNotificationAdapter dedupe with no shape change.
            OutboxId      = BuildInProcessIdempotencyKey(t),
            ChannelHints  = new[] { "system" },
        },

        TaskCompletedEvent t when !string.IsNullOrEmpty(t.CompletedByUserId) => new NotificationEnvelope
        {
            TemplateKey   = NotificationTemplateKeys.TaskCompleted,
            TenantId      = t.TenantId,
            EntityType    = "Task",
            EntityId      = t.TaskId.ToString(),
            Recipient     = NotificationRecipient.ForUser(t.CompletedByUserId!),
            Subject       = $"Task completed: {t.TaskTitle ?? t.TaskId.ToString()}",
            Body          = $"Task '{t.TaskTitle ?? t.TaskId.ToString()}' has been completed.",
            BodyVariables = new Dictionary<string, string?>
            {
                ["taskId"]            = t.TaskId.ToString(),
                ["taskTitle"]         = t.TaskTitle ?? t.TaskId.ToString(),
                ["completedByUserId"] = t.CompletedByUserId,
            },
            Severity      = NotificationSeverity.Info,
            Category      = NotificationCategory.Task,
            CorrelationId = t.TaskId.ToString(),
            OutboxId      = BuildInProcessIdempotencyKey(t),
            ChannelHints  = new[] { "system" },
        },

        WorkflowCompletedEvent w when !string.IsNullOrEmpty(w.UserId) => new NotificationEnvelope
        {
            TemplateKey   = NotificationTemplateKeys.WorkflowCompleted,
            TenantId      = w.TenantId,
            EntityType    = "Workflow",
            EntityId      = w.WorkflowId.ToString(),
            Recipient     = NotificationRecipient.ForUser(w.UserId!),
            Subject       = "Workflow completed",
            Body          = $"Workflow {w.WorkflowId} has completed.",
            BodyVariables = new Dictionary<string, string?>
            {
                // Aligned with E12.1 template token (`workflowInstanceId`).
                // The in-process WorkflowService event carries this id.
                ["workflowInstanceId"] = w.WorkflowId.ToString(),
            },
            Severity      = NotificationSeverity.Info,
            Category      = NotificationCategory.Workflow,
            CorrelationId = w.WorkflowId.ToString(),
            OutboxId      = BuildInProcessIdempotencyKey(w),
            ChannelHints  = new[] { "system" },
        },

        // workflow created / state changed → audit only by default
        _ => null,
    };

    // Deterministic dedupe key for the in-process notification pipe.
    // Format: "{eventKey}:{tenantOrAnon}:{occurredAtUtc:O}:{entityId}"
    // Stable across retries of the same logical operation; downstream
    // consumers dedupe on the existing `outboxId`/`idempotencyKey`
    // metadata keys produced by the translator helper.
    private static string BuildInProcessIdempotencyKey(IFlowEvent e)
    {
        var entityId = e switch
        {
            TaskAssignedEvent t      => t.TaskId.ToString(),
            TaskCompletedEvent t     => t.TaskId.ToString(),
            WorkflowCompletedEvent w => w.WorkflowId.ToString(),
            WorkflowCreatedEvent w   => w.WorkflowId.ToString(),
            WorkflowStateChangedEvent w => w.WorkflowId.ToString(),
            _ => string.Empty,
        };
        return $"{e.EventKey}:{e.TenantId ?? "anon"}:{e.OccurredAtUtc:O}:{entityId}";
    }

    private static bool HasAnyRecipient(TaskAssignedEvent t) =>
        !string.IsNullOrEmpty(t.AssignedToUserId)
        || !string.IsNullOrEmpty(t.AssignedToRoleKey)
        || !string.IsNullOrEmpty(t.AssignedToOrgId);

    private static NotificationRecipient ResolveRecipient(TaskAssignedEvent t)
    {
        if (!string.IsNullOrEmpty(t.AssignedToUserId))
            return NotificationRecipient.ForUser(t.AssignedToUserId!);
        if (!string.IsNullOrEmpty(t.AssignedToRoleKey))
            return NotificationRecipient.ForRole(t.AssignedToRoleKey!, t.AssignedToOrgId);
        return NotificationRecipient.ForOrg(t.AssignedToOrgId!);
    }

    // Mirrors OutboxDispatcher.ToNotificationMessage so both pipes
    // produce identical wire shapes from the same envelope.
    private static NotificationMessage ToNotificationMessage(
        NotificationEnvelope envelope,
        string legacyEventKey)
    {
        var descriptor = NotificationContractTranslator.ToLegacySubmit(envelope);

        var data = new Dictionary<string, string?>(descriptor.Metadata, StringComparer.Ordinal)
        {
            ["templateKey"] = descriptor.TemplateKey,
            ["severity"]    = descriptor.Severity,
            ["category"]    = descriptor.Category,
        };
        if (!string.IsNullOrEmpty(descriptor.IdempotencyKey))
        {
            data["idempotencyKey"] = descriptor.IdempotencyKey;
        }
        if (descriptor.TemplateData is not null)
        {
            foreach (var kv in descriptor.TemplateData)
            {
                if (!data.ContainsKey(kv.Key)) data[kv.Key] = kv.Value;
            }
        }

        descriptor.Recipient.TryGetValue("userId",  out var userId);
        descriptor.Recipient.TryGetValue("roleKey", out var roleKey);

        return new NotificationMessage(
            Channel:          descriptor.Channel,
            EventKey:         legacyEventKey,
            TenantId:         envelope.TenantId,
            RecipientUserId:  userId,
            RecipientRoleKey: roleKey,
            Subject:          envelope.Subject ?? descriptor.TemplateKey,
            Body:             envelope.Body    ?? string.Empty,
            Data:             data);
    }
}
