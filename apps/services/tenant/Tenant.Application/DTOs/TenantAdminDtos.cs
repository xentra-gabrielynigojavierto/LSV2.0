namespace Tenant.Application.DTOs;

/// <summary>
/// TENANT-B11 — Admin list item returned by GET /api/v1/admin/tenants.
///
/// Field names are intentionally camelCase-compatible with the control-center
/// mapTenantSummary mapper. Identity-owned fields not tracked in Tenant DB are
/// returned as sensible defaults (type="LawFirm", userCount=0, orgCount=0) so
/// the mapper handles them without errors.
/// </summary>
public record TenantAdminSummaryResponse(
    Guid    Id,
    string  Code,
    string  DisplayName,
    string  Status,
    bool    IsActive,
    string  Type,
    string  PrimaryContactName,
    int     UserCount,
    int     OrgCount,
    string? Subdomain,
    DateTime CreatedAtUtc);

/// <summary>
/// TENANT-B11 — Admin detail returned by GET /api/v1/admin/tenants/{id}.
///
/// Extends the summary with branding logos, entitlements, session-timeout
/// read-through from Identity, and Tenant-native detail fields.
/// All optional fields that Tenant does not own default to null or 0 so the
/// control-center mapTenantDetail mapper works without changes.
/// </summary>
public record TenantAdminDetailResponse(
    Guid    Id,
    string  Code,
    string  DisplayName,
    string  Status,
    bool    IsActive,
    string  Type,
    string  PrimaryContactName,
    int     UserCount,
    int     OrgCount,
    int     ActiveUserCount,
    int     LinkedOrgCount,
    string? Email,
    string? Subdomain,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    Guid?   LogoDocumentId,
    Guid?   LogoWhiteDocumentId,
    int?    SessionTimeoutMinutes,
    string  IdentityCompatSource,
    IList<AdminEntitlementItem> ProductEntitlements,
    int     DomainCount,
    int     CapabilityCount,
    TenantAdminSettingsSummary? SettingsSummary,
    TenantAdminBrandingSummary? BrandingSummary);

/// <summary>A single entitlement entry compatible with the control-center entitlement mapper.</summary>
public record AdminEntitlementItem(
    string  ProductCode,
    string  ProductName,
    bool    Enabled,
    string  Status,
    DateTime? EnabledAtUtc);

/// <summary>Light settings summary surfaced in the admin detail response.</summary>
public record TenantAdminSettingsSummary(
    string? DefaultProduct,
    string? Locale,
    string? TimeZone);

/// <summary>Branding basics surfaced in the admin detail response.</summary>
public record TenantAdminBrandingSummary(
    string? BrandName,
    string? PrimaryColor,
    Guid?   LogoDocumentId,
    Guid?   LogoWhiteDocumentId);
