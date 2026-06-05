namespace Tenant.Application.DTOs;

// ── Capability management DTOs ────────────────────────────────────────────────

/// <summary>Response returned for a single TenantCapability record.</summary>
public record CapabilityResponse(
    Guid   Id,
    Guid   TenantId,
    Guid?  ProductEntitlementId,
    string CapabilityKey,
    bool   IsEnabled,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

/// <summary>Request body for creating a new capability.</summary>
public record CreateCapabilityRequest(
    string CapabilityKey,
    bool   IsEnabled             = true,
    Guid?  ProductEntitlementId  = null);

/// <summary>Request body for updating an existing capability.</summary>
public record UpdateCapabilityRequest(
    string CapabilityKey,
    bool   IsEnabled,
    Guid?  ProductEntitlementId);
