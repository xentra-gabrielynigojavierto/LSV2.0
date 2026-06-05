using System.Text.Json;

namespace Flow.Application.Adapters.AuditAdapter;

/// <summary>
/// E13.1 — pure normalization of raw audit records into the timeline
/// DTO shape consumed by the Control Center workflow timeline view.
///
/// Design:
/// - Stateless and side-effect free; safe to call from a controller.
/// - Tolerates missing optional fields — every nullable input maps to
///   a nullable output, and the metadata bag silently omits absent keys.
/// - Deterministic ordering: ascending by <see cref="AuditEventRecord.OccurredAtUtc"/>,
///   tie-broken by <see cref="AuditEventRecord.EventId"/> (string ordinal).
/// - Raw <see cref="AuditEventRecord.Action"/> is preserved verbatim so
///   downstream UIs can fall back to a literal display when the
///   high-level <c>category</c> classification is "other".
/// </summary>
public static class AuditTimelineNormalizer
{
    // ── Action prefixes / known verbs ────────────────────────────────────
    // Kept in sync with the audit Action strings produced by:
    //   - Flow.Infrastructure.Events.FlowEventDispatcher
    //     (workflow.created, workflow.state_changed, workflow.completed,
    //      task.assigned, task.completed)
    //   - Flow.Infrastructure.Outbox.OutboxDispatcher / OutboxEventTypes
    //     (workflow.admin.retry, workflow.admin.force_complete,
    //      workflow.admin.cancel)
    private const string ActionWorkflowCreated      = "workflow.created";
    private const string ActionWorkflowStateChanged = "workflow.state_changed";
    private const string ActionWorkflowCompleted    = "workflow.completed";
    private const string ActionAdminRetry           = "workflow.admin.retry";
    private const string ActionAdminForceComplete   = "workflow.admin.force_complete";
    private const string ActionAdminCancel          = "workflow.admin.cancel";
    private const string ActionPrefixWorkflowSla    = "workflow.sla";
    private const string ActionPrefixNotification   = "notification";

    // E16 — task-scoped action constants. Two producers exist:
    //   * WorkflowTaskAssignmentService (E14.2) writes
    //     "workflow.task.claim" / "workflow.task.reassign" against
    //     EntityType=WorkflowTask.
    //   * FlowEventDispatcher (E11.4) writes "task.assigned" /
    //     "task.completed" against EntityType=Task.
    // Both ultimately surface on the unified timeline; categories
    // below give the UI a stable bucket per producer.
    private const string ActionTaskClaim    = "workflow.task.claim";
    private const string ActionTaskReassign = "workflow.task.reassign";
    private const string ActionTaskAssigned  = "task.assigned";
    private const string ActionTaskCompleted = "task.completed";

    public static IReadOnlyList<TimelineEvent> Normalize(IEnumerable<AuditEventRecord> records)
    {
        // Deterministic ordering — ascending by OccurredAt, tie-broken
        // by EventId. Both keys are stable per record so two callers
        // see the same sequence regardless of the upstream query plan.
        var ordered = records
            .OrderBy(r => r.OccurredAtUtc)
            .ThenBy(r => r.EventId, StringComparer.Ordinal)
            .ToList();

        var result = new List<TimelineEvent>(ordered.Count);
        foreach (var r in ordered)
        {
            result.Add(NormalizeOne(r));
        }
        return result;
    }

    private static TimelineEvent NormalizeOne(AuditEventRecord r)
    {
        var action   = r.Action ?? string.Empty;
        var category = ClassifyCategory(action, r.EventCategory);

        // Per-action enrichments. Every branch tolerates absent
        // metadata — the goal is "best-effort enrichment", not
        // "required schema".
        string? previousStatus = null;
        string? newStatus      = null;
        var metadata = new Dictionary<string, string?>(StringComparer.Ordinal);

        // Always-on enrichments.
        AddIfPresent(metadata, "sourceSystem",  r.SourceSystem);
        AddIfPresent(metadata, "sourceService", r.SourceService);
        AddIfPresent(metadata, "correlationId", r.CorrelationId);
        AddIfPresent(metadata, "requestId",     r.RequestId);
        AddIfPresent(metadata, "sessionId",     r.SessionId);
        AddIfPresent(metadata, "severity",      r.Severity);
        AddIfPresent(metadata, "visibility",    r.Visibility);
        AddIfPresent(metadata, "eventCategory", r.EventCategory);

        // Pull scalar keys from the audit record's metadata JSON, if any.
        // Object/array values are intentionally skipped to keep the bag
        // a flat string→string map; full detail remains in the audit
        // service for deep dives.
        MergeMetadataJson(r.MetadataJson, metadata);

        switch (action)
        {
            case ActionWorkflowStateChanged:
                // Description shape (FlowEventDispatcher):
                //   "Workflow state {From} → {To}"
                ExtractStateTransition(r.Description, out previousStatus, out newStatus);
                break;

            case ActionAdminRetry:
            case ActionAdminForceComplete:
            case ActionAdminCancel:
                // OutboxDispatcher / AdminActionPayload metadata keys.
                previousStatus = TryGet(metadata, "previousStatus");
                newStatus      = TryGet(metadata, "newStatus");
                break;

            case ActionTaskClaim:
            case ActionTaskReassign:
                // E14.2 producer carries prevMode/newMode + role/user/org.
                previousStatus = TryGet(metadata, "prevMode");
                newStatus      = TryGet(metadata, "newMode");
                break;
        }

        var actor = (r.ActorId is not null || r.ActorName is not null || r.ActorType is not null)
            ? new TimelineActor(r.ActorId, r.ActorName, r.ActorType)
            : null;

        // E16 — deterministic fallback summary when the producer left
        // r.Description blank (the legacy task.* producers usually do).
        // Generated purely from action + metadata so the result is
        // reproducible and never fabricates information not in audit.
        var summary = !string.IsNullOrWhiteSpace(r.Description)
            ? r.Description
            : BuildFallbackSummary(action, metadata);

        return new TimelineEvent(
            EventId:        r.EventId,
            AuditId:        r.AuditId,
            OccurredAtUtc:  r.OccurredAtUtc,
            Category:       category,
            Action:         action,
            Source:         r.SourceSystem ?? "flow",
            Actor:          actor,
            PerformedBy:    actor?.Name ?? actor?.Id,
            Summary:        summary,
            PreviousStatus: previousStatus,
            NewStatus:      newStatus,
            Metadata:       metadata);
    }

    /// <summary>
    /// E16 — produce a short, deterministic human summary for known
    /// task / workflow actions when the upstream audit row carried no
    /// <c>Description</c>. Returns <c>null</c> for unknown actions —
    /// the UI then falls back to the raw <c>action</c> verb.
    /// </summary>
    private static string? BuildFallbackSummary(string action, IDictionary<string, string?> metadata) => action switch
    {
        ActionTaskClaim    => "Task claimed",
        ActionTaskReassign => "Task reassigned",
        ActionTaskAssigned => BuildTaskAssignedSummary(metadata),
        ActionTaskCompleted          => "Task completed",
        ActionWorkflowCreated        => "Workflow created",
        ActionWorkflowCompleted      => "Workflow completed",
        ActionAdminRetry             => "Workflow retried by admin",
        ActionAdminForceComplete     => "Workflow force-completed by admin",
        ActionAdminCancel            => "Workflow cancelled by admin",
        "workflow.sla.dueSoon"       => "Task due soon",
        "workflow.sla.overdue"       => "Task overdue",
        "workflow.sla.escalated"     => "SLA escalated",
        _ => null,
    };

    private static string BuildTaskAssignedSummary(IDictionary<string, string?> metadata)
    {
        // FlowEventDispatcher.task.assigned does not write metadata; if
        // any of these keys are present (future producer enrichment),
        // surface the most specific assignee. Otherwise stay generic.
        var user = TryGet(metadata, "newAssignedUserId") ?? TryGet(metadata, "assignedUserId");
        var role = TryGet(metadata, "newAssignedRole")   ?? TryGet(metadata, "assignedRole");
        var org  = TryGet(metadata, "newAssignedOrgId")  ?? TryGet(metadata, "assignedOrgId");
        if (!string.IsNullOrEmpty(user)) return $"Task assigned to user {user}";
        if (!string.IsNullOrEmpty(role)) return $"Task assigned to role {role}";
        if (!string.IsNullOrEmpty(org))  return $"Task assigned to org {org}";
        return "Task assigned";
    }

    private static string ClassifyCategory(string action, string? eventCategory)
    {
        if (string.IsNullOrEmpty(action)) return "other";

        return action switch
        {
            ActionWorkflowCreated      => ActionWorkflowCreated,
            ActionWorkflowStateChanged => ActionWorkflowStateChanged,
            ActionWorkflowCompleted    => ActionWorkflowCompleted,
            ActionAdminRetry           => ActionAdminRetry,
            ActionAdminForceComplete   => ActionAdminForceComplete,
            ActionAdminCancel          => ActionAdminCancel,
            ActionTaskClaim            => ActionTaskClaim,
            ActionTaskReassign         => ActionTaskReassign,
            ActionTaskAssigned         => ActionTaskAssigned,
            ActionTaskCompleted        => ActionTaskCompleted,
            _ when action.StartsWith(ActionPrefixWorkflowSla,  StringComparison.Ordinal) => "workflow.sla",
            _ when action.StartsWith(ActionPrefixNotification, StringComparison.Ordinal) => "notification",
            _ when action.StartsWith("workflow.admin.",        StringComparison.Ordinal) => "workflow.admin",
            _ when action.StartsWith("workflow.",              StringComparison.Ordinal) => "workflow",
            _ when action.StartsWith("task.",                  StringComparison.Ordinal) => "task",
            _ => eventCategory is { Length: > 0 } ? eventCategory.ToLowerInvariant() : "other",
        };
    }

    /// <summary>
    /// Best-effort parse of a "X → Y" or "X -&gt; Y" segment out of a
    /// FlowEventDispatcher state-change description. Returns nulls if
    /// the description does not contain the pattern — the row still
    /// renders, just without the explicit transition columns.
    /// </summary>
    private static void ExtractStateTransition(string? description, out string? previous, out string? next)
    {
        previous = null;
        next     = null;
        if (string.IsNullOrEmpty(description)) return;

        // Try the unicode arrow first (FlowEventDispatcher uses it),
        // then a plain ASCII arrow as a friendlier fallback.
        var arrows = new[] { " → ", " -> " };
        foreach (var a in arrows)
        {
            var i = description.IndexOf(a, StringComparison.Ordinal);
            if (i <= 0) continue;
            // Walk back from `i` to the previous space — the token
            // immediately before the arrow is the previous state.
            var leftEnd   = i;
            var leftStart = description.LastIndexOf(' ', leftEnd - 1) + 1;
            var rightStart = i + a.Length;
            var rightEnd   = description.IndexOf(' ', rightStart);
            if (rightEnd < 0) rightEnd = description.Length;

            previous = description[leftStart..leftEnd].Trim();
            next     = description[rightStart..rightEnd].Trim();
            if (previous.Length == 0) previous = null;
            if (next.Length == 0)     next     = null;
            return;
        }
    }

    private static void AddIfPresent(IDictionary<string, string?> bag, string key, string? value)
    {
        if (string.IsNullOrEmpty(value)) return;
        bag[key] = value;
    }

    private static string? TryGet(IDictionary<string, string?> bag, string key)
        => bag.TryGetValue(key, out var v) ? v : null;

    private static void MergeMetadataJson(string? metadataJson, IDictionary<string, string?> bag)
    {
        if (string.IsNullOrWhiteSpace(metadataJson)) return;
        try
        {
            using var doc = JsonDocument.Parse(metadataJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return;

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                var value = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString(),
                    JsonValueKind.Number => prop.Value.ToString(),
                    JsonValueKind.True   => "true",
                    JsonValueKind.False  => "false",
                    JsonValueKind.Null   => null,
                    _                    => null, // skip nested object/array
                };
                if (value is null) continue;
                bag[prop.Name] = value;
            }
        }
        catch (JsonException)
        {
            // Malformed JSON in audit metadata is a data-quality issue,
            // not a reason to break the timeline view. Ignore silently.
        }
    }
}

// ────────────────────────────────────────────────────────────────────────
// Timeline DTOs — returned in the controller response. Kept here so the
// normalizer and its output type live next to each other.
// ────────────────────────────────────────────────────────────────────────

public sealed record TimelineEvent(
    string EventId,
    Guid? AuditId,
    DateTimeOffset OccurredAtUtc,
    string Category,
    string Action,
    string Source,
    TimelineActor? Actor,
    string? PerformedBy,
    string? Summary,
    string? PreviousStatus,
    string? NewStatus,
    IReadOnlyDictionary<string, string?> Metadata);

public sealed record TimelineActor(
    string? Id,
    string? Name,
    string? Type);
