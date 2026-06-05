# BLK-PERF-02 Report — Advanced Performance (Caching, Read Optimisation, Response Efficiency)

**Block:** BLK-PERF-02  
**Window:** TENANT-STABILIZATION (2026-04-23 → 2026-05-07)  
**Preceded by:** BLK-OPS-01 (commit `87ea2f70384aa8f771b404f1bf73ea1ae0c3207f`)  
**Status:** COMPLETE

---

## 1. Summary

BLK-PERF-02 added an `IMemoryCache` caching layer to CareConnect — the first caching
infrastructure for that service. Six endpoint surfaces were targeted based on their
access pattern (high read frequency, low change rate) and measured query cost. Cache
keys are designed with explicit tenant-isolation guarantees and every write path that
mutates cached data performs immediate key eviction, so stale reads are bounded to the
TTL only when no write occurs.

**Build verification:** `dotnet build CareConnect.Api.csproj` → ✅ 0 errors, 0 warnings  
**Test suite:** BuildingBlocks.Tests 29/29 ✅

---

## 2. Cacheable Endpoint Audit

### Pre-BLK-PERF-02 baseline: no caching in CareConnect

| Endpoint | Access pattern | Query cost | Mutated by |
|---|---|---|---|
| `GET /api/public/network/` | Every anonymous page load | 1 DB query (BLK-PERF-01 fixed N+1) | PUT/DELETE network |
| `GET /api/public/network/{id}/providers` | Provider map widget on every mount | 2 queries (existence + list) | POST/DELETE provider |
| `GET /api/public/network/{id}/providers/markers` | Duplicate of providers query | 2 queries | POST/DELETE provider |
| `GET /api/public/network/{id}/detail` | Full-page network detail | 1 large Include() query | PUT/DELETE network, POST/DELETE provider |
| `GET /api/categories` | Called on every CareConnect screen mount for every user | 1 query per request | Platform admin (external path) |
| `GET /api/admin/dashboard` | 6 × `CountAsync` aggregations per dashboard refresh | 6 DB round-trips | Every referral / blocked-provider write |

All six endpoints return data that is:
1. Read far more often than it is written.
2. Not user-specific (all callers with the same scope receive the same payload).
3. Tolerable at slight staleness (seconds, not milliseconds).

---

## 3. Cache Implementation

### 3.1 Mechanism

`Microsoft.Extensions.Caching.Memory.IMemoryCache` — the built-in ASP.NET Core in-process
cache. Chosen over a distributed cache because CareConnect currently runs as a single
replica; a Redis-based drop-in can replace it later with no changes to key names or
invalidation logic (only the DI registration changes).

**Registration** in `CareConnect.Infrastructure/DependencyInjection.cs`:

```csharp
services.AddMemoryCache(options =>
{
    options.SizeLimit = 4096; // max entries; LRU evicted when limit reached
});
```

Every entry declares `Size = 1`, so the limit is an entry count ceiling rather than a
byte budget. At typical tenant counts this allows hundreds of networks across many
tenants to coexist in the cache simultaneously.

### 3.2 Key definitions — `CareConnect.Application.Cache.CareConnectCacheKeys`

New file: `apps/services/careconnect/CareConnect.Application/Cache/CareConnectCacheKeys.cs`

Key schema: `{service}:{scope}:{data-type}:{dimensions}`

| Key helper | Format | Scope |
|---|---|---|
| `PublicNetworkList(tenantId)` | `cc:pub:network:list:{tenantId}` | Per-tenant |
| `PublicNetworkDetail(tenantId, networkId)` | `cc:pub:network:detail:{tenantId}:{networkId}` | Per-tenant, per-network |
| `PublicNetworkProviders(tenantId, networkId)` | `cc:pub:network:providers:{tenantId}:{networkId}` | Per-tenant, per-network |
| `PublicNetworkMarkers(tenantId, networkId)` | `cc:pub:network:markers:{tenantId}:{networkId}` | Per-tenant, per-network |
| `Categories` (const) | `cc:categories` | Platform-wide (no tenant — categories are non-PII classification codes) |
| `AdminDashboard(scopeKey)` | `cc:admin:dashboard:{tenantId\|"platform"}` | Per-scope |

### 3.3 TTL constants — `CareConnectCacheTtl`

| Constant | Value | Rationale |
|---|---|---|
| `PublicNetwork` | 60 s | Network config rarely changes; writes invalidate immediately |
| `Categories` | 300 s | Platform admin only; no tenant-specific data; very stable |
| `AdminDashboard` | 15 s | Near-real-time operational visibility required |

### 3.4 Why caching is applied at the endpoint layer

The Application project (`CareConnect.Application.csproj`) is infrastructure-agnostic
and has no reference to `Microsoft.Extensions.Caching.*`. Caching is injected into
Minimal API endpoint delegates (API layer) rather than service classes to preserve this
clean architecture boundary and to keep Application services fully testable without a
cache mock.

---

## 4. Cache Safety (Tenant Isolation)

**Rule:** every key that scopes to a tenant includes the `tenantId` GUID as a path
segment. Cross-tenant key collisions are architecturally impossible given distinct GUIDs.

**Trust boundary:** cache population occurs only **after** `ValidateTrustBoundaryAndResolveTenantId`
passes (BLK-SEC-02-02: two-layer HMAC-SHA256 validation). A request carrying a spoofed
`X-Tenant-Id` is rejected at layer 2 before any cache factory lambda is entered. There
is no path by which a forged tenant ID could populate or poison a real tenant's cache entry.

**Platform categories:** confirmed to contain classification codes only (no PII, no
tenant-specific data) before being assigned the no-tenant-scope key `cc:categories`.

---

## 5. Cache Invalidation Strategy

### Write paths that evict (NetworkEndpoints.cs)

`IMemoryCache` does not support wildcard removal. The invalidation uses the exhaustive
helper `CareConnectCacheKeys.PublicNetworkInvalidationKeys(tenantId, networkId)` which
yields the four keys known to encode data for that network:

| Write operation | Keys evicted |
|---|---|
| `PUT /api/networks/{id}` | list, detail, providers, markers |
| `DELETE /api/networks/{id}` | list, detail, providers, markers |
| `POST /api/networks/{id}/providers` | list, detail, providers, markers |
| `DELETE /api/networks/{id}/providers/{providerId}` | list, detail, providers, markers |

The list key (`cc:pub:network:list:{tenantId}`) is always included because the summary
list embeds provider counts, which change on any membership mutation.

### Categories

No active invalidation path — category mutations are performed via a separate
platform-admin interface and are not surfaced through a CareConnect endpoint. The
300 s TTL provides acceptable consistency for this low-churn reference set.

### Admin dashboard

No active invalidation — the 15 s TTL provides near-real-time refresh while absorbing
burst refreshes that would otherwise fan-out six parallel CountAsync calls per click.

---

## 6. Response Optimisation

No payload-level compression or `ETag` changes were required for BLK-PERF-02. The
response body content is unchanged from pre-cache. A future BLK-PERF-03 block could
add `OutputCache` middleware with `Vary` by tenant to push caching to the HTTP layer
and enable `304 Not Modified` support.

---

## 7. Redundant Computation Removal

The BLK-PERF-01 work already removed the redundant second `GetNetworkProvidersAsync`
call in the detail handler. BLK-PERF-02 preserves that single Include() path inside
the cache factory; subsequent requests within the TTL window execute zero DB queries
for the detail payload.

---

## 8. Admin Dashboard Optimisation

`GET /api/admin/dashboard` previously ran 6 `CountAsync` calls on every request
(referralToday, referralWeek, openReferrals, blockedToday, blockedWeek,
distinctBlockedUsersToday). With a 15 s cache, repeated dashboard refreshes within
the window cost zero DB queries. The worst-case DB cost (cold cache) is unchanged.

Scope isolation: PlatformAdmin and each TenantAdmin maintain separate entries so
platform-wide aggregates never leak into tenant-scoped views and vice versa.

---

## 9. Validation Results

| Check | Result |
|---|---|
| `dotnet build CareConnect.Api.csproj` | ✅ 0 errors, 0 warnings |
| BuildingBlocks.Tests (29 tests) | ✅ all pass |
| Trust boundary HMAC guard remains pre-cache | ✅ verified in code |
| Tenant key isolation audit | ✅ all tenant-scoped keys include tenantId |
| Cache invalidation on all 4 write paths | ✅ PUT, DELETE network, POST/DELETE provider |

---

## 10. Changed Files

| File | Change |
|---|---|
| `CareConnect.Application/Cache/CareConnectCacheKeys.cs` | **NEW** — key definitions, TTL constants, invalidation helpers |
| `CareConnect.Infrastructure/DependencyInjection.cs` | `AddMemoryCache(SizeLimit=4096)` registered |
| `CareConnect.Api/Endpoints/PublicNetworkEndpoints.cs` | 4 GET handlers wrapped with `GetOrCreateAsync` + `IMemoryCache` injection |
| `CareConnect.Api/Endpoints/NetworkEndpoints.cs` | 4 write handlers evict public keys via `cache.Remove`; `IMemoryCache` injected |
| `CareConnect.Api/Endpoints/CategoryEndpoints.cs` | `GetAllAsync` wrapped with `GetOrCreateAsync` + `IMemoryCache` injection |
| `CareConnect.Api/Endpoints/AdminDashboardEndpoints.cs` | `GetDashboardAsync` wrapped with `GetOrCreateAsync` + `IMemoryCache` injection |

---

## 11. Methods / Endpoints Updated

### PublicNetworkEndpoints.cs
- `MapGet("/")` — list: `GetOrCreateAsync(PublicNetworkList(tenantId), 60 s)`
- `MapGet("/{id}/providers")` — providers: `GetOrCreateAsync(PublicNetworkProviders(tenantId, id), 60 s)`
- `MapGet("/{id}/providers/markers")` — markers: `GetOrCreateAsync(PublicNetworkMarkers(tenantId, id), 60 s)`
- `MapGet("/{id}/detail")` — detail: `GetOrCreateAsync(PublicNetworkDetail(tenantId, id), 60 s)`

### NetworkEndpoints.cs (write paths — invalidation only)
- `MapPut("/{id}")` — evict `PublicNetworkInvalidationKeys(tenantId, id)`
- `MapDelete("/{id}")` — evict `PublicNetworkInvalidationKeys(tenantId, id)`
- `MapPost("/{id}/providers")` — evict `PublicNetworkInvalidationKeys(tenantId, id)`
- `MapDelete("/{id}/providers/{providerId}")` — evict `PublicNetworkInvalidationKeys(tenantId, id)`

### CategoryEndpoints.cs
- `MapGet("/api/categories")` — `GetOrCreateAsync(Categories, 300 s)`

### AdminDashboardEndpoints.cs
- `GetDashboardAsync` — `GetOrCreateAsync(AdminDashboard(scopeKey), 15 s)`

---

## 12. GitHub Commits

- `54713029cb24a2bec6eeab3d7f0acbdb8ca7770a` — BLK-PERF-02: Add IMemoryCache caching layer to CareConnect (public network, categories, admin dashboard); write-path invalidation on network/provider mutations

---

## 13. Issues / Gaps

| # | Description | Severity | Resolution |
|---|---|---|---|
| G1 | `IMemoryCache` is per-process — if CareConnect scales to multiple replicas, each replica has an independent cache; network writes on replica A do not evict on replica B | Low (current deployment is single-replica) | Document upgrade path: replace `AddMemoryCache` with `AddStackExchangeRedisCache` + `IDistributedCache` pattern when horizontal scaling is required |
| G2 | Category cache has no active invalidation path | Accepted | 300 s TTL is acceptable for platform-admin-managed reference data; out-of-band flush can be performed by restarting the process if immediate refresh is needed |
| G3 | Negative results (network not found) are not cached | Intentional | Caching 404 results would require a sentinel value and adds complexity without proportional benefit at current scale |
