namespace Reports.Application.Execution.DTOs;

public sealed class ReportRowResponse
{
    public Dictionary<string, object?> Values { get; init; } = new();
    public Dictionary<string, string>? FormattedValues { get; init; }
}
