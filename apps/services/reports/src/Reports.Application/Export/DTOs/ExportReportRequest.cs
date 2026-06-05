namespace Reports.Application.Export.DTOs;

public enum ExportFormat
{
    CSV,
    XLSX,
    PDF
}

public sealed class ExportReportRequest
{
    public string TenantId { get; init; } = string.Empty;
    public Guid TemplateId { get; init; }
    public string ProductCode { get; init; } = string.Empty;
    public string OrganizationType { get; init; } = string.Empty;
    public ExportFormat Format { get; init; } = ExportFormat.CSV;
    public string? ParametersJson { get; init; }
    public string RequestedByUserId { get; init; } = string.Empty;
    public bool UseOverride { get; init; } = true;
    public Guid? ViewId { get; init; }
}
