using Microsoft.Extensions.Logging;
using Reports.Contracts.Persistence;
using Reports.Domain.Entities;

namespace Reports.Infrastructure.Persistence;

public sealed class MockTemplateRepository : ITemplateRepository
{
    private readonly ILogger<MockTemplateRepository> _log;

    public MockTemplateRepository(ILogger<MockTemplateRepository> log) => _log = log;

    public Task<ReportTemplate?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        _log.LogDebug("MockTemplateRepository: GetById {Id}", id);
        return Task.FromResult<ReportTemplate?>(null);
    }

    public Task<ReportTemplate?> GetByCodeAsync(string code, CancellationToken ct)
    {
        _log.LogDebug("MockTemplateRepository: GetByCode {Code}", code);
        return Task.FromResult<ReportTemplate?>(null);
    }

    public Task<IReadOnlyList<ReportTemplate>> ListAsync(string? productCode, string? organizationType, bool? activeOnly, int page, int pageSize, CancellationToken ct)
    {
        _log.LogDebug("MockTemplateRepository: List productCode={ProductCode} orgType={OrgType}", productCode, organizationType);
        return Task.FromResult<IReadOnlyList<ReportTemplate>>(Array.Empty<ReportTemplate>());
    }

    public Task<ReportTemplate> CreateAsync(ReportTemplate template, CancellationToken ct)
    {
        if (template.Id == Guid.Empty)
            template.Id = Guid.NewGuid();
        _log.LogDebug("MockTemplateRepository: Created {Id}", template.Id);
        return Task.FromResult(template);
    }

    public Task<ReportTemplate> UpdateAsync(ReportTemplate template, CancellationToken ct)
    {
        _log.LogDebug("MockTemplateRepository: Updated {Id}", template.Id);
        return Task.FromResult(template);
    }

    public Task<ReportTemplateVersion?> GetVersionAsync(Guid templateId, int versionNumber, CancellationToken ct)
    {
        _log.LogDebug("MockTemplateRepository: GetVersion {Id} v{Version}", templateId, versionNumber);
        return Task.FromResult<ReportTemplateVersion?>(null);
    }

    public Task<ReportTemplateVersion?> GetActiveVersionAsync(Guid templateId, CancellationToken ct)
    {
        _log.LogDebug("MockTemplateRepository: GetActiveVersion {Id}", templateId);
        return Task.FromResult<ReportTemplateVersion?>(null);
    }

    public Task<ReportTemplateVersion?> GetPublishedVersionAsync(Guid templateId, CancellationToken ct)
    {
        _log.LogDebug("MockTemplateRepository: GetPublishedVersion {Id}", templateId);
        return Task.FromResult<ReportTemplateVersion?>(null);
    }

    public Task<ReportTemplateVersion?> GetLatestVersionAsync(Guid templateId, CancellationToken ct)
    {
        _log.LogDebug("MockTemplateRepository: GetLatestVersion {Id}", templateId);
        return Task.FromResult<ReportTemplateVersion?>(null);
    }

    public Task<IReadOnlyList<ReportTemplateVersion>> ListVersionsAsync(Guid templateId, CancellationToken ct)
    {
        _log.LogDebug("MockTemplateRepository: ListVersions {Id}", templateId);
        return Task.FromResult<IReadOnlyList<ReportTemplateVersion>>(Array.Empty<ReportTemplateVersion>());
    }

    public Task<ReportTemplateVersion> CreateVersionAsync(ReportTemplateVersion version, CancellationToken ct)
    {
        if (version.Id == Guid.Empty)
            version.Id = Guid.NewGuid();
        _log.LogDebug("MockTemplateRepository: Created version {Id}", version.Id);
        return Task.FromResult(version);
    }

    public Task<ReportTemplateVersion> UpdateVersionAsync(ReportTemplateVersion version, CancellationToken ct)
    {
        _log.LogDebug("MockTemplateRepository: Updated version {Id}", version.Id);
        return Task.FromResult(version);
    }

    public Task<ReportTemplateVersion> PublishVersionAtomicAsync(Guid templateId, int versionNumber, string publishedByUserId, CancellationToken ct)
    {
        _log.LogDebug("MockTemplateRepository: PublishVersionAtomic {TemplateId} v{Version}", templateId, versionNumber);
        var version = new ReportTemplateVersion
        {
            Id = Guid.NewGuid(),
            ReportTemplateId = templateId,
            VersionNumber = versionNumber,
            IsPublished = true,
            PublishedAtUtc = DateTimeOffset.UtcNow,
            PublishedByUserId = publishedByUserId
        };
        return Task.FromResult(version);
    }

    public Task<ReportTemplateVersion> CreateVersionAtomicAsync(ReportTemplate template, ReportTemplateVersion version, CancellationToken ct)
    {
        if (version.Id == Guid.Empty)
            version.Id = Guid.NewGuid();
        version.VersionNumber = template.CurrentVersion + 1;
        template.CurrentVersion = version.VersionNumber;
        _log.LogDebug("MockTemplateRepository: CreateVersionAtomic {TemplateId} v{Version}", template.Id, version.VersionNumber);
        return Task.FromResult(version);
    }
}
