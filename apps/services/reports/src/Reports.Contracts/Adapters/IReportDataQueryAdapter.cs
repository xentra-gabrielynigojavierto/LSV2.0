namespace Reports.Contracts.Adapters;

public sealed class ReportQueryContext
{
    public string TenantId { get; init; } = string.Empty;
    public string ProductCode { get; init; } = string.Empty;
    public Guid TemplateId { get; init; }
    public string TemplateCode { get; init; } = string.Empty;
    public string OrganizationType { get; init; } = string.Empty;
    public int VersionNumber { get; init; }
    public string? TemplateBody { get; init; }
    public string? LayoutConfigJson { get; init; }
    public string? ColumnConfigJson { get; init; }
    public string? FilterConfigJson { get; init; }
    public string? ParametersJson { get; init; }
    public int MaxRows { get; init; } = 500;
}

public sealed class TabularColumn
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string DataType { get; init; } = "string";
    public int Order { get; init; }
}

public sealed class TabularResultSet
{
    public List<TabularColumn> Columns { get; init; } = new();
    public List<Dictionary<string, object?>> Rows { get; init; } = new();
    public int TotalRowCount { get; init; }
    public bool WasTruncated { get; init; }
}

public interface IReportDataQueryAdapter
{
    bool SupportsProduct(string productCode);
    Task<AdapterResult<TabularResultSet>> ExecuteQueryAsync(ReportQueryContext context, CancellationToken ct = default);
}
