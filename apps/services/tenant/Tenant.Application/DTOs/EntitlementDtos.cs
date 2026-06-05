namespace Tenant.Application.DTOs;

// ── Entitlement management DTOs ───────────────────────────────────────────────

/// <summary>Response returned for a single TenantProductEntitlement record.</summary>
public record EntitlementResponse(
    Guid      Id,
    Guid      TenantId,
    string    ProductKey,
    string?   ProductDisplayName,
    bool      IsEnabled,
    bool      IsDefault,
    string?   PlanCode,
    DateTime? EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    DateTime  CreatedAtUtc,
    DateTime  UpdatedAtUtc);

/// <summary>Request body for creating a new product entitlement.</summary>
public record CreateEntitlementRequest(
    string    ProductKey,
    string?   ProductDisplayName = null,
    bool      IsEnabled          = true,
    bool      IsDefault          = false,
    string?   PlanCode           = null,
    DateTime? EffectiveFromUtc   = null,
    DateTime? EffectiveToUtc     = null);

/// <summary>Request body for updating an existing product entitlement.</summary>
public record UpdateEntitlementRequest(
    string    ProductKey,
    string?   ProductDisplayName,
    bool      IsEnabled,
    string?   PlanCode,
    DateTime? EffectiveFromUtc,
    DateTime? EffectiveToUtc);
