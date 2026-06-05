# LegalSynq — Target Identity / Core Schema Design
## Tenant-Aware, Multi-Organization, Product-Role Platform

---

## 1. Target Schema Overview

### Tenant / Organization Domain
| Table | Purpose |
|---|---|
| `Tenants` | Account boundary. Billing unit. Subdomain anchor. One-to-many with organizations. |
| `Organizations` | Business actor inside a tenant. Carries `org_type` (LAW_FIRM, PROVIDER, FUNDER, LIEN_OWNER, INTERNAL). |
| `OrganizationDomains` | Email and subdomain/custom-domain entries owned by an organization. Used for routing and future auto-provisioning. |

### Product Access Domain
| Table | Purpose |
|---|---|
| `Products` | Platform-wide capability bundles (SYNQ_FUND, SYNQ_LIENS, SYNQ_CARECONNECT, SYNQ_PAY, SYNQ_AI). |
| `TenantProducts` | Which products a tenant has purchased. Keeps billing separate from org access. |
| `OrganizationProducts` | Which products are enabled for a specific organization within a tenant. Subset of `TenantProducts`. |
| `ProductRoles` | Named participation rights within a product workflow, scoped to an org type (e.g., CARECONNECT_REFERRER for LAW_FIRM). |
| `Capabilities` | Fine-grained permission tokens within a product (e.g., `referral:create`, `lien:offer`). |
| `RoleCapabilities` | Many-to-many join. Maps which capabilities a product role grants. |

### Auth / User Domain
| Table | Purpose |
|---|---|
| `Users` | Platform user (login identity). Belongs to a tenant. Separate from Party. |
| `Roles` | System/admin roles for UI convenience (PlatformAdmin, TenantAdmin, StandardUser). Not used for capability resolution. |
| `UserRoles` | Legacy system role assignments (tenant-scoped). Kept for backward compatibility. |
| `UserOrganizationMemberships` | The primary auth link: user → organization, with a `member_role` (OWNER, ADMIN, MEMBER). |
| `UserRoleAssignments` | Optional org-scoped or product-scoped role overrides for fine-grained admin control. |

### Supporting
| Table | Purpose |
|---|---|
| `TenantProducts` | Tenant-level product subscriptions. Gate for org-level product enablement. |

---

## 2. Table-by-Table Schema Definition

---

### `Tenants`
**Purpose:** Account boundary. Every user, organization, and product subscription belongs to exactly one tenant.

| Column | Type | Notes |
|---|---|---|
| `Id` | `char(36)` | PK, GUID |
| `Name` | `varchar(200)` | Display name |
| `Code` | `varchar(100)` | Unique slug (e.g., LEGALSYNQ). Used in login. |
| `IsActive` | `tinyint(1)` | Soft disable |
| `CreatedAtUtc` | `datetime(6)` | |
| `UpdatedAtUtc` | `datetime(6)` | |

**PK:** `Id`
**Unique:** `Code`
**Notes:** Tenants are never org-type-aware. A tenant may contain organizations of any type.

---

### `Organizations`
**Purpose:** Business actor inside a tenant. Represents a law firm, provider practice, funder, or internal LegalSynq team.

| Column | Type | Notes |
|---|---|---|
| `Id` | `char(36)` | PK, GUID |
| `TenantId` | `char(36)` | FK → Tenants |
| `Name` | `varchar(200)` | Legal or operating name |
| `DisplayName` | `varchar(300)` | Optional friendly name |
| `OrgType` | `varchar(50)` | LAW_FIRM, PROVIDER, FUNDER, LIEN_OWNER, INTERNAL |
| `IsActive` | `tinyint(1)` | |
| `CreatedAtUtc` | `datetime(6)` | |
| `UpdatedAtUtc` | `datetime(6)` | |
| `CreatedByUserId` | `char(36)` | Nullable. Audit. |
| `UpdatedByUserId` | `char(36)` | Nullable. Audit. |

**PK:** `Id`
**FK:** `TenantId` → `Tenants(Id)` ON DELETE RESTRICT
**Unique:** `(TenantId, Name)`
**Indexes:** `(TenantId, OrgType)`, `(TenantId, IsActive)`
**Notes:** `OrgType` is a string enum (not a foreign key) to avoid a lookup table join on every auth check. It is validated at the application layer.

---

### `OrganizationDomains`
**Purpose:** Email domains and subdomains owned by an organization. Used for subdomain routing today; extensible to custom domains and auto-provisioning.

| Column | Type | Notes |
|---|---|---|
| `Id` | `char(36)` | PK, GUID |
| `OrganizationId` | `char(36)` | FK → Organizations |
| `Domain` | `varchar(253)` | e.g., `smith-law.legalsynq.com` or `smithlaw.com` |
| `DomainType` | `varchar(20)` | SUBDOMAIN, EMAIL, CUSTOM |
| `IsPrimary` | `tinyint(1)` | One primary per org |
| `IsVerified` | `tinyint(1)` | DNS/email verification flag |
| `CreatedAtUtc` | `datetime(6)` | |

**PK:** `Id`
**FK:** `OrganizationId` → `Organizations(Id)` ON DELETE CASCADE
**Unique:** `Domain` (platform-wide; no two orgs can claim the same domain)
**Indexes:** `OrganizationId`
**Notes:** `DomainType = EMAIL` enables future auto-provisioning (new user with a verified email domain is auto-added to that org). Custom domain support requires additional DNS verification columns (TXT record token, verified_at_utc).

---

### `Products`
**Purpose:** Platform-wide capability bundles. Owned by LegalSynq, not by any tenant.

| Column | Type | Notes |
|---|---|---|
| `Id` | `char(36)` | PK, GUID |
| `Code` | `varchar(100)` | SYNQ_FUND, SYNQ_LIENS, SYNQ_CARECONNECT, SYNQ_PAY, SYNQ_AI |
| `Name` | `varchar(200)` | |
| `Description` | `varchar(1000)` | Nullable |
| `IsActive` | `tinyint(1)` | |
| `CreatedAtUtc` | `datetime(6)` | |

**PK:** `Id`
**Unique:** `Code`
**Notes:** Products are global seed data. No tenant FKs here.

---

### `TenantProducts`
**Purpose:** Records which products a tenant has subscribed to (billing gate). Required before any org within that tenant can enable the product.

| Column | Type | Notes |
|---|---|---|
| `TenantId` | `char(36)` | FK → Tenants |
| `ProductId` | `char(36)` | FK → Products |
| `IsEnabled` | `tinyint(1)` | |
| `EnabledAtUtc` | `datetime(6)` | Nullable |

**PK:** `(TenantId, ProductId)`
**FK:** `TenantId` → `Tenants(Id)` ON DELETE CASCADE; `ProductId` → `Products(Id)` ON DELETE RESTRICT
**Notes:** A guard layer between billing and org-level access. Keeps `OrganizationProducts` clean — an org can only enable a product if `TenantProducts` has a matching enabled row.

---

### `OrganizationProducts`
**Purpose:** Which products are active for a specific organization within a tenant. This is the key gate for product-role resolution.

| Column | Type | Notes |
|---|---|---|
| `OrganizationId` | `char(36)` | FK → Organizations |
| `ProductId` | `char(36)` | FK → Products |
| `IsEnabled` | `tinyint(1)` | |
| `EnabledAtUtc` | `datetime(6)` | Nullable |
| `GrantedByUserId` | `char(36)` | Nullable. Audit. |

**PK:** `(OrganizationId, ProductId)`
**FK:** `OrganizationId` → `Organizations(Id)` ON DELETE CASCADE; `ProductId` → `Products(Id)` ON DELETE RESTRICT
**Indexes:** `ProductId`
**Notes:** Subset of `TenantProducts`. When computing product roles at login, we iterate over the org's enabled products and collect matching `ProductRoles` by `EligibleOrgType`.

---

### `ProductRoles`
**Purpose:** Named participation right within a product workflow, restricted to organizations of a specific type.

| Column | Type | Notes |
|---|---|---|
| `Id` | `char(36)` | PK, GUID |
| `ProductId` | `char(36)` | FK → Products |
| `Code` | `varchar(100)` | e.g., CARECONNECT_REFERRER |
| `Name` | `varchar(200)` | Human-readable label |
| `Description` | `varchar(1000)` | Nullable |
| `EligibleOrgType` | `varchar(50)` | Nullable. NULL = any org type eligible. |
| `IsActive` | `tinyint(1)` | |
| `CreatedAtUtc` | `datetime(6)` | |

**PK:** `Id`
**FK:** `ProductId` → `Products(Id)` ON DELETE RESTRICT
**Unique:** `Code`
**Indexes:** `(ProductId, EligibleOrgType)`
**Notes:** `EligibleOrgType` is the linchpin. At login, the system filters `ProductRoles` where `EligibleOrgType IS NULL OR EligibleOrgType = org.OrgType`. This removes the need for an explicit user-to-product-role assignment in the common case — the org type determines the role automatically.

---

### `Capabilities`
**Purpose:** Fine-grained permission tokens within a product. Resolved from product roles at request time.

| Column | Type | Notes |
|---|---|---|
| `Id` | `char(36)` | PK, GUID |
| `ProductId` | `char(36)` | FK → Products |
| `Code` | `varchar(100)` | e.g., `referral:create`, `lien:offer` |
| `Name` | `varchar(200)` | |
| `Description` | `varchar(1000)` | Nullable |
| `IsActive` | `tinyint(1)` | |
| `CreatedAtUtc` | `datetime(6)` | |

**PK:** `Id`
**FK:** `ProductId` → `Products(Id)` ON DELETE RESTRICT
**Unique:** `Code`
**Indexes:** `ProductId`
**Notes:** Capability codes use colon-separated namespacing (`resource:action` or `resource:action:scope`). They are never stored in the JWT; they are resolved server-side at the endpoint.

---

### `RoleCapabilities`
**Purpose:** Many-to-many join between `ProductRoles` and `Capabilities`. Defines the permission set granted by each product role.

| Column | Type | Notes |
|---|---|---|
| `ProductRoleId` | `char(36)` | FK → ProductRoles |
| `CapabilityId` | `char(36)` | FK → Capabilities |

**PK:** `(ProductRoleId, CapabilityId)`
**FK:** `ProductRoleId` → `ProductRoles(Id)` ON DELETE CASCADE; `CapabilityId` → `Capabilities(Id)` ON DELETE CASCADE
**Indexes:** `CapabilityId`
**Notes:** This is a seed-data table. Changes happen only when LegalSynq engineers update product role definitions, not at runtime per user.

---

### `Users`
**Purpose:** Platform login identity. One user per email per tenant. Separate from Party (real-world individuals in fund/lien workflows).

| Column | Type | Notes |
|---|---|---|
| `Id` | `char(36)` | PK, GUID |
| `TenantId` | `char(36)` | FK → Tenants |
| `Email` | `varchar(320)` | Lowercased |
| `PasswordHash` | `varchar(500)` | BCrypt |
| `FirstName` | `varchar(100)` | |
| `LastName` | `varchar(100)` | |
| `IsActive` | `tinyint(1)` | |
| `CreatedAtUtc` | `datetime(6)` | |
| `UpdatedAtUtc` | `datetime(6)` | |

**PK:** `Id`
**FK:** `TenantId` → `Tenants(Id)` ON DELETE RESTRICT
**Unique:** `(TenantId, Email)`
**Notes:** Users never have a `PartyId` foreign key. The Party relationship lives in product services (Fund, CareConnect) and is referenced by user ID.

---

### `UserOrganizationMemberships`
**Purpose:** The primary auth join. Links a user to an organization with a member-level role (OWNER, ADMIN, MEMBER). This is the record that drives org_id and product_roles claims in the JWT.

| Column | Type | Notes |
|---|---|---|
| `Id` | `char(36)` | PK, GUID |
| `UserId` | `char(36)` | FK → Users |
| `OrganizationId` | `char(36)` | FK → Organizations |
| `MemberRole` | `varchar(50)` | OWNER, ADMIN, MEMBER |
| `IsActive` | `tinyint(1)` | Soft disable for deprovisioning |
| `JoinedAtUtc` | `datetime(6)` | |
| `GrantedByUserId` | `char(36)` | Nullable. Audit. |

**PK:** `Id`
**FK:** `UserId` → `Users(Id)` ON DELETE CASCADE; `OrganizationId` → `Organizations(Id)` ON DELETE CASCADE
**Unique:** `(UserId, OrganizationId)` — a user belongs to an org at most once (one membership record per pair)
**Indexes:** `(UserId, IsActive)`, `OrganizationId`
**Notes:** `MemberRole` (OWNER/ADMIN/MEMBER) is for org administration UI only. It does not replace product-role-based capability checks. A user may have memberships in multiple organizations; the JWT carries the primary (earliest active) one today, with future support for session org-switching.

---

### `Roles`
**Purpose:** System/admin convenience roles (PlatformAdmin, TenantAdmin, StandardUser). Used for UI rendering and system access control, not for product capability resolution.

| Column | Type | Notes |
|---|---|---|
| `Id` | `char(36)` | PK, GUID |
| `TenantId` | `char(36)` | FK → Tenants |
| `Name` | `varchar(200)` | |
| `Description` | `varchar(1000)` | Nullable |
| `IsSystemRole` | `tinyint(1)` | System-owned, not user-modifiable |
| `CreatedAtUtc` | `datetime(6)` | |
| `UpdatedAtUtc` | `datetime(6)` | |

**PK:** `Id`
**FK:** `TenantId` → `Tenants(Id)` ON DELETE RESTRICT
**Unique:** `(TenantId, Name)`
**Notes:** `IsSystemRole = true` rows are seed data and must not be deleted. Custom roles for future UI-based admin tooling can be added by setting `IsSystemRole = false`.

---

### `UserRoles`
**Purpose:** Legacy many-to-many join between `Users` and `Roles` (system roles). Kept for backward compatibility. May be deprecated once `UserOrganizationMemberships` + `UserRoleAssignments` fully cover all use cases.

| Column | Type | Notes |
|---|---|---|
| `UserId` | `char(36)` | FK → Users |
| `RoleId` | `char(36)` | FK → Roles |
| `AssignedAtUtc` | `datetime(6)` | |

**PK:** `(UserId, RoleId)`
**FK:** `UserId` → `Users(Id)` ON DELETE CASCADE; `RoleId` → `Roles(Id)` ON DELETE CASCADE
**Indexes:** `RoleId`

---

### `UserRoleAssignments`
**Purpose:** Optional, explicit org-scoped or product-scoped role override. Used when a user needs a product role that is not auto-derived from their org type (e.g., a PROVIDER org user who is also a SYNQFUND_REFERRER by special agreement).

| Column | Type | Notes |
|---|---|---|
| `Id` | `char(36)` | PK, GUID |
| `UserId` | `char(36)` | FK → Users |
| `RoleId` | `char(36)` | FK → Roles |
| `OrganizationId` | `char(36)` | Nullable FK → Organizations. NULL = tenant-scoped. |
| `AssignedAtUtc` | `datetime(6)` | |
| `AssignedByUserId` | `char(36)` | Nullable. Audit. |

**PK:** `Id`
**FK:** `UserId` → `Users(Id)` ON DELETE CASCADE; `RoleId` → `Roles(Id)` ON DELETE CASCADE; `OrganizationId` → `Organizations(Id)` ON DELETE SET NULL
**Unique:** `(UserId, RoleId, OrganizationId)` — partial unique, handles NULL OrganizationId at the DB level with a nullable unique index
**Indexes:** `RoleId`, `OrganizationId`
**Notes:** This table is a future-phase feature. Today, product role resolution is fully automatic from org type. `UserRoleAssignments` enables manual overrides without changing the core automatic resolution path.

---

## 3. Relationship Model

### Tenant → Organizations
A `Tenant` is the account boundary. An unlimited number of `Organizations` can exist within one tenant, each with its own `OrgType`. A law firm and a provider practice can coexist inside the same tenant if, for example, a managed service provider provisions both on behalf of a client.

```
Tenant (1) ──< Organizations (many)
                │
                ├── OrgType = LAW_FIRM
                ├── OrgType = PROVIDER
                └── OrgType = INTERNAL
```

### Organization → OrganizationProducts
Each organization has a set of enabled products. These are a subset of the products the parent tenant has subscribed to. An org cannot enable a product the tenant has not purchased.

```
Organization (1) ──< OrganizationProducts (many) >── Products (1)
```

### OrganizationProduct → ProductRoles
Product roles are not directly assigned to organizations. Instead, they are matched dynamically: when resolving claims at login, the system queries `ProductRoles` where `ProductId` matches the org's enabled products AND `EligibleOrgType` matches the org's type (or is NULL).

```
OrganizationProducts (matched by ProductId)
    ↓
ProductRoles (filtered by EligibleOrgType = org.OrgType OR NULL)
    ↓
RoleCapabilities (resolved to capability codes)
```

### User → UserOrganizationMemberships
A user's organizational context is established through `UserOrganizationMemberships`. Each membership links a user to exactly one organization, with a `MemberRole` for admin-level control within that org. A user can have memberships in multiple organizations.

```
User (1) ──< UserOrganizationMemberships (many) >── Organization (1)
```

### UserRoleAssignments vs ProductRoles
These solve different problems:

| | `ProductRoles` | `UserRoleAssignments` |
|---|---|---|
| **Basis** | Derived from org type automatically | Explicitly assigned to a user |
| **Runtime** | Computed at login from org membership | Can be loaded at request time |
| **Purpose** | Core product workflow access | Override or supplement automatic roles |
| **Stored in JWT** | Yes (`product_roles` claim) | Future phase |
| **Requires admin action** | No | Yes |

---

## 4. Authorization Resolution Model

### Step 1: Login / Authentication
The user authenticates with `{tenantCode, email, password}`. The system:
1. Looks up the `Tenant` by code (must be active).
2. Looks up the `User` by `(TenantId, Email)` (must be active).
3. Verifies the password hash.

### Step 2: Tenant Context
Once the user is authenticated, the JWT receives:
- `tenant_id` — the tenant's GUID
- `tenant_code` — the tenant's slug

These claims scope all downstream service requests. Subdomain routing at the gateway is always tenant-based.

### Step 3: Organization Membership
The system loads the user's primary active `UserOrganizationMembership` (ordered by `JoinedAtUtc ASC`). The JWT receives:
- `org_id` — the organization GUID
- `org_type` — the organization's type (LAW_FIRM, PROVIDER, etc.)

If no membership exists, `org_id` and `org_type` are omitted. The user can still authenticate but will have no product role access.

### Step 4: Product Access
The system checks `OrganizationProducts` for the user's organization to find which products are enabled. This is computed as part of the product role resolution step.

### Step 5: Product Role Resolution
For each enabled `OrganizationProduct`, the system queries `ProductRoles` where:
- `ProductId` matches
- `EligibleOrgType IS NULL OR EligibleOrgType = org.OrgType`
- `IsActive = true`

The resulting set of product role codes is embedded in the JWT as the `product_roles` claim array.

### Step 6: Capability Resolution (Request Time)
Capabilities are **not stored in the JWT**. They are resolved server-side at each endpoint:

```
incoming request
  → extract product_roles from JWT
  → query RoleCapabilities JOIN Capabilities WHERE ProductRoleId IN (jwt.product_roles)
  → check if required capability code is in the resulting set
  → allow or deny
```

This keeps the JWT compact and allows capability changes to take effect without re-login.

### Step 7: System Role Checks (UI / Admin)
`UserRoles` (system roles) are embedded as standard `ClaimTypes.Role` claims in the JWT (e.g., `PlatformAdmin`). These are used only for system-level access gates (admin UI panels, tenant management endpoints). They do not gate product workflow capabilities.

### Cross-Tenant Workflow Participation
Cross-tenant reads are supported through explicit workflow records, not through broad cross-tenant JWT access:
- A CareConnect referral record stores both the referring org's ID and the receiving org's ID.
- A receiving provider (different tenant) gains read access to a specific referral because they are an authorized party to that workflow record, not because they hold a cross-tenant JWT.
- The receiving service verifies the user's `org_id` against the workflow record's `receiver_org_id`.

---

## 5. Seed Data Plan

### Products

| Id | Code | Name |
|---|---|---|
| `10000000-...-000000000001` | SYNQ_FUND | SynqFund |
| `10000000-...-000000000002` | SYNQ_LIENS | SynqLiens |
| `10000000-...-000000000003` | SYNQ_CARECONNECT | SynqCareConnect |
| `10000000-...-000000000004` | SYNQ_PAY | SynqPay |
| `10000000-...-000000000005` | SYNQ_AI | SynqAI |

### ProductRoles

| Code | Product | EligibleOrgType | Description |
|---|---|---|---|
| `CARECONNECT_REFERRER` | SYNQ_CARECONNECT | LAW_FIRM | Law firm that refers clients to providers |
| `CARECONNECT_RECEIVER` | SYNQ_CARECONNECT | PROVIDER | Provider that receives referrals |
| `SYNQLIEN_SELLER` | SYNQ_LIENS | LAW_FIRM | Law firm that creates and offers liens |
| `SYNQLIEN_BUYER` | SYNQ_LIENS | LIEN_OWNER | Lien owner that purchases liens |
| `SYNQLIEN_HOLDER` | SYNQ_LIENS | LIEN_OWNER | Lien owner that services and settles liens |
| `SYNQFUND_REFERRER` | SYNQ_FUND | LAW_FIRM | Law firm that submits fund applications |
| `SYNQFUND_FUNDER` | SYNQ_FUND | FUNDER | Funder that evaluates and funds applications |
| `SYNQFUND_APPLICANT_PORTAL` | SYNQ_FUND | NULL | Limited read-only portal for fund applicants |

### Capabilities

**CareConnect**

| Code | ProductRole(s) | Description |
|---|---|---|
| `referral:create` | REFERRER | Create a new referral |
| `referral:read:own` | REFERRER | View referrals you initiated |
| `referral:cancel` | REFERRER | Cancel a referral you initiated |
| `referral:read:addressed` | RECEIVER | View referrals addressed to your org |
| `referral:accept` | RECEIVER | Accept an incoming referral |
| `referral:decline` | RECEIVER | Decline an incoming referral |
| `provider:search` | REFERRER | Search for providers by criteria |
| `provider:map` | REFERRER | View providers on geographic map |
| `appointment:create` | RECEIVER | Schedule an appointment |
| `appointment:update` | RECEIVER | Modify an existing appointment |
| `appointment:read:own` | REFERRER, RECEIVER | View your org's appointments |

**SynqLien**

| Code | ProductRole(s) | Description |
|---|---|---|
| `lien:create` | SELLER | Create a new lien record |
| `lien:offer` | SELLER | Offer a lien for sale |
| `lien:read:own` | SELLER | View liens you created |
| `lien:browse` | BUYER | Browse available liens for purchase |
| `lien:purchase` | BUYER | Purchase a lien |
| `lien:read:held` | BUYER, HOLDER | View liens you hold |
| `lien:service` | HOLDER | Service an active lien |
| `lien:settle` | HOLDER | Settle and close a lien |

**SynqFund**

| Code | ProductRole(s) | Description |
|---|---|---|
| `application:create` | REFERRER | Submit a new fund application |
| `application:read:own` | REFERRER | View applications you submitted |
| `application:cancel` | REFERRER | Cancel a pending application |
| `application:read:addressed` | FUNDER | View applications addressed to your org |
| `application:evaluate` | FUNDER | Perform underwriting evaluation |
| `application:approve` | FUNDER | Approve and fund an application |
| `application:decline` | FUNDER | Decline a fund application |
| `party:create` | REFERRER | Create a party profile for a client |
| `party:read:own` | REFERRER | View party profiles you created |
| `application:status:view` | APPLICANT_PORTAL | View the status of a fund application |

---

## 6. Backward-Compatible Migration Plan

### Current Schema (Existing Tables)
- `Tenants` — keep as-is
- `Users` — keep as-is
- `Roles` — keep as-is (they become system/UI roles)
- `UserRoles` — keep as-is (legacy role assignments; still read at login for JWT `ClaimTypes.Role`)
- `Products` — keep as-is
- `TenantProducts` — keep as-is (billing gate)

### Phase 1: Add New Tables (Non-Destructive)
Add all new tables without altering any existing ones:
1. `Organizations`
2. `OrganizationDomains`
3. `OrganizationProducts`
4. `ProductRoles`
5. `Capabilities`
6. `RoleCapabilities`
7. `UserOrganizationMemberships`
8. `UserRoleAssignments`

Seed `ProductRoles`, `Capabilities`, and `RoleCapabilities` with the initial data set.

### Phase 2: Seed the Internal Organization
For each existing tenant, create one `INTERNAL` organization (representing the LegalSynq admin team). Add a `UserOrganizationMembership` for each existing platform admin user (matched by `UserRoles.RoleId = PlatformAdmin`).

This is done via a SQL migration that joins on existing data, not via hardcoded IDs.

### Phase 3: Application Layer Update
Update the login flow to:
- Load `UserOrganizationMembership` after authentication
- Compute `ProductRoles` from org type and enabled products
- Embed `org_id`, `org_type`, `product_roles` in the JWT
- Add these fields to `UserResponse`

New claims are additive — no existing claims are removed. Old tokens remain valid until expiry.

### Phase 4: Deprecation (Future)
Once all services consume `org_id` and `product_roles` claims:
- `UserRoles` can be deprecated (kept read-only for historical audit)
- `TenantProducts` billing logic may be merged with `OrganizationProducts` in a future refactor

### Stale `Applications` Table in `identity_db`
This table was created by an accidental Fund migration run against the identity database. It is safe to drop:
```sql
DROP TABLE IF EXISTS `identity_db`.`Applications`;
```
This should be added as a standalone migration in the Identity service with a guard:
```sql
DROP TABLE IF EXISTS `Applications`;
```
The guard (`IF EXISTS`) makes it safe to run even if it was already cleaned up manually.

---

## 7. EF Core Implementation Notes

### Entity Structure

```
Identity.Domain/
  Tenant.cs                        // IDs: Guid. NavProps: Organizations, Users, Roles, TenantProducts
  Organization.cs                  // IDs: Guid. NavProps: Tenant, Domains, OrganizationProducts, Memberships
  OrganizationDomain.cs
  OrganizationProduct.cs           // Composite PK: (OrganizationId, ProductId). NavProps: Organization, Product
  Product.cs                       // NavProps: TenantProducts, OrganizationProducts, ProductRoles, Capabilities
  ProductRole.cs                   // EligibleOrgType: string? (nullable string enum, not FK)
  Capability.cs
  RoleCapability.cs                // Composite PK: (ProductRoleId, CapabilityId)
  User.cs                          // NavProps: Tenant, UserRoles, OrganizationMemberships, RoleAssignments
  UserOrganizationMembership.cs
  UserRoleAssignment.cs
  Role.cs                          // IsSystemRole distinguishes seed roles from custom
  UserRole.cs                      // Legacy. Composite PK: (UserId, RoleId)
```

### DbContext Organization
Register all sets in `IdentityDbContext`. Apply configurations via `modelBuilder.ApplyConfigurationsFromAssembly(...)`. Keep one `IEntityTypeConfiguration<T>` file per entity.

### Many-to-Many Tables
EF Core 8 supports skip navigations for clean many-to-many, but given the explicit join tables with payload columns, all joins are configured as explicit entity types with composite PKs:
- `TenantProducts` (TenantId, ProductId) + `IsEnabled`, `EnabledAtUtc`
- `OrganizationProducts` (OrganizationId, ProductId) + `IsEnabled`, `EnabledAtUtc`, `GrantedByUserId`
- `RoleCapabilities` (ProductRoleId, CapabilityId) — pure join, no payload; could use EF skip navigation
- `UserRole` (UserId, RoleId) — legacy pure join

### Enum vs Lookup Table Decision
- `OrgType` → string constant class (`OrgType.LawFirm = "LAW_FIRM"`) — avoids a lookup table join on every auth check
- `MemberRole` → string constant class (`MemberRole.Owner = "OWNER"`) — same reasoning
- `DomainType` → string constant class (`DomainType.Subdomain = "SUBDOMAIN"`)
- `ProductRole.Code` → strongly-typed string constants in application layer (`ProductRoleCodes.CareConnectReferrer`)
- `Capability.Code` → same approach

Rationale: lookup tables add FK joins on high-read paths (auth, every request). String constants validated at the application layer are cheaper and sufficient for a controlled seed-data set.

### AuditableEntity Pattern
`CreatedAtUtc` and `UpdatedAtUtc` on mutable entities use `protected set` with `DbContext.SaveChanges` override or `ChangeTracker` interception. For migration seed data, set timestamps via `entry.Property(nameof(...)).CurrentValue = now` to bypass the protected setter.

---

## 8. Risks / Open Decisions

### 1. Multi-Org-Per-User Token Strategy
**Decision needed:** When a user belongs to multiple organizations, which org context does the JWT carry?
- **Current approach:** The earliest active membership (by `JoinedAtUtc`) is used — the "primary" org.
- **Risk:** A user who is both a law firm employee and a funder analyst will only get one org's product roles per token.
- **Options:** (a) org-switch endpoint that re-issues a JWT for a different org; (b) embed all org memberships in the JWT (payload bloat); (c) always include all product roles from all orgs and let the service layer filter by `org_id` on specific records.
- **Recommendation:** Implement org-switch endpoint in Phase 2. Today, most users belong to exactly one org.

### 2. Internal Admin Access to All Products
**Decision needed:** Should INTERNAL org users get all product role capabilities, or only the ones explicitly mapped?
- **Current behavior:** INTERNAL org users get only capabilities from roles where `EligibleOrgType IS NULL`.
- **Risk:** Platform admins can't view CareConnect referrals or SynqFund applications without a special role.
- **Options:** (a) add a super-capability checked before the standard capability; (b) add INTERNAL-specific product roles; (c) bypass capability checks for PlatformAdmin system role.
- **Recommendation:** Gate platform admin access via `ClaimTypes.Role = PlatformAdmin` at admin-specific endpoints. Do not add INTERNAL as an eligible org type to partner roles.

### 3. Custom Domain Routing
**Decision needed:** `OrganizationDomains` supports `DomainType = CUSTOM`, but the gateway currently routes only by tenant code (subdomain).
- **Risk:** Future custom domain support (`app.smithlaw.com`) requires the gateway to resolve tenant+org from the Host header, not from the JWT.
- **Action needed:** Add a `TenantId` lookup column to `OrganizationDomains`, and build a gateway middleware that resolves tenant from the domain before JWT validation.

### 4. Unique Constraint on `(UserId, RoleId, OrganizationId)` in `UserRoleAssignments`
**Issue:** Standard SQL unique constraints treat two NULLs as distinct, so `(userId, roleId, NULL)` and `(userId, roleId, NULL)` would both be allowed by the DB.
- **Recommendation:** Add an application-layer guard in the repository before insert: check for an existing row with `OrganizationId IS NULL` before inserting a tenant-scoped assignment.

### 5. `TenantProducts` as a Billing Gate vs. Org-Level Enablement
**Decision needed:** Is `TenantProducts.IsEnabled` enforced as a prerequisite for `OrganizationProducts.IsEnabled`, or are they independent?
- **Current design:** Both must be true for a product role to be computed.
- **Risk:** If a tenant's product subscription is suspended but `OrganizationProducts.IsEnabled` is still true, product roles are still blocked (correct). But if we skip the `TenantProducts` check, org-level enablement becomes the sole gate.
- **Recommendation:** Enforce the `TenantProducts` gate only at org provisioning time (when enabling a product for an org, validate the tenant subscription). Do not re-check on every login for performance.

### 6. Party vs. User Separation in Cross-Service Records
**Decision needed:** How are Fund/CareConnect records linked to individuals who are not platform users?
- **Current state:** `Party` entities exist in the Fund service. They are referenced by `user_id` (submitter) but the injured party has no `User` account.
- **Risk:** Future portal access (injured party views their fund application status) requires a lightweight Party auth flow, separate from the main multi-org JWT flow.
- **Recommendation:** Parties with portal access should have a separate `PartySession` token (short-lived, single-purpose, product-scoped) issued by the Identity service on demand. Do not add injured parties as full `Users`.

### 7. `Applications` Table Cleanup in `identity_db`
This is a known artifact from an accidental Fund service migration. It must be dropped via an explicit Identity service migration. No data in this table is authoritative — all application data lives in `fund_db`. The drop should be gated with `IF EXISTS` for safety.
