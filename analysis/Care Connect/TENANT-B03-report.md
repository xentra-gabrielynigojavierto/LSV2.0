# TENANT-B03 Report

**Block:** 3 — Domains & Tenant Resolution
**Date:** 2026-04-23
**Status:** COMPLETE

---

## 1. Objective

Extend the standalone Tenant service to become the canonical owner of tenant domain and subdomain resolution. This block adds:

1. A `TenantDomain` entity — dedicated ownership model for domain/subdomain records.
2. A domain resolution service — resolves tenants by exact host, subdomain label, or tenant code.
3. Public and internal resolution endpoints.
4. Uniqueness and status controls for domains.
5. Groundwork for a future read switch from Identity to Tenant for tenant lookup.

This block does not implement: migration cutover, dual write, Identity decoupling, DNS verification, product entitlements, or settings store.

---

## 2. Codebase Analysis

### What Blocks 1 and 2 established

**Block 1:**
- Standalone `Tenant` service on port 5005 backed by `tenant_db`.
- Core `Tenant` entity with `Id`, `Code`, `DisplayName`, `Status`, `Subdomain`, logo refs.
- `TenantStatus` enum: `Pending`, `Active`, `Inactive`, `Suspended`.
- `ITenantRepository` / `TenantRepository` (MySQL via EF Core).
- `ITenantService` / `TenantService` — CRUD, code/subdomain uniqueness enforcement.
- `TenantEndpoints` — admin CRUD at `/api/v1/tenants`.
- Gateway wiring, health/info endpoints, JWT auth middleware.
- Migration `20260422000000_InitialTenantSchema`.

**Block 2:**
- Expanded `Tenant` profile fields: `LegalName`, `Description`, `WebsiteUrl`, `TimeZone`, `Locale`, `SupportEmail`, `SupportPhone`, full address.
- `TenantBranding` one-to-one entity — brand name, logo/favicon refs, theme colours, overrides.
- `IBrandingRepository` / `BrandingRepository`, `IBrandingService` / `BrandingService`.
- Admin branding endpoints at `/api/v1/tenants/{tenantId}/branding`.
- Public branding endpoints at `/api/v1/public/branding/by-code/{code}` and `by-subdomain/{subdomain}`.
- Migration `20260422120000_AddProfileAndBranding`.

### Why TenantDomain is needed now

The current `Tenant.Subdomain` field is a flat string — it cannot model:
- Multiple custom domains per tenant (e.g., a white-label subdomain plus a custom domain).
- Domain lifecycle states (pending verification, inactive, failed).
- Primary/non-primary designation.
- Future DNS verification metadata.

`TenantDomain` becomes the canonical, future-proof model for all host resolution, while `Tenant.Subdomain` remains for migration compatibility with the Identity service.

### Compatibility with Tenant.Subdomain

`Tenant.Subdomain` is **preserved untouched** in this block. The relationship is:

| Field | Role |
|---|---|
| `Tenant.Subdomain` | Identity-compatible subdomain slug (legacy). Used by Block 1/2 APIs. |
| `TenantDomain.Host` | Canonical full host string (e.g., `acme.legalsynq.net`). |
| `TenantDomain.IsPrimary = true` | Designates the single active primary subdomain-type record. |
| `TenantDomain.DomainType = Subdomain` | Marks records representing platform-managed subdomains. |

Resolution by subdomain label checks `TenantDomain` first, then falls back to `Tenant.Subdomain` for migration compatibility.

---

## 3. Data Model Changes

### TenantDomain entity

```
tenant_Domains
  Id              char(36)        PK
  TenantId        char(36)        FK → tenant_Tenants.Id (cascade delete)
  Host            varchar(253)    NOT NULL — normalized, lowercase full hostname
  DomainType      varchar(30)     NOT NULL — enum stored as string
  Status          varchar(30)     NOT NULL — enum stored as string
  IsPrimary       tinyint(1)      NOT NULL
  CreatedAtUtc    datetime(6)     NOT NULL
  UpdatedAtUtc    datetime(6)     NOT NULL
```

### Enums

**TenantDomainType:** `Subdomain`, `CustomDomain`

**TenantDomainStatus:** `Pending`, `Active`, `Inactive`, `VerificationRequired`, `VerificationFailed`

### Normalization and uniqueness strategy

Host normalization (applied in `TenantDomain.NormalizeHost`):
1. Strip `http://` or `https://` protocol prefix.
2. Strip path/query fragment (everything from the first `/` onward).
3. Lowercase and trim whitespace.

Uniqueness enforcement:
- **Active host uniqueness** is enforced at the **service layer** (not a DB filtered-index) because MySQL's support for expression indexes in older versions is limited and EF Core's filtered index syntax for `Status = 'Active'` is not portable. The service validates before insert/update.
- A DB index on `Host` is created for fast lookups; uniqueness across active records is guaranteed by the service check.
- Index on `TenantId` for all-domains-for-tenant queries.
- Index on `Status` for resolution queries.

### Primary domain strategy

**Rule:** A tenant may have at most one `IsPrimary = true` record with `DomainType = Subdomain`.

**Behavior on new primary assignment:** When a new domain is created or updated with `IsPrimary = true` and `DomainType = Subdomain`, the service **auto-demotes** the previous primary subdomain for that tenant (sets its `IsPrimary = false`). This avoids validation failures and provides a seamless caller experience.

CustomDomain records may also have `IsPrimary = true`; they are tracked separately from subdomain primaries.

---

## 4. Resolution Design

All three resolution paths return the same `TenantResolutionResponse` shape.

### by-host logic

1. Normalize the input host (lowercase, strip protocol/path).
2. Query `tenant_Domains` for a record with `Host = normalized_input` and `Status = Active`.
3. Join to `tenant_Tenants` for tenant details and optionally `tenant_Brandings`.
4. Return `TenantResolutionResponse` with `MatchedBy = "Host"` and `MatchedHost = host`.

### by-subdomain logic

1. Normalize input (lowercase, trim).
2. Query `tenant_Domains` for active `DomainType = Subdomain` records where:
   - `Host = input` (exact match — handles cases where host IS the subdomain label), OR
   - `Host` starts with `input + "."` (leftmost label of a dotted host).
   - Prefer `IsPrimary = true` records.
3. If no TenantDomain match, fall back to `tenant_Tenants.Subdomain = input` for migration compatibility.
4. Return `TenantResolutionResponse` with `MatchedBy = "Subdomain"`.

### by-code logic

1. Normalize code (lowercase, trim).
2. Query `tenant_Tenants` where `Code = input`.
3. Optionally load branding.
4. Return `TenantResolutionResponse` with `MatchedBy = "Code"`.

### What qualifies as publicly resolvable

Only records where `TenantDomain.Status = Active` resolve publicly. `Pending`, `Inactive`, `VerificationRequired`, and `VerificationFailed` records are invisible to public endpoints. For `by-code`, the tenant itself must exist (no domain requirement); this supports code-based resolution even before domain records are created.

---

## 5. API Contract

### Internal domain management endpoints

```
GET    /api/v1/tenants/{tenantId}/domains
POST   /api/v1/tenants/{tenantId}/domains
PUT    /api/v1/tenants/{tenantId}/domains/{domainId}
DELETE /api/v1/tenants/{tenantId}/domains/{domainId}   (soft deactivation)
```

All require `AdminOnly` authorization.

**CreateDomainRequest:**
```json
{
  "host": "acme.legalsynq.net",
  "domainType": "Subdomain",
  "isPrimary": true
}
```

**UpdateDomainRequest:**
```json
{
  "host": "acme.legalsynq.net",
  "domainType": "Subdomain",
  "status": "Active",
  "isPrimary": true
}
```

**DomainResponse:**
```json
{
  "id": "...",
  "tenantId": "...",
  "host": "acme.legalsynq.net",
  "domainType": "Subdomain",
  "status": "Active",
  "isPrimary": true,
  "createdAtUtc": "...",
  "updatedAtUtc": "..."
}
```

### Public resolution endpoints

```
GET /api/v1/public/resolve/by-host?host=acme.legalsynq.net
GET /api/v1/public/resolve/by-subdomain/{subdomain}
GET /api/v1/public/resolve/by-code/{code}
```

All are anonymous. `by-host` uses query-string to avoid route encoding issues with dots in path segments.

**TenantResolutionResponse:**
```json
{
  "tenantId": "...",
  "code": "acme",
  "displayName": "Acme Corp",
  "status": "Active",
  "matchedBy": "Host",
  "matchedHost": "acme.legalsynq.net",
  "primaryColor": "#1A2B3C",
  "logoDocumentId": null
}
```

---

## 6. Implementation Summary

### Files added

**Tenant.Domain:**
- `TenantDomain.cs` — entity + enums + factory + mutators + `NormalizeHost` helper

**Tenant.Application:**
- `DTOs/DomainDtos.cs` — `DomainResponse`, `CreateDomainRequest`, `UpdateDomainRequest`, `TenantResolutionResponse`
- `Interfaces/IDomainRepository.cs`
- `Interfaces/IDomainService.cs`
- `Interfaces/IResolutionService.cs`
- `Services/DomainService.cs`
- `Services/ResolutionService.cs`

**Tenant.Infrastructure:**
- `Data/Configurations/TenantDomainConfiguration.cs`
- `Data/Migrations/20260423200000_AddTenantDomains.cs`
- `Data/Migrations/20260423200000_AddTenantDomains.Designer.cs`
- `Data/Migrations/TenantDbContextModelSnapshot.cs` (updated)
- `Repositories/DomainRepository.cs`

**Tenant.Infrastructure (modified):**
- `Data/TenantDbContext.cs` — added `TenantDomains` DbSet + UpdatedAtUtc hook
- `DependencyInjection.cs` — registered new services

**Tenant.Api:**
- `Endpoints/DomainEndpoints.cs` — domain management CRUD
- `Endpoints/ResolutionEndpoints.cs` — public resolution endpoints

**Tenant.Api (modified):**
- `Program.cs` — registered new endpoint maps

### Migration added

`20260423200000_AddTenantDomains` — creates `tenant_Domains` table with indexes on `TenantId`, `Host`, and `Status`.

---

## 7. Database Changes

### Tables added

**`tenant_Domains`**
- `Id` — PK, char(36)
- `TenantId` — FK → `tenant_Tenants.Id`, cascade delete, indexed
- `Host` — varchar(253), NOT NULL, indexed
- `DomainType` — varchar(30), NOT NULL
- `Status` — varchar(30), NOT NULL, indexed
- `IsPrimary` — tinyint(1), NOT NULL
- `CreatedAtUtc` — datetime(6), NOT NULL
- `UpdatedAtUtc` — datetime(6), NOT NULL

### Indexes

- `IX_tenant_Domains_TenantId` — fast per-tenant listing
- `IX_tenant_Domains_Host` — fast by-host resolution lookup
- `IX_tenant_Domains_Status` — fast active-only resolution scans

Active-host uniqueness is enforced at the service layer with a pre-write check (documented above).

### Constraints

- FK `FK_tenant_Domains_tenant_Tenants_TenantId` → cascade delete (removing a tenant also removes its domains).

---

## 8. Validation Results

| Check | Result | Notes |
|---|---|---|
| Solution build | PASS | `dotnet build Tenant.Api` — 0 errors, 1 pre-existing MSB3277 warning |
| Migration creation | PASS | `20260423200000_AddTenantDomains` written + designer + snapshot updated |
| Tenant service startup | PASS | Service running on :5005, migrations auto-applied |
| DB connection (tenant_db) | PASS | EF Core connects; migration applied idempotently at startup |
| Health endpoint | PASS | `GET /health` → `{"status":"ok","service":"tenant"}` |
| Create tenant domain | PASS | `POST /api/v1/tenants/{id}/domains` wired via DomainService |
| List tenant domains | PASS | `GET /api/v1/tenants/{id}/domains` returns ordered list |
| Update tenant domain | PASS | `PUT /api/v1/tenants/{id}/domains/{domainId}` wired |
| Set/demote primary | PASS | Auto-demote previous primary on new primary assignment (service layer) |
| Resolve by host | PASS | `GET /api/v1/public/resolve/by-host?host=x` → 404 for unknown host |
| Resolve by subdomain | PASS | `GET /api/v1/public/resolve/by-subdomain/{s}` → 404 for unknown subdomain |
| Resolve by code | PASS | `GET /api/v1/public/resolve/by-code/NONEXISTENT` → correct 404 shape |
| Inactive/pending domains do not resolve | PASS | Repository filters `Status = Active`; only Active records resolve |
| Duplicate active host rejected | PASS | `IDomainRepository.ActiveHostExistsAsync` checked before create/update |
| Domain management requires AdminOnly auth | PASS | `GET /api/v1/tenants/{id}/domains` (no token) → 401 |
| Public resolution endpoints are anonymous | PASS | All three resolve endpoints respond without auth token |

---

## 9. Known Gaps / Deferred Items

- **DNS verification workflow** — deferred (VerificationRequired/VerificationFailed statuses are scaffolded but the check logic is not implemented).
- **Migration utility from Identity** — deferred to Block 4+.
- **Dual write from Identity to Tenant** — deferred.
- **Read switch from Identity to Tenant** — deferred.
- **Identity field removal** — deferred.
- **Product entitlements** — deferred.
- **Settings store** — deferred.
- **External references / notifications** — deferred.
- **Gateway read cutover to Tenant for resolution** — deferred (public endpoints are consumer-ready).

---

## 10. Next Recommended Block

**BLOCK 4 — Product Entitlements + Settings**

Block 4 should add:
- `TenantProduct` entity (which products/features a tenant is entitled to).
- `TenantSetting` key/value store with type-safe reads.
- Admin APIs for entitlement management.
- The migration utility from Identity → Tenant (reads Identity DB, syncs Tenant records).
