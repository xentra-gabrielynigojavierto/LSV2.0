using Flow.Domain.Common;

namespace Flow.Domain.Entities;

/// <summary>
/// LS-FLOW-E10.2 — Transactional outbox row owned by Flow.
///
/// <para>
/// One row per durable side-effect that must follow a workflow or admin
/// state mutation. The row is INSERTed in the same EF
/// <c>SaveChangesAsync</c> as the state change so the two writes share an
/// implicit MySQL transaction — if the state UPDATE rolls back (e.g. on
/// optimistic-concurrency conflict) the outbox row never commits, and
/// vice-versa.
/// </para>
///
/// <para>
/// A background <c>OutboxProcessor</c> claims due rows in batches via
/// <c>FOR UPDATE SKIP LOCKED</c>, dispatches them to per-event-type
/// handlers (audit, notification, re-drive), reschedules retryable
/// failures with exponential backoff, and dead-letters rows that exhaust
/// the configured attempt budget. Workflow state remains committed even
/// if outbox processing later fails — that is the whole point of the
/// pattern.
/// </para>
///
/// <para>
/// Inherits <see cref="AuditableEntity"/> for <c>TenantId</c>,
/// <c>CreatedAt</c>, etc. <c>TenantId</c> is auto-populated from the
/// request's tenant context inside <see cref="FlowDbContext.SaveChangesAsync"/>.
/// </para>
/// </summary>
public class OutboxMessage : AuditableEntity
{
    /// <summary>Logical event type — see <c>OutboxEventTypes</c>.</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Workflow instance this event is about. Nullable only because some
    /// future event types may legitimately not have one; today every
    /// emitted event ties back to an instance.
    /// </summary>
    public Guid? WorkflowInstanceId { get; set; }

    /// <summary>JSON-serialised handler payload. Keep small; do not store secrets.</summary>
    public string PayloadJson { get; set; } = "{}";

    /// <summary>Pending / Processing / Succeeded / Failed / DeadLettered.</summary>
    public string Status { get; set; } = OutboxStatus.Pending;

    /// <summary>Number of dispatch attempts so far.</summary>
    public int AttemptCount { get; set; }

    /// <summary>Earliest UTC time at which the worker may attempt this row again.</summary>
    public DateTime NextAttemptAt { get; set; } = DateTime.UtcNow;

    /// <summary>Last exception type/message (truncated). Null on success.</summary>
    public string? LastError { get; set; }

    /// <summary>UTC time the row reached a terminal status (Succeeded or DeadLettered).</summary>
    public DateTime? ProcessedAt { get; set; }
}
