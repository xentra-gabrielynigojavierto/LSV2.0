using Tenant.Application.DTOs;

namespace Tenant.Application.Interfaces;

public interface IResolutionService
{
    /// <summary>
    /// Resolves a tenant by exact hostname.
    /// Input: full host, e.g. "acme.legalsynq.net".
    /// Only returns a result if a TenantDomain with Status=Active exists for that host.
    /// </summary>
    Task<TenantResolutionResponse?> ResolveByHostAsync(string host, CancellationToken ct = default);

    /// <summary>
    /// Resolves a tenant by subdomain label, e.g. "acme".
    /// Matches active Subdomain-type TenantDomain records where the leftmost host label equals the input.
    /// Falls back to Tenant.Subdomain for migration compatibility.
    /// </summary>
    Task<TenantResolutionResponse?> ResolveBySubdomainAsync(string subdomain, CancellationToken ct = default);

    /// <summary>
    /// Resolves a tenant by its short code, e.g. "acme".
    /// Does not require a TenantDomain record — resolves directly from Tenant.Code.
    /// </summary>
    Task<TenantResolutionResponse?> ResolveByCodeAsync(string code, CancellationToken ct = default);

    /// <summary>
    /// Resolves a tenant by its primary GUID.
    /// Used by cross-tenant public directory lookups (e.g. Common CareConnect portal).
    /// </summary>
    Task<TenantResolutionResponse?> ResolveByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// TENANT-B08 — Evicts resolution cache entries for the given code and/or subdomain.
    /// Called after a successful tenant sync so resolution reads immediately reflect updated data.
    /// </summary>
    void EvictCache(string? code, string? subdomain);
}
