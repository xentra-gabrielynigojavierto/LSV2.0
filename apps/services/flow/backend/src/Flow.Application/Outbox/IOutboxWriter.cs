using Flow.Domain.Entities;

namespace Flow.Application.Outbox;

/// <summary>
/// LS-FLOW-E10.2 — caller-facing seam for enqueueing transactional
/// outbox rows. Implementations MUST only attach the row to the
/// EF change tracker; the actual INSERT happens when the calling code
/// invokes <c>SaveChangesAsync</c> on its own <see cref="IFlowDbContext"/>,
/// which guarantees the outbox write commits in the same transaction
/// as the workflow-state mutation.
///
/// <para>
/// The returned <see cref="OutboxMessage"/> is exposed only so the caller
/// can include its <c>Id</c> in subsequent log lines / responses; it
/// must NOT be mutated after enqueue.
/// </para>
/// </summary>
public interface IOutboxWriter
{
    OutboxMessage Enqueue(
        string eventType,
        Guid? workflowInstanceId,
        object payload);
}
