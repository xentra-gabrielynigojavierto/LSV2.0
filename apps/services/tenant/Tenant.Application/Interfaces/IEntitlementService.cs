using Tenant.Application.DTOs;

namespace Tenant.Application.Interfaces;

public interface IEntitlementService
{
    Task<List<EntitlementResponse>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<EntitlementResponse>       GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<EntitlementResponse>       CreateAsync(Guid tenantId, CreateEntitlementRequest request, CancellationToken ct = default);
    Task<EntitlementResponse>       UpdateAsync(Guid tenantId, Guid id, UpdateEntitlementRequest request, CancellationToken ct = default);
    Task                            DeleteAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    /// <summary>
    /// Sets the specified entitlement as the tenant default.
    /// Auto-demotes any prior default entitlement.
    /// The target entitlement must be enabled.
    /// </summary>
    Task<EntitlementResponse>       SetDefaultAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    /// <summary>
    /// BLK-TS-02 — Idempotent product activation.
    /// Creates the entitlement if it does not exist; enables it if it exists but is disabled.
    /// Safe to call multiple times — returns the current state without error.
    /// </summary>
    Task<EntitlementResponse>       ActivateProductAsync(Guid tenantId, string productKey, string? displayName = null, CancellationToken ct = default);

    /// <summary>
    /// BLK-TS-02 — Returns true if the tenant has an active (IsEnabled=true) entitlement
    /// for the given product key. Returns false if the entitlement does not exist or is disabled.
    /// </summary>
    Task<bool>                      IsProductActiveAsync(Guid tenantId, string productKey, CancellationToken ct = default);
}
