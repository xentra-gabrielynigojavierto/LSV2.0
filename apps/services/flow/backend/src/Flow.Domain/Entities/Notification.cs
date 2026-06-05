using Flow.Domain.Common;

namespace Flow.Domain.Entities;

public class Notification : BaseEntity
{
    public Guid? TaskId { get; set; }
    public Guid? WorkflowDefinitionId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? TargetUserId { get; set; }
    public string? TargetRoleKey { get; set; }
    public string? TargetOrgId { get; set; }
    public string Status { get; set; } = NotificationStatus.Unread;
    public string SourceType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }

    public TaskItem? Task { get; set; }
    public FlowDefinition? WorkflowDefinition { get; set; }
}

public static class NotificationStatus
{
    public const string Unread = "Unread";
    public const string Read = "Read";
}

public static class NotificationType
{
    public const string TaskAssigned = "TASK_ASSIGNED";
    public const string TaskReassigned = "TASK_REASSIGNED";
    public const string TaskTransitioned = "TASK_TRANSITIONED";
    public const string AutomationSucceeded = "AUTOMATION_SUCCEEDED";
    public const string AutomationFailed = "AUTOMATION_FAILED";
    public const string WorkflowAssigned = "WORKFLOW_ASSIGNED";
}

public static class NotificationSourceType
{
    public const string WorkflowTransition = "WorkflowTransition";
    public const string AutomationHook = "AutomationHook";
    public const string Assignment = "Assignment";
    public const string System = "System";
}
