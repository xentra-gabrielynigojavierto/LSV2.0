# TENANT-B04 — Migration Dry-Run Reconciliation Report

**Generated:** 2026-04-23  
**Block:** TENANT-B04  
**Mode:** Foundation Only — Dry-Run (No Writes)  
**Endpoint:** `GET /api/admin/migration/dry-run` (requires AdminOnly JWT)

---

## Summary

This report describes the dry-run reconciliation foundation introduced in TENANT-B04.

The migration utility service (`MigrationUtilityService`) compares:
- **Identity service tenant data** (read via `IdentityDb` connection string, raw SQL, read-only)
- **Tenant service tenant data** (read via EF Core `TenantDbContext`, read-only)

No writes are performed in this block. Write mode is deferred to Block 5.

---

## Dry-Run Endpoint

```
GET /api/admin/migration/dry-run
Authorization: Bearer <platform_admin_jwt>
```

### Response Shape

```json
{
  "generatedAtUtc": "2026-04-23T03:35:00.000Z",
  "identityAccessible": true | false,
  "identityAccessError": null | "error message",
  "identityTenantCount": 0,
  "tenantServiceCount": 0,
  "missingInTenantService": 0,
  "codeMismatches": 0,
  "nameMismatches": 0,
  "statusMismatches": 0,
  "subdomainGaps": 0,
  "logoGaps": 0,
  "differences": [
    {
      "identityTenantId": "...",
      "identityCode": "...",
      "identityName": "...",
      "identityStatus": "...",
      "identitySubdomain": "...",
      "identityHasLogo": true | false,
      "tenantServiceId": "..." | null,
      "tenantServiceCode": "..." | null,
      "tenantServiceName": "..." | null,
      "tenantServiceStatus": "..." | null,
      "isMissing": true | false,
      "hasCodeMismatch": true | false,
      "hasNameMismatch": true | false,
      "hasStatusMismatch": true | false,
      "hasSubdomainGap": true | false,
      "hasLogoGap": true | false
    }
  ]
}
```

---

## Identity Access Configuration

The utility requires `ConnectionStrings:IdentityDb` in the Tenant service configuration.

If absent, the report returns:
```json
{
  "identityAccessible": false,
  "identityAccessError": "ConnectionStrings:IdentityDb is not configured.",
  ...
}
```

This is the expected behavior in development — the `IdentityDb` connection string is not exposed to the Tenant service by default. To enable Identity comparison, add the Identity MySQL connection string as `ConnectionStrings__IdentityDb` in the Tenant service environment.

---

## Reconciliation Logic

The utility performs the following comparisons for each Identity tenant:

| Check | Logic |
|---|---|
| **IsMissing** | No Tenant service tenant with matching Code exists |
| **HasCodeMismatch** | Code differs (case-insensitive) after lookup by code |
| **HasNameMismatch** | DisplayName/Name differs (case-insensitive) |
| **HasStatusMismatch** | Status interpretation differs after normalization |
| **HasSubdomainGap** | Identity has a subdomain but Tenant service does not |
| **HasLogoGap** | Identity has a logo but Tenant service shows no evidence |

Records with no differences (all flags false) are omitted from `differences` for brevity.

---

## Status Normalization

Identity status values are compared against Tenant service status strings after normalization via `NormalizeStatus()`. Both systems use the same string values (`Active`, `Inactive`, `Suspended`, `Pending`).

---

## Deferred Items

| Item | Reason Deferred |
|---|---|
| Write mode (actual migration) | Deferred to Block 5 |
| Dual write from Identity | Out of scope for B04 |
| Read switch cutover | Out of scope for B04 |
| Identity field removal | Out of scope for B04 |
| Automated dry-run scheduling | Out of scope for B04 |
| Domain/subdomain reconciliation | B03 TenantDomain table available — reconciliation adds `HasSubdomainGap` check |

---

## Next Steps (Block 5)

1. Add `ConnectionStrings__IdentityDb` as a read-only connection to Tenant service
2. Run live dry-run report and review `differences` list
3. Implement write mode with explicit `dryRun=false` query param
4. Add pre-migration validation guards
5. Implement dual-write hooks on Identity tenant CRUD operations

---

*This document represents the foundation-only dry-run plan. No data was modified during Block 4 implementation.*
