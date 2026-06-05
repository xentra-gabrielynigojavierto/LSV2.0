namespace Tenant.Application.DTOs;

/// <summary>
/// TENANT-B12 — Request body for POST /api/v1/admin/tenants.
///
/// This is now the canonical entry point for tenant creation.
/// Shape is backward-compatible with the existing BFF contract so
/// <c>tenants.create</c> requires no action-layer changes.
/// </summary>
public record AdminCreateTenantRequest(
    string   Name,
    string   Code,
    string   AdminEmail,
    string   AdminFirstName,
    string   AdminLastName,
    string   OrgType          = "LAW_FIRM",
    string?  AddressLine1     = null,
    string?  City             = null,
    string?  State            = null,
    string?  PostalCode       = null,
    double?  Latitude         = null,
    double?  Longitude        = null,
    string?  GeoPointSource   = null);

/// <summary>
/// TENANT-B12 — Structured lifecycle response for POST /api/v1/admin/tenants.
///
/// Fields are a superset of the existing BFF contract
/// (<c>tenantId</c>, <c>displayName</c>, <c>code</c>, <c>status</c>,
/// <c>adminUserId</c>, <c>adminEmail</c>, <c>temporaryPassword</c>,
/// <c>subdomain</c>, <c>provisioningStatus</c>, <c>hostname</c>)
/// so the BFF client requires NO changes.
///
/// Additional B12 fields (<c>tenantCreated</c>, <c>identityProvisioned</c>, etc.)
/// are surfaced for operator/tooling use only.
/// </summary>
public record AdminCreateTenantResponse(
    string   TenantId,
    string   DisplayName,
    string   Code,
    string   Status,
    string?  AdminUserId,
    string?  AdminEmail,
    string?  TemporaryPassword,
    string?  Subdomain,
    string?  ProvisioningStatus,
    string?  Hostname,
    bool     TenantCreated,
    bool     IdentityProvisioned,
    string   NextAction,
    List<string> ProvisioningWarnings,
    List<string> ProvisioningErrors);

/// <summary>
/// TENANT-B12 — Response for the admin entitlement toggle endpoint.
///
/// Fields match the shape expected by the control-center <c>mapEntitlementResponse</c>
/// mapper (<c>productCode</c>, <c>productName</c>, <c>enabled</c>, <c>status</c>,
/// <c>enabledAtUtc</c>) — no mapper changes required.
/// </summary>
public record AdminEntitlementToggleResponse(
    Guid     EntitlementId,
    Guid     TenantId,
    string   ProductCode,
    string   ProductName,
    bool     Enabled,
    string   Status,
    string?  EnabledAtUtc,
    bool     IdentitySynced);
