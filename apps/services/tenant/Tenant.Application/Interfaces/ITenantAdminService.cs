using Tenant.Application.DTOs;

namespace Tenant.Application.Interfaces;

/// <summary>
/// TENANT-B11/B12 — Admin-focused read/write service for tenant management.
///
/// Aggregates data from multiple Tenant repositories plus Identity compat
/// reads, producing responses compatible with the control-center admin mappers.
/// </summary>
public interface ITenantAdminService
{
    /// <summary>
    /// Returns a paged list of tenants with fields compatible with the
    /// control-center <c>mapTenantSummary</c> mapper.
    /// </summary>
    Task<(List<TenantAdminSummaryResponse> Items, int Total)> ListAdminAsync(
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the full admin detail for a single tenant, including branding
    /// logos, entitlements, domain count, settings summary, and a
    /// read-through for Identity-owned fields such as sessionTimeoutMinutes.
    /// Returns <c>null</c> if the tenant does not exist in Tenant DB.
    /// </summary>
    Task<TenantAdminDetailResponse?> GetAdminDetailAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Updates only the status field of the tenant.
    /// Returns the updated admin summary response, or throws NotFoundException.
    /// </summary>
    Task<TenantAdminSummaryResponse> UpdateStatusAsync(Guid id, string status, CancellationToken ct = default);

    /// <summary>
    /// TENANT-B12 — Canonical tenant creation (Tenant-first).
    ///
    /// 1. Creates the Tenant canonical record in Tenant DB (status=Active).
    /// 2. Calls Identity provisioning adapter to create admin user, org,
    ///    roles, and trigger DNS/product provisioning.
    /// 3. Returns a structured lifecycle response containing both the Tenant
    ///    record info and Identity provisioning outcome.
    ///
    /// Failure model: staged / compensating.
    ///   - If Identity provisioning fails, the Tenant record remains (it is canonical).
    ///   - Response includes IdentityProvisioned=false + error details for operator review.
    ///   - No silent partial success.
    /// </summary>
    Task<AdminCreateTenantResponse> CreateTenantAsync(
        AdminCreateTenantRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// TENANT-B12 — Admin entitlement toggle (Tenant-first).
    ///
    /// Upserts the TenantProductEntitlement record in Tenant DB (authoritative),
    /// then best-effort syncs to Identity so Identity-side TenantProduct records
    /// stay consistent. Identity sync failure does not fail the operation.
    ///
    /// Returns a shape compatible with the control-center mapEntitlementResponse mapper.
    /// </summary>
    Task<AdminEntitlementToggleResponse> ToggleEntitlementAsync(
        Guid   tenantId,
        string productCode,
        bool   enabled,
        CancellationToken ct = default);
}
