namespace Identity.Application.DTOs;

public record LoginRequest(
    string  Email,
    string  Password,
    // AUTH-B01: Tenant code — optional when ResolveByEmail is true (common portal).
    string?  TenantCode    = null,
    string?  Subdomain     = null,
    // AUTH-B01: Optional Tenant-service-resolved tenant ID.
    // Used as a final fallback when both code and subdomain lookups miss the
    // Identity idt_Tenants table (e.g. tenant was provisioned via Tenant service
    // but the Identity write-through row has a different code/no subdomain set).
    Guid?    TenantId      = null,
    // AUTH-CC01: Common-portal flag — when true, tenant is resolved from the
    // user's email rather than a tenant code or subdomain.  Used by portals
    // (e.g. careconnect-demo.legalsynq.com) that serve users from multiple tenants.
    bool     ResolveByEmail = false);
