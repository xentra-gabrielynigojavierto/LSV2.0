namespace Flow.Application.DTOs;

public record AutomationActionDto
{
    public Guid? Id { get; init; }
    public string ActionType { get; init; } = string.Empty;
    public string? ConfigJson { get; init; }
    public int? Order { get; init; }

    // Optional structured condition (single { field, operator, value } object).
    // Null/empty means the action runs unconditionally.
    public string? ConditionJson { get; init; }

    // LS-FLOW-019-C — retry / failure handling. All optional in requests.
    // Responses always include normalized values (RetryCount=0, StopOnFailure=false by default).
    public int? RetryCount { get; init; }
    public int? RetryDelaySeconds { get; init; }
    public bool? StopOnFailure { get; init; }
}

public record AutomationHookResponse
{
    public Guid Id { get; init; }
    public Guid WorkflowDefinitionId { get; init; }
    public Guid WorkflowTransitionId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string TriggerEventType { get; init; } = string.Empty;
    /// <summary>LS-FLOW-020-A — Always derived from and equal to the parent workflow.</summary>
    public string ProductKey { get; init; } = string.Empty;

    // Legacy single-action fields — mirror Actions[0] for backward compatibility.
    public string ActionType { get; init; } = string.Empty;
    public string? ConfigJson { get; init; }

    // New canonical multi-action list (always populated; never null).
    public List<AutomationActionDto> Actions { get; init; } = new();

    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public record CreateAutomationHookRequest
{
    public Guid WorkflowTransitionId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string TriggerEventType { get; init; } = string.Empty;

    // Legacy single-action shape — still accepted for backward compatibility.
    public string? ActionType { get; init; }
    public string? ConfigJson { get; init; }

    // New multi-action shape — takes precedence over legacy fields if provided.
    public List<AutomationActionDto>? Actions { get; init; }

    /// <summary>
    /// LS-FLOW-020-A — Optional. If supplied, must match the parent workflow's
    /// ProductKey or the request is rejected. If omitted, the workflow's
    /// ProductKey is used.
    /// </summary>
    public string? ProductKey { get; init; }
}

public record UpdateAutomationHookRequest
{
    public string Name { get; init; } = string.Empty;

    // Legacy single-action shape — still accepted for backward compatibility.
    public string? ActionType { get; init; }
    public string? ConfigJson { get; init; }

    // New multi-action shape — takes precedence over legacy fields if provided.
    public List<AutomationActionDto>? Actions { get; init; }

    public bool IsActive { get; init; } = true;

    /// <summary>LS-FLOW-020-A — Optional. If supplied, must match the parent workflow's ProductKey.</summary>
    public string? ProductKey { get; init; }
}

public record AutomationExecutionLogResponse
{
    public Guid Id { get; init; }
    public Guid TaskId { get; init; }
    public Guid WorkflowAutomationHookId { get; init; }
    public Guid? ActionId { get; init; }
    public string HookName { get; init; } = string.Empty;
    public string ActionType { get; init; } = string.Empty;
    // Succeeded | Failed | Skipped
    public string Status { get; init; } = string.Empty;
    public string? Message { get; init; }
    // LS-FLOW-019-C — number of attempts taken (0 for skipped rows).
    public int Attempts { get; init; }
    public DateTime ExecutedAt { get; init; }
}
