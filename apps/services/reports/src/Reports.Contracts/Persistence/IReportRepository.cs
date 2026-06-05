using Reports.Domain.Entities;

namespace Reports.Contracts.Persistence;

public interface IReportRepository
{
    Task<ReportExecution> SaveAsync(ReportExecution execution, CancellationToken ct = default);
    Task<ReportExecution?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ReportExecution>> ListByTenantAsync(string tenantId, int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<ReportExecution> UpdateAsync(ReportExecution execution, CancellationToken ct = default);
}
