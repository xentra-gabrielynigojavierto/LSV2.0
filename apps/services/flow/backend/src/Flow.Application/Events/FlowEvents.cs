namespace Flow.Application.Events;

/// <summary>
/// Lightweight in-process event abstraction. Application services raise
/// these events; the dispatcher fans out to the audit + notification
/// adapter seams. This is deliberately NOT a platform event bus — it
/// exists only to decouple Flow application code from adapter wiring.
/// </summary>
public interface IFlowEventDispatcher
{
    Task PublishAsync(IFlowEvent flowEvent, CancellationToken cancellationToken = default);
}

public interface IFlowEvent
{
    string EventKey { get; }
    string? TenantId { get; }
    DateTime OccurredAtUtc { get; }
}

public sealed record WorkflowCreatedEvent(
    Guid WorkflowId,
    string Name,
    string? ProductKey,
    string? TenantId,
    string? UserId,
    DateTime OccurredAtUtc) : IFlowEvent
{
    public string EventKey => "flow.workflow.created";
}

public sealed record WorkflowStateChangedEvent(
    Guid WorkflowId,
    string FromState,
    string ToState,
    string? TenantId,
    string? UserId,
    DateTime OccurredAtUtc) : IFlowEvent
{
    public string EventKey => "flow.workflow.state_changed";
}

public sealed record WorkflowCompletedEvent(
    Guid WorkflowId,
    string? TenantId,
    string? UserId,
    DateTime OccurredAtUtc) : IFlowEvent
{
    public string EventKey => "flow.workflow.completed";
}

public sealed record TaskAssignedEvent(
    Guid TaskId,
    string? AssignedToUserId,
    string? AssignedToRoleKey,
    string? AssignedToOrgId,
    string? TenantId,
    string? AssignedByUserId,
    DateTime OccurredAtUtc,
    string? TaskTitle = null) : IFlowEvent
{
    public string EventKey => "flow.task.assigned";
}

public sealed record TaskCompletedEvent(
    Guid TaskId,
    string? TenantId,
    string? CompletedByUserId,
    DateTime OccurredAtUtc,
    string? TaskTitle = null) : IFlowEvent
{
    public string EventKey => "flow.task.completed";
}
