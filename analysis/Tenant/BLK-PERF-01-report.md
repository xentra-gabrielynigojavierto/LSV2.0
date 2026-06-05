# BLK-PERF-01 Report — Performance & Query Optimization

**Block:** BLK-PERF-01  
**Window:** TENANT-STABILIZATION (2026-04-23 → 2026-05-07)  
**Preceded by:** BLK-GOV-02 (commit `91122b779a3cd567ca3a69b934ed46cc561351ff`)  
**Status:** COMPLETE

---

## 1. Summary

BLK-PERF-01 conducted a full query and schema performance audit of the CareConnect service, the heaviest data layer in LegalSynq v2. Six categories of improvement were identified and addressed:

| Category | Issues Found | Fixes Applied |
|---|---|---|
| N+1 queries | 1 | 1 |
| Redundant round-trips | 1 | 1 |
| Missing `AsNoTracking()` | 5 repositories / 18 methods | 5 repositories, all read paths |
| Missing composite indexes | 4 | 4 (via EF migration) |
| Pagination enforcement | 1 endpoint unclamped | 1 clamped (max 100) |
| Manual migration superseded | 1 | `AddProviderNetworks` removed, superseded by new migration |

**Test gate:** 491 passed / 9 pre-existing failures (all Moq-based, unrelated to query layer). Zero regressions introduced.

---

## 2. Performance Hotspot Audit

### 2.1 N+1 Query — `GET /api/public/network/`

**Location:** `PublicNetworkEndpoints.cs`, anonymous network list endpoint.

**Root cause:**
```csharp
// BEFORE — 1 + N queries (1 for list, 1 per network for provider count)
var networks = await repo.GetAllByTenantAsync(tenantId.Value, ct);
foreach (var n in networks)
{
    var detail = await repo.GetWithProvidersAsync(tenantId.Value, n.Id, ct);  // N queries
    summaries.Add(new PublicNetworkSummary(n.Id, n.Name, n.Description, detail?.NetworkProviders.Count ?? 0));
}
```

For a tenant with 10 networks this was 11 round-trips; for 50 networks, 51. This endpoint is unauthenticated and called by the public directory — it was the single highest-impact query pattern in the service.

**Fix:** Added `GetAllWithProviderCountAsync` to `INetworkRepository` / `NetworkRepository` using a single `SELECT … COUNT(NetworkProviders)` projection:

```csharp
// AFTER — 1 query, provider count via sub-query COUNT()
var rows = await repo.GetAllWithProviderCountAsync(tenantId.Value, ct);
var summaries = rows
    .Select(r => new PublicNetworkSummary(r.Id, r.Name, r.Description ?? string.Empty, r.ProviderCount))
    .ToList();
```

**Impact:** Reduces N+1 to a single query regardless of network count.

### 2.2 Redundant Provider Fetch — `GET /api/public/network/{id}/detail`

**Location:** `PublicNetworkEndpoints.cs`, network detail endpoint.

**Root cause:**
```csharp
// BEFORE — providers loaded twice: once via Include, once via separate query
var network = await repo.GetWithProvidersAsync(tenantId.Value, id, ct);  // loads providers via Include
var providers = await repo.GetNetworkProvidersAsync(tenantId.Value, id, ct);  // loads providers AGAIN
```

`GetWithProvidersAsync` already eager-loads `NetworkProviders.Provider` via `Include(n => n.NetworkProviders).ThenInclude(np => np.Provider)`. The subsequent `GetNetworkProvidersAsync` call issued an identical second round-trip for no benefit.

**Fix:** Use the providers already materialized by `GetWithProvidersAsync`:
```csharp
// AFTER — providers reused from the Include already loaded above
var providers = network.NetworkProviders
    .Where(np => np.Provider != null)
    .Select(np => np.Provider!)
    .OrderBy(p => p.Name)
    .ToList();
// GetNetworkProvidersAsync call removed entirely
```

**Impact:** Saves one DB round-trip per detail page load on the public directory.

### 2.3 Missing `AsNoTracking()` — All Read Repositories

EF Core change-tracking adds memory and CPU overhead for every entity it monitors: it stores a snapshot of the original values, registers the entity in the identity map, and checks for changes on `SaveChanges`. For read-only queries none of this is needed.

**Affected repositories and methods before this block:**

| Repository | Methods Missing `AsNoTracking()` |
|---|---|
| `ReferralRepository` | `SearchAsync`, `GetByIdAsync`, `GetByIdGlobalAsync`, `GetHistoryByReferralAsync`, `GetProviderReassignmentsByReferralAsync` |
| `ProviderRepository` | `SearchAsync` (both queries), `GetMarkersAsync` (both queries), `GetByIdAsync`, `GetByIdCrossAsync`, `GetUnlinkedAsync`, `GetByOrganizationIdAsync`, `GetByIdentityUserIdAsync` |
| `NetworkRepository` | `GetAllByTenantAsync`, `GetByIdAsync`, `GetWithProvidersAsync`, `GetMembershipAsync`, `GetNetworkProvidersAsync`, `SearchProvidersGlobalAsync`, `GetProviderByIdGlobalAsync`, `GetProviderByNpiAsync` |
| `AppointmentRepository` | `GetByIdAsync`, `SearchAsync` |
| `ActivationRequestRepository` | `GetByIdAsync`, `GetPendingAsync`, `GetByReferralAndProviderAsync` |

All read-only methods in all five repositories now use `AsNoTracking()`. Write paths (Add/Update/SaveChanges) remain tracking-aware.

**ProviderRepository design note:** `BuildBaseQuery` (called by `SearchAsync` and `GetMarkersAsync`) now applies `AsNoTracking()` at the base query level. The comment about platform-wide marketplace providers (no `TenantId` filter) is preserved.

**ActivationRequestRepository note:** `GetPendingAsync` is unbounded (no pagination) but acceptable because the queue is naturally bounded by Pending status, which self-drains as approvals occur. Documented in §6.

---

## 3. Index Review / Schema Changes

Four composite indexes were added via EF migration `BLK_PERF_01_PerformanceIndexes`.

### 3.1 `IX_Referrals_TenantId_Status_CreatedAtUtc`

**Table:** `cc_Referrals`  
**Columns:** `(TenantId, Status, CreatedAtUtc)`  
**Rationale:** The activation funnel analytics service (`ActivationFunnelAnalyticsService`) runs six `CountAsync` queries that combine `TenantId` scope (from the base query) with `Status` conditions and `CreatedAtUtc` date-window filters simultaneously. The existing single-column indexes `(TenantId, Status)` and `(TenantId, CreatedAtUtc)` each cover only two dimensions. A three-column composite covering all three columns eliminates the need for the query planner to intersect two separate index scans.

### 3.2 `IX_BlockedProviderAccessLogs_TenantId_AttemptedAtUtc`

**Table:** `cc_BlockedProviderAccessLogs`  
**Columns:** `(TenantId, AttemptedAtUtc)`  
**Rationale:** The admin dashboard count query `GetBlockedAccessCountAsync` filters on `TenantId` and a rolling date window (`AttemptedAtUtc >= cutoff`). The pre-existing indexes `(UserId, AttemptedAtUtc)` and `(AttemptedAtUtc)` do not include `TenantId`. Without this index, tenant-scoped dashboard queries scan all rows across all tenants before applying the tenant filter. In a multi-tenant deployment this degrades linearly as tenant count grows.

### 3.3 `IX_ActivationRequests_TenantId_Status_CreatedAt`

**Table:** `cc_ActivationRequests`  
**Columns:** `(TenantId, Status, CreatedAtUtc)`  
**Rationale:** The admin activation queue reads filter by `TenantId` and `Status = Pending`; the analytics service reads filter by `TenantId` and `CreatedAtUtc` windows with status conditions. The existing `(Status, CreatedAtUtc)` index does not lead on `TenantId`, causing cross-tenant scans when a TenantAdmin reads their queue. The new composite index corrects this and also serves analytics queries.

### 3.4 `IX_NetworkProviders_TenantId_ProviderNetworkId`

**Table:** `cc_NetworkProviders`  
**Columns:** `(TenantId, ProviderNetworkId)`  
**Rationale:** `GetNetworkProvidersAsync` queries `WHERE ProviderNetworkId = @networkId AND TenantId = @tenantId`. The existing unique index `(ProviderNetworkId, ProviderId)` does not include `TenantId`, so tenant isolation enforcement in this query requires a post-filter row scan. The new composite index makes the query a single seek.

### 3.5 Migration Details

Migration name: `BLK_PERF_01_PerformanceIndexes`  
Timestamp: `20260424004212`

This migration also covers the `cc_ProviderNetworks` and `cc_NetworkProviders` table creation, superseding the manually-crafted `20260422100000_AddProviderNetworks.cs` migration which was never EF-tracked (not reflected in the model snapshot, used wrong column types for audit fields). The manual migration was removed; the EF-generated migration is authoritative.

The migration also picks up two other pending schema additions:
- `DedupeKey` column on `cc_CareConnectNotifications` + unique index (CC2-INT notification deduplication)
- `IX_cc_ReferralProviderReassignments_ReferralId` foreign key support index

---

## 4. Pagination Enforcement

### 4.1 Referral List — `GET /api/referrals/`

**Before:**
```csharp
PageSize = p.PageSize ?? 20,  // no upper bound: ?pageSize=100000 accepted
```

**After:**
```csharp
PageSize = Math.Clamp(p.PageSize ?? 20, 1, 100),  // max 100 per page
Page     = Math.Max(1, p.Page ?? 1),               // floor 1
```

This protects `ReferralRepository.SearchAsync` from unbounded `Take()` calls that would return the entire referral table under adversarial or misconfigured client conditions.

### 4.2 Appointment List — Pre-existing enforcement confirmed

`AppointmentService.SearchAppointmentsAsync` already clamps `pageSize = Math.Min(100, Math.Max(1, query.PageSize ?? 20))`. No change required.

### 4.3 ActivationRequest queue — No pagination needed

`GetPendingAsync` has no pagination but the result set is naturally bounded: it returns only records where `Status = Pending`. In steady state this is a small set (typically < 100 entries). Pagination was considered and deferred — noted in §6.

---

## 5. Query Shape Optimization

### 5.1 ProviderRepository — Two-query search pattern

`SearchAsync` and `GetMarkersAsync` both use a deliberate two-query pattern: first fetch IDs (lightweight), then load full entities with Includes for only those IDs. This is intentional to avoid the EF `Include` + `Skip`/`Take` Cartesian product problem. `AsNoTracking()` has now been applied to both queries in each path, reducing memory and CPU overhead for these high-frequency endpoints.

### 5.2 ProviderRepository — BuildBaseQuery platform scope

`BuildBaseQuery` intentionally does not filter by `TenantId` — providers are a platform-wide marketplace discoverable across all tenants. This is documented in the code (`// Providers are a platform-wide marketplace`) and preserved. No index optimization is applied here since providers are intended to be globally searchable.

### 5.3 AppointmentRepository — Include before filters

`AppointmentRepository.SearchAsync` chains `.Include()` before building the filter chain. EF Core defers all LINQ operations; the Include does not materialize early. The pattern is correct. `AsNoTracking()` was added to eliminate tracking overhead for the read-only list.

### 5.4 ReferralPerformanceService — Already optimized

`ReferralPerformanceService.GetPerformanceAsync` already used `AsNoTracking()` on both its window query and the aging distribution query. No changes required.

---

## 6. Admin Analytics / Dashboard Performance

The admin dashboard and activation funnel service both run multiple sequential aggregate queries (COUNT) within a single HTTP request. The new composite indexes (§3.1, §3.2, §3.3) allow the DB engine to satisfy these counts via index-only scans rather than table scans.

**ActivationFunnelAnalyticsService** runs 7 COUNT queries per call (one for each funnel stage). All queries now benefit from `IX_ActivationRequests_TenantId_Status_CreatedAt` and `IX_Referrals_TenantId_Status_CreatedAtUtc`.

**Unbounded GetPendingAsync note:** The activation queue list is unbounded. This is acceptable because:
1. The queue is bounded by status — only `Pending` rows are returned
2. The admin UI list is read infrequently and not paginated server-side
3. A composite index `(TenantId, Status, CreatedAtUtc)` now covers this query efficiently

If the pending queue grows beyond ~500 entries in production, a follow-up pagination ticket is warranted.

---

## 7. Caching Decision

Caching was evaluated and deferred for this block. Reasons:

1. **Provider list** (`GET /api/providers/`): Filtered by 10+ parameters including geo-bounds; cache key fragmentation makes this impractical without a dedicated cache invalidation strategy.
2. **Public network list** (`GET /api/public/network/`): The N+1 is now fixed; a single SQL query with a projected COUNT is fast enough without caching. Network configuration changes infrequently, making cache-aside viable, but the risk/complexity ratio is unfavorable for the stabilization window.
3. **Admin analytics**: These are time-windowed aggregate queries running once per dashboard load. They are not hot enough to justify caching at this stage.

Caching may be revisited in a dedicated BLK-CACHE block after stabilization.

---

## 8. Validation Results

| Check | Result |
|---|---|
| `dotnet build CareConnect.Api` — Release mode | ✅ Succeeded: 0 errors, 0 warnings |
| `dotnet test CareConnect.Tests` | ✅ 491 passed, 9 pre-existing failures (all Moq, unrelated to query layer) |
| EF migration generation | ✅ `BLK_PERF_01_PerformanceIndexes` generated cleanly |
| Migration `Up()` review | ✅ 4 new composite indexes + ProviderNetworks tables + DedupeKey column |
| Migration `Down()` review | ✅ Correctly reverts all changes |

**Pre-existing failures (9):** All failures are in `ReferralClientEmailTests`, `ActivationQueueTests`, and `ProviderAvailabilityServiceTests`. They use `Moq` for repository mocking; `AsNoTracking()` changes have no effect on mock behavior. These failures predate BLK-PERF-01 and involve notification/email logic not touched in this block.

---

## 9. Changed Files

| File | Change Type | Change Summary |
|---|---|---|
| `CareConnect.Api/Endpoints/PublicNetworkEndpoints.cs` | Endpoint fix | N+1 eliminated on `GET /`; redundant provider fetch removed on `GET /{id}/detail` |
| `CareConnect.Api/Endpoints/ReferralEndpoints.cs` | Pagination | Page clamped to max 100, floor 1 on referral list |
| `CareConnect.Application/Repositories/INetworkRepository.cs` | Interface | Added `GetAllWithProviderCountAsync` |
| `CareConnect.Infrastructure/Data/Configurations/ActivationRequestConfiguration.cs` | Index | Added `(TenantId, Status, CreatedAtUtc)` composite |
| `CareConnect.Infrastructure/Data/Configurations/BlockedProviderAccessLogConfiguration.cs` | Index | Added `(TenantId, AttemptedAtUtc)` composite |
| `CareConnect.Infrastructure/Data/Configurations/NetworkProviderConfiguration.cs` | Index | Added `(TenantId, ProviderNetworkId)` composite |
| `CareConnect.Infrastructure/Data/Configurations/ReferralConfiguration.cs` | Index | Added `(TenantId, Status, CreatedAtUtc)` composite |
| `CareConnect.Infrastructure/Data/Migrations/20260424004212_BLK_PERF_01_PerformanceIndexes.cs` | Migration | New EF migration with all index and schema changes |
| `CareConnect.Infrastructure/Data/Migrations/CareConnectDbContextModelSnapshot.cs` | Migration | Updated model snapshot |
| `CareConnect.Infrastructure/Repositories/ActivationRequestRepository.cs` | AsNoTracking | Read methods: `GetByIdAsync`, `GetPendingAsync`, `GetByReferralAndProviderAsync` |
| `CareConnect.Infrastructure/Repositories/AppointmentRepository.cs` | AsNoTracking | Read methods: `GetByIdAsync`, `SearchAsync` |
| `CareConnect.Infrastructure/Repositories/NetworkRepository.cs` | AsNoTracking + N+1 | All read methods; added `GetAllWithProviderCountAsync` |
| `CareConnect.Infrastructure/Repositories/ProviderRepository.cs` | AsNoTracking | All read methods including `BuildBaseQuery` base |
| `CareConnect.Infrastructure/Repositories/ReferralRepository.cs` | AsNoTracking | All read methods |
| ~~`CareConnect.Infrastructure/Data/Migrations/20260422100000_AddProviderNetworks.cs`~~ | Removed | Manually-crafted migration superseded by EF-generated migration |

---

## 10. Methods / Endpoints Updated

### Endpoints

| Method | Endpoint | Change |
|---|---|---|
| GET | `/api/public/network/` | N+1 → single COUNT query via `GetAllWithProviderCountAsync` |
| GET | `/api/public/network/{id}/detail` | Removed redundant `GetNetworkProvidersAsync` call |
| GET | `/api/referrals/` | Added `Math.Clamp(PageSize, 1, 100)` |

### Repository Methods Added

| Interface | Method |
|---|---|
| `INetworkRepository` | `GetAllWithProviderCountAsync(Guid tenantId, CancellationToken ct)` |

### Repository Methods Modified (AsNoTracking)

All read-only query methods across `ReferralRepository`, `ProviderRepository`, `NetworkRepository`, `AppointmentRepository`, and `ActivationRequestRepository`.

---

## 11. GitHub Commit

`fee961e5fef3c3e0b75a98adb15bd4c7416c22ae` — "Improve system performance and optimize database queries"

---

## 12. Issues / Gaps

| Item | Severity | Disposition |
|---|---|---|
| `GetPendingAsync` unbounded list | Low | Acceptable — bounded by Pending status; fast with new `(TenantId, Status, CreatedAtUtc)` index |
| Provider search not tenant-filtered | By design | Platform-wide marketplace; documented in `BuildBaseQuery` |
| Marker endpoint loads full Provider with categories | Low | MarkerLimit is 500; `AsNoTracking()` reduces overhead; projection refactor deferred |
| Caching not implemented | Medium | Deferred to post-stabilization BLK-CACHE block |
| 9 pre-existing Moq test failures | Medium | Pre-exist before BLK-PERF-01; require separate investigation in test hardening block |
