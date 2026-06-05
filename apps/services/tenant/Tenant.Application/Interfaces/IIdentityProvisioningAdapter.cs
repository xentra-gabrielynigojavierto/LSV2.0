namespace Tenant.Application.Interfaces;

/// <summary>
/// TENANT-B12 — Adapter that orchestrates Identity-side provisioning work
/// after the Tenant service has created the canonical Tenant record.
/// TENANT-STABILIZATION — Extended with RetryProvisioningAsync and RetryVerificationAsync
/// so the Tenant service can proxy admin retry operations from Control Center.
///
/// Responsibilities:
///   - ProvisionAsync: create the Identity.Tenant entity plus admin user/org/provisioning
///   - RetryProvisioningAsync: proxy POST /api/admin/tenants/{id}/provisioning/retry
///   - RetryVerificationAsync: proxy POST /api/admin/tenants/{id}/verification/retry
///
/// Rules:
///   - HTTP/internal service calls only — no direct DB access
///   - ProvisionAsync: 30 s timeout (full provisioning includes DNS + product work)
///   - Retry operations: 15 s timeout
///   - Never throws — always returns a result
/// </summary>
public interface IIdentityProvisioningAdapter
{
    /// <summary>
    /// Calls the Identity internal provisioning endpoint to create the auth/admin
    /// context for a tenant that already exists in the Tenant service DB.
    /// </summary>
    Task<IdentityProvisioningResult> ProvisionAsync(
        IdentityProvisioningRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Proxies POST /api/admin/tenants/{id}/provisioning/retry to Identity.
    /// Returns a ProvisioningRetryResult; never throws.
    /// </summary>
    Task<ProvisioningRetryResult> RetryProvisioningAsync(
        Guid              tenantId,
        CancellationToken ct = default);

    /// <summary>
    /// Proxies POST /api/admin/tenants/{id}/verification/retry to Identity.
    /// Returns a ProvisioningRetryResult; never throws.
    /// </summary>
    Task<ProvisioningRetryResult> RetryVerificationAsync(
        Guid              tenantId,
        CancellationToken ct = default);
}

/// <summary>Inputs required to provision a tenant's Identity-side context.</summary>
public record IdentityProvisioningRequest(
    Guid     TenantId,
    string   Code,
    string   DisplayName,
    string   OrgType,
    string   AdminEmail,
    string   AdminFirstName,
    string   AdminLastName,
    string?  PreferredSubdomain    = null,
    string?  AddressLine1          = null,
    string?  City                  = null,
    string?  State                 = null,
    string?  PostalCode            = null,
    double?  Latitude              = null,
    double?  Longitude             = null,
    string?  GeoPointSource        = null,
    List<string>? Products         = null);

/// <summary>Result returned by the Identity provisioning adapter.</summary>
public record IdentityProvisioningResult(
    bool     Success,
    string?  AdminUserId,
    string?  AdminEmail,
    string?  TemporaryPassword,
    string?  ProvisioningStatus,
    string?  Hostname,
    string?  Subdomain,
    List<string> Warnings,
    List<string> Errors);

/// <summary>
/// Unified result for provisioning/verification retry proxy calls.
/// Maps the { success, provisioningStatus, hostname?, error? } Identity response shape.
/// </summary>
public record ProvisioningRetryResult(
    bool    Success,
    string  ProvisioningStatus,
    string? Hostname,
    string? FailureStage,
    string? Error);
