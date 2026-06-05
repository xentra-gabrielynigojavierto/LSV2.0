using System.Text.Json;
using Contracts.Notifications;
using Flow.Application.Adapters.AuditAdapter;
using Flow.Application.Adapters.NotificationAdapter;
using Flow.Application.Engines.WorkflowEngine;
using Flow.Application.Interfaces;
using Flow.Application.Outbox;
using Flow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Flow.Infrastructure.Outbox;

/// <summary>
/// LS-FLOW-E10.2 — per-event-type processing fan-out for outbox rows.
///
/// <para>
/// Scoped service: instantiated once per worker tick by
/// <see cref="OutboxProcessor"/>. Resolves the audit + notification
/// adapters from the same DI scope so HTTP/decorator policies (timeouts,
/// fallbacks) apply identically to inline and async paths.
/// </para>
///
/// <para>
/// Idempotency strategy:
///   1. Every dispatched audit/notification carries the originating
///      outbox row Id in metadata as <c>outboxId</c>; downstream
///      collectors that wish to dedupe can key off it.
///   2. The re-drive handler (<c>workflow.admin.retry</c>) is a no-op
///      whenever the workflow instance is no longer in a state that
///      benefits from a nudge — e.g. operator already cancelled it. The
///      handler does NOT re-execute engine logic; Flow remains the
///      single execution authority.
/// </para>
/// </summary>
public sealed class OutboxDispatcher
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private readonly IAuditAdapter _audit;
    private readonly INotificationAdapter _notifications;
    private readonly IFlowDbContext _db;
    private readonly ILogger<OutboxDispatcher> _log;

    public OutboxDispatcher(
        IAuditAdapter audit,
        INotificationAdapter notifications,
        IFlowDbContext db,
        ILogger<OutboxDispatcher> log)
    {
        _audit = audit;
        _notifications = notifications;
        _db = db;
        _log = log;
    }

    public async Task DispatchAsync(OutboxMessage row, CancellationToken ct)
    {
        switch (row.EventType)
        {
            // ----------- engine lifecycle (audit only by default) -----
            case OutboxEventTypes.WorkflowStart:
            case OutboxEventTypes.WorkflowAdvance:
            case OutboxEventTypes.WorkflowComplete:
            case OutboxEventTypes.WorkflowCancel:
            case OutboxEventTypes.WorkflowFail:
                await DispatchLifecycleAsync(row, ct);
                return;

            // ----------- admin overrides ------------------------------
            case OutboxEventTypes.AdminForceComplete:
            case OutboxEventTypes.AdminCancel:
                await DispatchAdminAsync(row, redrive: false, ct);
                return;

            case OutboxEventTypes.AdminRetry:
                await DispatchAdminAsync(row, redrive: true, ct);
                return;

            // ----------- LS-FLOW-E10.3 — SLA / timer transitions -----
            case OutboxEventTypes.WorkflowSlaDueSoon:
            case OutboxEventTypes.WorkflowSlaOverdue:
            case OutboxEventTypes.WorkflowSlaEscalated:
                await DispatchSlaTransitionAsync(row, ct);
                return;

            default:
                throw new InvalidOperationException(
                    $"Unknown outbox event type '{row.EventType}'. Register a handler in OutboxDispatcher.");
        }
    }

    // ------------------------------------------------------------------
    // Lifecycle (engine-emitted)
    // ------------------------------------------------------------------

    private async Task DispatchLifecycleAsync(OutboxMessage row, CancellationToken ct)
    {
        var p = JsonSerializer.Deserialize<WorkflowLifecyclePayload>(row.PayloadJson, JsonOpts)
                ?? throw new InvalidOperationException("Lifecycle payload deserialised to null.");

        var description = row.EventType switch
        {
            OutboxEventTypes.WorkflowStart    => $"Workflow started at step '{p.ToStepKey}'.",
            OutboxEventTypes.WorkflowAdvance  => $"Workflow advanced {p.FromStepKey} → {p.ToStepKey}.",
            OutboxEventTypes.WorkflowComplete => "Workflow completed.",
            OutboxEventTypes.WorkflowCancel   =>
                string.IsNullOrEmpty(p.Reason) ? "Workflow cancelled." : $"Workflow cancelled: {p.Reason}",
            OutboxEventTypes.WorkflowFail     => $"Workflow failed: {p.Reason ?? "(no detail)"}",
            _                                 => row.EventType,
        };

        await _audit.WriteEventAsync(new AuditEvent(
            Action:        row.EventType,
            EntityType:    "WorkflowInstance",
            EntityId:      p.WorkflowInstanceId.ToString(),
            TenantId:      row.TenantId,
            UserId:        p.PerformedBy,
            Description:   description,
            Metadata:      new Dictionary<string, string?>
            {
                ["outboxId"]       = row.Id.ToString(),
                ["productKey"]     = p.ProductKey,
                ["fromStepKey"]    = p.FromStepKey,
                ["toStepKey"]      = p.ToStepKey,
                ["fromStatus"]     = p.FromStatus,
                ["toStatus"]       = p.ToStatus,
                ["reason"]         = p.Reason,
            },
            OccurredAtUtc: p.OccurredAtUtc), ct);

        // Send a notification only on the natural end-of-life event.
        if (row.EventType == OutboxEventTypes.WorkflowComplete &&
            !string.IsNullOrEmpty(p.PerformedBy))
        {
            var envelope = new NotificationEnvelope
            {
                TemplateKey   = NotificationTemplateKeys.WorkflowCompleted,
                TenantId      = row.TenantId,
                ProductKey    = p.ProductKey,
                EntityType    = "WorkflowInstance",
                EntityId      = p.WorkflowInstanceId.ToString(),
                Recipient     = NotificationRecipient.ForUser(p.PerformedBy!),
                Subject       = "Workflow completed",
                Body          = $"Workflow {p.WorkflowInstanceId} has completed.",
                BodyVariables = new Dictionary<string, string?>
                {
                    ["workflowInstanceId"] = p.WorkflowInstanceId.ToString(),
                    ["productKey"]         = p.ProductKey,
                },
                Severity      = NotificationSeverity.Info,
                Category      = NotificationCategory.Workflow,
                CorrelationId = p.WorkflowInstanceId.ToString(),
                OutboxId      = row.Id.ToString(),
                ChannelHints  = new[] { "system" },
            };

            await _notifications.SendAsync(ToNotificationMessage(envelope, row.EventType), ct);
        }
    }

    // ------------------------------------------------------------------
    // LS-FLOW-E10.3 — SLA transitions (audit always; notification when assignee known)
    // ------------------------------------------------------------------

    private async Task DispatchSlaTransitionAsync(OutboxMessage row, CancellationToken ct)
    {
        var p = JsonSerializer.Deserialize<WorkflowSlaTransitionPayload>(row.PayloadJson, JsonOpts)
                ?? throw new InvalidOperationException("SLA payload deserialised to null.");

        var (subject, summary) = row.EventType switch
        {
            OutboxEventTypes.WorkflowSlaDueSoon =>
                ("Workflow due soon",
                 $"Workflow {p.WorkflowInstanceId} is due at {p.DueAt:O}."),
            OutboxEventTypes.WorkflowSlaOverdue =>
                ("Workflow overdue",
                 $"Workflow {p.WorkflowInstanceId} is overdue (due {p.DueAt:O})."),
            OutboxEventTypes.WorkflowSlaEscalated =>
                ("Workflow escalated",
                 $"Workflow {p.WorkflowInstanceId} has been overdue for {(p.OverdueDurationSeconds ?? 0) / 60} minute(s); escalation level {p.EscalationLevel}."),
            _ => (row.EventType, row.EventType),
        };

        // Always emit the audit row (durable forensic trail).
        await _audit.WriteEventAsync(new AuditEvent(
            Action:        row.EventType,
            EntityType:    "WorkflowInstance",
            EntityId:      p.WorkflowInstanceId.ToString(),
            TenantId:      row.TenantId,
            UserId:        null,                      // system actor
            Description:   summary,
            Metadata:      new Dictionary<string, string?>
            {
                ["outboxId"]               = row.Id.ToString(),
                ["productKey"]             = p.ProductKey,
                ["currentStepKey"]         = p.CurrentStepKey,
                ["dueAt"]                  = p.DueAt.ToString("O"),
                ["previousSlaStatus"]      = p.PreviousSlaStatus,
                ["newSlaStatus"]           = p.NewSlaStatus,
                ["escalationLevel"]        = p.EscalationLevel.ToString(),
                ["overdueDurationSeconds"] = p.OverdueDurationSeconds?.ToString(),
                ["assignedToUserId"]       = p.AssignedToUserId,
            },
            OccurredAtUtc: p.OccurredAtUtc), ct);

        // Notification: only when we know an assignee — anonymous SLA
        // events still produce the audit row but cannot be addressed
        // to a recipient. Role-based fan-out is deferred (see report
        // Known Issues / Gaps).
        if (!string.IsNullOrEmpty(p.AssignedToUserId))
        {
            var (templateKey, severity) = row.EventType switch
            {
                OutboxEventTypes.WorkflowSlaDueSoon =>
                    (NotificationTemplateKeys.WorkflowSlaDueSoon, NotificationSeverity.Warning),
                OutboxEventTypes.WorkflowSlaOverdue =>
                    (NotificationTemplateKeys.WorkflowSlaOverdue, NotificationSeverity.Critical),
                OutboxEventTypes.WorkflowSlaEscalated =>
                    (NotificationTemplateKeys.WorkflowSlaEscalated, NotificationSeverity.Critical),
                _ => (row.EventType, NotificationSeverity.Warning),
            };

            var envelope = new NotificationEnvelope
            {
                TemplateKey   = templateKey,
                TenantId      = row.TenantId,
                ProductKey    = p.ProductKey,
                EntityType    = "WorkflowInstance",
                EntityId      = p.WorkflowInstanceId.ToString(),
                Recipient     = NotificationRecipient.ForUser(p.AssignedToUserId!),
                Subject       = subject,
                Body          = summary,
                BodyVariables = new Dictionary<string, string?>
                {
                    ["workflowInstanceId"] = p.WorkflowInstanceId.ToString(),
                    ["productKey"]         = p.ProductKey,
                    ["currentStepKey"]     = p.CurrentStepKey,
                    ["dueAt"]              = p.DueAt.ToString("O"),
                    ["newSlaStatus"]       = p.NewSlaStatus,
                    ["escalationLevel"]    = p.EscalationLevel.ToString(),
                },
                Severity      = severity,
                Category      = NotificationCategory.Sla,
                CorrelationId = p.WorkflowInstanceId.ToString(),
                OutboxId      = row.Id.ToString(),
                ChannelHints  = new[] { "system" },
            };

            await _notifications.SendAsync(ToNotificationMessage(envelope, row.EventType), ct);
        }
    }

    // ------------------------------------------------------------------
    // E12.2 — translate the canonical envelope into the legacy
    // NotificationMessage shape so HttpNotificationAdapter / the
    // notifications service keep their current wire contract while
    // every send now carries severity / category / correlationId /
    // outboxId in the persisted metadata.
    //
    // <paramref name="legacyEventKey"/> preserves the original outbox
    // event-type string on <c>NotificationMessage.EventKey</c> so the
    // wire payload is bit-identical to pre-E12.2 sends. The canonical
    // template key still flows through the metadata
    // (<c>templateKey</c>) and is the field downstream consumers should
    // migrate to.
    // ------------------------------------------------------------------
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

    // ------------------------------------------------------------------
    // Admin actions (audit always; re-drive nudge for retry)
    // ------------------------------------------------------------------

    private async Task DispatchAdminAsync(OutboxMessage row, bool redrive, CancellationToken ct)
    {
        var p = JsonSerializer.Deserialize<AdminActionPayload>(row.PayloadJson, JsonOpts)
                ?? throw new InvalidOperationException("Admin payload deserialised to null.");

        // Load the workflow instance once: used both as the recipient
        // resolver for the notification envelope and (in the redrive
        // branch) as the consistency check for the re-drive nudge.
        var instance = await _db.WorkflowInstances
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == p.WorkflowInstanceId, ct);

        // Always emit the audit row first so the operator action is durably recorded.
        await _audit.WriteEventAsync(new AuditEvent(
            Action:      row.EventType,
            EntityType:  "WorkflowInstance",
            EntityId:    p.WorkflowInstanceId.ToString(),
            TenantId:    row.TenantId,
            UserId:      p.PerformedBy,
            Description: $"Admin action {p.Action}: {p.PreviousStatus} → {p.NewStatus}",
            Metadata:    new Dictionary<string, string?>
            {
                ["outboxId"]        = row.Id.ToString(),
                ["source"]          = "ControlCenterAdminAction",
                ["productKey"]      = p.ProductKey,
                ["previousStatus"]  = p.PreviousStatus,
                ["newStatus"]       = p.NewStatus,
                ["reason"]          = p.Reason,
                ["isPlatformAdmin"] = p.IsPlatformAdmin ? "true" : "false",
                ["attemptCount"]    = row.AttemptCount.ToString(),
            },
            OccurredAtUtc: p.OccurredAtUtc), ct);

        // E12.2 — emit a notification envelope for the admin override
        // when we have a recipient. We address the current workflow
        // assignee (loaded above) so the operator who owns the work
        // sees the override land. The audit row above is the durable
        // forensic trail; the notification is the user-facing signal.
        var assignee = instance?.AssignedToUserId;
        if (!string.IsNullOrEmpty(assignee))
        {
            var (templateKey, severity) = row.EventType switch
            {
                OutboxEventTypes.AdminRetry =>
                    (NotificationTemplateKeys.WorkflowAdminRetry, NotificationSeverity.Info),
                OutboxEventTypes.AdminForceComplete =>
                    (NotificationTemplateKeys.WorkflowAdminForceComplete, NotificationSeverity.Warning),
                OutboxEventTypes.AdminCancel =>
                    (NotificationTemplateKeys.WorkflowAdminCancel, NotificationSeverity.Warning),
                _ => (row.EventType, NotificationSeverity.Info),
            };

            var (subject, summary) = row.EventType switch
            {
                OutboxEventTypes.AdminRetry =>
                    ("Workflow re-drive issued",
                     $"An operator re-drove workflow {p.WorkflowInstanceId}: {p.PreviousStatus} → {p.NewStatus}."),
                OutboxEventTypes.AdminForceComplete =>
                    ("Workflow force-completed",
                     $"An operator force-completed workflow {p.WorkflowInstanceId} (was {p.PreviousStatus})."),
                OutboxEventTypes.AdminCancel =>
                    ("Workflow cancelled by operator",
                     $"An operator cancelled workflow {p.WorkflowInstanceId} (was {p.PreviousStatus})."),
                _ => (row.EventType, row.EventType),
            };

            var envelope = new NotificationEnvelope
            {
                TemplateKey   = templateKey,
                TenantId      = row.TenantId,
                ProductKey    = p.ProductKey,
                EntityType    = "WorkflowInstance",
                EntityId      = p.WorkflowInstanceId.ToString(),
                Recipient     = NotificationRecipient.ForUser(assignee!),
                Subject       = subject,
                Body          = summary,
                BodyVariables = new Dictionary<string, string?>
                {
                    ["workflowInstanceId"] = p.WorkflowInstanceId.ToString(),
                    ["productKey"]         = p.ProductKey,
                    ["action"]             = p.Action,
                    ["previousStatus"]     = p.PreviousStatus,
                    ["newStatus"]          = p.NewStatus,
                    ["reason"]             = p.Reason,
                    ["performedBy"]        = p.PerformedBy,
                    ["isPlatformAdmin"]    = p.IsPlatformAdmin ? "true" : "false",
                },
                Severity      = severity,
                Category      = NotificationCategory.Workflow,
                CorrelationId = p.WorkflowInstanceId.ToString(),
                OutboxId      = row.Id.ToString(),
                ChannelHints  = new[] { "system" },
            };

            await _notifications.SendAsync(ToNotificationMessage(envelope, row.EventType), ct);
        }

        if (!redrive) return;

        // ------- re-drive nudge (idempotent, read-only verification) ---
        // The mutation already reset the workflow to Active in the same
        // transaction as this outbox row. Here we verify the engine view
        // is still consistent (status is still Active and not a stale
        // ghost) and emit a structured log line that downstream platform
        // operators can monitor as the durable async re-drive signal.
        // We intentionally do NOT re-invoke WorkflowEngine.Advance here:
        // engine progression is the responsibility of the user/product
        // command path. This handler exists to prove the re-drive event
        // was processed, and to surface anomalies (e.g. status flipped
        // back to Failed by another writer between commit and pickup).
        if (instance is null)
        {
            _log.LogWarning(
                "Outbox redrive id={OutboxId} workflowInstance={InstanceId} — instance no longer exists; treating as success.",
                row.Id, p.WorkflowInstanceId);
            return;
        }

        if (instance.Status == WorkflowEngine.StatusActive)
        {
            _log.LogInformation(
                "Outbox redrive id={OutboxId} workflowInstance={InstanceId} tenant={TenantId} step={Step} — re-drive nudge processed; workflow is Active.",
                row.Id, instance.Id, instance.TenantId, instance.CurrentStepKey);
        }
        else
        {
            // Not an error: another writer (e.g. operator subsequently
            // cancelled) moved the instance after our re-arm committed.
            // Idempotency win — we don't try to "fix" it.
            _log.LogInformation(
                "Outbox redrive id={OutboxId} workflowInstance={InstanceId} status={Status} — re-drive no-op (state moved on).",
                row.Id, instance.Id, instance.Status);
        }
    }
}
