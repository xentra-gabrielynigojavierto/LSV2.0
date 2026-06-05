namespace Reports.Application.Execution.DTOs;

public sealed class ExecuteReportRequest
{
    public string TenantId { get; init; } = string.Empty;
    public Guid TemplateId { get; init; }
    public string ProductCode { get; init; } = string.Empty;
    public string OrganizationType { get; init; } = string.Empty;
    public string? ParametersJson { get; init; }
    public string RequestedByUserId { get; init; } = string.Empty;
    public bool UseOverride { get; init; } = true;
    public Guid? ViewId { get; init; }
}
