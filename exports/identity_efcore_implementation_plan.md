# LegalSynq — EF Core Implementation & Migration Plan
## Identity / Core Schema — Multi-Org Product Role Platform

---

## 1. EF Core Entity Plan

### Legend
- **NEW** — entity class to be created
- **EXISTING** — entity already in codebase, kept as-is or with minor additions
- **COMPOSITE KEY** — `HasKey(e => new { e.A, e.B })`
- **PAYLOAD** — join table with columns beyond the FK pair

---

### Tenant / Domain Entities

| Entity | Status | Key | Notes |
|---|---|---|---|
| `Tenant` | EXISTING | `Guid Id` | Add nav prop: `ICollection<TenantDomain>`, `ICollection<Organization>` |
| `TenantDomain` | **NEW** | `Guid Id` | Owns subdomain/custom domain routing. FK → Tenant. One primary subdomain per tenant. |
| `Organization` | **NEW** | `Guid Id` | PAYLOAD — FK: TenantId, OrgType string constant. Nav: Domains, Products, Memberships. |
| `OrganizationDomain` | **NEW** | `Guid Id` | Org-owned email domains and future custom domains. NOT used for subdomain routing. FK → Organization. |

### Product Entities

| Entity | Status | Key | Notes |
|---|---|---|---|
| `Product` | EXISTING | `Guid Id` | Add nav props: `ICollection<ProductRole>`, `ICollection<Capability>`, `ICollection<OrganizationProduct>` |
| `TenantProduct` | EXISTING | COMPOSITE `(TenantId, ProductId)` | Pure join with payload (IsEnabled, EnabledAtUtc). Keep as-is. |
| `OrganizationProduct` | **NEW** | COMPOSITE `(OrganizationId, ProductId)` | PAYLOAD — IsEnabled, EnabledAtUtc, GrantedByUserId. FK: OrganizationId, ProductId. |
| `ProductRole` | **NEW** | `Guid Id` | PAYLOAD — Code, Name, Description, EligibleOrgType (nullable string), IsActive. FK → Product. |
| `Capability` | **NEW** | `Guid Id` | PAYLOAD — Code, Name, Description, IsActive. FK → Product. |
| `RoleCapability` | **NEW** | COMPOSITE `(ProductRoleId, CapabilityId)` | Pure join, no payload. Seed data only; never written at runtime. |

### Auth / User Entities

| Entity | Status | Key | Notes |
|---|---|---|---|
| `User` | EXISTING | `Guid Id` | `TenantId` FK stays temporarily. Add nav: `ICollection<UserOrganizationMembership>` |
| `Role` | EXISTING | `Guid Id` | No changes. System roles stay here. |
| `UserRole` | EXISTING | COMPOSITE `(UserId, RoleId)` | Pure join. Kept for backward compat. Legacy. |
| `UserOrganizationMembership` | **NEW** | `Guid Id` | PAYLOAD — UserId, OrganizationId, MemberRole, IsActive, JoinedAtUtc, GrantedByUserId. Unique: (UserId, OrganizationId). |
| `UserRoleAssignment` | **NEW** | `Guid Id` | PAYLOAD — UserId, RoleId, OrganizationId (nullable). Org-scoped or tenant-scoped explicit role overrides. Future phase. |

---

## 2. DbContext and Configuration Plan

### New `DbSet<T>` Properties to Add to `IdentityDbContext`

```csharp
// New in this migration wave
public DbSet<TenantDomain> TenantDomains => Set<TenantDomain>();
public DbSet<Organization> Organizations => Set<Organization>();
public DbSet<OrganizationDomain> OrganizationDomains => Set<OrganizationDomain>();
public DbSet<OrganizationProduct> OrganizationProducts => Set<OrganizationProduct>();
public DbSet<ProductRole> ProductRoles => Set<ProductRole>();
public DbSet<Capability> Capabilities => Set<Capability>();
public DbSet<RoleCapability> RoleCapabilities => Set<RoleCapability>();
public DbSet<UserOrganizationMembership> UserOrganizationMemberships => Set<UserOrganizationMembership>();
public DbSet<UserRoleAssignment> UserRoleAssignments => Set<UserRoleAssignment>();
```

### `IEntityTypeConfiguration<T>` Classes Required

All configurations live in `Identity.Infrastructure/Data/Configurations/`.

---

#### `TenantDomainConfiguration`
```csharp
builder.ToTable("TenantDomains");
builder.HasKey(e => e.Id);
builder.Property(e => e.TenantId).IsRequired();
builder.Property(e => e.Domain).HasMaxLength(253).IsRequired();
builder.Property(e => e.DomainType).HasMaxLength(20).IsRequired(); // SUBDOMAIN, CUSTOM
builder.Property(e => e.IsPrimary).IsRequired();
builder.Property(e => e.IsVerified).IsRequired();

builder.HasOne(e => e.Tenant)
    .WithMany(t => t.Domains)
    .HasForeignKey(e => e.TenantId)
    .OnDelete(DeleteBehavior.Cascade);

builder.HasIndex(e => e.Domain).IsUnique();             // platform-wide unique domain
builder.HasIndex(e => e.TenantId);
builder.HasIndex(new[] { "TenantId", "IsPrimary" });    // fast primary subdomain lookup
```

---

#### `OrganizationConfiguration`
```csharp
builder.ToTable("Organizations");
builder.HasKey(e => e.Id);
builder.Property(e => e.TenantId).IsRequired();
builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
builder.Property(e => e.DisplayName).HasMaxLength(300);
builder.Property(e => e.OrgType).HasMaxLength(50).IsRequired();
builder.Property(e => e.IsActive).IsRequired();

builder.HasOne(e => e.Tenant)
    .WithMany(t => t.Organizations)
    .HasForeignKey(e => e.TenantId)
    .OnDelete(DeleteBehavior.Restrict);

builder.HasIndex(new[] { "TenantId", "Name" }).IsUnique();
builder.HasIndex(new[] { "TenantId", "OrgType" });
builder.HasIndex(new[] { "TenantId", "IsActive" });
```

---

#### `OrganizationDomainConfiguration`
```csharp
builder.ToTable("OrganizationDomains");
builder.HasKey(e => e.Id);
builder.Property(e => e.OrganizationId).IsRequired();
builder.Property(e => e.Domain).HasMaxLength(253).IsRequired();
builder.Property(e => e.DomainType).HasMaxLength(20).IsRequired(); // EMAIL, CUSTOM
builder.Property(e => e.IsPrimary).IsRequired();
builder.Property(e => e.IsVerified).IsRequired();

builder.HasOne(e => e.Organization)
    .WithMany(o => o.Domains)
    .HasForeignKey(e => e.OrganizationId)
    .OnDelete(DeleteBehavior.Cascade);

builder.HasIndex(e => e.Domain).IsUnique();
builder.HasIndex(e => e.OrganizationId);
```

---

#### `OrganizationProductConfiguration`
```csharp
builder.ToTable("OrganizationProducts");
builder.HasKey(e => new { e.OrganizationId, e.ProductId });
builder.Property(e => e.IsEnabled).IsRequired();
builder.Property(e => e.EnabledAtUtc);
builder.Property(e => e.GrantedByUserId); // nullable

builder.HasOne(e => e.Organization)
    .WithMany(o => o.OrganizationProducts)
    .HasForeignKey(e => e.OrganizationId)
    .OnDelete(DeleteBehavior.Cascade);

builder.HasOne(e => e.Product)
    .WithMany(p => p.OrganizationProducts)
    .HasForeignKey(e => e.ProductId)
    .OnDelete(DeleteBehavior.Restrict);

builder.HasIndex(e => e.ProductId);
```

---

#### `ProductRoleConfiguration`
```csharp
builder.ToTable("ProductRoles");
builder.HasKey(e => e.Id);
builder.Property(e => e.ProductId).IsRequired();
builder.Property(e => e.Code).HasMaxLength(100).IsRequired();
builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
builder.Property(e => e.Description).HasMaxLength(1000);
builder.Property(e => e.EligibleOrgType).HasMaxLength(50);  // nullable
builder.Property(e => e.IsActive).IsRequired();

builder.HasOne(e => e.Product)
    .WithMany(p => p.ProductRoles)
    .HasForeignKey(e => e.ProductId)
    .OnDelete(DeleteBehavior.Restrict);

builder.HasIndex(e => e.Code).IsUnique();
builder.HasIndex(new[] { "ProductId", "EligibleOrgType" });
```

---

#### `CapabilityConfiguration`
```csharp
builder.ToTable("Capabilities");
builder.HasKey(e => e.Id);
builder.Property(e => e.ProductId).IsRequired();
builder.Property(e => e.Code).HasMaxLength(100).IsRequired();
builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
builder.Property(e => e.Description).HasMaxLength(1000);
builder.Property(e => e.IsActive).IsRequired();

builder.HasOne(e => e.Product)
    .WithMany(p => p.Capabilities)
    .HasForeignKey(e => e.ProductId)
    .OnDelete(DeleteBehavior.Restrict);

builder.HasIndex(e => e.Code).IsUnique();
builder.HasIndex(e => e.ProductId);
```

---

#### `RoleCapabilityConfiguration`
```csharp
builder.ToTable("RoleCapabilities");
builder.HasKey(e => new { e.ProductRoleId, e.CapabilityId });

builder.HasOne(e => e.ProductRole)
    .WithMany(r => r.RoleCapabilities)
    .HasForeignKey(e => e.ProductRoleId)
    .OnDelete(DeleteBehavior.Cascade);

builder.HasOne(e => e.Capability)
    .WithMany(c => c.RoleCapabilities)
    .HasForeignKey(e => e.CapabilityId)
    .OnDelete(DeleteBehavior.Cascade);

builder.HasIndex(e => e.CapabilityId);
```

---

#### `UserOrganizationMembershipConfiguration`
```csharp
builder.ToTable("UserOrganizationMemberships");
builder.HasKey(e => e.Id);
builder.Property(e => e.UserId).IsRequired();
builder.Property(e => e.OrganizationId).IsRequired();
builder.Property(e => e.MemberRole).HasMaxLength(50).IsRequired(); // OWNER, ADMIN, MEMBER
builder.Property(e => e.IsActive).IsRequired();
builder.Property(e => e.JoinedAtUtc).IsRequired();
builder.Property(e => e.GrantedByUserId); // nullable

builder.HasOne(e => e.User)
    .WithMany(u => u.OrganizationMemberships)
    .HasForeignKey(e => e.UserId)
    .OnDelete(DeleteBehavior.Cascade);

builder.HasOne(e => e.Organization)
    .WithMany(o => o.Memberships)
    .HasForeignKey(e => e.OrganizationId)
    .OnDelete(DeleteBehavior.Cascade);

builder.HasIndex(new[] { "UserId", "OrganizationId" }).IsUnique(); // one membership per (user, org)
builder.HasIndex(new[] { "UserId", "IsActive" });
builder.HasIndex(e => e.OrganizationId);
```

---

#### `UserRoleAssignmentConfiguration`
```csharp
builder.ToTable("UserRoleAssignments");
builder.HasKey(e => e.Id);
builder.Property(e => e.UserId).IsRequired();
builder.Property(e => e.RoleId).IsRequired();
builder.Property(e => e.OrganizationId); // nullable — NULL = tenant-scoped
builder.Property(e => e.AssignedAtUtc).IsRequired();
builder.Property(e => e.AssignedByUserId); // nullable

builder.HasOne(e => e.User)
    .WithMany(u => u.RoleAssignments)
    .HasForeignKey(e => e.UserId)
    .OnDelete(DeleteBehavior.Cascade);

builder.HasOne(e => e.Role)
    .WithMany()
    .HasForeignKey(e => e.RoleId)
    .OnDelete(DeleteBehavior.Cascade);

builder.HasOne(e => e.Organization)
    .WithMany()
    .HasForeignKey(e => e.OrganizationId)
    .OnDelete(DeleteBehavior.SetNull);

builder.HasIndex(e => e.RoleId);
builder.HasIndex(e => e.OrganizationId);
// Application-layer guard enforces uniqueness on (UserId, RoleId, OrganizationId)
// because standard SQL UNIQUE treats two NULLs as distinct.
```

---

## 3. Migration Plan

All migrations are written manually (EF tooling may hang against AWS RDS). Each migration file includes a placeholder `Designer.cs` with an empty model; the full snapshot lives in `IdentityDbContextModelSnapshot.cs`.

### Phase A — Additive Structural Migrations

| Migration Name | Purpose |
|---|---|
| `20260328200000_AddMultiOrgProductRoleModel` | ✅ **Already applied.** Creates Organizations, OrganizationDomains, OrganizationProducts, ProductRoles, Capabilities, RoleCapabilities, UserOrganizationMemberships, UserRoleAssignments. |
| `20260329000001_AddTenantDomains` | Creates the `TenantDomains` table. Adds `TenantId` index. Adds platform-wide unique index on `Domain`. |

### Phase B — Seed Data Migrations

| Migration Name | Purpose |
|---|---|
| `20260328200000_AddMultiOrgProductRoleModel` | ✅ Seeds Organizations, OrganizationDomains, OrganizationProducts (5), ProductRoles (8), Capabilities (29), RoleCapabilities (32) via `INSERT IGNORE`. |
| `20260328200001_SeedAdminOrgMembership` | ✅ Links admin user to LegalSynq INTERNAL org via email lookup subquery. |
| `20260329000002_SeedTenantDomains` | Inserts primary subdomain record for the LegalSynq tenant into `TenantDomains`. |
| `20260329000003_CorrectSynqLienRoleMappings` | Corrects the SynqLien product role seed: PROVIDER → SYNQLIEN_SELLER, LIEN_OWNER → SYNQLIEN_BUYER + SYNQLIEN_HOLDER. Updates `RoleCapabilities` entries accordingly. |

### Phase C — Cleanup / Deprecation Migrations

| Migration Name | Purpose | Timing |
|---|---|---|
| `20260330000001_DropStaleApplicationsTable` | Drops the `Applications` table from `identity_db` (artifact of accidental Fund migration). `IF EXISTS` guard makes it idempotent. | Run immediately after Phase B is stable. |
| `20260400000001_DeprecateUserRoles` | Add `DeprecatedAt` column to `UserRoles` as a soft marker. No hard delete yet. | Future phase. |
| `20260400000002_DropUserRoles` | Drop `UserRoles` and `UserRole` entity, after all consumers migrated to `UserOrganizationMemberships`. | Future phase. |

---

## 4. Concrete Table Changes

### Existing Tables — What Changes

#### `Tenants`
| | |
|---|---|
| **Stays** | All existing columns. All existing FK relationships. |
| **Changes** | None at the DB level. EF nav property `ICollection<TenantDomain>` and `ICollection<Organization>` added to entity class. |
| **Temporary** | Nothing removed. Subdomain is not stored on `Tenants` directly; it lives in new `TenantDomains`. |

#### `Users`
| | |
|---|---|
| **Stays** | All existing columns including `TenantId` FK (kept temporarily). |
| **Changes** | EF nav property `ICollection<UserOrganizationMembership>` added to entity class. |
| **Temporary** | `TenantId` on `Users` is the backward-compat anchor. It stays until all product services have been updated to resolve org context from `UserOrganizationMemberships`. |

#### `Products`
| | |
|---|---|
| **Stays** | All existing columns and seed data. |
| **Changes** | EF nav properties added: `ICollection<ProductRole>`, `ICollection<Capability>`, `ICollection<OrganizationProduct>`. |

#### `TenantProducts`
| | |
|---|---|
| **Stays** | All existing columns. Composite PK `(TenantId, ProductId)`. |
| **Changes** | None. |

#### `Roles` and `UserRoles`
| | |
|---|---|
| **Stays** | All existing columns. Legacy role assignments remain valid. |
| **Changes** | None at DB level. |
| **Temporary** | `UserRoles` is the legacy system role assignment table. It is kept until `UserRoleAssignments` fully replaces it. |

---

### New Tables — Key Specifications

#### `TenantDomains`
| Aspect | Detail |
|---|---|
| **Key columns** | `Id` (Guid PK), `TenantId` (FK→Tenants), `Domain` varchar(253), `DomainType` varchar(20), `IsPrimary` bool, `IsVerified` bool |
| **Foreign keys** | `TenantId` → `Tenants(Id)` ON DELETE CASCADE |
| **Indexes** | Unique on `Domain` (platform-wide); composite on `(TenantId, IsPrimary)` |
| **Seed dependency** | Requires `Tenants` seed to already exist |
| **Notes** | `DomainType` values: `SUBDOMAIN` (e.g., `legalsynq.legalsynq.com`), `CUSTOM` (e.g., `app.example.com`). Gateway resolves tenant from this table by Host header. |

#### `Organizations`
| Aspect | Detail |
|---|---|
| **Key columns** | `Id`, `TenantId`, `Name`, `DisplayName`, `OrgType`, `IsActive`, audit timestamps |
| **Foreign keys** | `TenantId` → `Tenants(Id)` ON DELETE RESTRICT |
| **Indexes** | Unique `(TenantId, Name)`; composite `(TenantId, OrgType)`, `(TenantId, IsActive)` |
| **Seed dependency** | Requires `Tenants` |

#### `OrganizationDomains`
| Aspect | Detail |
|---|---|
| **Key columns** | `Id`, `OrganizationId`, `Domain`, `DomainType` (EMAIL, CUSTOM), `IsPrimary`, `IsVerified` |
| **Foreign keys** | `OrganizationId` → `Organizations(Id)` ON DELETE CASCADE |
| **Indexes** | Unique `Domain`; index on `OrganizationId` |
| **Notes** | Used for org email domain matching and future custom domains. NOT used for subdomain routing (that is `TenantDomains`). |

#### `OrganizationProducts`
| Aspect | Detail |
|---|---|
| **Key columns** | COMPOSITE PK `(OrganizationId, ProductId)`, `IsEnabled`, `EnabledAtUtc`, `GrantedByUserId` |
| **Foreign keys** | `OrganizationId` → `Organizations(Id)` CASCADE; `ProductId` → `Products(Id)` RESTRICT |
| **Indexes** | Index on `ProductId` |
| **Seed dependency** | Requires `Organizations`, `Products` |

#### `ProductRoles`
| Aspect | Detail |
|---|---|
| **Key columns** | `Id`, `ProductId`, `Code` (unique), `Name`, `EligibleOrgType` (nullable), `IsActive` |
| **Foreign keys** | `ProductId` → `Products(Id)` ON DELETE RESTRICT |
| **Indexes** | Unique `Code`; composite `(ProductId, EligibleOrgType)` |
| **Seed dependency** | Requires `Products` |

#### `Capabilities`
| Aspect | Detail |
|---|---|
| **Key columns** | `Id`, `ProductId`, `Code` (unique), `Name`, `Description`, `IsActive` |
| **Foreign keys** | `ProductId` → `Products(Id)` ON DELETE RESTRICT |
| **Indexes** | Unique `Code`; index on `ProductId` |
| **Seed dependency** | Requires `Products` |

#### `RoleCapabilities`
| Aspect | Detail |
|---|---|
| **Key columns** | COMPOSITE PK `(ProductRoleId, CapabilityId)` |
| **Foreign keys** | `ProductRoleId` → `ProductRoles(Id)` CASCADE; `CapabilityId` → `Capabilities(Id)` CASCADE |
| **Indexes** | Index on `CapabilityId` |
| **Seed dependency** | Requires `ProductRoles`, `Capabilities` |

#### `UserOrganizationMemberships`
| Aspect | Detail |
|---|---|
| **Key columns** | `Id`, `UserId`, `OrganizationId`, `MemberRole`, `IsActive`, `JoinedAtUtc`, `GrantedByUserId` |
| **Foreign keys** | `UserId` → `Users(Id)` CASCADE; `OrganizationId` → `Organizations(Id)` CASCADE |
| **Indexes** | Unique `(UserId, OrganizationId)`; composite `(UserId, IsActive)`; index on `OrganizationId` |
| **Seed dependency** | Requires `Users`, `Organizations` |

#### `UserRoleAssignments`
| Aspect | Detail |
|---|---|
| **Key columns** | `Id`, `UserId`, `RoleId`, `OrganizationId` (nullable), `AssignedAtUtc`, `AssignedByUserId` |
| **Foreign keys** | `UserId` → `Users(Id)` CASCADE; `RoleId` → `Roles(Id)` CASCADE; `OrganizationId` → `Organizations(Id)` SET NULL |
| **Indexes** | Index on `RoleId`, `OrganizationId` |
| **Notes** | Application-layer guard enforces `(UserId, RoleId, OrganizationId)` uniqueness including the NULL case |

---

## 5. Seed Data Implementation Plan

### Strategy
All seed data is inserted via `migrationBuilder.Sql(@"INSERT IGNORE ...")` in manually written migration files. Do not use:
- `migrationBuilder.InsertData(...)` — fails because `Designer.cs` has a placeholder model
- `DbContext.OnModelCreating` with `HasData(...)` — causes EF snapshot conflicts and makes future migrations carry all seed in their `Down()` methods

`INSERT IGNORE` is idempotent and safe to re-run.

---

### Products (reuse existing seed)
Already seeded in `InitialIdentitySchema`. Fixed GUIDs:

| Code | Id suffix |
|---|---|
| SYNQ_FUND | `10000000-...-000000000001` |
| SYNQ_LIENS | `10000000-...-000000000002` |
| SYNQ_CARECONNECT | `10000000-...-000000000003` |
| SYNQ_PAY | `10000000-...-000000000004` |
| SYNQ_AI | `10000000-...-000000000005` |

No changes needed.

---

### ProductRoles — Corrected Full Set

**CareConnect**

| Id suffix | Code | ProductId | EligibleOrgType |
|---|---|---|---|
| `50000000-...-000000000001` | `CARECONNECT_REFERRER` | SYNQ_CARECONNECT | `LAW_FIRM` |
| `50000000-...-000000000002` | `CARECONNECT_RECEIVER` | SYNQ_CARECONNECT | `PROVIDER` |

**SynqLien (corrected)**

| Id suffix | Code | ProductId | EligibleOrgType |
|---|---|---|---|
| `50000000-...-000000000003` | `SYNQLIEN_SELLER` | SYNQ_LIENS | `PROVIDER` |
| `50000000-...-000000000004` | `SYNQLIEN_BUYER` | SYNQ_LIENS | `LIEN_OWNER` |
| `50000000-...-000000000005` | `SYNQLIEN_HOLDER` | SYNQ_LIENS | `LIEN_OWNER` |

**SynqFund**

| Id suffix | Code | ProductId | EligibleOrgType |
|---|---|---|---|
| `50000000-...-000000000006` | `SYNQFUND_REFERRER` | SYNQ_FUND | `LAW_FIRM` |
| `50000000-...-000000000007` | `SYNQFUND_FUNDER` | SYNQ_FUND | `FUNDER` |
| `50000000-...-000000000008` | `SYNQFUND_APPLICANT_PORTAL` | SYNQ_FUND | `NULL` |

---

### Capabilities — Full Set

All `INSERT IGNORE` with fixed GUIDs (`60000000-...-00000000000N`).

**CareConnect — 11 capabilities**

| Code | ProductRole(s) |
|---|---|
| `referral:create` | CARECONNECT_REFERRER |
| `referral:read:own` | CARECONNECT_REFERRER |
| `referral:cancel` | CARECONNECT_REFERRER |
| `provider:search` | CARECONNECT_REFERRER |
| `provider:map` | CARECONNECT_REFERRER |
| `referral:read:addressed` | CARECONNECT_RECEIVER |
| `referral:accept` | CARECONNECT_RECEIVER |
| `referral:decline` | CARECONNECT_RECEIVER |
| `appointment:create` | CARECONNECT_RECEIVER |
| `appointment:update` | CARECONNECT_RECEIVER |
| `appointment:read:own` | CARECONNECT_REFERRER, CARECONNECT_RECEIVER |

**SynqLien — 8 capabilities**

| Code | ProductRole(s) |
|---|---|
| `lien:create` | SYNQLIEN_SELLER |
| `lien:offer` | SYNQLIEN_SELLER |
| `lien:read:own` | SYNQLIEN_SELLER |
| `lien:browse` | SYNQLIEN_BUYER |
| `lien:purchase` | SYNQLIEN_BUYER |
| `lien:read:held` | SYNQLIEN_BUYER, SYNQLIEN_HOLDER |
| `lien:service` | SYNQLIEN_HOLDER |
| `lien:settle` | SYNQLIEN_HOLDER |

**SynqFund — 10 capabilities**

| Code | ProductRole(s) |
|---|---|
| `application:create` | SYNQFUND_REFERRER |
| `application:read:own` | SYNQFUND_REFERRER |
| `application:cancel` | SYNQFUND_REFERRER |
| `party:create` | SYNQFUND_REFERRER |
| `party:read:own` | SYNQFUND_REFERRER |
| `application:read:addressed` | SYNQFUND_FUNDER |
| `application:evaluate` | SYNQFUND_FUNDER |
| `application:approve` | SYNQFUND_FUNDER |
| `application:decline` | SYNQFUND_FUNDER |
| `application:status:view` | SYNQFUND_APPLICANT_PORTAL |

### RoleCapabilities Insert Pattern
```sql
INSERT IGNORE INTO `RoleCapabilities` (`ProductRoleId`, `CapabilityId`) VALUES
  ('50000000-...-000000000001', '60000000-...-000000000001'), -- REFERRER → referral:create
  ...
```
All 32 entries are inserted in a single migration SQL block using the fixed GUIDs above.

---

### System Roles
Already seeded in `InitialIdentitySchema` (PlatformAdmin, TenantAdmin, StandardUser). No changes needed.

---

## 6. Backfill Plan

### Backfill Organizations for Existing Tenants

For each existing tenant in `identity_db`, create one `INTERNAL` organization representing the tenant's own administrative context.

```sql
INSERT IGNORE INTO `Organizations`
    (`Id`, `TenantId`, `Name`, `DisplayName`, `OrgType`, `IsActive`, `CreatedAtUtc`, `UpdatedAtUtc`)
SELECT
    -- Deterministic GUID derived from a known offset + TenantId row number avoids subquery GUIDs
    -- For the initial known tenant, use the fixed ID:
    '40000000-0000-0000-0000-000000000001',
    t.`Id`,
    CONCAT(t.`Name`, ' (Internal)'),
    t.`Name`,
    'INTERNAL',
    1,
    '2024-01-01 00:00:00',
    '2024-01-01 00:00:00'
FROM `Tenants` t
WHERE t.`Id` = '20000000-0000-0000-0000-000000000001'; -- LegalSynq tenant
```

For tenants without a known org type, `INTERNAL` is the safe default because:
- It does not grant any partner-facing product roles automatically
- Platform admins retain system access via `ClaimTypes.Role = PlatformAdmin`
- The org type can be corrected by a follow-up admin action without a new migration

For future tenants provisioned through the onboarding flow, org type will be set explicitly at tenant creation time.

---

### Backfill UserOrganizationMemberships for Existing Users

Done in migration `20260328200001_SeedAdminOrgMembership` (already applied). Pattern for each additional user:

```sql
INSERT IGNORE INTO `UserOrganizationMemberships`
    (`Id`, `UserId`, `OrganizationId`, `MemberRole`, `IsActive`, `JoinedAtUtc`, `GrantedByUserId`)
SELECT
    UUID(),                                          -- runtime GUID per row
    u.`Id`,
    o.`Id`,
    CASE WHEN ur.`RoleId` = '30000000-...-000000000001' THEN 'OWNER' ELSE 'MEMBER' END,
    1,
    '2024-01-01 00:00:00',
    NULL
FROM `Users` u
INNER JOIN `Tenants` t ON u.`TenantId` = t.`Id`
INNER JOIN `Organizations` o ON o.`TenantId` = t.`Id` AND o.`OrgType` = 'INTERNAL'
LEFT JOIN `UserRoles` ur ON ur.`UserId` = u.`Id`
    AND ur.`RoleId` = '30000000-0000-0000-0000-000000000001'  -- PlatformAdmin
WHERE u.`IsActive` = 1;
```

**Note:** `INSERT IGNORE` prevents duplicate rows because of the unique index on `(UserId, OrganizationId)`. This query can be re-run safely.

---

### Tenants with No Obvious Org Type

All such tenants receive an `INTERNAL` organization. The reasoning:
- INTERNAL → no auto-derived product roles from partner-type role filters
- PlatformAdmin users still get system-role JWT claims from `UserRoles`
- No existing user loses access because the legacy `UserRoles` + JWT `ClaimTypes.Role` path is unchanged

Future tenant provisioning flow will collect org type at signup and create the first organization of the appropriate type automatically.

---

### Preserving Login Behavior During Transition

Login behavior during the migration window:

| Scenario | Before | After |
|---|---|---|
| Admin user logs in | JWT has `tenant_id`, `role=PlatformAdmin` | JWT also gains `org_id`, `org_type`, `product_roles` (additive) |
| User with no membership | JWT has no `org_id` claim | Same — `org_id` is omitted if no active membership |
| Existing role checks (e.g., `[Authorize(Roles = "PlatformAdmin")]`) | Pass | Still pass — `ClaimTypes.Role` claim is unchanged |
| New capability checks (e.g., `referral:create`) | N/A | Fail safely with 403 if no membership or product role — no silent pass |

No existing token is invalidated. The new claims are strictly additive.

---

## 7. Auth / JWT Integration Notes

### New Claims to Add

| Claim key | Type | Source | When absent |
|---|---|---|---|
| `org_id` | `string` (GUID) | `UserOrganizationMemberships.OrganizationId` (primary, earliest active) | Omitted from token |
| `org_type` | `string` | `Organizations.OrgType` | Omitted from token |
| `product_roles` | `string` or `string[]` | Computed from enabled org products + matching ProductRoles | Omitted from token |

**Existing claims kept unchanged:**
- `sub` — user GUID
- `email`
- `jti` — token ID
- `tenant_id`
- `tenant_code`
- `http://schemas.microsoft.com/ws/2008/06/identity/claims/role` — system roles (PlatformAdmin, etc.)

---

### Login Resolution Sequence

```
1. Authenticate
   → Load Tenant by Code (IsActive = true)
   → Load User by (TenantId, Email) (IsActive = true)
   → Verify password hash

2. Load primary org membership
   → SELECT TOP 1 FROM UserOrganizationMemberships
     WHERE UserId = @userId AND IsActive = true
     ORDER BY JoinedAtUtc ASC
   → Include Organization
   → Include Organization.OrganizationProducts (IsEnabled = true)
       .ThenInclude Product
       .ThenInclude ProductRoles (IsActive = true)

3. Compute product roles
   → For each enabled OrganizationProduct:
       → Filter ProductRoles WHERE EligibleOrgType IS NULL
                                OR EligibleOrgType = org.OrgType
   → Collect distinct role codes

4. Build JWT claims
   → Standard claims (sub, email, jti, tenant_id, tenant_code, role)
   → If membership found: add org_id, org_type, product_roles

5. Return
   → accessToken (JWT)
   → expiresAtUtc
   → UserResponse (includes organizationId, orgType, productRoles)
```

---

### What Goes in the JWT vs. What Is Resolved at Request Time

| | JWT | Request time |
|---|---|---|
| Tenant identity | `tenant_id`, `tenant_code` | — |
| User identity | `sub`, `email` | — |
| System role | `role` (ClaimTypes.Role) | — |
| Org membership | `org_id`, `org_type` | — |
| Product roles | `product_roles` (array of codes) | — |
| Capabilities | **NOT in JWT** | Resolved per-endpoint from `RoleCapabilities` JOIN `Capabilities` WHERE `ProductRoleId IN jwt.product_roles` |
| Cross-org membership | Not in JWT | Loaded from `UserOrganizationMemberships` by user ID if needed |

**Why capabilities stay out of the JWT:**
- Capability sets are large (up to 29+ codes)
- They change when LegalSynq engineers update role definitions — changes take effect immediately at request time without requiring re-login
- Product role codes in the JWT are compact (8 codes max per user today) and stable across role definition changes

---

## 8. Open Issues / Recommended Decisions

### 1. SynqLien Role Mapping Correction Needed in Current Seed
**Status: Requires migration `20260329000003_CorrectSynqLienRoleMappings`**

The currently applied seed has incorrect EligibleOrgType assignments for SynqLien roles. The corrected mapping is:
- `SYNQLIEN_SELLER` → `PROVIDER` (not LAW_FIRM)
- `SYNQLIEN_BUYER` → `LIEN_OWNER`
- `SYNQLIEN_HOLDER` → `LIEN_OWNER`

This migration must UPDATE the existing `ProductRoles` rows (not INSERT new ones) to preserve the fixed GUIDs already in `RoleCapabilities`.

```sql
UPDATE `ProductRoles` SET `EligibleOrgType` = 'PROVIDER'   WHERE `Code` = 'SYNQLIEN_SELLER';
UPDATE `ProductRoles` SET `EligibleOrgType` = 'LIEN_OWNER' WHERE `Code` = 'SYNQLIEN_BUYER';
UPDATE `ProductRoles` SET `EligibleOrgType` = 'LIEN_OWNER' WHERE `Code` = 'SYNQLIEN_HOLDER';
```

---

### 2. TenantDomains Table Not Yet Created
**Status: Requires migration `20260329000001_AddTenantDomains`**

Subdomain routing is currently tenant-code-based (login form sends `tenantCode`). The gateway does not yet resolve tenant from a `Host` header. `TenantDomains` is the foundation for that upgrade. Until the gateway is updated to use it, the table exists but is not read at runtime.

---

### 3. Risk: `Users.TenantId` Temporary Status
**Risk level: Medium**

`Users.TenantId` is currently the primary tenant binding for users. During the migration window, it must stay:
- The login query uses `(TenantId, Email)` to find the user
- Downstream services may send `tenant_id` from the JWT and query `Users.TenantId` to scope results

When the org model matures:
- The correct tenant context will come from `UserOrganizationMemberships → Organization → TenantId`
- `Users.TenantId` becomes redundant but can be kept as a denormalized fast path forever (it never causes correctness issues as long as a user belongs to orgs within the same tenant)

**Recommended decision:** Keep `Users.TenantId` permanently as a denormalized fast path. It reduces join depth on the critical login query and never diverges from the org-derived tenant ID (a user cannot belong to orgs in two different tenants).

---

### 4. Multi-Organization-Per-User Session Switching
**Status: Future phase**

When a user has memberships in more than one organization, the JWT today carries only the primary one. Switching org context requires:

1. A new endpoint: `POST /identity/api/auth/switch-org` with body `{ organizationId: "..." }`
2. The endpoint verifies the user has an active `UserOrganizationMembership` for the requested org
3. It re-issues a JWT with the new `org_id`, `org_type`, `product_roles` claims
4. The client discards the old token and stores the new one

**Risk:** Until this endpoint exists, users with multiple org memberships will silently get only one org's product roles. If such users exist before the endpoint is built, they need manual intervention or a temporary admin UI to set their primary membership.

---

### 5. INTERNAL Org Users and Product Admin Access
**Status: Decision needed**

INTERNAL org users (LegalSynq staff) currently receive only `SYNQFUND_APPLICANT_PORTAL` (the only role with `EligibleOrgType = NULL`). They cannot access CareConnect referrals or SynqFund underwriting screens.

**Options:**
- (a) Add a middleware/policy that bypasses capability checks for `ClaimTypes.Role = PlatformAdmin` — system admins see everything
- (b) Add an `INTERNAL` org type variant for each product role — lets INTERNAL users get the same capabilities as partner orgs
- (c) Add INTERNAL-specific product roles (e.g., `SYNQFUND_ADMIN`) with full capability grants

**Recommendation:** Option (a). Implement a `PlatformAdminBypass` policy in the gateway or individual services. This is the least schema-invasive approach and keeps the product role model clean for partner-facing access.

---

### 6. Applications Table Cleanup Timing
**Status: Safe to run now**

`DROP TABLE IF EXISTS Applications;` on `identity_db` is blocked on no other migration. The Fund service owns its data in `fund_db`. The stale table in `identity_db` holds no useful data. This cleanup migration should run immediately after the Phase B seeds are confirmed stable — it is included as `20260330000001_DropStaleApplicationsTable`.

---

### 7. OrganizationDomains vs. TenantDomains Routing Boundary
**Status: Confirmed by correction in this document**

| Table | Used for |
|---|---|
| `TenantDomains` | Gateway subdomain/custom domain → tenant resolution |
| `OrganizationDomains` | Org email domain matching, future org-level custom domains, auto-provisioning |

These must never be conflated. The gateway reads `TenantDomains` only. `OrganizationDomains` is an internal identity service concern.
