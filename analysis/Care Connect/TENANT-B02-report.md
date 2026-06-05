# TENANT-B02 Report

## 1. Objective

Extend the Block 1 Tenant service foundation to become a usable tenant/company master-data service with:
- Expanded profile + contact + address metadata on the core Tenant entity
- Formalized lifecycle/status model
- Dedicated TenantBranding entity (one-to-one)
- Full branding management API (authenticated)
- Public branding endpoint (unauthenticated, safe for login/public pages)

## 2. Codebase Analysis

### Block 1 established
- `tenant_Tenants` table with: Id, Code, DisplayName, LegalName, Status, Subdomain, LogoDocumentId, LogoWhiteDocumentId, TimeZone, CreatedAtUtc, UpdatedAtUtc
- TenantStatus enum: Pending, Active, Inactive, Suspended
- CRUD endpoints: GET /api/v1/tenants, GET /api/v1/tenants/{id}, GET /api/v1/tenants/by-code/{code}, POST /api/v1/tenants, PUT /api/v1/tenants/{id}, DELETE /api/v1/tenants/{id}
- DDD entity with private setters and factory methods
- EF Core with Pomelo MySQL, migration 20260422000000_InitialTenantSchema
- Service running on port 5005, gateway route /tenant/{**catch-all}

### Block 2 gaps filled
- No description, website, locale, support contact, or address fields on Tenant
- No dedicated branding entity — logos stored inline on Tenant
- No branding management API
- No public (unauthenticated) branding endpoint
- UpdateProfile mutator scope too narrow

### Table naming convention
Block 1 uses `tenant_Tenants` (prefix `tenant_`, PascalCase set name).
New branding table follows same convention: `tenant_Brandings`.

## 3. Data Model Changes

### Tenant fields added
| Field | Type | Notes |
|---|---|---|
| Description | varchar(2000) nullable | Company description/bio |
| WebsiteUrl | varchar(500) nullable | Canonical company website |
| Locale | varchar(20) nullable | IETF BCP 47 locale (e.g. "en-US") |
| SupportEmail | varchar(320) nullable | Public support email |
| SupportPhone | varchar(30) nullable | Public support phone |
| AddressLine1 | varchar(200) nullable | Street address line 1 |
| AddressLine2 | varchar(200) nullable | Street address line 2 |
| City | varchar(100) nullable | City |
| StateOrProvince | varchar(100) nullable | State or province |
| PostalCode | varchar(20) nullable | Postal/ZIP code |
| CountryCode | varchar(2) nullable | ISO 3166-1 alpha-2 |

### Lifecycle / status model
Block 1 already established the correct enum: `Pending, Active, Inactive, Suspended`.
- **Active** — tenant is live and operational
- **Inactive** — tenant is deactivated (soft delete)
- **Pending** — tenant provisioning/onboarding in progress
- **Suspended** — temporarily suspended (billing/compliance hold)

Identity migration compatibility: `IsActive=true → Active`, `IsActive=false → Inactive`, `ProvisioningStatus=Pending → Pending`.
Status is stored as string in MySQL (varchar 50) for human-readable schema.

### TenantBranding entity
Separate one-to-one entity keyed on TenantId. Tenant service owns branding metadata; Documents service owns binaries.

| Field | Type | Notes |
|---|---|---|
| Id | Guid PK | |
| TenantId | Guid FK | Unique, FK to tenant_Tenants.Id |
| BrandName | varchar(300) nullable | Override brand name if different from DisplayName |
| LogoDocumentId | Guid nullable | Document ref (replaces inline logo on Tenant entity) |
| LogoWhiteDocumentId | Guid nullable | White logo variant ref |
| FaviconDocumentId | Guid nullable | Favicon ref |
| PrimaryColor | varchar(7) nullable | Hex color e.g. #1A2B3C |
| SecondaryColor | varchar(7) nullable | Hex color |
| AccentColor | varchar(7) nullable | Hex color |
| TextColor | varchar(7) nullable | Hex color |
| BackgroundColor | varchar(7) nullable | Hex color |
| WebsiteUrlOverride | varchar(500) nullable | Overrides Tenant.WebsiteUrl for brand context |
| SupportEmailOverride | varchar(320) nullable | Overrides Tenant.SupportEmail for brand context |
| SupportPhoneOverride | varchar(30) nullable | Overrides Tenant.SupportPhone for brand context |
| CreatedAtUtc | datetime(6) | |
| UpdatedAtUtc | datetime(6) | |

**Note**: LogoDocumentId and LogoWhiteDocumentId are kept on the Tenant entity for Identity backward-compatibility but also surfaced through the branding model. The branding entity's document references are the canonical future home.

## 4. API Contract

### Profile endpoints (updated)
| Method | Path | Auth | Notes |
|---|---|---|---|
| GET | /api/v1/tenants | AdminOnly | Paginated list with richer fields |
| GET | /api/v1/tenants/{id} | AdminOnly | Full profile response |
| GET | /api/v1/tenants/by-code/{code} | AdminOnly | Full profile response |
| POST | /api/v1/tenants | AdminOnly | Richer create request |
| PUT | /api/v1/tenants/{id} | AdminOnly | Richer update request |
| DELETE | /api/v1/tenants/{id} | AdminOnly | Soft deactivate |

### Branding endpoints
| Method | Path | Auth | Notes |
|---|---|---|---|
| GET | /api/v1/tenants/{id}/branding | AdminOnly | Full branding (creates empty if not exists) |
| PUT | /api/v1/tenants/{id}/branding | AdminOnly | Upsert branding |

### Public branding endpoints
| Method | Path | Auth | Notes |
|---|---|---|---|
| GET | /api/v1/public/branding/by-code/{code} | Anonymous | Safe public response |
| GET | /api/v1/public/branding/by-subdomain/{subdomain} | Anonymous | Safe public response |

Public response includes: tenantId, code, displayName, brandName, logoDocumentId, logoWhiteDocumentId, faviconDocumentId, primaryColor, secondaryColor, accentColor, textColor, backgroundColor, websiteUrl, supportEmail. Does NOT expose: addresss, internal IDs, status, locale.

## 5. Implementation Summary

### Files added
- `Tenant.Domain/TenantBranding.cs` — branding domain entity
- `Tenant.Application/DTOs/BrandingDtos.cs` — BrandingResponse, UpdateBrandingRequest, PublicBrandingResponse
- `Tenant.Application/Interfaces/IBrandingRepository.cs`
- `Tenant.Application/Interfaces/IBrandingService.cs`
- `Tenant.Application/Services/BrandingService.cs`
- `Tenant.Infrastructure/Data/Configurations/TenantBrandingConfiguration.cs`
- `Tenant.Infrastructure/Repositories/BrandingRepository.cs`
- `Tenant.Api/Endpoints/BrandingEndpoints.cs`
- `Tenant.Infrastructure/Data/Migrations/20260422120000_AddProfileAndBranding.cs`
- `Tenant.Infrastructure/Data/Migrations/20260422120000_AddProfileAndBranding.Designer.cs`

### Files modified
- `Tenant.Domain/Tenant.cs` — add profile/contact/address fields + mutators
- `Tenant.Application/DTOs/CreateTenantRequest.cs` — add new fields
- `Tenant.Application/DTOs/TenantResponse.cs` — add new fields
- `Tenant.Application/DTOs/UpdateTenantRequest.cs` — add new fields
- `Tenant.Application/Interfaces/ITenantRepository.cs` — add GetBySubdomainAsync
- `Tenant.Application/Interfaces/ITenantService.cs` — add branding ops
- `Tenant.Application/Services/TenantService.cs` — handle new fields + validation
- `Tenant.Infrastructure/Data/TenantDbContext.cs` — add TenantBrandings DbSet + intercept
- `Tenant.Infrastructure/Data/Configurations/TenantConfiguration.cs` — new column configs
- `Tenant.Infrastructure/Repositories/TenantRepository.cs` — add GetBySubdomainAsync
- `Tenant.Infrastructure/DependencyInjection.cs` — register branding services
- `Tenant.Api/Program.cs` — map branding endpoints
- `Tenant.Infrastructure/Data/Migrations/TenantDbContextModelSnapshot.cs` — updated

### Validation rules added
- Email: RFC-safe format check via `MailAddress` parse
- URL: `Uri.TryCreate` with http/https scheme check
- Hex color: regex `^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})$`
- CountryCode: must be 2 characters if supplied
- Code uniqueness: existing check preserved
- Subdomain uniqueness: existing check preserved

## 6. Database Changes

### Tables altered
- `tenant_Tenants`: 11 new nullable columns added (Description, WebsiteUrl, Locale, SupportEmail, SupportPhone, AddressLine1, AddressLine2, City, StateOrProvince, PostalCode, CountryCode)

### Tables added
- `tenant_Brandings`: new one-to-one branding table, FK → tenant_Tenants.Id, unique index on TenantId

### Migration
- `20260422120000_AddProfileAndBranding`

## 7. Validation Results

| Check | Result | Notes |
|---|---|---|
| Solution build | ✅ | 0 errors, 1 pre-existing version warning |
| Migration 20260422120000_AddProfileAndBranding | ✅ | Applied and recorded in __EFMigrationsHistory |
| Service startup on port 5005 | ✅ | `Now listening on: http://0.0.0.0:5005` |
| DB connection (tenant_db) | ✅ | Migrations applied on startup |
| tenant_Tenants table (11 new columns) | ✅ | Confirmed via DESCRIBE |
| tenant_Brandings table (16 columns) | ✅ | Confirmed via DESCRIBE + SHOW TABLES |
| Health endpoint | ✅ | `{"status":"ok","service":"tenant"}` |
| Info endpoint | ✅ | `{"service":"tenant","environment":"Development","version":"v1"}` |
| GET /info | ✅ | `{"service":"tenant","environment":"Development","version":"v1"}` |
| GET /api/v1/tenants (auth guard) | ✅ | 401 without token |
| POST /api/v1/tenants (auth guard) | ✅ | 401 without token |
| GET /api/v1/tenants/by-code/{code} (auth guard) | ✅ | 401 without token |
| GET /api/v1/tenants/{id}/branding (auth guard) | ✅ | 401 without token |
| PUT /api/v1/tenants/{id}/branding (auth guard) | ✅ | 401 without token |
| GET /api/v1/public/branding/by-code/{code} | ✅ | 200 unauthenticated; 404 with structured JSON for unknown code |
| GET /api/v1/public/branding/by-subdomain/{subdomain} | ✅ | 200 unauthenticated; 404 with structured JSON for unknown subdomain |
| Duplicate code conflict | ✅ | 409 Conflict |
| Email validation | ✅ | 400 Bad Request; MailAddress parse in TenantService + BrandingService |
| URL validation | ✅ | 400 Bad Request; Uri.TryCreate + http/https scheme check |
| Hex color validation | ✅ | 400 Bad Request; Regex ^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})$ in BrandingService |
| CountryCode validation | ✅ | Must be 2 chars if supplied |
| Subdomain uniqueness check | ✅ | ExistsBySubdomainAsync with excludeId for self-update |

## 8. Known Gaps / Deferred Items

- **Product entitlements** — deferred to a later block
- **Tenant settings key-value store** — deferred
- **Migration utility (Identity → Tenant)** — deferred
- **Dual write** — deferred
- **Read switch** — deferred
- **Identity decoupling / field removal** — deferred
- **Custom domain verification workflow** — deferred
- **Document upload flow** — deferred (document refs only stored)
- **Notification service integration** — deferred
- **Logo/branding fields on Tenant entity** — kept for Identity backward-compat; canonical future home is TenantBranding

## 9. Next Recommended Block

The next logical block is:

**BLOCK 3 — Domains & Tenant Resolution**

Scope: tenant domain ownership model, subdomain-to-tenant resolution API, and the groundwork for a future read switch from Identity to Tenant service for tenant lookup in authenticated flows.
