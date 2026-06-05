using Reports.Contracts.Context;

namespace Reports.Contracts.Adapters;

public sealed class ReportNotification
{
    public string ReportId { get; init; } = string.Empty;
    public string ReportName { get; init; } = string.Empty;
    public string? Reason { get; init; }
}

public interface INotificationAdapter
{
    Task<AdapterResult<bool>> NotifyReportReadyAsync(RequestContext ctx, TenantContext tenant, string userId, ReportNotification notification, CancellationToken ct = default);
    Task<AdapterResult<bool>> NotifyReportFailedAsync(RequestContext ctx, TenantContext tenant, string userId, ReportNotification notification, CancellationToken ct = default);
}
