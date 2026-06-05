# BLK-TS-01 Report
**Block:** Tenant Core Foundation
**Status:** Complete
**Date:** 2026-04-23

---

## 1. Summary

BLK-TS-01 establishes the Tenant service as the canonical system of record for tenant/company data. The service already had a strong foundation from prior blocks. This block added the three remaining gaps:

| Gap | Change |
|-----|--------|
| No code format validation | Added `IsValidCodeFormat` regex validator to `TenantService` |
| No code-check API | Added `GET /api/v1/tenants/check-code?code=acme` |
| No provision API | Added `POST /api/v1/tenants/provision` |

**Scope boundary respected:** Identity, CareConnect, DNS provisioning, product activation, and onboarding flows were NOT touched.

---

## 2. Domain Model

The canonical Tenant entity is **fully implemented** in `Tenant.Domain/Tenant.cs`:

| Field | Type | Notes |
|-------|------|-------|
| `Id` | `Guid` | PK — preserved as Identity cross-service FK |
| `Code` | `string` | Unique slug, lowercase, 2–50 chars |
| `DisplayName` | `string` | Human-readable name |
| `LegalName` | `string?` | Optional formal entity name |
| `Description` | `string?` | — |
| `Status` | `TenantStatus` | `Pending / Active / Inactive / Suspended` |
| `Subdomain` | `string?` | Unique; defaults to `Code` on provision |
| `LogoDocumentId` | `Guid?` | Reference to Documents service |
| `LogoWhiteDocumentId` | `Guid?` | White-variant logo |
| `WebsiteUrl` | `string?` | — |
| `TimeZone` | `string?` | IANA tz id |
| `Locale` | `string?` | BCP 47 tag |
| `SupportEmail` | `string?` | — |
| `SupportPhone` | `string?` | — |
| `AddressLine1-2` | `string?` | — |
| `City`, `StateOrProvince`, `PostalCode` | `string?` | — |
| `CountryCode` | `string?` | ISO 3166-1 alpha-2 |
| `CreatedAtUtc` | `DateTime` | — |
| `UpdatedAtUtc` | `DateTime` | Auto-updated by DbContext |

Supporting entities: `TenantBranding`, `TenantDomain`, `TenantCapability`, `TenantSetting`, `TenantProductEntitlement`.

---

## 3. API Endpoints

### 3.1 GET /api/v1/tenants/check-code?code={code}

**Auth:** Anonymous (reveals only availability — no sensitive data)

**Behavior:**
1. Returns 400 if `code` param is missing
2. Normalizes (trim → lowercase)
3. Validates format (regex, length)
4. Checks uniqueness against DB
5. Returns structured response

**Response:**
```json
// Available
{ "available": true,  "normalizedCode": "acme" }

// Unavailable — taken
{ "available": false, "normalizedCode": "acme", "error": "The code 'acme' is already taken." }

// Unavailable — invalid format
{ "available": false, "normalizedCode": "-acme", "error": "Code must not start or end with a hyphen." }
```

---

### 3.2 POST /api/v1/tenants/provision

**Auth:** `AdminOnly` (platform admin JWT role)

**Request:**
```json
{ "tenantName": "Acme Medical", "tenantCode": "acme" }
```

**Behavior:**
1. Validates tenantName and tenantCode are non-empty
2. Normalizes code (trim → lowercase)
3. Validates code format
4. Checks code uniqueness
5. Subdomain defaults to normalized code; checks subdomain uniqueness
6. Creates `Tenant` entity via `Tenant.Domain.Tenant.Create()`
7. Persists to `tenant_Tenants` table

**Response (201 Created):**
```json
{ "tenantId": "...", "tenantCode": "acme", "subdomain": "acme" }
```

**Error responses:**
- `400` — missing/empty fields
- `409` — code or subdomain already taken
- `422` — invalid code format

---

### 3.3 Existing admin CRUD (unchanged)

| Method | Path | Auth |
|--------|------|------|
| GET | `/api/v1/tenants` | AdminOnly |
| GET | `/api/v1/tenants/{id}` | AdminOnly |
| GET | `/api/v1/tenants/by-code/{code}` | AdminOnly |
| POST | `/api/v1/tenants` | AdminOnly |
| PUT | `/api/v1/tenants/{id}` | AdminOnly |
| DELETE | `/api/v1/tenants/{id}` | AdminOnly |

### 3.4 Existing resolution (unchanged, anonymous)

| Method | Path |
|--------|------|
| GET | `/api/v1/public/resolve/by-host?host=` |
| GET | `/api/v1/public/resolve/by-subdomain/{subdomain}` |
| GET | `/api/v1/public/resolve/by-code/{code}` |

---

## 4. Persistence / Migrations

**No new migrations required.** The schema was already complete from prior blocks:

| Migration | What it added |
|-----------|---------------|
| `20260422000000_InitialTenantSchema` | `tenant_Tenants` table with `UNIQUE INDEX` on `Code` + filtered `UNIQUE INDEX` on `Subdomain` (where not null) |
| `20260422120000_AddProfileAndBranding` | Profile, address, contact, branding columns |
| `20260423035637_AddMigrationRunTracking` | Migration run tracking tables |
| `20260423200000_AddTenantDomains` | `TenantDomain` table |
| `20260423210000_AddEntitlementsCapabilitiesSettings` | `TenantCapability`, `TenantSetting`, `TenantProductEntitlement` tables |

**DB uniqueness constraints:**
- `Code` — `UNIQUE NOT NULL` (enforced at DB level via EF unique index)
- `Subdomain` — `UNIQUE` filtered on `NOT NULL` (enforced at DB level via EF unique index)

---

## 5. Validation Rules

### Tenant Code Format (BLK-TS-01 addition)

| Rule | Detail |
|------|--------|
| Charset | Lowercase letters (`a-z`), digits (`0-9`), hyphens (`-`) only |
| No leading hyphen | Must start with `[a-z0-9]` |
| No trailing hyphen | Must end with `[a-z0-9]` |
| Min length | 2 characters |
| Max length | 50 characters |
| Regex | `^[a-z0-9]([a-z0-9\-]*[a-z0-9])?$` |

Applied in: `TenantService.CheckCodeAsync`, `TenantService.ProvisionAsync`, `TenantService.CreateAsync`.

### Other field validation (pre-existing)
- `SupportEmail` — valid RFC 5321 email if provided
- `WebsiteUrl` — valid http/https URI if provided
- `CountryCode` — 2-char ISO 3166-1 alpha-2 if provided

---

## 6. Test Results

### Manual validation against local service (port 5005)

| # | Test | Input | Expected | Result |
|---|------|-------|----------|--------|
| 1 | Valid code available | `GET /check-code?code=newclient` | `{ available: true }` | ✅ |
| 2 | Invalid format — leading hyphen | `GET /check-code?code=-acme` | `{ available: false, error: "...hyphen..." }` | ✅ |
| 3 | Invalid format — too short | `GET /check-code?code=a` | `{ available: false, error: "at least 2..." }` | ✅ |
| 4 | Invalid format — uppercase | `GET /check-code?code=ACME` | normalized to `acme`, checked | ✅ |
| 5 | Duplicate code rejected | `GET /check-code?code=lienscom` (existing) | `{ available: false, error: "already taken" }` | ✅ |
| 6 | Provision succeeds | `POST /provision { tenantName: "Test Co", tenantCode: "testco" }` | `201 { tenantId, tenantCode, subdomain }` | ✅ |
| 7 | Provision persists | DB query for `testco` after provision | Record present in `tenant_Tenants` | ✅ |
| 8 | Duplicate provision fails | Second `POST /provision` with same code | `409 Conflict` | ✅ |
| 9 | `dotnet build` | `Tenant.Api.csproj` | Build succeeds, 0 errors, 1 pre-existing MSB3277 warning (JwtBearer version resolution — not introduced by BLK-TS-01) | ✅ |

---

## 7. Files Changed

### New files
| File | Purpose |
|------|---------|
| `Tenant.Application/DTOs/ProvisionDtos.cs` | `CheckCodeResponse`, `ProvisionRequest`, `ProvisionResponse` DTOs |
| `Tenant.Api/Endpoints/ProvisionEndpoints.cs` | `check-code` + `provision` minimal API endpoints |

### Modified files
| File | Change |
|------|--------|
| `Tenant.Application/Interfaces/ITenantService.cs` | Added `CheckCodeAsync` and `ProvisionAsync` to interface |
| `Tenant.Application/Services/TenantService.cs` | Implemented `CheckCodeAsync`, `ProvisionAsync`, `IsValidCodeFormat`; added format guard to `CreateAsync` |
| `Tenant.Api/Program.cs` | Registered `MapProvisionEndpoints()` |

---

## 8. Issues / Gaps

| # | Description | Disposition |
|---|-------------|-------------|
| 1 | Provision endpoint is admin-only; no self-service onboarding path yet | By design — onboarding flow is deferred (out of BLK-TS-01 scope) |
| 2 | `check-code` is anonymous — throttling/rate-limiting not implemented | Acceptable for foundation block; add in a hardening pass |
| 3 | Code format validation not applied to `UpsertFromSyncAsync` (Identity sync path) | Intentional — sync path must accept whatever codes Identity holds; validation is for new creation only |
| 4 | No DNS provisioning | Out of scope per block spec |
| 5 | No Identity/CareConnect refactor | Out of scope per block spec |
