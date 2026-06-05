using Tenant.Application.DTOs;

namespace Tenant.Application.Interfaces;

public interface IBrandingService
{
    Task<BrandingResponse>        GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default);
    Task<BrandingResponse>        UpsertAsync(Guid tenantId, UpdateBrandingRequest request, CancellationToken ct = default);

    /// <summary>
    /// TENANT-B10 — Sets only the primary logo document reference.
    /// Does NOT touch any other branding field (colors, brand-name, etc.).
    /// Pass <c>null</c> to clear the logo.
    /// </summary>
    Task<BrandingResponse> SetLogoAsync(Guid tenantId, Guid? documentId, CancellationToken ct = default);

    /// <summary>
    /// TENANT-B10 — Sets only the white/reversed logo document reference.
    /// Does NOT touch any other branding field.
    /// Pass <c>null</c> to clear the white logo.
    /// </summary>
    Task<BrandingResponse> SetLogoWhiteAsync(Guid tenantId, Guid? documentId, CancellationToken ct = default);
    Task<PublicBrandingResponse?> GetPublicByCodeAsync(string code, CancellationToken ct = default);
    Task<PublicBrandingResponse?> GetPublicBySubdomainAsync(string subdomain, CancellationToken ct = default);

    /// <summary>
    /// TENANT-B08 — Evicts public branding cache entries for the given code and/or subdomain.
    /// Called after a successful tenant sync so reads immediately reflect updated data.
    /// </summary>
    void EvictPublicCache(string? code, string? subdomain);
}
