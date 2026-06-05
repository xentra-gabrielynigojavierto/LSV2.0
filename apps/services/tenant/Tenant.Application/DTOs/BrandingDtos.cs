namespace Tenant.Application.DTOs;

/// <summary>Full branding response — returned to authenticated admin callers.</summary>
public record BrandingResponse(
    Guid     Id,
    Guid     TenantId,
    string?  BrandName,
    Guid?    LogoDocumentId,
    Guid?    LogoWhiteDocumentId,
    Guid?    FaviconDocumentId,
    string?  PrimaryColor,
    string?  SecondaryColor,
    string?  AccentColor,
    string?  TextColor,
    string?  BackgroundColor,
    string?  WebsiteUrlOverride,
    string?  SupportEmailOverride,
    string?  SupportPhoneOverride,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

/// <summary>Upsert request for branding fields. All fields are optional; omitted fields are cleared to null.</summary>
public record UpdateBrandingRequest(
    string? BrandName            = null,
    Guid?   LogoDocumentId       = null,
    Guid?   LogoWhiteDocumentId  = null,
    Guid?   FaviconDocumentId    = null,
    string? PrimaryColor         = null,
    string? SecondaryColor       = null,
    string? AccentColor          = null,
    string? TextColor            = null,
    string? BackgroundColor      = null,
    string? WebsiteUrlOverride   = null,
    string? SupportEmailOverride = null,
    string? SupportPhoneOverride = null);

/// <summary>
/// Safe public branding response — returned unauthenticated to login / public pages.
/// Does NOT expose address, internal IDs, status, locale, or admin-only fields.
/// </summary>
public record PublicBrandingResponse(
    Guid    TenantId,
    string  Code,
    string  DisplayName,
    string? BrandName,
    Guid?   LogoDocumentId,
    Guid?   LogoWhiteDocumentId,
    Guid?   FaviconDocumentId,
    string? PrimaryColor,
    string? SecondaryColor,
    string? AccentColor,
    string? TextColor,
    string? BackgroundColor,
    string? WebsiteUrl,
    string? SupportEmail);
