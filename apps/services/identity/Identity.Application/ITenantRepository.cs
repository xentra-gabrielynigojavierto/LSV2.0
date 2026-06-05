using Identity.Domain;

namespace Identity.Application;

public interface ITenantRepository
{
    Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Tenant?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<Tenant?> GetBySubdomainAsync(string subdomain, CancellationToken ct = default);

    /// <summary>
    /// Resolves a tenant from the full hostname (e.g. "firm-a.legalsynq.com").
    /// Looks up TenantDomains where Domain matches the host exactly.
    /// Used by the anonymous branding endpoint for production subdomain-based resolution.
    /// </summary>
    Task<Tenant?> GetByHostAsync(string host, CancellationToken ct = default);

    /// <summary>
    /// Returns the internal DB product codes (e.g. "SYNQ_FUND", "SYNQ_CARECONNECT") for all
    /// products that are currently enabled for the given tenant.
    /// Used by auth/me to include <c>enabledProducts</c> in the session envelope.
    /// </summary>
    Task<List<string>> GetEnabledProductCodesAsync(Guid tenantId, CancellationToken ct = default);
}
