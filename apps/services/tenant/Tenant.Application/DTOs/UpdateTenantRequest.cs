namespace Tenant.Application.DTOs;

public record UpdateTenantRequest(
    string  DisplayName,
    string? LegalName           = null,
    string? Description         = null,
    string? WebsiteUrl          = null,
    string? TimeZone            = null,
    string? Locale              = null,
    string? SupportEmail        = null,
    string? SupportPhone        = null,
    string? AddressLine1        = null,
    string? AddressLine2        = null,
    string? City                = null,
    string? StateOrProvince     = null,
    string? PostalCode          = null,
    string? CountryCode         = null,
    string? Subdomain           = null,
    string? Status              = null,
    Guid?   LogoDocumentId      = null,
    Guid?   LogoWhiteDocumentId = null);
