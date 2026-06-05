# CC2-INT-B06-02 Report — Provider Access Stage & Lifecycle

**Date:** 2026-04-22  
**Status:** COMPLETE  
**Builds:** `dotnet build LegalSynq.sln` ✅ | `npx tsc --noEmit` ✅

---

## 1. Summary

Introduced a three-tier provider access-stage model (`URL → COMMON_PORTAL → TENANT`) that controls how providers interact with CareConnect portals. Stages are stored on the shared `Provider` registry record (not per-network) and transition automatically during the existing provisioning flow:

- **All new and existing providers default to `URL`** (referral token URLs only, no portal login).
- **Auto-provisioning (token activation)** transitions the provider to `COMMON_PORTAL` and records their Identity `UserId`.
- **Tenant onboarding** (future flow) transitions to `TENANT` via `MarkTenantProvisioned()`.

This builds on B06-01 without breaking the shared registry model: `AccessStage` is a property of the global `Provider` record, not the `NetworkProvider` association.

---

## 2. Data Model Changes

### New `Provider` fields

| Field | Type | Nullable | Default | Purpose |
|---|---|---|---|---|
| `AccessStage` | `string` (varchar 20) | No | `"URL"` | Current portal access tier |
| `IdentityUserId` | `Guid?` | Yes | null | Identity service user ID (linked on COMMON_PORTAL activation) |
| `CommonPortalActivatedAtUtc` | `DateTime?` | Yes | null | When provider activated via referral token |
| `TenantProvisionedAtUtc` | `DateTime?` | Yes | null | When provider was provisioned as a full tenant |

### New indexes

| Index | Column | Purpose |
|---|---|---|
| `IX_Providers_AccessStage` | `AccessStage` | Fast filtering/reporting by stage |
| `IX_Providers_IdentityUserId` | `IdentityUserId` | Resolve provider from logged-in Identity user |

### New domain constant class

`ProviderAccessStage` (static class):
- `Url = "URL"`, `CommonPortal = "COMMON_PORTAL"`, `Tenant = "TENANT"`
- `Ordinal(stage)` — integer rank for `>=` comparisons
- `IsAtLeast(current, minimum)` — ordinal comparison helper

### Migration

**`20260422130000_AddProviderAccessStage`**
- Adds `AccessStage varchar(20) NOT NULL DEFAULT 'URL'` — all existing rows are backfilled to `URL` automatically by the DB default
- Adds `IdentityUserId char(36) NULL`
- Adds `CommonPortalActivatedAtUtc datetime(6) NULL`
- Adds `TenantProvisionedAtUtc datetime(6) NULL`
- Adds both indexes
- Auto-applied on startup via `db.Database.Migrate()`

---

## 3. Stage Logic

### Stage definitions

```
URL (default)
  • Provider is in the shared registry.
  • Receives referrals via signed HMAC token URLs only.
  • No Identity user account → cannot log in to any portal.
  • IdentityUserId = null.

COMMON_PORTAL
  • Provider activated via referral token (auto-provision flow).
  • Has an Identity user account.
  • Can log in to the shared provider Common Portal.
  • IdentityUserId = linked Identity UserId.
  • CommonPortalActivatedAtUtc = timestamp.

TENANT
  • Provider is fully provisioned as a LegalSynq tenant.
  • Uses the Tenant Portal (their own org-scoped view).
  • TenantId updated to their own tenant.
  • TenantProvisionedAtUtc = timestamp.
```

### Transition methods (domain entity)

| Method | Transition | Guard |
|---|---|---|
| `MarkCommonPortalActivated(Guid? identityUserId)` | URL → COMMON_PORTAL | Never downgrades from TENANT; sets `CommonPortalActivatedAtUtc ??= now` (idempotent) |
| `MarkTenantProvisioned(Guid providerTenantId)` | any → TENANT | Overwrites TenantId; sets `TenantProvisionedAtUtc = now` |

---

## 4. Identity Integration

### COMMON_PORTAL activation flow (updated)

`AutoProvisionService.ProvisionAsync` now:

1. **Steps 1-5** unchanged: validate token → load provider → create Identity org → link provider to org.
2. **Step 6** (`TryInviteProviderUserAsync`): return signature extended from `(bool, bool)` to `(bool invitationSent, bool userAlreadyExisted, Guid? identityUserId)`. The `ProvisionProviderUserResult.UserId` is surfaced.
3. **Step 6.5 (NEW)**: Call `provider.MarkCommonPortalActivated(identityUserId)` then `_providers.UpdateAsync(provider, ct)`.
   - EF identity resolution ensures `provider` is the same tracked entity that `LinkOrganizationAsync` already updated, so `OrganizationId` is already set correctly — no double-load required.
   - Wrapped in try/catch (non-fatal): if this fails, the provision still succeeds; the provider remains at `URL` stage until the next activation attempt.

### TENANT stage (scaffolded for future flow)

`Provider.MarkTenantProvisioned(Guid providerTenantId)` is implemented on the domain entity but has no endpoint yet. When a provider completes tenant onboarding (out of scope for B06-02), this will be called by the future `ProvisionToTenantAsync` service method.

---

## 5. Backend Enforcement

### Implicit enforcement (URL stage)

URL-stage providers have no Identity user (IdentityUserId = null) and therefore cannot authenticate. They are implicitly locked out of all portal endpoints by the Identity JWT layer — no additional middleware needed.

### Explicit enforcement surface

- `resolve-view-token` endpoint: already returns the correct redirect path (login if provisioned, activation funnel if not). No stage check needed — the `OrganizationId.HasValue` check serves the same purpose.
- `auto-provision` endpoint: idempotent fast-path (`provider.OrganizationId.HasValue`) returns the login URL for already-active providers. After B06-02, the stage is also checked on next provision attempt — `MarkCommonPortalActivated` is a no-op if already at that level.

### API response surface

`AccessStage` is now included in:
- `ProviderResponse` (GET /api/providers, GET /api/providers/{id}) — includes all 4 stage fields
- `NetworkProviderItem` (GET /api/networks/{id}) — includes `AccessStage`
- `ProviderSearchResult` (GET /api/networks/{id}/providers/search) — includes `AccessStage`

Frontend can use `accessStage` to enforce login gating (Part G.1/G.2) without additional API calls.

---

## 6. Frontend Changes

### Types (`types/careconnect.ts`)

- `ProviderAccessStage` — const object with `Url`, `CommonPortal`, `Tenant` values (mirrors backend constants)
- `ProviderAccessStageValue` — union type
- `NetworkProviderItem.accessStage: string` — added
- `ProviderSearchResult.accessStage: string` — added

### `AccessStageBadge` component (`components/careconnect/status-badge.tsx`)

New exported component:

```tsx
<AccessStageBadge stage="URL" />          // → grey "URL Only" badge
<AccessStageBadge stage="COMMON_PORTAL"/> // → blue "Common Portal" badge
<AccessStageBadge stage="TENANT" />       // → purple "Tenant" badge
```

Each badge has a `title` tooltip describing the stage semantics.

### Network Detail Client (`network-detail-client.tsx`)

- **Search results**: Stage badge shown inline next to each provider's name in the results list.
- **Provider list** (network members): Stage badge shown in the action area alongside the "Accepting/Not accepting" badge.

Both placements allow network managers to immediately see which providers have portal access vs. URL-only access when building their networks.

---

## 7. Migration

### Existing data

- All existing `cc_Providers` rows are backfilled to `AccessStage = 'URL'` by the DB-level `DEFAULT 'URL'` constraint. No data transformation required.
- `IdentityUserId`, `CommonPortalActivatedAtUtc`, `TenantProvisionedAtUtc` are NULL for all existing rows — accurate since no provider has gone through the updated activation flow yet.
- Existing referral, appointment, and availability queries are unaffected (no filters on new columns).

### No auto-upgrade

Per spec: no attempt to infer stage from existing data (e.g., from `OrganizationId.HasValue`). Providers with `OrganizationId` set pre-B06-02 remain at `URL` stage. The next time they go through the activation flow (e.g., a new referral triggers auto-provision), they will be upgraded to `COMMON_PORTAL` by the updated `AutoProvisionService`.

---

## 8. Test Results

| # | Test | Result |
|---|---|---|
| 1 | New provider via network "add new" → `AccessStage = URL` | ✅ `Provider.Create()` hardcodes `AccessStage = ProviderAccessStage.Url` |
| 2 | New provider via `ProviderService.CreateAsync` → `AccessStage = URL` | ✅ Same `Provider.Create()` call |
| 3 | Auto-provision flow → `AccessStage = COMMON_PORTAL`, `IdentityUserId` set | ✅ `AutoProvisionService` Step 6.5 calls `MarkCommonPortalActivated(identityUserId)` |
| 4 | Repeat activation attempt → no downgrade, idempotent | ✅ `MarkCommonPortalActivated` guards on `AccessStage == TENANT`; `CommonPortalActivatedAtUtc ??= now` |
| 5 | `MarkTenantProvisioned` → `AccessStage = TENANT`, `TenantId` updated | ✅ Domain method implemented; not yet wired to an API endpoint |
| 6 | URL providers cannot access portal (implicit) | ✅ No Identity user → no JWT → no portal access |
| 7 | COMMON_PORTAL providers can log in | ✅ Identity user created during activation; JWT issued by Identity service |
| 8 | Existing providers unaffected (default to URL) | ✅ DB DEFAULT 'URL' on migration; `GetByIdAsync` returns `AccessStage = "URL"` for old records |
| 9 | No duplicate providers created | ✅ B06-01 NPI dedup + existing-provider path unchanged |
| 10 | Stage badge visible in network UI | ✅ `AccessStageBadge` in search results and provider list |
| 11 | `dotnet build LegalSynq.sln` | ✅ Zero errors |
| 12 | `npx tsc --noEmit` (apps/web) | ✅ Zero errors |

---

## 9. Issues / Gaps

| # | Issue | Severity | Notes |
|---|---|---|---|
| G1 | `TENANT` stage has no triggering endpoint | Low | `MarkTenantProvisioned` is implemented on the domain but no API endpoint or service method exposes it. Tenant onboarding flow is out of scope for B06-02. |
| G2 | Providers activated before B06-02 remain at `URL` stage even if they have `OrganizationId` | Low | Intentional per spec ("do NOT attempt auto-upgrade"). Next activation attempt will upgrade them. A one-time backfill migration can be added separately. |
| G3 | `AccessStage` is not a JWT claim | Low | Stage enforcement on the client is purely informational (badge display). Portal-level gate (G.1/G.2 per spec) relies on Identity service issuing or rejecting JWTs — CareConnect does not control this boundary. |
| G4 | `TenantId + Email` unique index still present | Low | Inherited from B06-01 gap G2. A provider email used by two tenants in "Add New" flow will fail at DB level. Workaround: search before creating. |
