using CareConnect.Application.DTOs;

namespace CareConnect.Application.Interfaces;

public interface IProviderService
{
    Task<PagedResponse<ProviderResponse>> SearchAsync(Guid tenantId, GetProvidersQuery query, CancellationToken ct = default);
    Task<List<ProviderMarkerResponse>> GetMarkersAsync(Guid tenantId, GetProvidersQuery query, CancellationToken ct = default);
    Task<ProviderResponse> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<ProviderResponse> CreateAsync(Guid tenantId, Guid? userId, CreateProviderRequest request, CancellationToken ct = default);
    Task<ProviderResponse> UpdateAsync(Guid tenantId, Guid id, Guid? userId, UpdateProviderRequest request, CancellationToken ct = default);

    /// <summary>
    /// Returns open appointment slots for the provider in the given date/time range,
    /// optionally filtered by service offering and facility.
    /// </summary>
    Task<ProviderAvailabilityResponse> GetAvailabilityAsync(
        Guid      tenantId,
        Guid      providerId,
        DateTime  from,
        DateTime  to,
        Guid?     serviceOfferingId = null,
        Guid?     facilityId        = null,
        CancellationToken ct        = default);

    /// <summary>
    /// LSCC-002: Admin-safe endpoint that explicitly sets the Identity OrganizationId
    /// for a provider that was seeded or created before org linkage was enforced.
    /// Idempotent — calling again with the same organizationId is a no-op.
    /// </summary>
    // LSCC-002: Admin provider org-link backfill entry point
    Task<ProviderResponse> LinkOrganizationAsync(
        Guid tenantId,
        Guid providerId,
        Guid organizationId,
        CancellationToken ct = default);

    /// <summary>
    /// LSCC-01-005-01 (DEF-001): Cross-tenant variant of LinkOrganizationAsync.
    /// Uses a global (tenant-agnostic) provider lookup, allowing a PlatformAdmin to
    /// link a provider whose TenantId differs from the activation request's TenantId.
    /// </summary>
    // LSCC-01-005-01: Cross-tenant activation approval — provider link without tenant scope
    Task<ProviderResponse> LinkOrganizationGlobalAsync(
        Guid providerId,
        Guid organizationId,
        CancellationToken ct = default);

    /// <summary>
    /// LSCC-002-01: Returns active providers that have no Identity OrganizationId set.
    /// Used by admins to identify what requires manual backfill.
    /// </summary>
    // LSCC-002-01: Provider backfill tooling — list unlinked providers
    Task<List<ProviderResponse>> GetUnlinkedProvidersAsync(
        Guid tenantId,
        CancellationToken ct = default);

    /// <summary>
    /// LSCC-002-01: Bulk-links providers to organizations from an explicit admin mapping.
    /// Idempotent per item — already-linked providers are counted as skipped.
    /// Never auto-guesses org mappings.
    /// </summary>
    // LSCC-002-01: Provider bulk backfill — safe, explicit, admin-only
    Task<BulkLinkReport> BulkLinkOrganizationAsync(
        Guid tenantId,
        IReadOnlyList<ProviderOrgLinkItem> items,
        CancellationToken ct = default);

    /// <summary>
    /// LSCC-01-003: Admin-safe idempotent activation — ensures provider IsActive and
    /// AcceptingReferrals are both true. Used by the admin provisioning interface to
    /// unblock a provider after Identity-side provisioning is complete.
    /// Cross-tenant: the admin operates across tenants.
    /// </summary>
    // LSCC-01-003: CareConnect receiver provisioning — CC-side activation
    Task<ProviderActivationResult> ActivateForCareConnectAsync(Guid providerId, CancellationToken ct = default);
}

/// <summary>LSCC-002-01: Single provider-to-org mapping item for bulk backfill.</summary>
public sealed record ProviderOrgLinkItem(Guid ProviderId, Guid OrganizationId);

/// <summary>LSCC-002-01: Result of a bulk provider org-link operation.</summary>
public sealed record BulkLinkReport(int Total, int Updated, int Skipped, int Unresolved);

/// <summary>LSCC-01-003: Result of admin CareConnect activation for a provider.</summary>
public sealed record ProviderActivationResult(
    Guid ProviderId,
    bool AlreadyActive,
    bool IsActive,
    bool AcceptingReferrals);
