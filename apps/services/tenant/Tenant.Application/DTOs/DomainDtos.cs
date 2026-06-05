namespace Tenant.Application.DTOs;

// ── Domain management DTOs ────────────────────────────────────────────────────

/// <summary>Response returned for a single TenantDomain record.</summary>
public record DomainResponse(
    Guid     Id,
    Guid     TenantId,
    string   Host,
    string   DomainType,
    string   Status,
    bool     IsPrimary,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

/// <summary>Request body for creating a new domain record.</summary>
public record CreateDomainRequest(
    string  Host,
    string  DomainType,
    bool    IsPrimary,
    string? Status = null);

/// <summary>Request body for updating an existing domain record.</summary>
public record UpdateDomainRequest(
    string  Host,
    string  DomainType,
    string  Status,
    bool    IsPrimary);

// ── Resolution DTOs ───────────────────────────────────────────────────────────

/// <summary>
/// Safe public resolution response — returned to anonymous callers.
/// Does not expose internal addresses, migration identifiers, or admin-only fields.
/// </summary>
public record TenantResolutionResponse(
    Guid    TenantId,
    string  Code,
    string  DisplayName,
    string  Status,
    string  MatchedBy,
    string? MatchedHost,
    string? PrimaryColor,
    Guid?   LogoDocumentId);
