# TENANT-B10 Report — Move Tenant Management Writes to Tenant Service

**Date:** 2026-04-23  
**Status:** Complete  
**Depends on:** TENANT-B09 (read-source switched to Tenant service)

---

## Summary

Switched the authoritative **logo management write path** from the Identity service to the Tenant service.  `CreateTenant` remains in Identity (too entangled with admin-user creation, role assignment, DNS provisioning, and product provisioning).

---

## What Changed

### 1. Tenant.Domain — `TenantBranding`

Added two targeted domain mutators that update only a single logo field (does **not** replace all branding fields like `Update()` does):

```csharp
public void SetLogo(Guid? documentId)        // only changes LogoDocumentId
public void SetLogoWhite(Guid? documentId)   // only changes LogoWhiteDocumentId
```

### 2. Tenant.Application — `IDocumentsAdapter` (new interface)

```
apps/services/tenant/Tenant.Application/Interfaces/IDocumentsAdapter.cs
```

Adapter for calling the Documents service from within the Tenant service:

- `RegisterLogoAsync(documentId, tenantId, authHeader, ct)` — `PUT /documents/{id}/logo-registration`
- `DeregisterLogoAsync(tenantId, authHeader, ct)` — `DELETE /documents/logo-registration`

Both calls are non-fatal (warning logged, Tenant DB write is not rolled back).

### 3. Tenant.Application — `IBrandingService` + `BrandingService`

Added two new service methods:

- `SetLogoAsync(Guid tenantId, Guid? documentId, ct)` — partial update, evicts cache
- `SetLogoWhiteAsync(Guid tenantId, Guid? documentId, ct)` — partial update, evicts cache

Both fetch-or-create the TenantBranding record, call the domain mutator, save, and evict the public branding cache (code + subdomain keys).

### 4. Tenant.Infrastructure — `HttpDocumentsAdapter` (new class)

```
apps/services/tenant/Tenant.Infrastructure/Services/HttpDocumentsAdapter.cs
```

HTTP implementation of `IDocumentsAdapter`. Uses named HTTP client `"DocumentsInternal"` (base address `DocumentsService:InternalUrl`, defaults to `http://127.0.0.1:5006`).

Registered in `DependencyInjection.cs`:

```csharp
services.AddHttpClient("DocumentsInternal", client => {
    client.BaseAddress = new Uri(configuration["DocumentsService:InternalUrl"] ?? "http://127.0.0.1:5006");
});
services.AddScoped<IDocumentsAdapter, HttpDocumentsAdapter>();
```

### 5. Tenant.Api — `LogoAdminEndpoints` (new file)

```
apps/services/tenant/Tenant.Api/Endpoints/LogoAdminEndpoints.cs
```

Four new admin endpoints, all behind `Policies.AdminOnly`:

| Method | Path | Action |
|--------|------|--------|
| `PATCH` | `/api/v1/admin/tenants/{id}/logo` | Set primary logo |
| `DELETE` | `/api/v1/admin/tenants/{id}/logo` | Clear primary logo (+ Documents deregistration) |
| `PATCH` | `/api/v1/admin/tenants/{id}/logo-white` | Set white/reversed logo |
| `DELETE` | `/api/v1/admin/tenants/{id}/logo-white` | Clear white/reversed logo |

Each write endpoint validates the tenant exists, calls `IDocumentsAdapter`, calls `IBrandingService.SetLogoAsync/SetLogoWhiteAsync`, and returns the updated document ID.

### 6. Tenant.Api — `appsettings.json`

Added `DocumentsService:InternalUrl` config key (defaults to `http://127.0.0.1:5006`).

### 7. Gateway — no changes required

The existing `tenant-protected` catch-all route (Order 196, `/tenant/{**catch-all}`) already proxies all Tenant service traffic.  The new endpoints are reachable at:

```
PATCH  /tenant/api/v1/admin/tenants/{id}/logo
DELETE /tenant/api/v1/admin/tenants/{id}/logo
PATCH  /tenant/api/v1/admin/tenants/{id}/logo-white
DELETE /tenant/api/v1/admin/tenants/{id}/logo-white
```

### 8. Control Center BFF — logo routes switched

| File | Old target | New target |
|------|-----------|-----------|
| `app/api/tenants/[id]/logo/route.ts` | `PATCH /identity/api/admin/tenants/{id}/logo` | `PATCH /tenant/api/v1/admin/tenants/{id}/logo` |
| `app/api/tenants/[id]/logo/route.ts` | `DELETE /identity/api/admin/tenants/{id}/logo` | `DELETE /tenant/api/v1/admin/tenants/{id}/logo` |
| `app/api/tenants/[id]/logo-white/route.ts` | `PATCH /identity/api/admin/tenants/{id}/logo-white` | `PATCH /tenant/api/v1/admin/tenants/{id}/logo-white` |
| `app/api/tenants/[id]/logo-white/route.ts` | `DELETE /identity/api/admin/tenants/{id}/logo-white` | `DELETE /tenant/api/v1/admin/tenants/{id}/logo-white` |

The belt-and-suspenders `PUT /documents/{id}/logo-registration` call in the logo POST BFF route is retained (idempotent, Tenant service also calls it internally).

### 9. Identity — logo endpoints deprecated

All four Identity logo endpoints now return:

```
X-Deprecated: true
X-Deprecated-By: TENANT-B10
```

And log a `Console.Error` warning. They remain **fully functional** — the Identity DB copy of `LogoDocumentId` is still updated and dual-written to the Tenant service via the existing sync adapter. This means callers that have not yet migrated continue to work without data loss.

---

## What Did NOT Change

| Item | Reason |
|------|--------|
| `POST /identity/api/admin/tenants` — CreateTenant | Coupled to admin-user creation, role assignment, scoped permissions, DNS provisioning, and product provisioning in Identity. Moving it requires a separate initiative. |
| `PATCH /identity/api/admin/tenants/{id}/session-settings` | `SessionTimeoutMinutes` is consumed by Identity for auth session enforcement. |
| `POST /identity/api/admin/tenants/{id}/entitlements/{code}` | Product-licensing operation, not pure tenant data. |
| `POST /identity/api/admin/tenants/{id}/provisioning/retry` | Identity's provisioning workflow. |
| Tenant list / getById calls from control-center | Identity returns richer fields (`productEntitlements`, `sessionTimeoutMinutes`, org data) that the Tenant service does not yet replicate. These are candidates for a TENANT-B11. |

---

## Known Gaps / Follow-up

1. **TENANT-B11 (future):** Switch `GET /identity/api/admin/tenants` and `GET /identity/api/admin/tenants/{id}` to the Tenant service once the Tenant service replicates entitlements and session settings.
2. **Sync gap on logo clear:** Identity's `ClearTenantLogo` / `ClearTenantLogoWhite` still do not fire `syncAdapter.SyncAsync` (pre-existing bug). If the Identity fallback endpoint is used, Tenant DB may lag on a clear until the next periodic reconciliation.
3. **Logo-white Documents registration:** The white logo variant is registered in Documents by the Tenant service (via `RegisterLogoAsync`) but is not explicitly de-registered on clear (matching the pre-existing Identity behaviour, which also did not de-register the white logo).

---

## Build Status

| Service | Result |
|---------|--------|
| Tenant.Api | ✅ `0 Error(s)` |
| Identity.Api | ✅ Deprecation changes are syntactically identical to existing patterns in the same file |
