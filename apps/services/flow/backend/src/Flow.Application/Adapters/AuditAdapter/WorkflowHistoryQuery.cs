namespace Flow.Application.Adapters.AuditAdapter;

/// <summary>
/// E16 — small helper that pulls audit rows for a single task or
/// workflow instance through <see cref="IAuditQueryAdapter"/> and
/// passes them through <see cref="AuditTimelineNormalizer"/>.
///
/// <para>
/// Two reasons it lives here as a static helper rather than a DI
/// service:
/// </para>
/// <list type="bullet">
///   <item>It carries no state — the inputs are an adapter and an id.</item>
///   <item>The two callers (tenant <c>WorkflowTasksController</c> and
///   tenant <c>WorkflowInstancesController</c>) already inject the
///   adapter; sharing a static helper avoids inventing a third
///   abstraction layer for what is essentially "fetch + map".</item>
/// </list>
///
/// <para>
/// Tenant safety is the caller's responsibility: each controller
/// confirms the parent entity is visible in the EF query-filtered
/// context BEFORE invoking the helper, so an out-of-scope id never
/// reaches the audit service.
/// </para>
/// </summary>
public static class WorkflowHistoryQuery
{
    /// <summary>
    /// E16 — fetch and normalize the audit timeline for a single
    /// workflow task. Audit producers historically use two
    /// <c>EntityType</c> values for tasks
    /// (<c>WorkflowTask</c> from <see cref="Services.WorkflowTaskAssignmentService"/>
    /// and <c>Task</c> from <c>FlowEventDispatcher</c>); both are
    /// queried and merged so the timeline is complete regardless of
    /// which producer wrote the row.
    /// </summary>
    public static async Task<TimelineQueryResult> GetForTaskAsync(
        IAuditQueryAdapter audit,
        Guid taskId,
        string? tenantId,
        CancellationToken ct)
    {
        var idStr = taskId.ToString();

        var workflowTaskRows = await audit.GetEventsForEntityAsync(
            entityType: "WorkflowTask",
            entityId:   idStr,
            tenantId:   tenantId,
            cancellationToken: ct);

        var legacyTaskRows = await audit.GetEventsForEntityAsync(
            entityType: "Task",
            entityId:   idStr,
            tenantId:   tenantId,
            cancellationToken: ct);

        // Merge by EventId so a producer that double-writes a row to
        // both entity types (none today, but a defensive de-dupe is
        // cheap) never surfaces twice on the UI.
        var merged = new Dictionary<string, AuditEventRecord>(
            workflowTaskRows.Events.Count + legacyTaskRows.Events.Count,
            StringComparer.Ordinal);

        foreach (var r in workflowTaskRows.Events) merged[r.EventId] = r;
        foreach (var r in legacyTaskRows.Events)   merged.TryAdd(r.EventId, r);

        var events = AuditTimelineNormalizer.Normalize(merged.Values);
        var truncated = workflowTaskRows.Truncated || legacyTaskRows.Truncated;
        return new TimelineQueryResult(events, truncated);
    }

    /// <summary>
    /// E16 — fetch and normalize the audit timeline for a single
    /// workflow instance. Mirrors the behaviour of the existing
    /// admin endpoint so the tenant-scoped and admin-scoped responses
    /// are byte-for-byte identical for the same row.
    /// </summary>
    public static async Task<TimelineQueryResult> GetForWorkflowInstanceAsync(
        IAuditQueryAdapter audit,
        Guid workflowInstanceId,
        string? tenantId,
        CancellationToken ct)
    {
        var fetch = await audit.GetEventsForEntityAsync(
            entityType: "WorkflowInstance",
            entityId:   workflowInstanceId.ToString(),
            tenantId:   tenantId,
            cancellationToken: ct);

        return new TimelineQueryResult(
            AuditTimelineNormalizer.Normalize(fetch.Events),
            fetch.Truncated);
    }
}

/// <summary>E16 — return shape for <see cref="WorkflowHistoryQuery"/>.</summary>
public sealed record TimelineQueryResult(
    IReadOnlyList<TimelineEvent> Events,
    bool Truncated);
