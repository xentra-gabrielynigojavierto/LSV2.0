using System.Globalization;
using Microsoft.Extensions.Logging;
using Reports.Application.Formulas;

namespace Reports.Application.Formatting;

public static class ReportFormattingService
{
    public static List<Dictionary<string, string>> FormatRows(
        List<Dictionary<string, object?>> rows,
        List<ColumnFormattingRule> rules,
        ILogger? log = null)
    {
        if (rules.Count == 0)
            return rows.Select(_ => new Dictionary<string, string>()).ToList();

        var ruleMap = new Dictionary<string, ColumnFormattingRule>(StringComparer.OrdinalIgnoreCase);
        foreach (var rule in rules)
        {
            if (!string.IsNullOrWhiteSpace(rule.FieldName) && !string.IsNullOrWhiteSpace(rule.FormatType))
                ruleMap[rule.FieldName] = rule;
        }

        var result = new List<Dictionary<string, string>>(rows.Count);

        foreach (var row in rows)
        {
            var formatted = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in row)
            {
                if (ruleMap.TryGetValue(kvp.Key, out var rule))
                {
                    formatted[kvp.Key] = FormatValue(kvp.Value, rule, log);
                }
            }

            result.Add(formatted);
        }

        return result;
    }

    internal static string FormatValue(object? value, ColumnFormattingRule rule, ILogger? log = null)
    {
        try
        {
            return rule.FormatType.ToLowerInvariant() switch
            {
                "currency"   => FormatCurrency(value, rule),
                "number"     => FormatNumber(value, rule),
                "percentage" => FormatPercentage(value, rule),
                "date"       => FormatDate(value, rule),
                "boolean"    => FormatBoolean(value, rule),
                "text"       => FormatText(value, rule),
                _            => FallbackFormat(value, rule)
            };
        }
        catch (Exception ex)
        {
            log?.LogWarning(ex,
                "Formatting failed for field '{FieldName}' with type '{FormatType}', falling back to raw value",
                rule.FieldName, rule.FormatType);
            return FallbackFormat(value, rule);
        }
    }

    private static string FormatCurrency(object? value, ColumnFormattingRule rule)
    {
        if (value is null) return rule.NullLabel ?? "";

        var numericValue = ToDouble(value);
        if (numericValue is null) return FallbackFormat(value, rule);

        var decimals = rule.DecimalPlaces ?? 2;
        var prefix = rule.Prefix ?? "$";

        var formatted = numericValue.Value.ToString($"N{decimals}", CultureInfo.InvariantCulture);
        return $"{prefix}{formatted}";
    }

    private static string FormatNumber(object? value, ColumnFormattingRule rule)
    {
        if (value is null) return rule.NullLabel ?? "";

        var numericValue = ToDouble(value);
        if (numericValue is null) return FallbackFormat(value, rule);

        var decimals = rule.DecimalPlaces ?? 0;
        return numericValue.Value.ToString($"N{decimals}", CultureInfo.InvariantCulture);
    }

    private static string FormatPercentage(object? value, ColumnFormattingRule rule)
    {
        if (value is null) return rule.NullLabel ?? "";

        var numericValue = ToDouble(value);
        if (numericValue is null) return FallbackFormat(value, rule);

        var decimals = rule.DecimalPlaces ?? 1;
        var displayValue = numericValue.Value * 100.0;

        var suffix = rule.Suffix ?? "%";
        return $"{displayValue.ToString($"N{decimals}", CultureInfo.InvariantCulture)}{suffix}";
    }

    private static string FormatDate(object? value, ColumnFormattingRule rule)
    {
        if (value is null) return rule.NullLabel ?? "";

        var dateFormat = rule.DateFormat ?? rule.FormatPattern ?? "yyyy-MM-dd";

        if (value is DateTime dt)
            return dt.ToString(dateFormat, CultureInfo.InvariantCulture);
        if (value is DateTimeOffset dto)
            return dto.ToString(dateFormat, CultureInfo.InvariantCulture);
        if (value is string s && DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            return parsed.ToString(dateFormat, CultureInfo.InvariantCulture);

        return FallbackFormat(value, rule);
    }

    private static string FormatBoolean(object? value, ColumnFormattingRule rule)
    {
        if (value is null) return rule.NullLabel ?? "";

        var trueLabel = rule.TrueLabel ?? "Yes";
        var falseLabel = rule.FalseLabel ?? "No";

        if (value is bool b) return b ? trueLabel : falseLabel;
        if (value is int i) return i != 0 ? trueLabel : falseLabel;
        if (value is long l) return l != 0 ? trueLabel : falseLabel;
        if (value is string s)
        {
            if (bool.TryParse(s, out var parsed)) return parsed ? trueLabel : falseLabel;
            if (s == "1") return trueLabel;
            if (s == "0") return falseLabel;
        }

        return FallbackFormat(value, rule);
    }

    private static string FormatText(object? value, ColumnFormattingRule rule)
    {
        if (value is null) return rule.NullLabel ?? "";
        return value.ToString() ?? rule.NullLabel ?? "";
    }

    private static string FallbackFormat(object? value, ColumnFormattingRule rule)
    {
        if (value is null) return rule.NullLabel ?? "";
        return value.ToString() ?? "";
    }

    private static double? ToDouble(object? value)
    {
        if (value is null) return null;
        if (value is double d) return d;
        if (value is decimal dec) return (double)dec;
        if (value is float f) return f;
        if (value is int i) return i;
        if (value is long l) return l;
        if (value is short s) return s;
        if (value is string str && double.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            return parsed;
        return null;
    }
}
