# TENANT-B01 Report

## 1. Objective

Establish the foundation for a standalone Tenant service that will become the canonical owner of
tenant/company master data, separate from the Identity service. Block 1 creates the service
scaffold, EF Core persistence, initial tenant_db schema, health/diagnostic endpoints, and a
base REST API contract. No migration cutover, dual write, or read switch is performed in this block.

---

## 2. Codebase Analysis

### Where existing Identity tenant data lives

| File | Role |
|------|------|
| `apps/services/identity/Identity.Domain/Tenant.cs` | Canonical Identity `Tenant` entity (rich DDD aggregate) |
| `apps/services/identity/Identity.Infrastructure/Data/Configurations/TenantConfiguration.cs` | EF mapping → `idt_Tenants` table |
| `apps/services/identity/Identity.Infrastructure/Data/IdentityDbContext.cs` | Hosts `DbSet<Tenant>` |
| `apps/services/identity/Identity.Api/Endpoints/AdminEndpoints.cs` | Admin CRUD endpoints |
| `apps/services/identity/Identity.Api/Endpoints/TenantBrandingEndpoints.cs` | Logo/branding endpoints |

Key Identity fields:
- `Id` (Guid) — primary key; preserved exactly in Tenant service
- `Name` (string) — human-readable name → maps to `DisplayName` in Tenant service
- `Code` (string, slug) — short identifier → preserved as `Code`
- `IsActive` (bool) — soft active/inactive → maps to `Status` enum
- `Subdomain` (string?) — assigned subdomain slug → maps to `Subdomain`
- `ProvisioningStatus` (enum) → preserved as-is in Tenant service for migration tracking
- `LogoDocumentId` (Guid?) → preserved as `LogoDocumentId`
- `LogoWhiteDocumentId` (Guid?) → preserved as `LogoWhiteDocumentId`
- `CreatedAtUtc` / `UpdatedAtUtc` → preserved as `CreatedAtUtc` / `UpdatedAtUtc`

### Current service patterns reused

- **Layer structure**: `<Name>.Domain` / `<Name>.Application` / `<Name>.Infrastructure` / `<Name>.Api`
  (identical to Fund, CareConnect, Liens)
- **EF Core**: `Pomelo.EntityFrameworkCore.MySql 8.0.2`, `Microsoft.EntityFrameworkCore.Design 8.0.2`
- **DbContext pattern**: inherits from `DbContext`, overrides `SaveChangesAsync` for timestamp injection
- **DI extension**: `AddInfrastructure(IConfiguration)` static extension method
- **Minimal API endpoints**: `MapXxxEndpoints()` extension on `IEndpointRouteBuilder`
- **Middleware**: `ExceptionHandlingMiddleware` for `NotFoundException`, `ValidationException`, `ConflictException`
- **Health/info**: `GET /health` → `HealthResponse`, `GET /info` → `InfoResponse` (from shared Contracts)
- **Auto-migrate on startup**: `db.Database.MigrateAsync()` in `Program.cs`
- **Migration coverage probe**: `BuildingBlocks.Diagnostics.MigrationCoverageProbe.RunAsync()`
- **Shared types**: `BuildingBlocks.Domain.AuditableEntity`, `BuildingBlocks.Exceptions.*`,
  `Contracts.HealthResponse`, `Contracts.InfoResponse`

### Final location for new Tenant service

```
apps/services/tenant/
  Tenant.Domain/
  Tenant.Application/
  Tenant.Infrastructure/
  Tenant.Api/
```

### Database / orchestration implications

- New MySQL database: `tenant_db` (separate from `identity_db`)
- Connection string key: `TenantDb`
- New port: `5005`
- Added to `LegalSynq.sln`
- Added to `run-dev.sh` build+run
- Added to `run-prod.sh` launch + health probe
- Added to `build-prod.sh` build list
- Gateway routes added under `/tenant-service/...` prefix

---

## 3. Field Mapping Contract

### Identity.Domain.Tenant → Tenant.Domain.Tenant (Block 1 core schema)

| Identity Field | Type | Tenant Service Field | Type | Notes |
|---------------|------|---------------------|------|-------|
| `Id` | `Guid` | `Id` | `Guid` | **Preserved exactly** — cross-service key |
| `Name` | `string` | `DisplayName` | `string` | Renamed for clarity |
| `Code` | `string` | `Code` | `string` | Slug identifier |
| `IsActive` | `bool` | `Status` | `TenantStatus` enum | `Active`/`Inactive` |
| `Subdomain` | `string?` | `Subdomain` | `string?` | Assigned subdomain slug |
| `LogoDocumentId` | `Guid?` | `LogoDocumentId` | `Guid?` | Document reference only |
| `LogoWhiteDocumentId` | `Guid?` | `LogoWhiteDocumentId` | `Guid?` | Document reference only |
| `CreatedAtUtc` | `DateTime` | `CreatedAtUtc` | `DateTime` | UTC timestamp |
| `UpdatedAtUtc` | `DateTime` | `UpdatedAtUtc` | `DateTime` | UTC timestamp |

### Block 1 additional fields (not in Identity, added for Tenant service core model)

| Field | Type | Rationale |
|-------|------|-----------|
| `LegalName` | `string?` | Formal legal entity name; nullable — Identity lacks this |
| `TimeZone` | `string?` | IANA timezone code; scaffolded for future use |

### Deferred to later blocks (NOT in Block 1 schema)

| Identity / planned field | Deferred reason |
|--------------------------|----------------|
| `AddressLine1/City/State/PostalCode` | Address sub-model → Block 2/3 |
| `Latitude/Longitude/GeoPointSource` | Geo data → Block 3 |
| `ProvisioningStatus` + lifecycle fields | Provisioning workflow → Block 3 |
| `TenantProducts` (entitlements) | Product entitlements → Block 2 |
| `TenantDomains` | Domain ownership → Block 3 |
| `Organizations` | Org hierarchy → Block 4+ |
| `SessionTimeoutMinutes` | Settings model → Block 2 |
| Branding metadata beyond logo refs | Branding model → Block 2 |

---

## 4. Implementation Summary

### Service/projects created

| Project | Path |
|---------|------|
| `Tenant.Domain` | `apps/services/tenant/Tenant.Domain/` |
| `Tenant.Application` | `apps/services/tenant/Tenant.Application/` |
| `Tenant.Infrastructure` | `apps/services/tenant/Tenant.Infrastructure/` |
| `Tenant.Api` | `apps/services/tenant/Tenant.Api/` |

### Files added

**Tenant.Domain**
- `Tenant.cs` — `Tenant` entity + `TenantStatus` enum

**Tenant.Application**
- `Interfaces/ITenantRepository.cs`
- `Interfaces/ITenantService.cs`
- `Services/TenantService.cs`
- `DTOs/TenantResponse.cs`
- `DTOs/CreateTenantRequest.cs`
- `DTOs/UpdateTenantRequest.cs`

**Tenant.Infrastructure**
- `Data/TenantDbContext.cs`
- `Data/Configurations/TenantConfiguration.cs`
- `Data/Migrations/` (generated by `dotnet ef migrations add InitialTenantSchema`)
- `Repositories/TenantRepository.cs`
- `DependencyInjection.cs`

**Tenant.Api**
- `Program.cs`
- `Endpoints/TenantEndpoints.cs`
- `Middleware/ExceptionHandlingMiddleware.cs`
- `appsettings.json`
- `appsettings.Development.json`
- `appsettings.Production.json`

### Files modified

- `LegalSynq.sln` — 4 new project entries
- `scripts/run-dev.sh` — tenant service build + run
- `scripts/run-prod.sh` — tenant service launch + health probe
- `scripts/build-prod.sh` — tenant project in `BUILD_PROJECTS`
- `apps/gateway/Gateway.Api/appsettings.json` — tenant-service cluster + routes
- `scripts/_startup-helpers.sh` — `_svc_label_for` mapping for `Tenant.Api`

### Endpoints added

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/health` | Service health check |
| `GET` | `/info` | Service metadata |
| `GET` | `/api/v1/tenants` | List all tenants (paginated) |
| `GET` | `/api/v1/tenants/{id}` | Get tenant by ID |
| `GET` | `/api/v1/tenants/by-code/{code}` | Get tenant by code |
| `POST` | `/api/v1/tenants` | Create tenant |
| `PUT` | `/api/v1/tenants/{id}` | Update tenant |
| `DELETE` | `/api/v1/tenants/{id}` | Soft-delete (deactivate) tenant |

---

## 5. Database Changes

- **Database name**: `tenant_db`
- **Migration name**: `InitialTenantSchema`
- **Table created**: `tenant_Tenants`
- **Key columns**:
  - `Id` CHAR(36) PRIMARY KEY
  - `Code` VARCHAR(100) NOT NULL (unique index)
  - `DisplayName` VARCHAR(300) NOT NULL
  - `LegalName` VARCHAR(300) NULL
  - `Status` VARCHAR(50) NOT NULL (default `Active`)
  - `Subdomain` VARCHAR(63) NULL (unique index, sparse)
  - `LogoDocumentId` CHAR(36) NULL
  - `LogoWhiteDocumentId` CHAR(36) NULL
  - `TimeZone` VARCHAR(100) NULL
  - `CreatedAtUtc` DATETIME(6) NOT NULL
  - `UpdatedAtUtc` DATETIME(6) NOT NULL

---

## 6. Validation Results

| Check | Result | Notes |
|-------|--------|-------|
| Solution build | ✅ Pass | `dotnet build LegalSynq.sln` succeeds |
| Migration creation | ✅ Pass | `InitialTenantSchema` migration generated |
| Tenant service startup | ✅ Pass | Starts on port 5005 |
| DB connection | ✅ Pass | Connects to `tenant_db` |
| `GET /health` | ✅ Pass | Returns `{"status":"ok","service":"tenant"}` |
| `POST /api/v1/tenants` | ✅ Pass | Creates and returns tenant |
| `GET /api/v1/tenants/{id}` | ✅ Pass | Returns tenant by ID |
| `GET /api/v1/tenants/by-code/{code}` | ✅ Pass | Returns tenant by code |
| `PUT /api/v1/tenants/{id}` | ✅ Pass | Updates and returns tenant |

---

## 7. Known Gaps / Deferred Items

- **Address sub-model** — deferred to Block 2
- **Geo data** (lat/lng) — deferred to Block 3
- **Provisioning lifecycle** — deferred to Block 3 (complex state machine)
- **Branding full model** — logo ref exists; full palette/theme deferred to Block 2
- **Domain/subdomain ownership** — subdomain field present; full `TenantDomain` table deferred Block 3
- **Product entitlements** — deferred to Block 2
- **Organization hierarchy** — deferred to Block 4+
- **Migration utility** — no ETL from Identity yet; deferred to Block 4+
- **Dual write** — not implemented; deferred to Block 4+
- **Read switch** — Identity remains primary reader; switch deferred to Block 5+
- **Cutover** — no removal of tenant fields from Identity in this block

---

## 8. Next Recommended Block

**BLOCK 2 — Tenant Profile + Branding Core**

Block 2 should:
1. Extend the Tenant entity with a `TenantSettings` sub-table (session timeout, features)
2. Add a `TenantBranding` table (primary colour, accent, custom CSS variables, theme token)
3. Add `TenantProduct` entitlement table (product code, enabled, tier, effective dates)
4. Add `TenantAddress` sub-table (line1, city, state, postal, country)
5. Expose branding read/write via the Tenant service API
6. Begin dual-write for new tenant creates (write to both Identity and Tenant service)
7. Validate migration coverage with the `MigrationCoverageProbe` for the new tables
