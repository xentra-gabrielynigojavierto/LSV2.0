using System.Text.Json;

namespace Reports.Application.Formulas;

public sealed class ColumnFormattingRule
{
    public string FieldName { get; init; } = string.Empty;
    public string FormatType { get; init; } = string.Empty;
    public string? FormatPattern { get; init; }
    public int? DecimalPlaces { get; init; }
    public string? Prefix { get; init; }
    public string? Suffix { get; init; }
    public string? TrueLabel { get; init; }
    public string? FalseLabel { get; init; }
    public string? NullLabel { get; init; }
    public string? DateFormat { get; init; }
    public string? TextTransform { get; init; }
}

public static class FormattingConfigParser
{
    private static readonly HashSet<string> AllowedFormatTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "currency", "number", "percentage", "date", "boolean", "text"
    };

    private static readonly JsonSerializerOptions _caseInsensitiveOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static List<ColumnFormattingRule>? Parse(string? formattingConfigJson)
    {
        if (string.IsNullOrWhiteSpace(formattingConfigJson))
            return null;

        try
        {
            return JsonSerializer.Deserialize<List<ColumnFormattingRule>>(formattingConfigJson, _caseInsensitiveOptions);
        }
        catch
        {
            return null;
        }
    }

    public static string? Validate(string? formattingConfigJson)
    {
        if (string.IsNullOrWhiteSpace(formattingConfigJson))
            return null;

        var rules = Parse(formattingConfigJson);
        if (rules is null)
            return "Invalid JSON format for formatting config.";

        var fieldNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rule in rules)
        {
            if (string.IsNullOrWhiteSpace(rule.FieldName))
                return "Each formatting rule must have a FieldName.";

            if (!fieldNames.Add(rule.FieldName))
                return $"Duplicate formatting rule for field '{rule.FieldName}'.";

            if (!AllowedFormatTypes.Contains(rule.FormatType))
                return $"Unsupported format type '{rule.FormatType}' for field '{rule.FieldName}'. Allowed: {string.Join(", ", AllowedFormatTypes)}.";
        }

        return null;
    }
}
