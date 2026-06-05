using Microsoft.Extensions.Logging;
using Reports.Contracts.Adapters;
using Reports.Contracts.Context;

namespace Reports.Infrastructure.Adapters;

public sealed class MockNotificationAdapter : INotificationAdapter
{
    private readonly ILogger<MockNotificationAdapter> _log;

    public MockNotificationAdapter(ILogger<MockNotificationAdapter> log) => _log = log;

    public Task<AdapterResult<bool>> NotifyReportReadyAsync(RequestContext ctx, TenantContext tenant, string userId, ReportNotification notification, CancellationToken ct)
    {
        _log.LogInformation("MockNotificationAdapter: Report ready — {ReportName} ({ReportId}) [Correlation={CorrelationId}]",
            notification.ReportName, notification.ReportId, ctx.CorrelationId);
        return Task.FromResult(AdapterResult<bool>.Ok(true));
    }

    public Task<AdapterResult<bool>> NotifyReportFailedAsync(RequestContext ctx, TenantContext tenant, string userId, ReportNotification notification, CancellationToken ct)
    {
        _log.LogWarning("MockNotificationAdapter: Report failed — {ReportId}: {Reason} [Correlation={CorrelationId}]",
            notification.ReportId, notification.Reason, ctx.CorrelationId);
        return Task.FromResult(AdapterResult<bool>.Ok(true));
    }
}
