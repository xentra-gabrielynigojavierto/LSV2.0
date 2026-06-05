using Reports.Domain.Entities;

namespace Reports.Contracts.Persistence;

public interface ITemplateRepository
{
    Task<ReportTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ReportTemplate?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<IReadOnlyList<ReportTemplate>> ListAsync(string? productCode = null, string? organizationType = null, bool? activeOnly = null, int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<ReportTemplate> CreateAsync(ReportTemplate template, CancellationToken ct = default);
    Task<ReportTemplate> UpdateAsync(ReportTemplate template, CancellationToken ct = default);
    Task<ReportTemplateVersion?> GetVersionAsync(Guid templateId, int versionNumber, CancellationToken ct = default);
    Task<ReportTemplateVersion?> GetActiveVersionAsync(Guid templateId, CancellationToken ct = default);
    Task<ReportTemplateVersion?> GetPublishedVersionAsync(Guid templateId, CancellationToken ct = default);
    Task<ReportTemplateVersion?> GetLatestVersionAsync(Guid templateId, CancellationToken ct = default);
    Task<IReadOnlyList<ReportTemplateVersion>> ListVersionsAsync(Guid templateId, CancellationToken ct = default);
    Task<ReportTemplateVersion> CreateVersionAsync(ReportTemplateVersion version, CancellationToken ct = default);
    Task<ReportTemplateVersion> UpdateVersionAsync(ReportTemplateVersion version, CancellationToken ct = default);
    Task<ReportTemplateVersion> PublishVersionAtomicAsync(Guid templateId, int versionNumber, string publishedByUserId, CancellationToken ct = default);
    Task<ReportTemplateVersion> CreateVersionAtomicAsync(ReportTemplate template, ReportTemplateVersion version, CancellationToken ct = default);
}
