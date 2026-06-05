using Liens.Application.DTOs;

namespace Liens.Application.Interfaces;

public interface ILienTaskGovernanceService
{
    /// <summary>
    /// Returns governance for the tenant, trying the Task service first and
    /// falling back to the Liens DB. Returns null if no settings are configured
    /// in either system (governance is optional).
    /// Does NOT create a default record.
    /// </summary>
    Task<TaskGovernanceSettingsResponse?> GetAsync(
        Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Returns governance for the tenant, falling back to creating a default
    /// Liens-side record if nothing exists in either system.
    /// Used by admin/settings endpoints.
    /// </summary>
    Task<TaskGovernanceSettingsResponse> GetOrCreateAsync(
        Guid tenantId, Guid actingUserId, string updateSource, CancellationToken ct = default);

    Task<TaskGovernanceSettingsResponse> UpdateAsync(
        Guid tenantId, Guid actingUserId,
        UpdateTaskGovernanceSettingsRequest request, CancellationToken ct = default);
}
