using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Reports.Application.Formulas;

public static partial class FormulaEvaluator
{
    [GeneratedRegex(@"\[([^\]]+)\]")]
    private static partial Regex FieldReferencePattern();

    [GeneratedRegex(@"IF\s*\(\s*(.+?)\s*,\s*(.+?)\s*,\s*(.+?)\s*\)", RegexOptions.IgnoreCase)]
    private static partial Regex IfFunctionPattern();

    [GeneratedRegex(@"COALESCE\s*\(\s*(.+?)\s*,\s*(.+?)\s*\)", RegexOptions.IgnoreCase)]
    private static partial Regex CoalesceFunctionPattern();

    [GeneratedRegex(@"(ABS|ROUND|FLOOR|CEIL|MIN|MAX)\s*\(\s*(.+?)\s*\)", RegexOptions.IgnoreCase)]
    private static partial Regex MathFunctionPattern();

    private static readonly JsonSerializerOptions _caseInsensitiveOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static List<FormulaDefinition>? ParseConfig(string? formulaConfigJson)
    {
        if (string.IsNullOrWhiteSpace(formulaConfigJson))
            return null;

        try
        {
            return JsonSerializer.Deserialize<List<FormulaDefinition>>(formulaConfigJson, _caseInsensitiveOptions);
        }
        catch
        {
            return null;
        }
    }

    public static void ApplyFormulas(
        List<FormulaDefinition> formulas,
        List<Dictionary<string, object?>> rows,
        List<Reports.Contracts.Adapters.TabularColumn> columns)
    {
        foreach (var formula in formulas)
        {
            if (!columns.Any(c => string.Equals(c.Key, formula.FieldName, StringComparison.OrdinalIgnoreCase)))
            {
                columns.Add(new Reports.Contracts.Adapters.TabularColumn
                {
                    Key = formula.FieldName,
                    Label = formula.Label,
                    DataType = formula.DataType,
                    Order = formula.Order > 0 ? formula.Order : columns.Count + 1
                });
            }

            foreach (var row in rows)
            {
                var value = EvaluateExpression(formula.Expression, row);
                row[formula.FieldName] = value;
            }
        }
    }

    private static object? EvaluateExpression(string expression, Dictionary<string, object?> row)
    {
        try
        {
            var resolved = FieldReferencePattern().Replace(expression, match =>
            {
                var fieldName = match.Groups[1].Value;
                if (row.TryGetValue(fieldName, out var val) && val is not null)
                    return Convert.ToDouble(val, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
                return "0";
            });

            resolved = ProcessFunctions(resolved, row);

            return EvaluateArithmetic(resolved);
        }
        catch
        {
            return null;
        }
    }

    private static string ProcessFunctions(string expr, Dictionary<string, object?> row)
    {
        expr = IfFunctionPattern().Replace(expr, match =>
        {
            var condition = match.Groups[1].Value.Trim();
            var trueVal = match.Groups[2].Value.Trim();
            var falseVal = match.Groups[3].Value.Trim();

            var condResult = EvaluateCondition(condition, row);
            return condResult ? trueVal : falseVal;
        });

        expr = CoalesceFunctionPattern().Replace(expr, match =>
        {
            var first = match.Groups[1].Value.Trim();
            var second = match.Groups[2].Value.Trim();

            var firstResolved = ResolveFieldRef(first, row);
            return firstResolved is not null ? Convert.ToDouble(firstResolved, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture) : second;
        });

        expr = MathFunctionPattern().Replace(expr, match =>
        {
            var funcName = match.Groups[1].Value.ToUpperInvariant();
            var argStr = match.Groups[2].Value.Trim();

            var argVal = EvaluateArithmetic(argStr);
            if (argVal is null) return "0";

            var d = Convert.ToDouble(argVal, CultureInfo.InvariantCulture);
            return funcName switch
            {
                "ABS" => Math.Abs(d).ToString(CultureInfo.InvariantCulture),
                "ROUND" => Math.Round(d).ToString(CultureInfo.InvariantCulture),
                "FLOOR" => Math.Floor(d).ToString(CultureInfo.InvariantCulture),
                "CEIL" => Math.Ceiling(d).ToString(CultureInfo.InvariantCulture),
                _ => d.ToString(CultureInfo.InvariantCulture)
            };
        });

        return expr;
    }

    private static bool EvaluateCondition(string condition, Dictionary<string, object?> row)
    {
        var parts = condition.Split(new[] { ">=", "<=", "!=", ">", "<", "==" }, StringSplitOptions.None);
        if (parts.Length != 2) return false;

        var leftStr = parts[0].Trim();
        var rightStr = parts[1].Trim();

        var left = ResolveToDouble(leftStr, row);
        var right = ResolveToDouble(rightStr, row);

        if (condition.Contains(">=")) return left >= right;
        if (condition.Contains("<=")) return left <= right;
        if (condition.Contains("!=")) return Math.Abs(left - right) > 0.0001;
        if (condition.Contains(">")) return left > right;
        if (condition.Contains("<")) return left < right;
        if (condition.Contains("==")) return Math.Abs(left - right) < 0.0001;

        return false;
    }

    private static double ResolveToDouble(string value, Dictionary<string, object?> row)
    {
        var resolved = ResolveFieldRef(value, row);
        if (resolved is not null)
            return Convert.ToDouble(resolved, CultureInfo.InvariantCulture);

        if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
            return d;

        return 0;
    }

    private static object? ResolveFieldRef(string value, Dictionary<string, object?> row)
    {
        var match = FieldReferencePattern().Match(value);
        if (match.Success)
        {
            var fieldName = match.Groups[1].Value;
            if (row.TryGetValue(fieldName, out var val))
                return val;
        }
        return null;
    }

    private static object? EvaluateArithmetic(string expression)
    {
        try
        {
            var tokens = Tokenize(expression.Trim());
            var result = ParseAddSubtract(tokens, 0, out _);
            return result;
        }
        catch
        {
            return null;
        }
    }

    private static List<string> Tokenize(string expr)
    {
        var tokens = new List<string>();
        var current = "";
        for (int i = 0; i < expr.Length; i++)
        {
            var c = expr[i];
            if (char.IsWhiteSpace(c))
            {
                if (current.Length > 0) { tokens.Add(current); current = ""; }
                continue;
            }
            if (c is '+' or '-' or '*' or '/' or '%' or '(' or ')')
            {
                if (current.Length > 0) { tokens.Add(current); current = ""; }
                tokens.Add(c.ToString());
            }
            else
            {
                current += c;
            }
        }
        if (current.Length > 0) tokens.Add(current);
        return tokens;
    }

    private static double ParseAddSubtract(List<string> tokens, int pos, out int newPos)
    {
        var left = ParseMulDiv(tokens, pos, out pos);
        while (pos < tokens.Count && tokens[pos] is "+" or "-")
        {
            var op = tokens[pos]; pos++;
            var right = ParseMulDiv(tokens, pos, out pos);
            left = op == "+" ? left + right : left - right;
        }
        newPos = pos;
        return left;
    }

    private static double ParseMulDiv(List<string> tokens, int pos, out int newPos)
    {
        var left = ParseUnary(tokens, pos, out pos);
        while (pos < tokens.Count && tokens[pos] is "*" or "/" or "%")
        {
            var op = tokens[pos]; pos++;
            var right = ParseUnary(tokens, pos, out pos);
            left = op switch
            {
                "*" => left * right,
                "/" => right != 0 ? left / right : 0,
                "%" => right != 0 ? left % right : 0,
                _ => left
            };
        }
        newPos = pos;
        return left;
    }

    private static double ParseUnary(List<string> tokens, int pos, out int newPos)
    {
        if (pos < tokens.Count && tokens[pos] == "-")
        {
            pos++;
            var val = ParsePrimary(tokens, pos, out pos);
            newPos = pos;
            return -val;
        }
        return ParsePrimary(tokens, pos, out newPos);
    }

    private static double ParsePrimary(List<string> tokens, int pos, out int newPos)
    {
        if (pos < tokens.Count && tokens[pos] == "(")
        {
            pos++;
            var val = ParseAddSubtract(tokens, pos, out pos);
            if (pos < tokens.Count && tokens[pos] == ")") pos++;
            newPos = pos;
            return val;
        }

        if (pos < tokens.Count && double.TryParse(tokens[pos], NumberStyles.Any, CultureInfo.InvariantCulture, out var num))
        {
            newPos = pos + 1;
            return num;
        }

        newPos = pos + 1;
        return 0;
    }
}
