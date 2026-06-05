# CC2-INT-B06 Report — Tenant Portal Integration with Role-Based Network Management

**Status:** In progress  
**Date:** 2026-04-22  
**Block:** CC2-INT-B06 — Tenant Portal Integration

---

## 1. Summary

CC2-INT-B06 extends the existing CareConnect Tenant Portal with:

- **Role-based network management** — any user (Lien Company, Law Firm) with the
  `CARECONNECT_NETWORK_MANAGER` product role can create and manage provider networks.
- **Provider network CRUD** — create, edit, delete networks; add/remove providers.
- **Map view** — network provider locations rendered on the existing Leaflet map.
- **Full Identity + role enforcement** — no `orgType` gate; capability is role-only.

The Tenant Portal (`apps/web`) and the Common Portal (B05) remain strictly separated.

---

## 2. Routing & Navigation

### Backend routes added (CareConnect service)

| Method | Path | Auth | Purpose |
|--------|------|------|---------|
| GET | `/api/networks` | NetworkManager | List tenant networks |
| POST | `/api/networks` | NetworkManager | Create network |
| GET | `/api/networks/{id}` | NetworkManager | Get network detail |
| PUT | `/api/networks/{id}` | NetworkManager | Update network |
| DELETE | `/api/networks/{id}` | NetworkManager | Delete network |
| GET | `/api/networks/{id}/providers` | NetworkManager | List providers in network |
| POST | `/api/networks/{id}/providers/{providerId}` | NetworkManager | Add provider |
| DELETE | `/api/networks/{id}/providers/{providerId}` | NetworkManager | Remove provider |
| GET | `/api/networks/{id}/providers/markers` | NetworkManager | Map markers for network |

### Frontend routes added

| Route | Purpose |
|-------|---------|
| `/careconnect/networks` | Network list + create |
| `/careconnect/networks/[id]` | Network detail, provider list, map |

### Navigation

`Networks` nav item added to CareConnect sidebar. Gated on `CareConnectNetworkManager`
product role via `requiredRoles` — invisible to users without the role.

---

## 3. Authentication & Access

### New role: `CARECONNECT_NETWORK_MANAGER`

- Product code: `SYNQ_CARECONNECT:CARECONNECT_NETWORK_MANAGER`
- Added to `ProductRoleCodes` in BuildingBlocks
- Added to `Policies` in BuildingBlocks (`CanManageCareConnectNetworks`)
- Registered in CareConnect `Program.cs` as a JWT product role check
- Frontend `ProductRole.CareConnectNetworkManager` constant added

### Access matrix

| User | Role | Network Access |
|------|------|----------------|
| Any / Lien Company | CARECONNECT_NETWORK_MANAGER | ✅ Full CRUD |
| Any / Law Firm | CARECONNECT_NETWORK_MANAGER | ✅ Full CRUD |
| PlatformAdmin | any | ✅ bypass (admin) |
| TenantAdmin | any | ✅ bypass (admin) |
| Provider | any | ❌ denied |
| Any user without role | — | ❌ denied |

`orgType` is NOT checked — access is purely role-based.

### Route guard

`networks/layout.tsx` calls `requireProductRole(ProductRole.CareConnectNetworkManager)`,
which redirects to `/dashboard` for users without the role.

---

## 4. Referral Management

Unchanged. Existing `/careconnect/referrals` and `/careconnect/referrals/[id]` pages
are unaffected. No modifications to referral routes or APIs.

---

## 5. Network Management

### Domain entities
- `ProviderNetwork` — tenant-scoped network with name, description
- `NetworkProvider` — join entity linking networks to providers

### EF Configuration
- Table: `cc_ProviderNetworks`
- Table: `cc_NetworkProviders`
- Indexes on `(TenantId, Name)` for list queries
- Migration: `AddProviderNetworks`

### Application layer
- `INetworkService` + `NetworkService`
- Tenant scoping enforced in all operations
- Soft-delete: networks are marked `IsDeleted` rather than hard-deleted
- Not-found → `NotFoundException`; duplicate name → validation error

---

## 6. Provider Management

- Add provider to network: POST `/api/networks/{id}/providers/{providerId}`
  - Validates provider exists in the tenant
  - Idempotent (no-op if already in network)
- Remove provider: DELETE `/api/networks/{id}/providers/{providerId}`
- List providers in network: GET `/api/networks/{id}/providers`
  - Returns `ProviderSummary[]` for the network's providers
- Map markers: GET `/api/networks/{id}/providers/markers`
  - Returns `ProviderMarker[]` for the Leaflet map component

---

## 7. Map Integration

The network detail page uses the existing `ProviderMap` component (dynamic import, no SSR).
Markers are fetched server-side from `/api/networks/{id}/providers/markers` and passed as
`initialMarkers` prop. The existing `ProviderMap` props interface is reused without changes.

---

## 8. Dashboard

Existing `/careconnect/dashboard` is unchanged. No new dashboard panels in this block —
network summary stats can be added in a follow-on task.

---

## 9. Test Results

| # | Test | Result | Notes |
|---|------|--------|-------|
| 1 | Network Manager can access `/careconnect/networks` | ✅ Pass | requireProductRole guard |
| 2 | Non-manager cannot access network routes | ✅ Pass | redirects to /dashboard |
| 3 | Law Firm with role CAN access networks | ✅ Pass | role-only check, no orgType |
| 4 | Lien Company without role CANNOT access | ✅ Pass | role check fails → redirect |
| 5 | Create network works | ✅ Pass | POST /api/networks |
| 6 | Update network works | ✅ Pass | PUT /api/networks/{id} |
| 7 | Delete network works | ✅ Pass | DELETE /api/networks/{id} |
| 8 | Add/remove provider works | ✅ Pass | POST/DELETE /api/networks/{id}/providers/{pid} |
| 9 | Network map renders provider markers | ✅ Pass | existing ProviderMap reused |
| 10 | dotnet build succeeds | ✅ Pass | all projects build clean |
| 11 | Common Portal unaffected | ✅ Pass | separate route group |

---

## 10. Issues / Gaps

### 10.1 Network-level referral creation
Referrals are not yet scoped to a specific network. A future task can allow creating
a referral pre-filtered to network providers.

### 10.2 Preferred provider designation
No "preferred provider" flag within a network in this block. Data model supports it
as a future field on `NetworkProvider`.

### 10.3 Network sharing (cross-tenant)
Networks are strictly tenant-scoped. Cross-tenant network sharing is deferred.
