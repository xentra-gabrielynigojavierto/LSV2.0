using Microsoft.Extensions.Logging;
using Reports.Contracts.Persistence;
using Reports.Domain.Entities;

namespace Reports.Infrastructure.Persistence;

public sealed class MockReportRepository : IReportRepository
{
    private readonly ILogger<MockReportRepository> _log;

    public MockReportRepository(ILogger<MockReportRepository> log) => _log = log;

    public Task<ReportExecution> SaveAsync(ReportExecution execution, CancellationToken ct)
    {
        if (execution.Id == Guid.Empty)
            execution.Id = Guid.NewGuid();
        _log.LogDebug("MockReportRepository: Saved execution {Id}", execution.Id);
        return Task.FromResult(execution);
    }

    public Task<ReportExecution?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        _log.LogDebug("MockReportRepository: GetById {Id}", id);
        return Task.FromResult<ReportExecution?>(null);
    }

    public Task<IReadOnlyList<ReportExecution>> ListByTenantAsync(string tenantId, int page, int pageSize, CancellationToken ct)
    {
        _log.LogDebug("MockReportRepository: ListByTenant {TenantId} page={Page}", tenantId, page);
        return Task.FromResult<IReadOnlyList<ReportExecution>>(Array.Empty<ReportExecution>());
    }

    public Task<ReportExecution> UpdateAsync(ReportExecution execution, CancellationToken ct)
    {
        _log.LogDebug("MockReportRepository: Updated execution {Id}", execution.Id);
        return Task.FromResult(execution);
    }
}
