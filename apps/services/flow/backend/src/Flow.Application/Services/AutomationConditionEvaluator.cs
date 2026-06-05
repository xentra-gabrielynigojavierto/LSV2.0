using System.Text.Json;
using Flow.Application.Exceptions;
using Flow.Domain.Entities;

namespace Flow.Application.Services;

public static class ConditionFields
{
    public const string Status = "status";
    public const string AssignedToUserId = "assignedToUserId";
    public const string AssignedToRoleKey = "assignedToRoleKey";
    public const string AssignedToOrgId = "assignedToOrgId";
    public const string WorkflowStageId = "workflowStageId";
    public const string FlowDefinitionId = "flowDefinitionId";

    public static readonly string[] All =
    [
        Status, AssignedToUserId, AssignedToRoleKey, AssignedToOrgId,
        WorkflowStageId, FlowDefinitionId
    ];
}

public static class ConditionOperators
{
    public const string Equals = "equals";
    public const string NotEquals = "not_equals";
    public const string In = "in";
    public const string NotIn = "not_in";

    public static readonly string[] All = [Equals, NotEquals, In, NotIn];
    public static readonly string[] ScalarOps = [Equals, NotEquals];
    public static readonly string[] ArrayOps = [In, NotIn];
}

public sealed class AutomationCondition
{
    public string Field { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;

    // Scalar value (for equals/not_equals) — a JSON string/number/null.
    public string? ScalarValue { get; set; }

    // Array of string values (for in/not_in). Serialized to strings for uniform comparison.
    public List<string>? ArrayValues { get; set; }

    public bool IsArrayOp => ConditionOperators.ArrayOps.Contains(Operator);
}

public static class AutomationConditionEvaluator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Parse + validate the structured condition JSON. Throws ValidationException on
    /// any structural problem. Use at create/update time so runtime can trust persisted shape.
    /// </summary>
    public static AutomationCondition Parse(string conditionJson)
    {
        if (string.IsNullOrWhiteSpace(conditionJson))
            throw new ValidationException("Condition JSON is empty");

        JsonElement root;
        try
        {
            root = JsonSerializer.Deserialize<JsonElement>(conditionJson, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new ValidationException($"Condition JSON is malformed: {ex.Message}");
        }

        if (root.ValueKind != JsonValueKind.Object)
            throw new ValidationException("Condition must be a JSON object");

        var field = ReadStringProp(root, "field");
        var op = ReadStringProp(root, "operator");

        if (string.IsNullOrWhiteSpace(field))
            throw new ValidationException("Condition 'field' is required");
        if (!ConditionFields.All.Contains(field))
            throw new ValidationException($"Unsupported condition field: {field}");
        if (string.IsNullOrWhiteSpace(op))
            throw new ValidationException("Condition 'operator' is required");
        if (!ConditionOperators.All.Contains(op))
            throw new ValidationException($"Unsupported condition operator: {op}");

        if (!root.TryGetProperty("value", out var valueEl))
            throw new ValidationException("Condition 'value' is required");

        var cond = new AutomationCondition { Field = field, Operator = op };

        if (ConditionOperators.ArrayOps.Contains(op))
        {
            if (valueEl.ValueKind != JsonValueKind.Array)
                throw new ValidationException($"Operator '{op}' requires an array 'value'");
            var items = new List<string>();
            foreach (var item in valueEl.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Null)
                    throw new ValidationException($"Operator '{op}' array values cannot be null");
                if (item.ValueKind == JsonValueKind.Object || item.ValueKind == JsonValueKind.Array)
                    throw new ValidationException($"Operator '{op}' array values must be scalars (string/number/bool)");
                items.Add(JsonElementToString(item));
            }
            if (items.Count == 0)
                throw new ValidationException($"Operator '{op}' requires a non-empty array 'value'");
            cond.ArrayValues = items;
        }
        else
        {
            if (valueEl.ValueKind == JsonValueKind.Array || valueEl.ValueKind == JsonValueKind.Object)
                throw new ValidationException($"Operator '{op}' requires a scalar 'value'");
            cond.ScalarValue = valueEl.ValueKind == JsonValueKind.Null ? null : JsonElementToString(valueEl);
        }

        return cond;
    }

    /// <summary>
    /// Returns true if the action should run.
    ///   - Null/empty ConditionJson → true (unconditional).
    ///   - Valid condition that matches → true.
    ///   - Valid condition that does not match → false (caller should skip).
    /// Throws ValidationException if the persisted ConditionJson is malformed (caller
    /// should treat as a Failed action and continue with the next).
    /// </summary>
    public static bool Evaluate(AutomationAction action, TaskItem task)
    {
        if (string.IsNullOrWhiteSpace(action.ConditionJson)) return true;
        var cond = Parse(action.ConditionJson);
        return Matches(cond, task);
    }

    /// <summary>
    /// Render the condition for human-readable log messages, e.g. "status equals Done".
    /// </summary>
    public static string Describe(AutomationCondition cond)
    {
        var valueStr = cond.IsArrayOp
            ? "[" + string.Join(",", cond.ArrayValues ?? new List<string>()) + "]"
            : (cond.ScalarValue ?? "null");
        return $"{cond.Field} {cond.Operator} {valueStr}";
    }

    private static bool Matches(AutomationCondition cond, TaskItem task)
    {
        var taskFieldValue = ReadTaskField(task, cond.Field);

        // Null/missing task field policy: condition is treated as not matched
        // for every operator. equals/in against null → false. not_equals/not_in
        // against null → also false (we never treat null as matchable).
        if (taskFieldValue is null) return false;

        return cond.Operator switch
        {
            ConditionOperators.Equals    => string.Equals(taskFieldValue, cond.ScalarValue, StringComparison.Ordinal),
            ConditionOperators.NotEquals => !string.Equals(taskFieldValue, cond.ScalarValue, StringComparison.Ordinal),
            ConditionOperators.In        => cond.ArrayValues?.Contains(taskFieldValue) == true,
            ConditionOperators.NotIn     => cond.ArrayValues?.Contains(taskFieldValue) != true,
            _ => false
        };
    }

    private static string? ReadTaskField(TaskItem task, string field) => field switch
    {
        ConditionFields.Status            => task.Status.ToString(),
        ConditionFields.AssignedToUserId  => task.AssignedToUserId,
        ConditionFields.AssignedToRoleKey => task.AssignedToRoleKey,
        ConditionFields.AssignedToOrgId   => task.AssignedToOrgId,
        ConditionFields.WorkflowStageId   => task.WorkflowStageId?.ToString(),
        ConditionFields.FlowDefinitionId  => task.FlowDefinitionId?.ToString(),
        _ => null
    };

    private static string? ReadStringProp(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var el)) return null;
        return el.ValueKind == JsonValueKind.String ? el.GetString() : null;
    }

    private static string JsonElementToString(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.String => el.GetString() ?? string.Empty,
        JsonValueKind.Number => el.GetRawText(),
        JsonValueKind.True   => "true",
        JsonValueKind.False  => "false",
        _ => el.GetRawText()
    };
}
