using Flow.Domain.Common;

namespace Flow.Domain.Entities;

public class AutomationAction : BaseEntity
{
    public Guid HookId { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string? ConfigJson { get; set; }
    public int Order { get; set; }

    // Optional structured condition (single { field, operator, value } object).
    // Null/empty means the action runs unconditionally. See AutomationConditionEvaluator
    // for the supported field/operator whitelist.
    public string? ConditionJson { get; set; }

    // LS-FLOW-019-C: retry / failure handling.
    // RetryCount = number of retries AFTER the first attempt. Total attempts = 1 + RetryCount.
    // Default 0 → single attempt, identical to pre-019-C behavior.
    public int RetryCount { get; set; }

    // Optional synchronous delay between retry attempts. null or 0 → no delay.
    public int? RetryDelaySeconds { get; set; }

    // If true, when this action ultimately fails (after all retries are exhausted)
    // the executor stops processing remaining actions in the same hook. Default false.
    public bool StopOnFailure { get; set; }

    public WorkflowAutomationHook Hook { get; set; } = null!;
}
