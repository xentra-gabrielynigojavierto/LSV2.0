using Reports.Application.Templates.DTOs;

namespace Reports.Application.Templates;

public interface ITemplateManagementService
{
    Task<ServiceResult<TemplateResponse>> CreateTemplateAsync(CreateTemplateRequest request, CancellationToken ct = default);
    Task<ServiceResult<TemplateResponse>> UpdateTemplateAsync(Guid templateId, UpdateTemplateRequest request, CancellationToken ct = default);
    Task<ServiceResult<TemplateResponse>> GetTemplateByIdAsync(Guid templateId, CancellationToken ct = default);
    Task<ServiceResult<IReadOnlyList<TemplateResponse>>> ListTemplatesAsync(string? productCode, string? organizationType, int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<ServiceResult<TemplateVersionResponse>> CreateVersionAsync(Guid templateId, CreateTemplateVersionRequest request, CancellationToken ct = default);
    Task<ServiceResult<TemplateVersionResponse>> GetLatestVersionAsync(Guid templateId, CancellationToken ct = default);
    Task<ServiceResult<TemplateVersionResponse>> GetPublishedVersionAsync(Guid templateId, CancellationToken ct = default);
    Task<ServiceResult<TemplateVersionResponse>> PublishVersionAsync(Guid templateId, int versionNumber, PublishTemplateVersionRequest request, CancellationToken ct = default);
    Task<ServiceResult<IReadOnlyList<TemplateVersionResponse>>> ListVersionsAsync(Guid templateId, CancellationToken ct = default);
}
