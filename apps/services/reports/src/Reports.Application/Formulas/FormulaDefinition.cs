namespace Reports.Application.Formulas;

public sealed class FormulaDefinition
{
    public string FieldName { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Expression { get; init; } = string.Empty;
    public string DataType { get; init; } = "number";
    public int Order { get; init; }
}
