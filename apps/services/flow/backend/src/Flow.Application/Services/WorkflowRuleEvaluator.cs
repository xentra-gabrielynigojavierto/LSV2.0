using System.Text.Json;
using Flow.Domain.Entities;

namespace Flow.Application.Services;

public class TransitionRules
{
    public bool RequireTitle { get; set; }
    public bool RequireDescription { get; set; }
    public bool RequireAssignment { get; set; }
    public bool RequireDueDate { get; set; }
}

public class RuleEvaluationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
}

public static class WorkflowRuleEvaluator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static TransitionRules? ParseRules(string? rulesJson)
    {
        if (string.IsNullOrWhiteSpace(rulesJson))
            return null;

        try
        {
            return JsonSerializer.Deserialize<TransitionRules>(rulesJson, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public static RuleEvaluationResult Evaluate(TransitionRules? rules, TaskItem task)
    {
        var result = new RuleEvaluationResult { IsValid = true };

        if (rules is null)
            return result;

        if (rules.RequireTitle && string.IsNullOrWhiteSpace(task.Title))
        {
            result.Errors.Add("Title is required for this transition");
        }

        if (rules.RequireDescription && string.IsNullOrWhiteSpace(task.Description))
        {
            result.Errors.Add("Description is required for this transition");
        }

        if (rules.RequireAssignment)
        {
            var hasAssignment = !string.IsNullOrWhiteSpace(task.AssignedToUserId)
                             || !string.IsNullOrWhiteSpace(task.AssignedToRoleKey);
            if (!hasAssignment)
            {
                result.Errors.Add("Assignment (user or role) is required for this transition");
            }
        }

        if (rules.RequireDueDate && !task.DueDate.HasValue)
        {
            result.Errors.Add("Due date is required for this transition");
        }

        result.IsValid = result.Errors.Count == 0;
        return result;
    }

    public static string? SerializeRules(TransitionRules? rules)
    {
        if (rules is null)
            return null;

        if (!rules.RequireTitle && !rules.RequireDescription && !rules.RequireAssignment && !rules.RequireDueDate)
            return null;

        return JsonSerializer.Serialize(rules, JsonOptions);
    }
}
