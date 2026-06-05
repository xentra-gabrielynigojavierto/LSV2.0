using System.Text.Json;
using System.Text.RegularExpressions;

namespace Reports.Application.Formulas;

public static partial class FormulaValidator
{
    private static readonly HashSet<string> AllowedOperators = new()
    {
        "+", "-", "*", "/", "%"
    };

    private static readonly HashSet<string> AllowedFunctions = new(StringComparer.OrdinalIgnoreCase)
    {
        "ABS", "ROUND", "FLOOR", "CEIL", "MIN", "MAX", "IF", "COALESCE"
    };

    private static readonly HashSet<string> AllowedDataTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "number", "string", "boolean", "date"
    };

    private static readonly JsonSerializerOptions _caseInsensitiveOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    [GeneratedRegex(@"^[a-zA-Z_][a-zA-Z0-9_]*$")]
    private static partial Regex FieldNamePattern();

    public static string? Validate(string formulaConfigJson)
    {
        if (string.IsNullOrWhiteSpace(formulaConfigJson))
            return null;

        List<FormulaDefinition>? formulas;
        try
        {
            formulas = JsonSerializer.Deserialize<List<FormulaDefinition>>(formulaConfigJson, _caseInsensitiveOptions);
        }
        catch (JsonException ex)
        {
            return $"Invalid JSON: {ex.Message}";
        }

        if (formulas is null || formulas.Count == 0)
            return null;

        var fieldNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var formula in formulas)
        {
            if (string.IsNullOrWhiteSpace(formula.FieldName))
                return "Each formula must have a FieldName.";

            if (!FieldNamePattern().IsMatch(formula.FieldName))
                return $"Invalid FieldName '{formula.FieldName}': must start with a letter/underscore and contain only alphanumeric/underscore characters.";

            if (!fieldNames.Add(formula.FieldName))
                return $"Duplicate FieldName '{formula.FieldName}'.";

            if (string.IsNullOrWhiteSpace(formula.Label))
                return $"Formula '{formula.FieldName}' must have a Label.";

            if (string.IsNullOrWhiteSpace(formula.Expression))
                return $"Formula '{formula.FieldName}' must have an Expression.";

            if (formula.Expression.Length > 500)
                return $"Formula '{formula.FieldName}' expression exceeds 500 character limit.";

            if (!AllowedDataTypes.Contains(formula.DataType))
                return $"Formula '{formula.FieldName}' has unsupported DataType '{formula.DataType}'. Allowed: {string.Join(", ", AllowedDataTypes)}.";

            var exprError = ValidateExpression(formula.Expression);
            if (exprError is not null)
                return $"Formula '{formula.FieldName}': {exprError}";
        }

        return null;
    }

    private static string? ValidateExpression(string expression)
    {
        if (expression.Contains(';'))
            return "Semicolons are not allowed in expressions.";

        if (expression.Contains("--") || expression.Contains("/*") || expression.Contains("*/"))
            return "SQL-style comments are not allowed.";

        var dangerousKeywords = new[] { "SELECT", "INSERT", "UPDATE", "DELETE", "DROP", "ALTER", "EXEC", "EXECUTE", "UNION" };
        var upper = expression.ToUpperInvariant();
        foreach (var keyword in dangerousKeywords)
        {
            if (Regex.IsMatch(upper, $@"\b{keyword}\b"))
                return $"Keyword '{keyword}' is not allowed in expressions.";
        }

        return null;
    }
}
