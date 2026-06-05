namespace Reports.Application.Execution.DTOs;

public sealed class ReportColumnResponse
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string DataType { get; init; } = "string";
    public int Order { get; init; }
}
