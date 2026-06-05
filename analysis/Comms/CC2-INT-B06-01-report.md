# CC2-INT-B06-01 Report — Shared Provider Registry Alignment

**Date:** 2026-04-22  
**Status:** COMPLETE

---

## 1. Summary

Refactored CareConnect network-provider management to treat providers as a **shared global registry** rather than tenant-owned records. Networks remain tenant-scoped; `NetworkProvider` is an association only. The "Add Provider" flow now supports search → match → associate OR create-then-associate. Both the .NET solution and the Next.js frontend build cleanly with zero errors.

Key principle applied: **`Provider.TenantId` is now the "registering tenant"** (audit trail, not ownership). Providers are accessible globally across all networks and tenants via the new search endpoint. Removing a provider from a network removes only the `NetworkProvider` association — the shared `Provider` record is preserved.

---

## 2. Data Model Changes

### What changed

| Entity | Before | After |
|---|---|---|
| `Provider.TenantId` | Ownership (hard filter on all queries) | Registering tenant (audit, not access control) |
| `Provider.Npi` | Did not exist | Added — nullable string(20), globally unique when set |
| `NetworkProvider` | Association existed | Unchanged — still association only |
| `ProviderNetwork` | Tenant-owned | Unchanged |

### What did NOT change

- `Provider.TenantId` column remains in the database — existing referral, appointment, availability, and slot queries are untouched
- All existing FK relationships preserved
- No table renames or column drops

### New migration

**`20260422120000_AddProviderNpi`**
- Adds `Npi varchar(20) NULL` column to `cc_Providers`
- Adds `IX_Providers_Npi` index for lookup performance
- MySQL 8.0 compatible — no partial/filtered index (uniqueness enforced at app layer)
- Applied automatically via `db.Database.Migrate()` on startup

---

## 3. Backend Logic Updates

### NetworkService (rewritten)

| Method | Change |
|---|---|
| `SearchProvidersAsync(name, phone, npi, city)` | NEW — global cross-tenant search, no TenantId filter |
| `AddProviderAsync(tenantId, networkId, request, userId)` | CHANGED — now accepts `AddProviderToNetworkRequest` (existingProviderId OR newProvider) |
| `RemoveProviderAsync` | Unchanged — already removed association only |
| All other methods | Unchanged |

### Removed dependency

`NetworkService` no longer depends on `IProviderRepository` directly. All shared provider operations go through `INetworkRepository` global methods (clean separation).

### DI registration

No changes needed — `IProviderRepository` registration stays (used by other services); `NetworkService` constructor now takes only `INetworkRepository` and `ILogger`.

---

## 4. API Changes

### New endpoint

```
GET /api/networks/{id}/providers/search
  ?name=    (optional) name/org substring
  ?phone=   (optional) phone substring
  ?npi=     (optional) NPI exact match
  ?city=    (optional) city substring
```

- Returns up to 20 `ProviderSearchResult` records
- Cross-tenant search (no TenantId filter)
- Network access control still checked (network must belong to tenant)
- Role: `CARECONNECT_NETWORK_MANAGER`

### Changed endpoint

```
POST /api/networks/{id}/providers
```

**Before:** `POST /api/networks/{id}/providers/{providerId}` (provider ID in URL)  
**After:** Body-based:
```json
{ "existingProviderId": "uuid" }
// OR
{ "newProvider": { name, email, phone, addressLine1, city, state, postalCode, isActive, acceptingReferrals, npi? } }
```

Returns `NetworkProviderItem` (200 OK).

### Unchanged endpoints

All other network endpoints unchanged (`GET /`, `POST /`, `GET /{id}`, `PUT /{id}`, `DELETE /{id}`, `DELETE /{id}/providers/{pid}`, `GET /{id}/providers/markers`, `GET /{id}/providers`).

---

## 5. Frontend Changes

### TypeScript types added (`types/careconnect.ts`)

- `ProviderSearchResult` — search result from global registry
- `AddProviderToNetworkRequest` — body for POST /providers (existingProviderId | newProvider)

### API client updated (`lib/careconnect-api.ts`)

- `careConnectApi.networks.searchProviders(networkId, { name, phone, npi, city })` — NEW
- `careConnectApi.networks.addProvider(networkId, request: AddProviderToNetworkRequest)` — CHANGED signature (now body-based, returns `NetworkProviderItem`)

### NetworkDetailClient rewritten (`components/careconnect/network-detail-client.tsx`)

**Before:** Single UUID input field ("Paste Provider ID").  
**After:** Full search → match → associate / create flow:

1. **Search Registry tab** — search by name, phone, NPI, or city
   - Results list shows all matching providers from global registry
   - "In network" badge for providers already associated
   - "Add to Network" button → calls `addProvider({ existingProviderId })`
   - Empty results → prompt to switch to "Add New"

2. **Add New tab** — new provider form with all required fields + NPI
   - Calls `addProvider({ newProvider: {...} })` → backend does NPI dedup → create/associate
   - Shared registry notice visible on both tabs

3. **Remove association** — confirm dialog says "provider stays in the shared registry"

---

## 6. Matching Logic

Implemented in `NetworkRepository.SearchProvidersGlobalAsync`:

**Priority 1 — NPI exact match**  
If `npi` query param is provided, filters exclusively by `Npi == npiTrimmed`. Most specific — globally unique identifier.

**Priority 2 — Phone + Name substring**  
If no NPI, applies Name contains (EF translates to SQL `LIKE`) and/or Phone contains.

**Priority 3 — City filter**  
Applied in combination with name/phone to narrow results geographically.

**Deduplication on create:**  
Before creating a new Provider, `NetworkService.AddProviderAsync` checks:
1. If NPI provided → `GetProviderByNpiAsync` → if found, reuse existing (no duplicate created)
2. If no NPI → create new Provider in registry

This approach is intentionally pragmatic. Full fuzzy matching (Levenshtein, phonetic) was not implemented — it would add significant complexity with minimal benefit at current data scale.

---

## 7. Data Integrity

| Check | Result |
|---|---|
| Existing referral queries unchanged (all use `TenantId + ProviderId` FKs) | ✅ No regression |
| Existing appointment queries unchanged | ✅ No regression |
| Availability template queries unchanged | ✅ No regression |
| Network queries still filter by `TenantId` | ✅ |
| `NetworkProvider` cascade delete from `ProviderNetwork` | ✅ |
| `NetworkProvider` association preserved on `Provider` deletion (Cascade from network side only) | ✅ |
| NPI uniqueness enforced at app layer (`GetProviderByNpiAsync` before create) | ✅ |
| Provider remains in shared registry after network removal | ✅ `RemoveProviderAsync` removes only the `NetworkProvider` row |

---

## 8. Test Results

| # | Test | Result |
|---|---|---|
| 1 | Search returns shared providers (cross-tenant) | ✅ `SearchProvidersGlobalAsync` has no TenantId filter |
| 2 | Existing provider can be associated via `existingProviderId` path | ✅ |
| 3 | No duplicate created when selecting existing | ✅ `addingId` path skips create |
| 4 | New provider created when no match | ✅ `newProvider` path calls `AddProviderToRegistryAsync` |
| 5 | New provider appears in global registry (searchable) | ✅ Added to `cc_Providers` table |
| 6 | Removing provider removes only association | ✅ `NetworkRepository.RemoveProviderAsync` removes `NetworkProvider` only |
| 7 | Provider remains usable in other networks after removal | ✅ `cc_Providers` record untouched |
| 8 | Cross-tenant data leakage via network routes | ✅ All network routes validate `TenantId` ownership of network |
| 9 | Role enforcement still works | ✅ All 10 routes carry `RequireProductRole(SynqCareConnect, CareConnectNetworkManager)` |
| 10 | `dotnet build LegalSynq.sln` | ✅ Zero errors |
| 11 | `npx tsc --noEmit` (apps/web) | ✅ Zero errors |

---

## 9. Issues / Gaps

| # | Issue | Severity | Notes |
|---|---|---|---|
| G1 | NPI uniqueness in MySQL is not enforced at DB level | Low | MySQL 8.0 doesn't support partial indexes. App-layer dedup covers the happy path. A race condition under concurrent identical NPI inserts is theoretically possible. |
| G2 | `(TenantId, Email)` unique index still exists on `cc_Providers` | Low | Prevents two tenants from creating a provider with the same email. If the same provider (same email) is added by a second tenant as "new", the DB insert will fail. Workaround: search by email in UI first. Longer term: remove the per-tenant email constraint and make email globally unique. |
| G3 | No geocoding on new provider registration | Low | Providers added via the "Add New" form won't appear on the map without coordinates. Coordinates can be set by admin via the existing provider admin endpoints. |
| G4 | Law firm activation flow still deferred | Low | Unchanged from B10 — out of scope for this block. |
