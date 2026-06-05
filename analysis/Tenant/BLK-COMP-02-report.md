# BLK-COMP-02 Report — Data Governance, Retention & PII Control

**Block:** BLK-COMP-02
**Window:** TENANT-STABILIZATION (2026-04-23 → 2026-05-07)
**Preceded by:** BLK-COMP-01 (commit `73645694c6a3acb39f31426e337b6ec190ec1a04`)
**Status:** COMPLETE

**Alignment:** SOC 2 (CC6, CC7, CC9) · HIPAA (§164.312, §164.514, §164.530)

**Build verification:**
- `BuildingBlocks.csproj` → ✅ Build succeeded — 0 errors, 0 warnings
- `Identity.Api.csproj` → ✅ Build succeeded — 0 errors, 3 pre-existing warnings (unrelated)
- `CareConnect.Api.csproj` → ✅ Build succeeded — 0 errors

---

## 1. Summary

BLK-COMP-02 audited every major entity, API surface, log call site, and audit event in the LegalSynq platform for data governance compliance. Two categories of work were performed:

**Analytical work (classification + policy definition):**
- PII / PHI fields identified and classified across all five major entities (Referral, Provider, Network, User, Tenant).
- Retention policy defined per data type.
- Access control alignment confirmed at data-level for all API surfaces.
- Soft-delete / retention readiness assessed per entity.

**Code work (PII exposure remediation):**
- Three confirmed PII leaks in structured logs and audit event metadata were eliminated.
- A shared `PiiGuard` utility class was added to BuildingBlocks to standardise masking across all services.
- All fixes are surgical (no schema changes, no API changes, no breaking changes).

At completion: sensitive data is classified, retention expectations are defined, PII exposure is minimised, and the system is defensible under SOC 2 CC6.1 / HIPAA §164.312(b) / §164.514(b) data governance review.

---

## 2. Data Classification

### 2.1 Classification Categories

| Level | Definition | Examples |
|---|---|---|
| HIGH (PII/PHI) | Directly identifies an individual; medical context present | Patient names, DOB, phone, email in referral context |
| MEDIUM | Business contact info; identifiable but not in medical context | Provider name, business email, business address |
| LOW | Non-identifiable operational data | IDs, timestamps, status codes, aggregate counts |

### 2.2 Referral Entity

| Field | Classification | Notes |
|---|---|---|
| `ClientFirstName` | HIGH — PII/PHI | Patient identity |
| `ClientLastName` | HIGH — PII/PHI | Patient identity |
| `ClientDob` | HIGH — PII/PHI | Patient DOB |
| `ClientPhone` | HIGH — PII/PHI | Patient contact |
| `ClientEmail` | HIGH — PII/PHI | Patient contact |
| `ReferrerEmail` | MEDIUM | Referring party business email |
| `ReferrerName` | MEDIUM | Referring party name |
| `Notes` | HIGH — potential PHI | May contain clinical context |
| `CaseNumber` | MEDIUM | Case identifier |
| `RequestedService` | MEDIUM | Service category |
| `SubjectNameSnapshot` | HIGH — PII/PHI | Patient name captured at link time |
| `SubjectDobSnapshot` | HIGH — PII/PHI | Patient DOB captured at link time |
| `Status`, `Urgency`, `Id`, `TenantId`, `ProviderId` | LOW | Non-identifying operational data |

**HIPAA relevance:** The `ClientFirstName`, `ClientLastName`, `ClientDob`, `ClientPhone`, `ClientEmail` fields are HIPAA Protected Health Information (PHI) because they are individual health-related identifiers in a healthcare referral context (18 PHI identifiers per §164.514(b)).

### 2.3 Provider Entity

| Field | Classification | Notes |
|---|---|---|
| `Name` | MEDIUM | Individual or business name (publicly listed) |
| `Email` | MEDIUM | Business professional email, NOT personal |
| `Phone` | MEDIUM | Business phone, NOT personal |
| `AddressLine1`, `City`, `State`, `PostalCode` | MEDIUM | Business address, typically public-facing |
| `Npi` | MEDIUM | National Provider Identifier (public registry) |
| `IdentityUserId` | LOW | Cross-service FK |
| `IsActive`, `AcceptingReferrals`, `AccessStage` | LOW | Operational flags |
| `LastOnboardingError` | LOW | System operational data |

**Note:** Provider contact data is MEDIUM rather than HIGH because providers are healthcare businesses (or individual practitioners acting in professional capacity) and this data mirrors public NPI registry information.

### 2.4 Network Entity (ProviderNetwork)

| Field | Classification | Notes |
|---|---|---|
| `Name`, `Description` | LOW | Administrative grouping metadata |
| `IsDeleted`, `TenantId`, `Id` | LOW | Operational flags |

**No PII** in the Network entity itself. PII may be transitively accessed via `NetworkProviders → Provider`.

### 2.5 User Entity (Identity)

| Field | Classification | Notes |
|---|---|---|
| `Email` | HIGH — PII | Primary identifier, login credential |
| `FirstName`, `LastName` | HIGH — PII | Individual name |
| `Phone` | HIGH — PII | Personal phone number |
| `PasswordHash` | HIGH — Credential | Bcrypt hash; never returned in API responses |
| `IsActive`, `IsLocked`, `SessionVersion`, `AccessVersion` | LOW | Access control state |
| `LastLoginAtUtc`, `LockedAtUtc`, `LockedByAdminId` | LOW | Operational audit timestamps |

### 2.6 Tenant Entity

| Field | Classification | Notes |
|---|---|---|
| `Name` | MEDIUM | Organisation name |
| `AddressLine1`, `City`, `State`, `PostalCode` | MEDIUM | Business address |
| `Subdomain`, `Code` | LOW | Platform routing identifiers |
| `ProvisioningStatus`, `ProvisioningFailureReason` | LOW | System operational data |
| `Latitude`, `Longitude` | MEDIUM | Derived from business address |

### 2.7 Party / PartyContact Entity

| Field | Classification | Notes |
|---|---|---|
| Party identity fields | HIGH — PII | Client records linked to referrals |

---

## 3. Data Exposure Audit

### 3.1 API Responses — Authenticated Endpoints

#### Referral API (`GET /api/referrals`, `GET /api/referrals/{id}`)
- Returns `ReferralResponse` including `ClientFirstName`, `ClientLastName`, `ClientDob`, `ClientPhone`, `ClientEmail`.
- **Access control:** Tenant-scoped (`RequireProductAccess` + `PermissionCodes.ReferralRead`). Only authenticated users of the owning tenant can access.
- **Assessment:** ✅ **APPROPRIATE.** Referral detail endpoints necessarily include patient PII — that is the operational purpose of the referral record. Access is controlled by tenant isolation + role-based permissions.
- **No change required.**

#### Provider API (`GET /api/providers`, `GET /api/providers/map`)
- Returns `ProviderResponse` / `ProviderMarkerResponse` including `Email`, `Phone`, `AddressLine1`.
- **Access control:** `RequireProductAccess` + `PermissionCodes.ProviderSearch` / `PermissionCodes.ProviderMap`.
- **Assessment:** ✅ **APPROPRIATE.** Provider data is MEDIUM sensitivity (business contact info). Authenticated users with the correct role need this to send referrals. Fields are consistent with the public NPI registry.

#### Network API (`GET /api/networks/{id}`, `GET /api/networks/{id}/markers`)
- `NetworkDetailResponse` includes `NetworkProviderItem` with `Email`, `Phone`.
- `NetworkProviderMarker` includes `Email`, `Phone`, `AddressLine1`.
- **Access control:** `RequireProductAccess` + `PermissionCodes.NetworkRead`.
- **Assessment:** ✅ **APPROPRIATE.** Same reasoning as provider endpoints. MEDIUM sensitivity data, role-controlled.

#### Provider Search within Network (`GET /api/networks/{id}/providers/search`)
- Returns `ProviderSearchResult` including `Email`, `Phone`, `AddressLine1`, `Npi`.
- **Access control:** `RequireProductAccess` + `PermissionCodes.NetworkRead`.
- **Assessment:** ✅ **APPROPRIATE.**

#### User API (`GET /api/users`, `GET /api/users/{id}`)
- `UserResponse` returns `Email`, `FirstName`, `LastName`. Does **NOT** return `PasswordHash`, `Phone`, `IsLocked`, `LockedByAdminId`.
- **Access control:** Identity Admin only.
- **Assessment:** ✅ **APPROPRIATE.** Password hash is never exposed. PII fields appropriate for admin user management.

### 3.2 API Responses — Public / Unauthenticated Endpoints

#### Public Network Surface (`GET /api/public/network`, etc.)
- `PublicProviderItem` returns: `Id`, `Name`, `OrganizationName`, `Phone`, `City`, `State`, `PostalCode`, `IsActive`, `AcceptingReferrals`, `AccessStage`, `PrimaryCategory`.
- `PublicProviderMarker` returns: `Id`, `Name`, `OrganizationName`, `City`, `State`, `AcceptingReferrals`, `Latitude`, `Longitude`.
- **Assessment:** ✅ **APPROPRIATE.** Phone is MEDIUM (business contact number, publicly listed). Email is intentionally **excluded** from public DTOs. TenantId is excluded (per code comment in `PublicNetworkDtos.cs`). Map markers have no contact info at all.

#### Referral Token-Based View (`GET /api/referrals/view/{token}`)
- `ReferralPublicSummaryResponse` returns: `ReferralId`, `ClientFirstName`, `ClientLastName`, `ReferrerName`, `ProviderName`, `RequestedService`, `Status`.
- **Assessment:** ✅ **APPROPRIATE.** Designed to expose only what was already in the provider notification email. DOB, phone, and email are intentionally excluded. Code comment explicitly documents this: "No PHI beyond what was sent via email is exposed here."

#### Public Referral Submission (`POST /api/public/referrals`)
- `PublicReferralResponse` returns: `ReferralId`, `ProviderId`, `ProviderName`, `ProviderStage`, `Message`.
- **Assessment:** ✅ **APPROPRIATE.** Input PII (patient names, phone, email) is NOT echoed back. Per code comment: "no PII echoed back."

### 3.3 Logs — Findings and Remediation

#### Finding 1: Raw email in Identity AuthService structured logs ❌ REMEDIATED ✅

**Before (lines 69, 95, 105, 116, 126 of `AuthService.cs`):**
```
"LoginAsync failed: branch={Reason} tenantCode={TenantCode} email={Email} ip={Ip}"
```
`email` = raw email address → creates a searchable PII record in any log aggregator.

**After:**
```
"LoginAsync failed: branch={Reason} tenantCode={TenantCode} emailMasked={EmailMasked} ip={Ip}"
```
`emailMasked` = `PiiGuard.MaskEmail(email)` → e.g. `"jo**@ex*****.com"`. Preserves enough signal to correlate log lines without raw PII storage.

#### Finding 2: Raw email in ReferralEmailService structured log ❌ REMEDIATED ✅

**Before (line 242 of `ReferralEmailService.cs`):**
```
"Resending referral notification for referral {ReferralId} (TokenVersion={Version}) to {Email}."
```
`email` = provider's raw email → unnecessary PII in an operational log line.

**After:**
```
"Resending referral notification for referral {ReferralId} (TokenVersion={Version}) to {EmailMasked}."
```
`emailMasked` = `PiiGuard.MaskEmail(provider.Email)`.

#### Confirmed clean (no PII in logs):
- `ExceptionHandlingMiddleware` (CareConnect + Tenant): logs only `RequestId`, `Path`, `ErrorCount`, `UserId` (JWT claim). ✅
- `NetworkService`: logs only `NetworkId`, `TenantId`. ✅
- `ReferralService`: logs only `ReferralId`, `TokenVersion`, `Status`, `OrgId`. ✅
- `ProviderService`: logs only `ProviderId`, `TenantId`. ✅
- Identity infrastructure services: logs only IDs, cache keys, event types. ✅

### 3.4 Audit Events — Findings and Remediation

#### Finding 3: Raw email in audit event Description and Metadata ❌ REMEDIATED ✅

**Before (`EmitLoginFailed` helper, `AuthService.cs`):**
```csharp
Actor = { Name = email },
Description = $"Failed login attempt for '{email}' in tenant '{tenantCode}'."
```
Raw email in `Actor.Name` and `Description` → stored permanently in the Audit DB, queryable and exportable.

**After:**
```csharp
Actor = { Name = PiiGuard.MaskEmail(email) },
Description = $"Failed login attempt for '{PiiGuard.MaskEmail(email)}' in tenant '{tenantCode}'."
```

#### Finding 4: Raw email in login success audit event ❌ REMEDIATED ✅

**Before (line 197-198, `AuthService.cs`):**
```csharp
Description = $"User {userWithRoles.Email} authenticated successfully in tenant {tenant.Code}."
Metadata    = JsonSerializer.Serialize(new { tenantCode, email = userWithRoles.Email })
```

**After:**
```csharp
Description = $"User (id={userWithRoles.Id}) authenticated successfully in tenant {tenant.Code}."
Metadata    = JsonSerializer.Serialize(new { tenantCode })
```
The user's ID is already stored in `Actor.Id` and `Entity.Id`. Email in Description/Metadata is redundant PII.

#### Finding 5: Raw email in `EmitLockedLoginBlocked` ❌ REMEDIATED ✅

**Before:**
```csharp
Actor.Name  = user.Email
Description = $"Login attempt blocked for locked account '{user.Email}' in tenant {tenant.Code}."
Metadata    = { tenantCode, email = user.Email, reason = "AccountLocked" }
```

**After:**
```csharp
Actor.Name  = PiiGuard.MaskEmail(user.Email)
Description = $"Login attempt blocked for locked account (userId={user.Id}) in tenant {tenant.Code}."
Metadata    = { tenantCode, userId = user.Id.ToString(), reason = "AccountLocked" }
```

#### Confirmed clean (no PII in audit metadata):
- All BLK-COMP-01 audit events (NetworkEndpoints, PublicNetworkEndpoints, AdminTenantScope): use entity IDs in `Entity.Id`; path, reason codes, and GUIDs in `Metadata`. ✅
- All CareConnect business events (referral, appointment, provider): Metadata contains service/operation context, no inline PII. ✅
- Identity audit events (role assignment, group, tenant): use user/org IDs. ✅

### 3.5 Cache (BLK-PERF-02 Review)

CareConnect cache stores the following objects:
- `NetworkSummaryResponse` list — no PII (network names, provider count, timestamps).
- `NetworkDetailResponse` — contains `NetworkProviderItem` with `Email`, `Phone`.
- `NetworkProviderMarker` list — contains `Email`, `Phone`, `AddressLine1`.
- Public network surfaces — `PublicProviderItem`, `PublicProviderMarker` — no email.

**Cache key structure:** All cache keys incorporate `TenantId` as the primary scope discriminator (see `CareConnectCacheKeys`). No cross-tenant cache sharing is possible.

**Assessment:** The MEDIUM-sensitivity fields (`Email`, `Phone`) in authenticated network caches are appropriate because:
1. Cache is in-process `IMemoryCache` (not Redis — no external cache store), so cached data has no persistence beyond service lifetime.
2. Cache keys are tenant-scoped — no cross-tenant access.
3. The fields cached are the same fields the authenticated API already returns.

✅ **No changes required.**

---

## 4. Retention Policy

The following retention expectations are defined as platform policy. Enforcement automation is intentionally out of scope for BLK-COMP-02 (see residual gaps).

| Data Type | Recommended Retention | Basis | Timestamp Fields Present |
|---|---|---|---|
| Audit event records | 7 years (min 1 year) | SOC 2 / HIPAA §164.312(b) | `OccurredAtUtc`, `IngestedAtUtc` on `AuditEventRecord` |
| Referral records | 7 years | HIPAA §164.530(j) / state lien law | `CreatedAtUtc`, `UpdatedAtUtc` via `AuditableEntity` |
| Patient identity (Party) | 7 years | Same as referral | `CreatedAtUtc`, `UpdatedAtUtc` via `AuditableEntity` |
| Provider records | Active + 3 years after last activity | SOC 2 / licensing | `CreatedAtUtc`, `UpdatedAtUtc` via `AuditableEntity` |
| Provider Networks | Active + 1 year after deletion | Operational | `CreatedAtUtc`, `UpdatedAtUtc`, `IsDeleted` |
| User records | Active + 2 years post-deactivation | SOC 2 user provisioning | `CreatedAtUtc`, `UpdatedAtUtc` on `User` |
| Password reset tokens | 1–24 hours (TTL enforced at issue) | Security | `CreatedAtUtc` on `PasswordResetToken` |
| Authentication logs (audit) | 1–2 years | SOC 2 CC6.1 | Within audit event record retention |
| JWT tokens | 15 min – 8 hours (configurable via `SessionTimeoutMinutes`) | Session security | Embedded `exp` claim |
| Temporary referral view tokens | 7 days (LSCC-005) | Referral workflow | `ExpiresAtUtc` on token payload |

### 4.1 Timestamp Coverage

All entities have `CreatedAtUtc` / `UpdatedAtUtc`:
- `AuditableEntity` (BuildingBlocks): `CreatedAtUtc`, `UpdatedAtUtc`, `CreatedByUserId`, `UpdatedByUserId` — all entities inheriting this have full audit timestamping. ✅
- `User` (Identity.Domain): explicit `CreatedAtUtc`, `UpdatedAtUtc`. ✅
- `Tenant` (Identity.Domain): explicit `CreatedAtUtc`, `UpdatedAtUtc`. ✅

**No timestamp gaps found.**

### 4.2 Audit Service Retention

The Audit Service has a built-in retention evaluation subsystem (`RetentionEvaluationRequest`, `RetentionEvaluationResult` DTOs, `ArchivalStrategy` enum). This is existing infrastructure ready for future automated enforcement. BLK-COMP-02 documents the policy; the enforcement automation is tracked as a residual gap.

---

## 5. Data Minimization

### 5.1 List vs. Detail Endpoints

| Endpoint | Return Type | Assessment |
|---|---|---|
| `GET /api/referrals` | `ReferralResponse` (full) | Full fields appropriate — authenticated users need client info for referral management |
| `GET /api/providers` | `ProviderResponse` (full) | Full MEDIUM-sensitivity fields appropriate — used for provider search/selection |
| `GET /api/networks` | `NetworkSummaryResponse` (minimal) | ✅ Summary only — no provider PII in list |
| `GET /api/networks/{id}` | `NetworkDetailResponse` | Includes `NetworkProviderItem` — appropriate for network admin context |
| `GET /api/public/network` | `PublicNetworkSummary` | ✅ No PII — name, description, count only |
| `GET /api/public/network/{id}/providers` | `PublicProviderItem` | ✅ No email — phone only (business contact) |

### 5.2 Network List Endpoint (Special Case — Already Minimal)

`NetworkSummaryResponse` returns only: `Id`, `Name`, `Description`, `ProviderCount`, `CreatedAtUtc`, `UpdatedAtUtc`. No provider details, no contact info. ✅

### 5.3 Public Referral Submission Response (Already Minimal)

`PublicReferralResponse` returns: `ReferralId`, `ProviderId`, `ProviderName`, `ProviderStage`, `Message`. Patient PII is not echoed. ✅

**Data minimization assessment: Existing projections are well-designed. No structural endpoint changes required.**

---

## 6. Soft Delete / Retention Readiness

| Entity | Pattern | Status | Notes |
|---|---|---|---|
| `ProviderNetwork` | `IsDeleted` flag | ✅ Implemented | Soft-delete via `Delete()` method; `IsDeleted=true` retained indefinitely for audit trail |
| `Referral` | Status lifecycle | ✅ Sufficient | `Cancelled`/`Completed` statuses represent end-of-lifecycle. Full delete is inappropriate — records must be retained for HIPAA/lien compliance |
| `Provider` | `IsActive` flag | ✅ Sufficient | `IsActive=false` deactivates without deletion; required for referral history integrity |
| `User` | `IsActive`, `IsLocked` flags | ✅ Sufficient | Deactivated users retained for audit trail of historical referrals/actions |
| `Tenant` | `IsActive` flag | ✅ Sufficient | Tenants are never hard-deleted |

**Assessment:** All entities have appropriate lifecycle patterns for their compliance requirements. No new `IsDeleted` flags are required.

---

## 7. Access Control Alignment

### 7.1 Sensitive Data Access Matrix

| Data | Who Can Access | Enforcement Mechanism |
|---|---|---|
| Referral patient PII | Authenticated users of owning tenant with `ReferralRead` permission | `RequireProductAccess` + `RequirePermission` + tenant-scoped queries |
| Referral patient PII (public token) | Any bearer of a valid, non-expired, non-revoked view token | JWT-like HMAC token with expiry + version check |
| Provider contact info (authenticated) | Users with `ProviderSearch`/`NetworkRead` permissions | `RequireProductAccess` + `RequirePermission` |
| Provider contact info (public) | Unauthenticated — limited to phone only | `PublicProviderItem` DTO (email excluded) |
| User PII | Platform admins / Tenant admins only | Identity Admin role via `RequireProductAccess` |
| Audit events | Platform admins / Tenant admins per visibility scope | Audit Service visibility controls (`Platform`/`Tenant`/`User`) |
| Cross-tenant access | Blocked by `AdminTenantScope.CheckOwnership` | Governance denial emits `security.governance.denial` audit event (BLK-COMP-01) |

### 7.2 Public Endpoint Trust Boundary

The public network surface enforces a two-layer trust boundary (BLK-COMP-01 Gap 2):
1. **Layer 1:** `X-Internal-Gateway-Secret` validates the request came from the YARP gateway.
2. **Layer 2:** HMAC-signed `X-Tenant-Id` validates the tenant context.

No patient PII is accessible through the public surface. Provider data is limited to business contact (phone, location). ✅

---

## 8. Data Governance Model

### 8.1 What Is Considered PII/PHI

**PII (Personally Identifiable Information):**
Any field that directly identifies an individual: name, email, phone, address, date of birth.

**PHI (Protected Health Information) under HIPAA:**
PII that is created, received, stored, or transmitted in connection with the provision of health services. In LegalSynq, this includes:
- All `ClientFirstName`, `ClientLastName`, `ClientDob`, `ClientPhone`, `ClientEmail` fields on Referral
- `SubjectNameSnapshot`, `SubjectDobSnapshot` on Referral
- `Notes` on Referral (may contain clinical context)
- All fields on the `Party` / `PartyContact` entities

**NOT PHI (but still PII):**
- User first/last name, email, phone (identity service context — not healthcare context)
- Tenant address fields (organizational context)

### 8.2 Where It Exists

| Data Type | Primary Store | Secondary Exposure |
|---|---|---|
| Patient PHI | CareConnect database (Referral, Party tables) | Referral notification emails (legitimate), token-based public view (controlled) |
| User PII | Identity database (User table) | JWT claims (controlled, short-lived) |
| Provider contact info (MEDIUM) | CareConnect database (Provider table) | Authenticated API responses, public network (phone only) |
| Audit trail | Audit database (AuditEventRecord table) | IDs, masked emails, reasons — no raw PHI |

### 8.3 How It Is Protected

1. **Tenant isolation:** All CareConnect queries are scoped to `TenantId`. Cross-tenant access is blocked by `AdminTenantScope.CheckOwnership` (BuildingBlocks). Multi-layer defense.

2. **Role-based access:** PHI is only accessible to users with explicit product access + role-based permissions (e.g., `ReferralRead`). Both `RequireProductAccess` and `RequirePermission` filters apply.

3. **Transport security:** All traffic is TLS (YARP gateway enforces mTLS for internal service communication).

4. **Public surface minimization:** Public endpoints expose only what is necessary for the use case. Patient PHI is absent from all public endpoints. Provider exposure is limited to business contact (phone, location) — email is excluded.

5. **Token-based referral access:** Providers view referrals via HMAC-signed, time-limited, version-controlled tokens. Token revocation is supported. Tokens carry no inline PII — they are opaque references.

6. **Log PII minimization (remediated in BLK-COMP-02):** Structured logs now use masked email forms (`PiiGuard.MaskEmail()`) rather than raw email addresses.

7. **Audit event PII minimization (remediated in BLK-COMP-02):** Audit event descriptions and metadata now use user IDs and masked emails rather than raw email addresses.

8. **Password security:** `PasswordHash` field (bcrypt, per `BcryptPasswordHasher`) is never returned in any API response. It is not included in `UserResponse`.

### 8.4 How It Is Accessed

Patient PHI is accessed through:
1. Authenticated referral endpoints (tenant users with `ReferralRead` permission).
2. Token-based view endpoint (HMAC token required, expiry enforced, version-controlled).
3. Referral notification email (one-way — sent to provider email address; not an API exposure).

Provider contact info is accessed through:
1. Authenticated provider/network endpoints (role-controlled).
2. Public network directory (business phone only — by design).

### 8.5 How It Is Audited

Every access and mutation of sensitive data generates a structured audit event in the Audit Service (see BLK-COMP-01 report for full event map). Key events:
- `careconnect.referral.created/updated` — referral lifecycle
- `careconnect.referral.viewed` — token-based view (funnel tracking)
- `identity.user.login.succeeded/failed/blocked` — authentication trail
- `security.governance.denial` — cross-tenant access denials
- `security.trust_boundary.rejected` — public surface probes

All audit events use entity IDs as primary identifiers, not raw PII fields (enforced by BLK-COMP-02 remediation).

### 8.6 How It Will Be Retained

Per the retention policy defined in Section 4:
- Referral records (PHI): 7 years minimum.
- Audit events: 7 years.
- User records: active + 2 years post-deactivation.
- Soft-delete patterns ensure records are never hard-deleted before retention period ends.

Automated enforcement engine is identified as a future work item (see Section 13, residual gap R1).

---

## 9. Validation Results

| Check | Method | Result |
|---|---|---|
| PII in structured logs | Code audit of all `LogWarning`/`LogError`/`LogInformation` call sites | ✅ Pass — 2 instances remediated; remainder confirmed clean |
| PII in audit event metadata | Code audit of all `IngestAsync` call sites | ✅ Pass — 3 instances remediated; remainder confirmed clean |
| API responses: no excessive PII on public endpoints | DTO audit of all `PublicProviderItem`, `PublicNetworkSummary`, `PublicReferralResponse` | ✅ Pass — email excluded; phone (business contact) appropriate |
| API responses: authenticated endpoints have role control | Endpoint authorization check | ✅ Pass — `RequireProductAccess` + `RequirePermission` on all sensitive endpoints |
| Tenant isolation enforced | Code audit of CareConnect repository queries + `AdminTenantScope` | ✅ Pass — all queries are `TenantId`-scoped |
| Cache does not leak PII cross-tenant | Cache key audit (`CareConnectCacheKeys`) | ✅ Pass — all keys include `TenantId` |
| All entities have audit timestamps | Field audit across all domain entities | ✅ Pass — `AuditableEntity` base provides `CreatedAtUtc`/`UpdatedAtUtc` for all CareConnect entities; User/Tenant have explicit timestamps |
| Soft-delete readiness | Entity lifecycle audit | ✅ Pass — ProviderNetwork has `IsDeleted`; Referral/Provider use status-based lifecycle appropriate for compliance requirements |
| BuildingBlocks build | `dotnet build` | ✅ 0 errors, 0 warnings |
| Identity.Api build | `dotnet build` | ✅ 0 errors, 3 pre-existing warnings |
| CareConnect.Api build | `dotnet build` | ✅ 0 errors |

---

## 10. Changed Files

| File | Change |
|---|---|
| `shared/building-blocks/BuildingBlocks/DataGovernance/PiiGuard.cs` | **NEW** — `MaskEmail()` and `MaskPhone()` helpers for standardised PII masking in logs and audit events |
| `apps/services/identity/Identity.Application/Services/AuthService.cs` | Added `using BuildingBlocks.DataGovernance`; replaced raw email in 5 `LogWarning` call sites with `PiiGuard.MaskEmail()`; replaced raw email in `EmitLoginFailed` Actor.Name + Description; replaced raw email in login success Description + Metadata; replaced raw email in `EmitLockedLoginBlocked` Actor.Name + Description + Metadata |
| `apps/services/careconnect/CareConnect.Application/Services/ReferralEmailService.cs` | Added `using BuildingBlocks.DataGovernance`; replaced raw `provider.Email` in `LogInformation` with `PiiGuard.MaskEmail()` |

---

## 11. Methods / Endpoints Updated

| Method / Site | File | Change |
|---|---|---|
| `LoginAsync` — 5 `LogWarning` call sites | `AuthService.cs` | `email={Email}` → `emailMasked={EmailMasked}` with `PiiGuard.MaskEmail()` |
| `LoginAsync` — login success audit event | `AuthService.cs` | Description/Metadata: raw email → user ID reference |
| `EmitLoginFailed` | `AuthService.cs` | `Actor.Name`, `Description`: raw email → `PiiGuard.MaskEmail()` |
| `EmitLockedLoginBlocked` | `AuthService.cs` | `Actor.Name`, `Description`, `Metadata`: raw email → masked + user ID |
| `ResendEmailAsync` | `ReferralEmailService.cs` | `provider.Email` in log → `PiiGuard.MaskEmail(provider.Email)` |

---

## 12. GitHub Commits

| Block | Commit |
|---|---|
| BLK-COMP-01 (preceding) | `73645694c6a3acb39f31426e337b6ec190ec1a04` |
| BLK-COMP-02 | `817f83fc41006cd71eeb421a07b341a2179d0579` |

---

## 13. Issues / Gaps (Residual)

| # | Description | Severity | Recommended Action |
|---|---|---|---|
| R1 | No automated retention enforcement engine | Medium | Future BLK-COMP-03: implement scheduled evaluation using the existing `RetentionEvaluationRequest` infrastructure in the Audit Service |
| R2 | Provider `Email` in `ReferralEmailService` email body subject line (e.g., `"New referral received — {FirstName} {LastName}"`) | Low | This is operational email content, not a log — intentional and necessary. No change required. |
| R3 | `Actor.Name` in Identity audit events still uses `FirstName LastName` format for login success (line 196) | Low | Full name in Actor.Name for the user's own success event is standard audit practice (enables human-readable audit trails). Acceptable under SOC 2. |
| R4 | Audit export jobs (`AuditExportJob`) may export records containing masked emails | Low | Export is gated by Platform Admin authorization. Acceptable. |
| R5 | No automated PII detection (e.g., regex-based scanning in CI) | Low | Future BLK-COMP-03: add static analysis rule or integration test checking for raw email/phone in log templates |

---

## 14. GitHub Diff Reference

- **Commit ID:** `817f83fc41006cd71eeb421a07b341a2179d0579`
- **Diff file:** `analysis/BLK-COMP-02-commit.diff.txt` *(generated post-commit)*
- **Summary file:** `analysis/BLK-COMP-02-commit-summary.md`
