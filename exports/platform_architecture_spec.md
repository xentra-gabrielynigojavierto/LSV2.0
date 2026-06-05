# Technical Architecture Specification
## Transition to a Tenant-Aware, Multi-Organization Product Role Platform
**LegalSynq Platform — Architecture Change Proposal**
**Date:** 2026-03-28 | **Status:** Draft for Review

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Domain Model](#2-domain-model)
3. [Authorization Model](#3-authorization-model)
4. [Product Role Matrix](#4-product-role-matrix)
5. [Migration Strategy](#5-migration-strategy)
6. [Risks and Tradeoffs](#6-risks-and-tradeoffs)

---

## 1. Executive Summary

### The Problem

The current platform treats a Tenant as a single, monolithic business actor. Every user within a tenant shares the same organizational context. This breaks down as soon as a single tenant needs to operate across multiple business roles simultaneously — for example, a law firm that refers clients and also acts as a lien owner — or when a workflow requires coordinated participation from actors in different tenants (cross-tenant referrals, cross-tenant funding).

The current data model has no concept of the business entity inside a tenant, no way to associate product access with organizational context, and no way to express that a particular organization plays a specific role in a product's workflow.

### The Change

This specification defines a platform transition that introduces **Organization** as the central business actor, sitting between Tenant and User. Organizations have types (LAW_FIRM, PROVIDER, FUNDER, LIEN_OWNER, INTERNAL). Product access is granted at the Tenant level, workflow participation rights are granted at the Organization level via ProductRoles, and users inherit capabilities through their organizational membership.

The transition does not remove or replace Tenant. Tenant remains the account boundary, the billing unit, and the subdomain-routing anchor. Organization is additive — it contextualizes actors within that boundary and enables precise, role-based workflow participation across products.

### What This Enables

| Capability | Before | After |
|---|---|---|
| Law firm acts as referrer in CareConnect | Implicit, unmodeled | Explicit via LAW_FIRM → REFERRER role |
| Provider receives referrals across tenants | Not possible | Cross-tenant role participation |
| Funder evaluates a fund application | Unmodeled | FUNDER org → FUNDER role in SynqFund |
| Party (injured person) has their own interface | Not possible | Party entity separate from User |
| Multiple product contexts per tenant | Not possible | Organization × Product → ProductRole |
| Subdomain routing | Works (Tenant.Code) | Unchanged |

### Scope

This specification covers: identity_db, the gateway routing layer, the CareConnect and SynqFund workflow models, and the authorization policy framework. It does not cover UI implementation, external identity providers, or payment processing.

---

## 2. Domain Model

### 2.1 Concept Definitions

#### Tenant
The account boundary. Maps one-to-one with a subdomain (`{tenant.code}.legalsynq.com`). Controls billing, data isolation, and top-level configuration. A Tenant does not represent a single business entity — it represents an account that may house multiple organizations.

**Why Tenant and Organization must remain separate:**
A law firm (Tenant: `lawfirm_a`) may have multiple internal departments each acting as distinct organizations (litigation team, settlement team). Separately, a large healthcare network (Tenant: `healthnet`) may contain dozens of provider organizations. Collapsing Organization into Tenant forces a 1:1 constraint that does not reflect reality, prevents a tenant from having heterogeneous business actors, and makes cross-product role assignment impossible without duplicating tenants.

#### Organization
The business actor inside a tenant. Has a type that constrains which product roles it can play. Users belong to one or more organizations within their tenant. An organization is always owned by exactly one tenant — it cannot span tenants.

**Organization Types:**
- `LAW_FIRM` — legal representation, referral initiation
- `PROVIDER` — healthcare or service delivery, referral receipt
- `FUNDER` — capital deployment, fund application evaluation
- `LIEN_OWNER` — lien purchase and management
- `INTERNAL` — platform-operator organizations (LegalSynq staff)

#### Product
A named platform capability bundle. Products have defined workflow roles. Current products: `CARECONNECT`, `SYNQLIEN`, `SYNQFUND`. Products are platform-wide — they are not owned by tenants. Access to a product is granted to tenants via TenantProducts (already modeled).

#### ProductRole
A named participation role within a specific product's workflow. A ProductRole is only meaningful in the context of a (Product, OrganizationType) pair. Examples: `CARECONNECT.REFERRER`, `CARECONNECT.RECEIVER`, `SYNQFUND.FUNDER`, `SYNQFUND.APPLICANT_PORTAL`. ProductRoles determine which workflow operations an organization can perform.

#### Capability
A fine-grained permission within a ProductRole. Example: `CARECONNECT.REFERRER` has capabilities `referral:create`, `referral:cancel`, `provider:search`. Capabilities are evaluated at the API and service layer.

#### Party
A real-world individual who participates in a workflow but is not a platform user in the traditional sense. Parties are not authenticated users — they are data subjects. Examples: injured party in SynqFund, referred client in CareConnect. A Party may have a limited-access portal (authenticated separately) but is not an Organization member.

**Why Party is separate from User:**
Users are platform operators — they log in, manage workflows, make decisions. Parties are the subject of those workflows. An injured party filling out a fund application form should not have access to the fund management dashboard. Conflating the two forces inappropriate access control compromises and prevents the platform from supporting a distinct "client portal" experience.

#### Cross-Tenant Workflow Participation
Some workflows span organizational boundaries that cross tenant lines. For example: a law firm (Tenant A) creates a CareConnect referral targeting a provider (Tenant B). The referral is a shared workflow artifact. Cross-tenant participation is modeled by:

1. The referring organization (Tenant A, LAW_FIRM) creates the referral in their tenant context.
2. The receiving organization (Tenant B, PROVIDER) is referenced by `OrganizationId` in the referral record.
3. The receiving organization's users can see the referral through a cross-tenant read scope — they see only referrals addressed to their organization, not the referring tenant's full data.
4. Neither tenant's data isolation is broken — the referral record lives in a shared workflow partition or in the initiating tenant's context with a cross-tenant read grant.

Cross-tenant participation never grants write access into another tenant's data. It grants read access to specific workflow artifacts that name the receiving organization.

---

### 2.2 Target Domain Model

#### Entity Relationship Summary

```
Tenant (1) ──────────────── (N) Organization
   │                                │
   │                                ├── OrganizationType: LAW_FIRM | PROVIDER |
   │                                │   FUNDER | LIEN_OWNER | INTERNAL
   │                                │
   │                                └── (N) OrganizationProductRole
   │                                          │
   │                                          └── ProductRole (Product × Role)
   │
   ├── (N) TenantProduct ─────────── Product
   │
   └── (N) User
              │
              └── (N) OrganizationMembership ── Organization
                           │
                           └── MemberRole: ADMIN | MEMBER | READ_ONLY


Party ── (1) PartyProfile
           └── linked to workflow artifacts (Referral, Application)
               via PartyId, not via OrganizationId
```

#### Target Record Shapes

**Tenant**
```json
{
  "id": "uuid",
  "name": "Lawson & Partners LLP",
  "code": "LAWSON",
  "subdomain": "lawson.legalsynq.com",
  "isActive": true,
  "createdAtUtc": "2026-01-01T00:00:00Z"
}
```

**Organization**
```json
{
  "id": "uuid",
  "tenantId": "uuid",
  "name": "Lawson Litigation Group",
  "organizationType": "LAW_FIRM",
  "isActive": true,
  "createdAtUtc": "2026-01-01T00:00:00Z"
}
```

**OrganizationProductRole**
```json
{
  "id": "uuid",
  "organizationId": "uuid",
  "productCode": "CARECONNECT",
  "productRole": "REFERRER",
  "grantedAtUtc": "2026-01-01T00:00:00Z",
  "grantedByUserId": "uuid"
}
```

**OrganizationMembership**
```json
{
  "id": "uuid",
  "userId": "uuid",
  "organizationId": "uuid",
  "memberRole": "ADMIN",
  "joinedAtUtc": "2026-01-01T00:00:00Z"
}
```

**Party**
```json
{
  "id": "uuid",
  "firstName": "Maria",
  "lastName": "Gonzalez",
  "dateOfBirth": "1985-04-12",
  "email": "maria.g@email.com",
  "phone": "614-555-0101",
  "createdAtUtc": "2026-01-01T00:00:00Z",
  "createdByTenantId": "uuid"
}
```

---

### 2.3 Workflow Artifact Shapes

#### CareConnect Referral (target shape)

```json
{
  "id": "uuid",
  "tenantId": "uuid",
  "referringOrganizationId": "uuid",
  "referringOrganizationTenantId": "uuid",
  "receivingOrganizationId": "uuid",
  "receivingOrganizationTenantId": "uuid",
  "providerId": "uuid",
  "partyId": "uuid",
  "status": "PENDING",
  "notes": "Client requires chiropractic evaluation.",
  "referredAtUtc": "2026-03-28T10:00:00Z",
  "createdByUserId": "uuid",
  "createdAtUtc": "2026-03-28T10:00:00Z",
  "updatedAtUtc": "2026-03-28T10:00:00Z"
}
```

Key changes from current model:
- `referringOrganizationId` replaces implicit tenant-as-referrer assumption
- `receivingOrganizationId` is explicit and may reference a different tenant
- `partyId` replaces free-text client fields, linking to the Party entity
- Both `tenantId` fields are preserved for data isolation queries

#### SynqFund Application (target shape)

```json
{
  "id": "uuid",
  "applicationNumber": "FUND-2026-A1B2C3D4",
  "tenantId": "uuid",
  "applicantPartyId": "uuid",
  "referringOrganizationId": "uuid",
  "referringOrganizationTenantId": "uuid",
  "fundingOrganizationId": "uuid",
  "fundingOrganizationTenantId": "uuid",
  "requestedAmountCents": 1500000,
  "status": "UNDER_REVIEW",
  "caseDescription": "Personal injury — motor vehicle accident.",
  "submittedAtUtc": "2026-03-28T08:00:00Z",
  "createdByUserId": "uuid",
  "createdAtUtc": "2026-03-28T08:00:00Z",
  "updatedAtUtc": "2026-03-28T08:00:00Z"
}
```

Key changes from current model:
- `applicantPartyId` references the Party entity (injured person), not a User
- `referringOrganizationId` is the law firm (LAW_FIRM org type) making the referral on behalf of the party
- `fundingOrganizationId` is the funder (FUNDER org type) who receives and evaluates
- The funder may be in a completely different tenant from the law firm and party

---

## 3. Authorization Model

### 3.1 Current Model

```
JWT Claims: tenant_id, user_id, role
Policies:   AuthenticatedUser, PlatformOrTenantAdmin
Scope:      All data filtered by tenant_id
```

The current model is flat. Every user within a tenant has the same product access. There is no concept of organizational context, product-specific roles, or cross-tenant access grants.

### 3.2 Target Model

Authorization is evaluated in four layers, applied in order:

```
Layer 1: Tenant Authentication     — Is this JWT issued by a known tenant?
Layer 2: Product Access            — Does this tenant have the requested product enabled?
Layer 3: Organization × ProductRole — Does the user's organization have the requested role in this product?
Layer 4: Capability Check          — Does the ProductRole include the required capability for this operation?
```

#### JWT Claim Set (target)

```json
{
  "sub": "user-uuid",
  "tenant_id": "tenant-uuid",
  "tenant_code": "LAWSON",
  "org_id": "org-uuid",
  "org_type": "LAW_FIRM",
  "product_roles": ["CARECONNECT.REFERRER", "SYNQFUND.REFERRER"],
  "platform_role": "TenantAdmin",
  "iss": "legalsynq-identity",
  "aud": "legalsynq-platform",
  "exp": 1743200000
}
```

`product_roles` is a computed claim derived from the user's active OrganizationMemberships and their organizations' OrganizationProductRoles. It is computed at login time and embedded in the token. It does not change within a token's lifetime — role changes take effect on next login.

For cross-tenant operations, the JWT always carries the user's home tenant. The receiving service validates the cross-tenant grant by checking that the workflow artifact's `receivingOrganizationId` matches the user's `org_id`, regardless of the tenant boundary.

#### Policy Definitions (target)

| Policy Name | Requirement |
|---|---|
| `AuthenticatedUser` | Valid JWT, active tenant |
| `TenantAdmin` | `platform_role` = TenantAdmin or PlatformAdmin |
| `PlatformAdmin` | `platform_role` = PlatformAdmin |
| `CareConnectReferrer` | `product_roles` contains `CARECONNECT.REFERRER` |
| `CareConnectReceiver` | `product_roles` contains `CARECONNECT.RECEIVER` |
| `SynqFundReferrer` | `product_roles` contains `SYNQFUND.REFERRER` |
| `SynqFundFunder` | `product_roles` contains `SYNQFUND.FUNDER` |
| `CrossTenantReferralRead` | Valid JWT + org_id matches referral.receivingOrganizationId |

#### Subdomain-Based Tenant Routing (unchanged)

The Gateway reads the `Host` header, extracts the subdomain, resolves it to a `Tenant.Code`, and injects `X-Tenant-Code` into downstream requests. Downstream services validate the JWT's `tenant_id` against the resolved tenant. This mechanism is not changed by this specification.

```
Request: GET lawson.legalsynq.com/careconnect/api/referrals
Gateway: Extract "lawson" → resolve Tenant.Code=LAWSON → inject X-Tenant-Code: LAWSON
Downstream: Validate JWT.tenant_id == Tenants WHERE Code = 'LAWSON'
```

For cross-tenant reads (e.g., a provider reading a referral sent from lawfirm_a): the provider logs in at their own subdomain (`healthnet.legalsynq.com`), gets a JWT for their tenant, and the CareConnect service resolves cross-tenant access by matching `JWT.org_id == Referral.receivingOrganizationId` rather than `JWT.tenant_id == Referral.tenantId`.

---

## 4. Product Role Matrix

### 4.1 Organization Type → Eligible ProductRoles

| Organization Type | CARECONNECT | SYNQLIEN | SYNQFUND |
|---|---|---|---|
| `LAW_FIRM` | REFERRER | SELLER | REFERRER |
| `PROVIDER` | RECEIVER | — | — |
| `FUNDER` | — | — | FUNDER |
| `LIEN_OWNER` | — | BUYER, HOLDER | — |
| `INTERNAL` | ADMIN | ADMIN | ADMIN |

An organization may not be granted a ProductRole that its type does not support. This constraint is enforced at the service layer when a TenantAdmin assigns roles, not in the JWT itself.

### 4.2 ProductRole → Capabilities

#### CARECONNECT

| ProductRole | Capabilities |
|---|---|
| `REFERRER` | `referral:create`, `referral:cancel`, `referral:read:own`, `provider:search`, `provider:map`, `appointment:read:own` |
| `RECEIVER` | `referral:read:addressed`, `referral:accept`, `referral:decline`, `appointment:create`, `appointment:update`, `appointment:read:own` |
| `ADMIN` | All REFERRER + RECEIVER capabilities, plus `category:manage`, `facility:manage`, `provider:manage` |

#### SYNQFUND

| ProductRole | Capabilities |
|---|---|
| `REFERRER` | `application:create`, `application:read:own`, `application:cancel`, `party:create`, `party:read:own` |
| `FUNDER` | `application:read:addressed`, `application:evaluate`, `application:approve`, `application:decline` |
| `ADMIN` | All REFERRER + FUNDER capabilities, plus `application:manage:all` |

#### SYNQLIEN

| ProductRole | Capabilities |
|---|---|
| `SELLER` | `lien:create`, `lien:offer`, `lien:read:own` |
| `BUYER` | `lien:browse`, `lien:purchase`, `lien:read:addressed` |
| `HOLDER` | `lien:read:held`, `lien:service`, `lien:settle` |
| `ADMIN` | All lien capabilities plus `lien:manage:all` |

### 4.3 Workflow Participation by Product

#### CareConnect: Referral Workflow

```
LAW_FIRM (REFERRER)                    PROVIDER (RECEIVER)
      │                                       │
      ├─ Creates Referral ──────────────────► │
      │                                       ├─ Accepts / Declines
      │                                       ├─ Schedules Appointment
      ├─ Receives notifications ◄─────────── │
      │                                       │
      └─ (optional) Cancels Referral          └─ Completes / No-shows
```

Party (injured person / client) is referenced as a data subject on the Referral via `partyId`. The Party does not participate in workflow actions — the law firm acts on their behalf.

#### SynqFund: Application Workflow

```
INJURED PARTY (Party entity)   LAW_FIRM (REFERRER)           FUNDER (FUNDER)
       │                              │                             │
       │ ◄── Party profile created ── │                             │
       │                              ├─ Submits Application ──►   │
       │                              │                             ├─ Evaluates
       │                              │ ◄── Status updates ─────── │
       │                              │                             ├─ Approves/Declines
       │                              │ ◄── Funding decision ────── │
       │ ◄── Party portal notified ── │                             │
```

The injured party has a separate, limited-scope portal (authenticated via Party credentials, not User credentials) where they can view application status. This is a distinct authentication path from the main platform JWT flow.

---

## 5. Migration Strategy

### 5.1 Guiding Principles

1. **Non-destructive additions first.** No existing tables are dropped or renamed in Phase 1. New tables are additive.
2. **Backfill before enforcement.** New columns added to existing records as nullable, backfilled, then made non-nullable after backfill.
3. **Feature-flagged authorization.** New policy checks are deployed behind a feature flag. Old checks remain active until flag is enabled per tenant.
4. **Tenant by tenant rollout.** Migration is applied per-tenant, not all at once. Each tenant's data is migrated and validated before enabling new authorization for that tenant.

---

### 5.2 Phase 1 — Foundation (identity_db)

**Goal:** Introduce Organization, OrganizationMembership, OrganizationProductRole, and Party tables without touching any existing tables.

**New migrations:**

```sql
-- identity_db

CREATE TABLE Organizations (
  Id            char(36)     NOT NULL,
  TenantId      char(36)     NOT NULL,
  Name          varchar(200) NOT NULL,
  OrgType       varchar(50)  NOT NULL,   -- LAW_FIRM | PROVIDER | FUNDER | LIEN_OWNER | INTERNAL
  IsActive      tinyint(1)   NOT NULL DEFAULT 1,
  CreatedAtUtc  datetime(6)  NOT NULL,
  UpdatedAtUtc  datetime(6)  NOT NULL,
  PRIMARY KEY (Id),
  CONSTRAINT FK_Organizations_Tenants FOREIGN KEY (TenantId) REFERENCES Tenants(Id) ON DELETE RESTRICT,
  KEY IX_Organizations_TenantId_OrgType (TenantId, OrgType)
);

CREATE TABLE OrganizationMemberships (
  Id             char(36)    NOT NULL,
  UserId         char(36)    NOT NULL,
  OrganizationId char(36)    NOT NULL,
  MemberRole     varchar(50) NOT NULL,   -- ADMIN | MEMBER | READ_ONLY
  JoinedAtUtc    datetime(6) NOT NULL,
  PRIMARY KEY (Id),
  UNIQUE KEY UX_OrgMembership_User_Org (UserId, OrganizationId),
  CONSTRAINT FK_OrgMemberships_Users FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
  CONSTRAINT FK_OrgMemberships_Orgs FOREIGN KEY (OrganizationId) REFERENCES Organizations(Id) ON DELETE CASCADE
);

CREATE TABLE OrganizationProductRoles (
  Id             char(36)    NOT NULL,
  OrganizationId char(36)    NOT NULL,
  ProductCode    varchar(50) NOT NULL,   -- CARECONNECT | SYNQLIEN | SYNQFUND
  ProductRole    varchar(50) NOT NULL,   -- REFERRER | RECEIVER | FUNDER | etc.
  GrantedAtUtc   datetime(6) NOT NULL,
  GrantedByUserId char(36)   NULL,
  PRIMARY KEY (Id),
  UNIQUE KEY UX_OrgProductRole (OrganizationId, ProductCode, ProductRole),
  CONSTRAINT FK_OrgProductRoles_Orgs FOREIGN KEY (OrganizationId) REFERENCES Organizations(Id) ON DELETE CASCADE
);

CREATE TABLE Parties (
  Id               char(36)     NOT NULL,
  FirstName        varchar(100) NOT NULL,
  LastName         varchar(100) NOT NULL,
  DateOfBirth      date         NULL,
  Email            varchar(320) NULL,
  Phone            varchar(50)  NULL,
  CreatedByTenantId char(36)    NOT NULL,
  CreatedAtUtc     datetime(6)  NOT NULL,
  UpdatedAtUtc     datetime(6)  NOT NULL,
  PRIMARY KEY (Id),
  KEY IX_Parties_CreatedByTenantId (CreatedByTenantId)
);
```

**Outcome:** New tables exist, no existing behavior changes, no JWT changes yet.

---

### 5.3 Phase 2 — Backfill Existing Tenants

**Goal:** For every existing tenant, create a default Organization record matching the tenant's apparent type, and create OrganizationMembership records linking existing users to it.

**Backfill logic (per tenant):**

1. Detect the tenant's type from context (e.g., all existing tenants are `LAW_FIRM` or `INTERNAL` based on the LEGALSYNQ seed data).
2. Create one `Organizations` record per tenant with the detected type.
3. Create one `OrganizationMemberships` record per existing user, assigning `MemberRole = ADMIN` for TenantAdmins and `MEMBER` for StandardUsers.
4. Create `OrganizationProductRoles` for each enabled `TenantProduct`, assigning the default role for the org type.

**This backfill can be run as a one-time migration script** (not an EF migration) against identity_db. It is idempotent — safe to run multiple times.

---

### 5.4 Phase 3 — JWT Claim Expansion

**Goal:** Add `org_id`, `org_type`, and `product_roles` to the JWT without removing existing claims.

**AuthService changes:**
- At login, after resolving user + tenant, resolve the user's primary OrganizationMembership.
- Compute `product_roles` from that organization's OrganizationProductRoles.
- Embed all new claims in the JWT alongside existing `tenant_id` and `role` claims.

**Backward compatibility:** Old claims (`tenant_id`, `role`) remain present. Services that have not yet been updated to use new claims continue to work. New claims are additive.

---

### 5.5 Phase 4 — Workflow Table Updates (careconnect_db, fund_db)

**Goal:** Add organization and party references to workflow artifacts.

**careconnect_db — Referrals table additions:**
```sql
ALTER TABLE Referrals
  ADD COLUMN ReferringOrganizationId      char(36) NULL,
  ADD COLUMN ReferringOrganizationTenantId char(36) NULL,
  ADD COLUMN ReceivingOrganizationId      char(36) NULL,
  ADD COLUMN ReceivingOrganizationTenantId char(36) NULL,
  ADD COLUMN PartyId                      char(36) NULL;
```

Backfill: Set `ReferringOrganizationId` = the default organization for the referral's `TenantId`. `PartyId` left null until a migration tool matches existing free-text client data to Party records.

**fund_db — Applications table additions:**
```sql
ALTER TABLE Applications
  ADD COLUMN ApplicantPartyId             char(36) NULL,
  ADD COLUMN ReferringOrganizationId      char(36) NULL,
  ADD COLUMN ReferringOrganizationTenantId char(36) NULL,
  ADD COLUMN FundingOrganizationId        char(36) NULL,
  ADD COLUMN FundingOrganizationTenantId  char(36) NULL;
```

All new columns are nullable during backfill. Enforcement (NOT NULL) is deferred to Phase 5.

---

### 5.6 Phase 5 — Authorization Policy Enforcement

**Goal:** Enable new product-role-based policies per tenant, retire flat role checks.

**Sequence per tenant:**
1. Verify that all users have OrganizationMemberships and that all organizations have OrganizationProductRoles.
2. Enable feature flag `org_roles_enabled` for the tenant.
3. Monitor errors for 24 hours.
4. If stable, enforce new policies permanently by removing the feature flag bypass.

**Rollback:** If a tenant experiences authorization failures, disable the feature flag for that tenant. Users fall back to flat-role checks until the issue is resolved.

---

### 5.7 Phase 6 — Party Portal (net-new)

**Goal:** Introduce a separate authentication path for Party entities.

- `POST /identity/api/party-auth/login` — accepts party credentials (email + verification code), returns a scoped Party JWT.
- Party JWT contains `party_id`, no `tenant_id`, no `org_id`, no `product_roles`.
- Party-scoped endpoints in CareConnect and SynqFund validate the Party JWT and return only records where `PartyId` matches the JWT's `party_id`.

This phase is fully additive and does not affect any existing user authentication flows.

---

### 5.8 Migration Timeline Estimate

| Phase | Scope | Estimated Effort |
|---|---|---|
| Phase 1 — Foundation | identity_db new tables | 1–2 days |
| Phase 2 — Backfill | Data migration script | 1 day |
| Phase 3 — JWT Claims | AuthService update | 2–3 days |
| Phase 4 — Workflow Tables | careconnect_db, fund_db | 2–3 days |
| Phase 5 — Policy Enforcement | Per-product, per-tenant rollout | 3–5 days |
| Phase 6 — Party Portal | Net-new auth path + UI | 5–7 days |
| **Total** | | **2–4 weeks** |

---

## 6. Risks and Tradeoffs

### 6.1 Risks

#### R1 — Token Size Growth
**Risk:** Embedding `product_roles` in the JWT increases token size. If a user belongs to many organizations with many roles across many products, the token could exceed browser cookie limits (~4KB) or cause latency issues.

**Mitigation:** Cap `product_roles` at the active session's organization context. If a user switches organization context (e.g., a user who belongs to both a LAW_FIRM and a LIEN_OWNER org), require re-authentication with the desired org selected. Alternatively, move `product_roles` to a token introspection endpoint and cache at the gateway.

---

#### R2 — Cross-Tenant Data Isolation Regression
**Risk:** Cross-tenant referral reads could accidentally expose data if the cross-tenant read scope logic is implemented incorrectly. A provider could see referrals they are not the addressee of.

**Mitigation:** Cross-tenant reads must always filter by `ReceivingOrganizationId = JWT.org_id`, never by `TenantId`. This must be enforced in repository-layer queries, not just API endpoints. Include cross-tenant isolation in the standard test suite as non-negotiable.

---

#### R3 — Backfill Accuracy
**Risk:** Automatically assigning organization types and product roles to existing tenants may be wrong for some tenants. A tenant that assumed they were LAW_FIRM may actually need PROVIDER access.

**Mitigation:** The backfill script should generate a review manifest (CSV/JSON) listing every tenant and its proposed type and roles before applying changes. A platform admin must approve the manifest before execution. Backfill should be reversible within Phase 2 (before JWT changes in Phase 3 lock in the model).

---

#### R4 — Multi-Organization User Complexity
**Risk:** A user belonging to multiple organizations (e.g., a user who is both a LAW_FIRM admin and a LIEN_OWNER member) creates ambiguity about which organization context is active for a given session.

**Mitigation:** Define a "primary organization" per user (first or explicitly selected). The JWT is issued for a single org context. Organization switching requires re-authentication or token refresh. Document this constraint clearly in onboarding.

---

#### R5 — Party Data Privacy
**Risk:** Party records contain PII (name, DOB, email). Party records created by one tenant's law firm may need to be referenced by another tenant's provider (in a referral). This creates a cross-tenant PII access vector.

**Mitigation:** Party records should be accessed by ID only, with PII fields resolved only in the context of a workflow the requesting organization is authorized to see. No endpoint should allow browsing or listing Parties across tenant boundaries. Party lookups must always be scoped to a specific workflow artifact.

---

#### R6 — SynqFund Application Ownership Ambiguity
**Risk:** If the injured party, the law firm, and the funder all have some form of access to a fund application, write conflict and status inconsistency could occur.

**Mitigation:** Define a strict state machine for Application status transitions and enforce that each transition can only be triggered by a specific ProductRole. `REFERRER` can create and cancel. `FUNDER` can evaluate, approve, and decline. The Party portal is read-only for the party. No concurrent write paths to the same application from different roles.

---

### 6.2 Tradeoffs

| Decision | Chosen Approach | Alternative Considered | Reason for Choice |
|---|---|---|---|
| Organization scope | Org is always within one Tenant | Orgs span tenants | Simpler isolation; cross-tenant behavior handled via role grants, not org sharing |
| Product role storage | Roles computed at login, embedded in JWT | Roles checked live on every request | Reduces DB round trips; acceptable given short token lifetime (60 min) |
| Party authentication | Separate Party JWT, separate login endpoint | Party uses same User JWT with restricted scope | Cleaner separation; Party must not inherit User session capabilities |
| Backfill strategy | Default org per tenant, approval-gated | Manual org creation per tenant | Ensures all existing tenants function immediately post-migration |
| Policy enforcement | Feature-flagged, per-tenant rollout | Global cutover | Reduces blast radius; allows rapid rollback without a deployment |
| Cross-tenant referral storage | Lives in initiating tenant's DB partition | Shared workflow DB | Preserves existing DB isolation model; simpler to reason about |

---

*End of Specification*
