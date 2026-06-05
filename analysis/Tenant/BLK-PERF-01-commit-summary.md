# BLK-PERF-01 Commit Summary

**Block:** BLK-PERF-01 — Performance & Query Optimization  
**Window:** TENANT-STABILIZATION (2026-04-23 → 2026-05-07)  
**Service scope:** CareConnect (apps/services/careconnect)

---

## What Changed

### 1. N+1 Query Eliminated — Public Network List

`GET /api/public/network/` previously fetched all networks (1 query) then issued N separate `GetWithProvidersAsync` calls to count providers per network. Replaced with a single `GetAllWithProviderCountAsync` query that projects network fields + `COUNT(NetworkProviders)` using a DB-level sub-query.

New interface method: `INetworkRepository.GetAllWithProviderCountAsync(Guid tenantId, CancellationToken ct)`.

### 2. Redundant Provider Fetch Removed — Network Detail Endpoint

`GET /api/public/network/{id}/detail` called `GetWithProvidersAsync` (which eager-loads providers via `Include`) and then `GetNetworkProvidersAsync` (same data, second round-trip). The second call was removed; providers are now reused from the already-materialized `NetworkProviders` navigation property.

### 3. AsNoTracking Applied to All Read-Only Repository Methods

EF Core change-tracking was removed from all read-only queries across five repositories:

- `ReferralRepository` — 5 read methods
- `ProviderRepository` — all read methods including `BuildBaseQuery` base
- `NetworkRepository` — all read methods
- `AppointmentRepository` — `GetByIdAsync`, `SearchAsync`
- `ActivationRequestRepository` — `GetByIdAsync`, `GetPendingAsync`, `GetByReferralAndProviderAsync`

### 4. Four Composite Indexes Added

All added via EF migration `BLK_PERF_01_PerformanceIndexes`:

| Index | Table | Purpose |
|---|---|---|
| `IX_Referrals_TenantId_Status_CreatedAtUtc` | `cc_Referrals` | Analytics funnel: status+date window queries |
| `IX_BlockedProviderAccessLogs_TenantId_AttemptedAtUtc` | `cc_BlockedProviderAccessLogs` | Dashboard: tenant-scoped rolling-window counts |
| `IX_ActivationRequests_TenantId_Status_CreatedAt` | `cc_ActivationRequests` | Admin queue + analytics: tenant+status+date |
| `IX_NetworkProviders_TenantId_ProviderNetworkId` | `cc_NetworkProviders` | Network provider list: tenant-scoped lookup |

### 5. Referral Pagination Clamped

`GET /api/referrals/` now enforces `Math.Clamp(PageSize, 1, 100)`. Previously there was no server-side upper bound on page size.

### 6. Manual Migration Superseded

`20260422100000_AddProviderNetworks.cs` was a hand-written migration (not EF-tracked, wrong column types for audit fields) that was never reflected in the model snapshot. It has been removed and superseded by the EF-generated `BLK_PERF_01_PerformanceIndexes` migration, which correctly creates the ProviderNetworks tables and all new indexes in a single authoritative migration.

---

## Files Changed

```
apps/services/careconnect/CareConnect.Api/Endpoints/PublicNetworkEndpoints.cs
apps/services/careconnect/CareConnect.Api/Endpoints/ReferralEndpoints.cs
apps/services/careconnect/CareConnect.Application/Repositories/INetworkRepository.cs
apps/services/careconnect/CareConnect.Infrastructure/Data/Configurations/ActivationRequestConfiguration.cs
apps/services/careconnect/CareConnect.Infrastructure/Data/Configurations/BlockedProviderAccessLogConfiguration.cs
apps/services/careconnect/CareConnect.Infrastructure/Data/Configurations/NetworkProviderConfiguration.cs
apps/services/careconnect/CareConnect.Infrastructure/Data/Configurations/ReferralConfiguration.cs
apps/services/careconnect/CareConnect.Infrastructure/Data/Migrations/20260424004212_BLK_PERF_01_PerformanceIndexes.cs (new)
apps/services/careconnect/CareConnect.Infrastructure/Data/Migrations/CareConnectDbContextModelSnapshot.cs
apps/services/careconnect/CareConnect.Infrastructure/Repositories/ActivationRequestRepository.cs
apps/services/careconnect/CareConnect.Infrastructure/Repositories/AppointmentRepository.cs
apps/services/careconnect/CareConnect.Infrastructure/Repositories/NetworkRepository.cs
apps/services/careconnect/CareConnect.Infrastructure/Repositories/ProviderRepository.cs
apps/services/careconnect/CareConnect.Infrastructure/Repositories/ReferralRepository.cs
apps/services/careconnect/CareConnect.Infrastructure/Data/Migrations/20260422100000_AddProviderNetworks.cs (removed)
```

---

## Validation

```
dotnet build CareConnect.Api -c Release   →  Succeeded: 0 errors, 0 warnings
dotnet test CareConnect.Tests -c Release  →  Passed: 491 / Failed: 9 (pre-existing, unrelated)
ef migrations add                         →  BLK_PERF_01_PerformanceIndexes generated cleanly
```
