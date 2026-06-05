using Flow.Domain.Enums;

namespace Flow.Domain.Rules;

public static class TaskStateTransitions
{
    private static readonly Dictionary<TaskItemStatus, HashSet<TaskItemStatus>> AllowedTransitions = new()
    {
        [TaskItemStatus.Open] = new() { TaskItemStatus.InProgress, TaskItemStatus.Blocked, TaskItemStatus.Cancelled },
        [TaskItemStatus.InProgress] = new() { TaskItemStatus.Blocked, TaskItemStatus.Done, TaskItemStatus.Cancelled },
        [TaskItemStatus.Blocked] = new() { TaskItemStatus.Open, TaskItemStatus.InProgress, TaskItemStatus.Cancelled },
        [TaskItemStatus.Done] = new() { TaskItemStatus.Open },
        [TaskItemStatus.Cancelled] = new() { TaskItemStatus.Open }
    };

    public static bool IsValidTransition(TaskItemStatus from, TaskItemStatus to)
    {
        return AllowedTransitions.TryGetValue(from, out var allowed) && allowed.Contains(to);
    }

    public static IReadOnlySet<TaskItemStatus> GetAllowedTransitions(TaskItemStatus from)
    {
        return AllowedTransitions.TryGetValue(from, out var allowed)
            ? allowed
            : new HashSet<TaskItemStatus>();
    }
}
