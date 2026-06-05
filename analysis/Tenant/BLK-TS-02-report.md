# BLK-TS-02 — Tenant Infrastructure & Activation

**Status:** COMPLETE  
**Date:** 2026-04-23  
**Block:** Tenant-Stabilization  
**Window:** 2026-04-23 → 2026-05-07

---

## Objective

Implement Tenant-owned provisioning state tracking and idempotent product activation
endpoints, so the Tenant service is the authoritative orchestrator for DNS/infrastructure
lifecycle and per-product entitlement control.

---

## Deliverables

### 1 — Domain: `TenantProvisioningStatus` enum + fields on `Tenant`

**File:** `apps/services/tenant/Tenant.Domain/Tenant.cs`

Added `TenantProvisioningStatus` enum with values:
- `Unknown` — default for new/migrated records
- `InProgress` — provisioning call in flight
- `Provisioned` — DNS + infrastructure ready
- `Failed` — provisioning attempted but failed

Added three fields to the `Tenant` aggregate:
- `ProvisioningStatus TenantProvisioningStatus` — required, default `Unknown`
- `ProvisionedAtUtc DateTime?` — set on first successful provision
- `LastProvisioningError string?` — last failure message (max 1000 chars, cleared on success)

Added mutator `SetProvisioningStatus(status, error?)` with the following rules:
- On `Provisioned`: sets `ProvisionedAtUtc = DateTime.UtcNow`, clears `LastProvisioningError`
- On `Failed`: truncates and stores error message
- On all transitions: sets `UpdatedAtUtc = DateTime.UtcNow`

Also added `ProductKeys` static class with canonical product key constants:
- `ProductKeys.CareConnect = "synq_careconnect"`
- `ProductKeys.Liens = "liens"`
- `ProductKeys.Task = "task"`

---

### 2 — EF Configuration

**File:** `apps/services/tenant/Tenant.Infrastructure/Data/Configurations/TenantConfiguration.cs`

Added configuration for all three new columns:
- `ProvisioningStatus` — `varchar(50)`, required, default `"Unknown"`, stored as string
- `ProvisionedAtUtc` — `datetime(6)`, nullable
- `LastProvisioningError` — `varchar(1000)`, nullable

---

### 3 — Migration: `20260423220000_AddProvisioningState`

**File:** `apps/services/tenant/Tenant.Infrastructure/Data/Migrations/20260423220000_AddProvisioningState.cs`

Adds three columns to `tenant_Tenants`:
- `ProvisioningStatus varchar(50) NOT NULL DEFAULT 'Unknown'`
- `ProvisionedAtUtc datetime(6) NULL`
- `LastProvisioningError varchar(1000) NULL`

Fully reversible via `Down()`.

Model snapshot updated at `TenantDbContextModelSnapshot.cs`.

---

### 4 — `IEntitlementService` extensions

**File:** `apps/services/tenant/Tenant.Application/Interfaces/IEntitlementService.cs`

Added:
```csharp
Task<EntitlementResponse> ActivateProductAsync(Guid tenantId, string productKey, string? displayName = null, CancellationToken ct = default);
Task<bool>                IsProductActiveAsync(Guid tenantId, string productKey, CancellationToken ct = default);
```

---

### 5 — `EntitlementService` implementation

**File:** `apps/services/tenant/Tenant.Application/Services/EntitlementService.cs`

`ActivateProductAsync`:
- Normalizes `productKey` via `TenantProductEntitlement.NormalizeKey()`
- Looks up existing entitlement by `(tenantId, normalizedKey)`
- If exists and disabled — enables it (idempotent re-enable)
- If not exists — creates new entitlement with `IsEnabled = true`
- Safe to call multiple times; never throws on duplicate

`IsProductActiveAsync`:
- Returns `true` only if entitlement exists **and** `IsEnabled == true`
- Returns `false` if missing or disabled

---

### 6 — `TenantAdminService.CreateTenantAsync` update

**File:** `apps/services/tenant/Tenant.Application/Services/TenantAdminService.cs`

After calling `_identityProvisioning.ProvisionAsync(...)`:
- Calls `tenant.SetProvisioningStatus(Provisioned | Failed, errorMessage?)` based on the result
- Combines subdomain update + provisioning state into a single `_tenantRepo.UpdateAsync(tenant)` call
- Provisioning errors from Identity are joined with `"; "` and stored in `LastProvisioningError`

---

### 7 — New `ActivationEndpoints.cs`

**File:** `apps/services/tenant/Tenant.Api/Endpoints/ActivationEndpoints.cs`

Three admin-only endpoints:

#### `GET /api/v1/tenants/{id}/provisioning-status`
Returns current provisioning state:
```json
{
  "tenantId": "...",
  "tenantCode": "lienscom",
  "provisioningStatus": "Provisioned",
  "provisionedAtUtc": "2026-04-23T22:00:00Z",
  "lastProvisioningError": null
}
```

#### `GET /api/v1/tenants/{id}/products`
Lists all product entitlements:
```json
{
  "tenantId": "...",
  "products": [
    { "productKey": "synq_careconnect", "isActive": true, "isDefault": false }
  ]
}
```

#### `POST /api/v1/tenants/{id}/products/{productCode}/activate`
Idempotent product activation:
```json
{
  "tenantId": "...",
  "productKey": "synq_careconnect",
  "isActive": true,
  "activatedAtUtc": "2026-04-23T22:00:00Z"
}
```

All three endpoints require `AdminOnly` authorization policy.

---

### 8 — `TenantResponse` DTO extension

**File:** `apps/services/tenant/Tenant.Application/DTOs/TenantResponse.cs`

Added three new optional fields to the record (with defaults, backward-compatible):
- `ProvisioningStatus string = "Unknown"`
- `ProvisionedAtUtc DateTime? = null`
- `LastProvisioningError string? = null`

`TenantService.ToResponse()` updated to populate all three from the domain entity.

---

### 9 — `Program.cs` registration

```csharp
app.MapActivationEndpoints();     // BLK-TS-02
```

Added after existing endpoint registrations.

---

## API Surface Summary

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/api/v1/tenants/{id}/provisioning-status` | Admin | Tenant provisioning state |
| GET | `/api/v1/tenants/{id}/products` | Admin | List product entitlements |
| POST | `/api/v1/tenants/{id}/products/{productCode}/activate` | Admin | Idempotent product activation |

---

## Architecture Notes

- **Tenant service** is the authoritative owner of provisioning state — DNS execution stays in Identity (holds credentials)
- Provisioning state is updated by Tenant service **after** the Identity adapter call returns
- `ActivateProductAsync` is fully idempotent — safe to call on scheduler retry without duplicating entitlements
- `ProductKeys.*` constants ensure consistent product key normalization across services
- All new columns have safe defaults (`Unknown`, `NULL`) — no data backfill required for existing rows

---

## Files Changed

| File | Change |
|------|--------|
| `Tenant.Domain/Tenant.cs` | +`TenantProvisioningStatus` enum, +`ProductKeys` class, +3 fields, +`SetProvisioningStatus()` mutator |
| `Tenant.Infrastructure/Data/Configurations/TenantConfiguration.cs` | +EF mapping for 3 new columns |
| `Tenant.Infrastructure/Data/Migrations/20260423220000_AddProvisioningState.cs` | NEW migration |
| `Tenant.Infrastructure/Data/Migrations/TenantDbContextModelSnapshot.cs` | +3 new property entries |
| `Tenant.Application/Interfaces/IEntitlementService.cs` | +`ActivateProductAsync`, +`IsProductActiveAsync` |
| `Tenant.Application/Services/EntitlementService.cs` | Implemented both new methods |
| `Tenant.Application/Services/TenantAdminService.cs` | Set provisioning state after Identity provisioning call |
| `Tenant.Application/DTOs/TenantResponse.cs` | +3 provisioning fields (optional, backward-compat) |
| `Tenant.Application/Services/TenantService.cs` | `ToResponse()` maps new fields |
| `Tenant.Api/Endpoints/ActivationEndpoints.cs` | NEW — 3 activation endpoints |
| `Tenant.Api/Program.cs` | +`MapActivationEndpoints()` |
| `analysis/BLK-TS-02-report.md` | This report |
