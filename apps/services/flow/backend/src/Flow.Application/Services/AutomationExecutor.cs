using System.Text.Json;
using Flow.Application.Interfaces;
using Flow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Flow.Application.Services;

public static class TriggerEventTypes
{
    public const string TransitionCompleted = "TRANSITION_COMPLETED";

    public static readonly string[] All = [TransitionCompleted];
}

public static class ActionTypes
{
    public const string AddActivityEvent = "ADD_ACTIVITY_EVENT";
    public const string SetDueDateOffsetDays = "SET_DUE_DATE_OFFSET_DAYS";
    public const string AssignRole = "ASSIGN_ROLE";
    public const string AssignUser = "ASSIGN_USER";
    public const string AssignOrg = "ASSIGN_ORG";

    public static readonly string[] All = [AddActivityEvent, SetDueDateOffsetDays, AssignRole, AssignUser, AssignOrg];
}

public class AutomationExecutionResult
{
    public Guid HookId { get; set; }
    public string HookName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Message { get; set; }
}

public interface IAutomationExecutor
{
    Task<List<AutomationExecutionResult>> ExecuteTransitionHooksAsync(
        Guid transitionId,
        TaskItem task,
        CancellationToken cancellationToken = default);
}

public class AutomationExecutor : IAutomationExecutor
{
    private readonly IFlowDbContext _db;
    private readonly ILogger<AutomationExecutor> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public AutomationExecutor(IFlowDbContext db, ILogger<AutomationExecutor> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<List<AutomationExecutionResult>> ExecuteTransitionHooksAsync(
        Guid transitionId,
        TaskItem task,
        CancellationToken cancellationToken = default)
    {
        var hooks = await _db.AutomationHooks
            .AsNoTracking()
            .Include(h => h.Actions)
            .Where(h => h.WorkflowTransitionId == transitionId
                     && h.IsActive
                     && h.TriggerEventType == TriggerEventTypes.TransitionCompleted)
            .ToListAsync(cancellationToken);

        if (hooks.Count == 0) return [];

        var results = new List<AutomationExecutionResult>();

        foreach (var hook in hooks)
        {
            var result = new AutomationExecutionResult
            {
                HookId = hook.Id,
                HookName = hook.Name
            };

            // Resolve the action list: prefer the new Actions collection; fall back
            // to a synthetic single-item list built from the legacy ActionType/ConfigJson
            // for hooks that predate LS-FLOW-019-A (or were created via a legacy path).
            var actions = hook.Actions is { Count: > 0 }
                ? hook.Actions.OrderBy(a => a.Order).ToList()
                : new List<AutomationAction>
                {
                    new()
                    {
                        Id = Guid.Empty, // sentinel: no child row — log with ActionId = null
                        HookId = hook.Id,
                        ActionType = hook.ActionType,
                        ConfigJson = hook.ConfigJson,
                        Order = 0
                    }
                };

            var anyFailed = false;
            var anySucceeded = false;
            var stepMessages = new List<string>();
            var stoppedEarly = false;
            AutomationAction? stopTrigger = null;
            var skippedRemaining = 0;

            for (var i = 0; i < actions.Count; i++)
            {
                var action = actions[i];

                // Evaluate the optional condition first. A malformed persisted
                // condition (only reachable via direct DB edit, since create/update
                // validates) is logged as Failed and we move on to the next action.
                // Note: a malformed condition is itself a hook-level failure, but it
                // is NOT subject to retry (the failure is in policy, not execution)
                // and it does NOT trigger StopOnFailure (we couldn't even reach the
                // action's stop policy decision).
                bool shouldRun;
                AutomationCondition? parsedCondition = null;
                try
                {
                    if (string.IsNullOrWhiteSpace(action.ConditionJson))
                    {
                        shouldRun = true;
                    }
                    else
                    {
                        parsedCondition = AutomationConditionEvaluator.Parse(action.ConditionJson);
                        shouldRun = AutomationConditionEvaluator.Evaluate(action, task);
                    }
                }
                catch (Exception condEx)
                {
                    _logger.LogWarning(condEx,
                        "Automation hook {HookId} action {ActionType} (order {Order}) has malformed condition for task {TaskId}",
                        hook.Id, action.ActionType, action.Order, task.Id);
                    EmitLog(task, hook, action, "Failed", $"Malformed condition: {condEx.Message}", attempts: 0);
                    anyFailed = true;
                    stepMessages.Add($"[{action.Order}:{action.ActionType}] Failed");
                    continue;
                }

                if (!shouldRun)
                {
                    // Skipped actions never trigger retries and never trigger StopOnFailure.
                    var skipMsg = parsedCondition is null
                        ? "Condition not met"
                        : $"Condition not met: {AutomationConditionEvaluator.Describe(parsedCondition)}";
                    EmitLog(task, hook, action, "Skipped", skipMsg, attempts: 0);
                    stepMessages.Add($"[{action.Order}:{action.ActionType}] Skipped");
                    continue;
                }

                // -------- Retry loop --------
                // Total attempts = 1 + RetryCount. A successful attempt breaks early.
                // Between failed attempts, optionally wait RetryDelaySeconds (synchronously).
                var maxAttempts = 1 + Math.Max(0, action.RetryCount);
                var attempt = 0;
                var succeeded = false;
                Exception? lastError = null;
                while (attempt < maxAttempts)
                {
                    attempt++;
                    try
                    {
                        ExecuteAction(action, hook, task);
                        succeeded = true;
                        break;
                    }
                    catch (Exception ex)
                    {
                        lastError = ex;
                        _logger.LogWarning(ex,
                            "Automation hook {HookId} action {ActionType} (order {Order}) attempt {Attempt}/{Max} failed for task {TaskId}",
                            hook.Id, action.ActionType, action.Order, attempt, maxAttempts, task.Id);

                        if (attempt < maxAttempts)
                        {
                            var delay = action.RetryDelaySeconds ?? 0;
                            if (delay > 0)
                            {
                                await Task.Delay(TimeSpan.FromSeconds(delay), cancellationToken);
                            }
                        }
                    }
                }

                if (succeeded)
                {
                    var msg = attempt == 1
                        ? $"Action '{action.ActionType}' (order {action.Order}) executed successfully"
                        : $"Action '{action.ActionType}' (order {action.Order}) succeeded on attempt {attempt}";
                    EmitLog(task, hook, action, "Succeeded", msg, attempts: attempt);
                    anySucceeded = true;
                    stepMessages.Add($"[{action.Order}:{action.ActionType}] Succeeded ({attempt} attempt{(attempt == 1 ? "" : "s")})");
                }
                else
                {
                    var msg = $"Failed after {attempt} attempt{(attempt == 1 ? "" : "s")}: {lastError?.Message}";
                    EmitLog(task, hook, action, "Failed", msg, attempts: attempt);
                    anyFailed = true;
                    stepMessages.Add($"[{action.Order}:{action.ActionType}] Failed after {attempt} attempt{(attempt == 1 ? "" : "s")}");

                    if (action.StopOnFailure)
                    {
                        stoppedEarly = true;
                        stopTrigger = action;
                        skippedRemaining = actions.Count - (i + 1);
                        break; // Do not produce log rows for remaining actions.
                    }
                }
            }

            // Hook-level aggregation:
            //  - any failure -> Failed (with stop-on-failure note if applicable)
            //  - else if at least one success -> Succeeded
            //  - else (everything skipped) -> Succeeded with explicit note
            if (anyFailed)
            {
                result.Status = "Failed";
                if (stoppedEarly && stopTrigger is not null)
                {
                    stepMessages.Add($"execution stopped after [{stopTrigger.Order}:{stopTrigger.ActionType}] (stop-on-failure); {skippedRemaining} action{(skippedRemaining == 1 ? "" : "s")} not executed");
                }
            }
            else if (anySucceeded)
            {
                result.Status = "Succeeded";
            }
            else
            {
                result.Status = "Succeeded";
                stepMessages.Add("(all actions skipped — no work to do)");
            }
            result.Message = string.Join("; ", stepMessages);
            results.Add(result);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return results;
    }

    private void EmitLog(TaskItem task, WorkflowAutomationHook hook, AutomationAction action, string status, string? message, int attempts)
    {
        _db.AutomationExecutionLogs.Add(new AutomationExecutionLog
        {
            TaskId = task.Id,
            WorkflowAutomationHookId = hook.Id,
            ActionId = action.Id == Guid.Empty ? null : action.Id,
            // Snapshot so the log stays meaningful even if the action
            // is later edited or deleted.
            ActionType = action.ActionType,
            ActionOrder = action.Order,
            Status = status,
            Message = message,
            Attempts = attempts,
            ExecutedAt = DateTime.UtcNow
        });
    }

    private void ExecuteAction(AutomationAction action, WorkflowAutomationHook hook, TaskItem task)
    {
        switch (action.ActionType)
        {
            case ActionTypes.AddActivityEvent:
                ExecuteAddActivityEvent(action, hook, task);
                break;

            case ActionTypes.SetDueDateOffsetDays:
                ExecuteSetDueDateOffset(action, task);
                break;

            case ActionTypes.AssignRole:
                ExecuteAssignRole(action, task);
                break;

            case ActionTypes.AssignUser:
                ExecuteAssignUser(action, task);
                break;

            case ActionTypes.AssignOrg:
                ExecuteAssignOrg(action, task);
                break;

            default:
                throw new InvalidOperationException($"Unsupported action type: {action.ActionType}");
        }
    }

    private void ExecuteAddActivityEvent(AutomationAction action, WorkflowAutomationHook hook, TaskItem task)
    {
        var config = ParseConfig<AddActivityEventConfig>(action.ConfigJson);
        var message = config?.MessageTemplate ?? $"Automation '{hook.Name}' executed";
        _logger.LogInformation("Automation activity for task {TaskId}: {Message}", task.Id, message);
    }

    private void ExecuteSetDueDateOffset(AutomationAction action, TaskItem task)
    {
        var config = ParseConfig<SetDueDateOffsetConfig>(action.ConfigJson);
        if (config is null || config.Days <= 0)
            throw new InvalidOperationException("SET_DUE_DATE_OFFSET_DAYS requires positive 'days' value in config");

        if (!task.DueDate.HasValue)
        {
            task.DueDate = DateTime.UtcNow.AddDays(config.Days);
        }
    }

    private void ExecuteAssignRole(AutomationAction action, TaskItem task)
    {
        var config = ParseConfig<AssignRoleConfig>(action.ConfigJson);
        if (string.IsNullOrWhiteSpace(config?.RoleKey))
            throw new InvalidOperationException("ASSIGN_ROLE requires 'roleKey' in config");

        task.AssignedToRoleKey = config.RoleKey;
    }

    private void ExecuteAssignUser(AutomationAction action, TaskItem task)
    {
        var config = ParseConfig<AssignUserConfig>(action.ConfigJson);
        if (string.IsNullOrWhiteSpace(config?.UserId))
            throw new InvalidOperationException("ASSIGN_USER requires 'userId' in config");

        task.AssignedToUserId = config.UserId;
    }

    private void ExecuteAssignOrg(AutomationAction action, TaskItem task)
    {
        var config = ParseConfig<AssignOrgConfig>(action.ConfigJson);
        if (string.IsNullOrWhiteSpace(config?.OrgId))
            throw new InvalidOperationException("ASSIGN_ORG requires 'orgId' in config");

        task.AssignedToOrgId = config.OrgId;
    }

    private static T? ParseConfig<T>(string? configJson) where T : class
    {
        if (string.IsNullOrWhiteSpace(configJson)) return null;
        try
        {
            return JsonSerializer.Deserialize<T>(configJson, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public static void ValidateConfig(string actionType, string? configJson)
    {
        switch (actionType)
        {
            case ActionTypes.AddActivityEvent:
                if (!string.IsNullOrWhiteSpace(configJson))
                {
                    var actConfig = ParseConfigStatic<AddActivityEventConfig>(configJson);
                    if (actConfig is null)
                        throw new Exceptions.ValidationException("ADD_ACTIVITY_EVENT has malformed config JSON");
                }
                break;

            case ActionTypes.SetDueDateOffsetDays:
                var dueDateConfig = ParseConfigStatic<SetDueDateOffsetConfig>(configJson);
                if (dueDateConfig is null || dueDateConfig.Days <= 0)
                    throw new Exceptions.ValidationException("SET_DUE_DATE_OFFSET_DAYS requires a positive 'days' value in config JSON");
                break;

            case ActionTypes.AssignRole:
                var roleConfig = ParseConfigStatic<AssignRoleConfig>(configJson);
                if (string.IsNullOrWhiteSpace(roleConfig?.RoleKey))
                    throw new Exceptions.ValidationException("ASSIGN_ROLE requires 'roleKey' in config JSON");
                break;

            case ActionTypes.AssignUser:
                var userConfig = ParseConfigStatic<AssignUserConfig>(configJson);
                if (string.IsNullOrWhiteSpace(userConfig?.UserId))
                    throw new Exceptions.ValidationException("ASSIGN_USER requires 'userId' in config JSON");
                break;

            case ActionTypes.AssignOrg:
                var orgConfig = ParseConfigStatic<AssignOrgConfig>(configJson);
                if (string.IsNullOrWhiteSpace(orgConfig?.OrgId))
                    throw new Exceptions.ValidationException("ASSIGN_ORG requires 'orgId' in config JSON");
                break;

            default:
                throw new Exceptions.ValidationException($"Unsupported action type: {actionType}");
        }
    }

    private static T? ParseConfigStatic<T>(string? configJson) where T : class
    {
        if (string.IsNullOrWhiteSpace(configJson)) return null;
        try
        {
            return JsonSerializer.Deserialize<T>(configJson, JsonOptions);
        }
        catch
        {
            return null;
        }
    }
}

public class AddActivityEventConfig
{
    public string? MessageTemplate { get; set; }
}

public class SetDueDateOffsetConfig
{
    public int Days { get; set; }
}

public class AssignRoleConfig
{
    public string? RoleKey { get; set; }
}

public class AssignUserConfig
{
    public string? UserId { get; set; }
}

public class AssignOrgConfig
{
    public string? OrgId { get; set; }
}
