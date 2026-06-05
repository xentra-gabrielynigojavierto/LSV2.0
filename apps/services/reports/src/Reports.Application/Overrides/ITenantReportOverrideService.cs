using Reports.Application.Overrides.DTOs;
using Reports.Application.Templates.DTOs;

namespace Reports.Application.Overrides;

public interface ITenantReportOverrideService
{
    Task<ServiceResult<TenantReportOverrideResponse>> CreateOverrideAsync(Guid templateId, CreateTenantReportOverrideRequest request, CancellationToken ct = default);
    Task<ServiceResult<TenantReportOverrideResponse>> UpdateOverrideAsync(Guid templateId, Guid overrideId, UpdateTenantReportOverrideRequest request, CancellationToken ct = default);
    Task<ServiceResult<TenantReportOverrideResponse>> GetOverrideByIdAsync(Guid templateId, Guid overrideId, CancellationToken ct = default);
    Task<ServiceResult<IReadOnlyList<TenantReportOverrideResponse>>> ListOverridesAsync(Guid templateId, string? tenantId, CancellationToken ct = default);
    Task<ServiceResult<TenantReportOverrideResponse>> DeactivateOverrideAsync(Guid templateId, Guid overrideId, CancellationToken ct = default);
    Task<ServiceResult<TenantEffectiveReportResponse>> ResolveEffectiveReportAsync(Guid templateId, string tenantId, CancellationToken ct = default);
}
