namespace Tenant.Application.DTOs;

public record TenantResponse(
    Guid      Id,
    string    Code,
    string    DisplayName,
    string?   LegalName,
    string?   Description,
    string    Status,
    string?   Subdomain,
    Guid?     LogoDocumentId,
    Guid?     LogoWhiteDocumentId,
    string?   WebsiteUrl,
    string?   TimeZone,
    string?   Locale,
    string?   SupportEmail,
    string?   SupportPhone,
    string?   AddressLine1,
    string?   AddressLine2,
    string?   City,
    string?   StateOrProvince,
    string?   PostalCode,
    string?   CountryCode,
    DateTime  CreatedAtUtc,
    DateTime  UpdatedAtUtc,
    // BLK-TS-02 — provisioning state
    string    ProvisioningStatus    = "Unknown",
    DateTime? ProvisionedAtUtc      = null,
    string?   LastProvisioningError = null);
