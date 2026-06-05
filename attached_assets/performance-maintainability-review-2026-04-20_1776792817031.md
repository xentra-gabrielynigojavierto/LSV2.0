# Performance & Maintainability Code Review — LegalSynq v2.0

**Reviewed by:** GitHub Copilot (code-review-excellence skill)
**Date:** April 20, 2026
**Focus:** Performance & Maintainability
**Scope:** `apps/services/identity`, `apps/services/careconnect`, `apps/services/notifications`, `apps/services/flow`, `apps/web`, `apps/control-center`

---

## Summary Table

| # | Severity | Category | Issue | File |
|---|----------|----------|-------|------|
| 1 | 🟠 High | Performance | Sequential DB queries in `ComputeEffectiveAccessAsync` (should be parallel) | `Identity.Infrastructure/Services/EffectiveAccessService.cs` |
| 2 | 🟠 High | Performance | 7 sequential `COUNT` queries in `ActivationFunnelAnalyticsService` | `CareConnect.Infrastructure/Services/ActivationFunnelAnalyticsService.cs` |
| 3 | 🟠 High | Maintainability | `NotificationService.cs` god class (~1500 lines, 10+ responsibilities) | `Notifications.Infrastructure/Services/NotificationService.cs` |
| 4 | 🟠 High | Performance | Fan-out notification dispatch is fully sequential per recipient | `Notifications.Infrastructure/Services/NotificationService.cs` |
| 5 | 🟡 Medium | Performance | `JsonSerializerOptions` allocated per call in hot paths | `NotificationService.cs`, `AutomationExecutor.cs`, `FormulaEvaluator.cs` |
| 6 | 🟡 Medium | Performance | `GetAllWithRolesAsync` / `GetByTenantWithRolesAsync` — unbounded, no pagination | `Identity.Infrastructure/Repositories/UserRepository.cs` |
| 7 | 🟡 Medium | Performance | `GET /api/users` fires 3 sequential DB round-trips | `Identity.Api/Endpoints/UserEndpoints.cs` |
| 8 | 🟡 Medium | Performance | `useNavBadges` polls full referral search for a count badge every 30s | `apps/web/src/hooks/use-nav-badges.ts` |
| 9 | 🟡 Medium | Maintainability | `BuildEventTimelineAsync` duplicated between `GetEventsAsync` and private helper | `Notifications.Infrastructure/Services/NotificationService.cs` |
| 10 | 🟡 Medium | Maintainability | `ComputeEffectiveAccessAsync` is a 300+ line method doing too much | `Identity.Infrastructure/Services/EffectiveAccessService.cs` |
| 11 | 🟢 Low | Performance | `GetPrimaryOrgMembershipAsync` over-fetches via deeply nested 2-chain Include | `Identity.Infrastructure/Repositories/UserRepository.cs` |
| 12 | 🟢 Low | Maintainability | Migration recovery guard as 100+ lines of raw SQL in `Program.cs` startup | `Identity.Api/Program.cs` |

---

## 🟠 HIGH

---

### 1. Sequential DB Queries in `ComputeEffectiveAccessAsync` — Should Run in Parallel

**File:** `apps/services/identity/Identity.Infrastructure/Services/EffectiveAccessService.cs`
**Impact:** Every login and every `/auth/me` call on a cache miss hits this path. 6 independent queries execute sequentially rather than concurrently.

```csharp
// ❌ Current: 6 sequential DB round-trips on the critical login path
var activeEntitlements = await _db.TenantProducts...ToListAsync(ct);
var isTenantAdmin = await _db.ScopedRoleAssignments.AnyAsync(..., ct);
var directProducts = await _db.UserProductAccessRecords...ToListAsync(ct);
var activeGroupIds = await _db.AccessGroupMemberships...ToListAsync(ct);
```

The first four queries (`activeEntitlements`, `isTenantAdmin`, `directProducts`, `activeGroupIds`) are completely independent of each other — they share no data dependency. On a 5ms DB round-trip each, that is **at minimum 20ms of serialized network time** before any computation can begin.

**Fix:** Parallelize independent queries with `Task.WhenAll`:

```csharp
// ✅ Fire all independent queries concurrently
var entitlementsTask   = _db.TenantProducts
    .Where(tp => tp.TenantId == tenantId && tp.IsEnabled)
    .Select(tp => tp.Product.Code)
    .ToListAsync(ct);

var isTenantAdminTask  = _db.ScopedRoleAssignments
    .AnyAsync(s => s.UserId == userId && s.IsActive
        && s.ScopeType == ScopedRoleAssignment.ScopeTypes.Global
        && s.Role.Name == "TenantAdmin", ct);

var directProductsTask = _db.UserProductAccessRecords
    .Where(a => a.TenantId == tenantId && a.UserId == userId && a.AccessStatus == AccessStatus.Granted)
    .Select(a => a.ProductCode)
    .ToListAsync(ct);

var activeGroupIdsTask = _db.AccessGroupMemberships
    .Where(m => m.TenantId == tenantId && m.UserId == userId && m.MembershipStatus == MembershipStatus.Active)
    .Select(m => m.GroupId)
    .ToListAsync(ct);

await Task.WhenAll(entitlementsTask, isTenantAdminTask, directProductsTask, activeGroupIdsTask);

var activeEntitlements = await entitlementsTask;
var isTenantAdmin      = await isTenantAdminTask;
var directProducts     = await directProductsTask;
var activeGroupIds     = await activeGroupIdsTask;
```

> **Note:** EF Core does **not** allow concurrent queries on the same `DbContext` instance. Use this pattern only if the app is configured with one `DbContext` per request (which it is via scoped DI). Alternatively, create a second `DbContext` scope for the parallel tasks.
> A simpler approach is to keep EF safety: only parallelize the two tasks that don't require a shared `DbContext` scope, e.g., `isTenantAdmin` via `AnyAsync` and `activeEntitlements` via a second injected context instance.

---

### 2. Seven Sequential `CountAsync` Queries in `ActivationFunnelAnalyticsService`

**File:** `apps/services/careconnect/CareConnect.Infrastructure/Services/ActivationFunnelAnalyticsService.cs`
**Impact:** Every funnel analytics request fires 7 separate `COUNT(*)` queries against 2 tables. Total wall time ~30–50ms of serialized network on an idle DB.

```csharp
// ❌ 7 sequential CountAsync calls to the same tables in the same date window
var referralsSent          = await _db.Referrals.CountAsync(..., ct);
var referralsAccepted      = await _db.Referrals.CountAsync(..., ct);
var activationStarted      = await _db.ActivationRequests.CountAsync(..., ct);
var autoProvisionSucceeded = await _db.ActivationRequests.CountAsync(..., ct);
var adminApproved          = await _db.ActivationRequests.CountAsync(..., ct);
var fallbackPending        = await _db.ActivationRequests.CountAsync(..., ct);
// + one more inside ComputeCountsAsync
```

**Fix Option A — Parallelize with `Task.WhenAll`** (quick, no DB schema change):

```csharp
// ✅ All 7 counts fire at the same time
var t1 = _db.Referrals.CountAsync(r => r.CreatedAtUtc >= from && r.CreatedAtUtc < to, ct);
var t2 = _db.Referrals.CountAsync(r => acceptedStatuses.Contains(r.Status)
    && r.CreatedAtUtc >= from && r.CreatedAtUtc < to, ct);
var t3 = _db.ActivationRequests.CountAsync(a => a.CreatedAtUtc >= from && a.CreatedAtUtc < to, ct);
// ... etc.
await Task.WhenAll(t1, t2, t3, ...);
```

> Same EF Core single-context caveat applies. Use separate scoped contexts or switch to a raw SQL aggregation query.

**Fix Option B — Single aggregation query** (best performance):

```sql
SELECT
  COUNT(*) FILTER (WHERE source = 'referral')                 AS referralsSent,
  COUNT(*) FILTER (WHERE source = 'referral' AND accepted)    AS referralsAccepted,
  COUNT(*) FILTER (WHERE source = 'activation')               AS activationStarted,
  -- ...
FROM (combined CTE)
```

---

### 3. `NotificationService.cs` Is a God Class (~1500+ Lines, 10+ Responsibilities)

**File:** `apps/services/notifications/Notifications.Infrastructure/Services/NotificationService.cs`
**Impact:** Maintainability, testability, change isolation.

A single class currently handles:
- Single notification dispatch (`DispatchSingleAsync`)
- Fan-out dispatch (`DispatchFanOutAsync`)
- Retry logic (`RetryAsync`, `ProcessAutoRetryAsync`)
- Resend logic (`ResendAsync`)
- Admin operations (`AdminRetryAsync`, `AdminResendAsync`, `AdminListPagedAsync`, etc.)
- Event timeline building (`BuildEventTimelineAsync`, `GetEventsAsync`)
- Stats aggregation (`GetStatsAsync`, `AdminGetStatsAsync`)
- Stalled notification reconciliation (`ReconcileStalledAsync`)
- Template rendering coordination
- Metering calls

This violates the Single Responsibility Principle and makes the class impossible to understand in a single read or safely modify without side effects.

**Fix:** Decompose by responsibility. Suggested split:

| New Class | Responsibility |
|-----------|----------------|
| `NotificationDispatchService` | Single dispatch, fan-out, send loop |
| `NotificationRetryService` | Retry, resend, auto-retry, reconcile stalled |
| `NotificationQueryService` | List, get, stats (tenant + admin) |
| `NotificationEventService` | Event timeline, issue tracking |
| `NotificationAdminService` | Admin cross-tenant operations |

---

### 4. Fan-Out Notification Dispatch Is Fully Sequential Per Recipient

**File:** `apps/services/notifications/Notifications.Infrastructure/Services/NotificationService.cs`

```csharp
// ❌ Each recipient dispatched one-at-a-time — O(n) sequential network I/O
foreach (var r in resolved)
{
    var skipReason = ClassifySkipReason(request.Channel, r);
    if (skipReason != null) { ...; continue; }

    var perRequest     = ClonePerRecipient(request, r);
    var perRecipientJson = JsonSerializer.Serialize(perRequest.Recipient);
    var dispatchResult = await DispatchSingleAsync(tenantId, perRequest, perRecipientJson); // blocks
    dispatched.Add(dispatchResult);
    ...
}
```

For a fan-out to 50 recipients, each taking ~100ms (template render + provider call), this loop takes **~5 seconds** serially.

**Fix:** Dispatch concurrently with bounded parallelism:

```csharp
// ✅ Bounded concurrency — e.g. max 8 in-flight at once
var semaphore = new SemaphoreSlim(initialCount: 8);
var tasks = resolved.Where(r => ClassifySkipReason(request.Channel, r) == null)
    .Select(async r =>
    {
        await semaphore.WaitAsync(ct);
        try
        {
            var perRequest       = ClonePerRecipient(request, r);
            var perRecipientJson = JsonSerializer.Serialize(perRequest.Recipient);
            return await DispatchSingleAsync(tenantId, perRequest, perRecipientJson);
        }
        finally { semaphore.Release(); }
    });

var dispatched = await Task.WhenAll(tasks);
```

---

## 🟡 MEDIUM

---

### 5. `JsonSerializerOptions` Allocated Per-Call in Hot Paths

**Files:**
- `Notifications.Infrastructure/Services/NotificationService.cs` (line ~1342)
- `Flow.Application/Services/AutomationExecutor.cs` (line ~397)
- `Reports.Application/Formulas/FormulaEvaluator.cs`, `FormattingConfig.cs`, `FormulaValidator.cs`

```csharp
// ❌ New JsonSerializerOptions allocated on every call — allocates and JIT-compiles metadata each time
var camelOpts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

// ❌ Also in AutomationExecutor within a per-action retry loop:
return JsonSerializer.Deserialize<T>(configJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
```

`JsonSerializerOptions` is expensive to construct — it triggers reflection-based type metadata generation. The ASP.NET Core `System.Text.Json` docs explicitly state: *"Creating a new JsonSerializerOptions instance is expensive. Cache and reuse instances."*

Note: `AutomationExecutor` already has a static `private static readonly JsonSerializerOptions JsonOptions`, but the `ParseConfig` fallback in `AutomationExecutor.cs:397` bypasses it.

**Fix:** Use `static readonly` fields:

```csharp
// ✅ Allocated once, shared across all calls
private static readonly JsonSerializerOptions CamelCaseOptions = new()
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
};

private static readonly JsonSerializerOptions CaseInsensitiveOptions = new()
{
    PropertyNameCaseInsensitive = true
};
```

---

### 6. `GetAllWithRolesAsync` / `GetByTenantWithRolesAsync` — No Pagination

**File:** `apps/services/identity/Identity.Infrastructure/Repositories/UserRepository.cs`

```csharp
// ❌ Loads ALL users with full role navigation properties — unbounded memory allocation
public Task<List<User>> GetAllWithRolesAsync(CancellationToken ct = default) =>
    _db.Users
        .Include(u => u.ScopedRoleAssignments.Where(s => s.IsActive))
            .ThenInclude(s => s.Role)
        .OrderBy(u => u.LastName)
        .ThenBy(u => u.FirstName)
        .ToListAsync(ct);  // No LIMIT
```

`GET /api/admin/users` in the admin panel calls `GetAllWithRolesAsync` with no cap. A tenant with 500 users loads all 500 into memory with their full role subgraph before serialization. This will degrade proportionally with tenant size and eventually cause memory pressure in the identity service.

**Fix:** Add `skip` / `take` parameters and push work into the query:

```csharp
// ✅ Paginated, projection-only (no tracked navigation graphs for reads)
public Task<List<UserSummaryDto>> GetByTenantPagedAsync(
    Guid tenantId, int skip, int take, CancellationToken ct = default) =>
    _db.Users
        .AsNoTracking()
        .Where(u => u.TenantId == tenantId)
        .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
        .Skip(skip).Take(take)
        .Select(u => new UserSummaryDto
        {
            Id        = u.Id,
            Email     = u.Email,
            FirstName = u.FirstName,
            LastName  = u.LastName,
            Roles     = u.ScopedRoleAssignments
                .Where(s => s.IsActive)
                .Select(s => s.Role.Name)
                .ToList(),
        })
        .ToListAsync(ct);
```

---

### 7. `GET /api/users` Fires 3 Sequential DB Round-Trips

**File:** `apps/services/identity/Identity.Api/Endpoints/UserEndpoints.cs`

```csharp
// ❌ Round-trip 1: all users for tenant
var users = await userService.GetByTenantAsync(tenantId, ct);
var userIds = users.Select(u => u.Id).ToList();

// ❌ Round-trip 2: group counts dictionary
var groupCounts = await db.AccessGroupMemberships...ToDictionaryAsync(..., ct);

// ❌ Round-trip 3: product counts dictionary
var productCounts = await db.UserProductAccessRecords...ToDictionaryAsync(..., ct);
```

After `GetByTenantAsync` completes and materializes all users, the two count queries are **independent** — neither depends on the other. They serialize unnecessarily.

**Fix:** Fire them in parallel after the user list is loaded:

```csharp
var users   = await userService.GetByTenantAsync(tenantId, ct);
var userIds = users.Select(u => u.Id).ToList();

// ✅ Fire independent count queries concurrently
var groupCountsTask = db.AccessGroupMemberships
    .Where(am => userIds.Contains(am.UserId) && am.MembershipStatus == MembershipStatus.Active)
    .GroupBy(am => am.UserId)
    .Select(g => new { userId = g.Key, count = g.Count() })
    .ToDictionaryAsync(x => x.userId, x => x.count, ct);

var productCountsTask = db.UserProductAccessRecords
    .Where(upa => userIds.Contains(upa.UserId) && upa.AccessStatus == AccessStatus.Granted)
    .GroupBy(upa => upa.UserId)
    .Select(g => new { userId = g.Key, count = g.Count() })
    .ToDictionaryAsync(x => x.userId, x => x.count, ct);

await Task.WhenAll(groupCountsTask, productCountsTask);
var groupCounts   = await groupCountsTask;
var productCounts = await productCountsTask;
```

---

### 8. `useNavBadges` Polls Full Referral Search Query for a Simple Count Badge

**File:** `apps/web/src/hooks/use-nav-badges.ts`

```typescript
// ❌ Full search query executed every 30 seconds just to read totalCount
const { data } = await careConnectApi.referrals.search({
  status: 'New',
  page: 1,
  pageSize: 1,   // pageSize: 1 but still triggers full count query + 1 row hydration
});
setBadges(prev => ({ ...prev, newReferrals: data.totalCount ?? 0 }));
```

This executes a full paginated referral search (`SELECT ... WHERE status='New' LIMIT 1` + a `COUNT(*)` for `totalCount`) every 30 seconds for every active provider user. The full query path involves routing through Next.js → Gateway → CareConnect service → MySQL, all to read a single integer.

**Fix Option A:** Add a dedicated lightweight count endpoint:

```typescript
// ✅ Dedicated count endpoint — e.g. GET /careconnect/referrals/count?status=New
const { data } = await careConnectApi.referrals.count({ status: 'New' });
setBadges(prev => ({ ...prev, newReferrals: data.count ?? 0 }));
```

**Fix Option B:** Increase the poll interval to 60s (or switch to a push-based mechanism like SSE or a WebSocket subscription if real-time accuracy matters). The current 30s interval with a full search query is unnecessarily aggressive.

---

### 9. `BuildEventTimelineAsync` Logic Duplicated Between `GetEventsAsync` and Private Helper

**File:** `apps/services/notifications/Notifications.Infrastructure/Services/NotificationService.cs`

The `GetEventsAsync` (public, tenant-scoped) and `BuildEventTimelineAsync` (private, used by admin endpoints) contain nearly identical code for building notification event timelines — synthesizing `created`, `attempted`, `sent`/`attempt_failed`, and provider webhook events. Both methods:
1. Add a synthetic `created` event
2. Loop over `attempts` from `_attemptRepo`
3. Loop over `providerEvents` from `_eventRepo`
4. Add a terminal status event

The public `GetEventsAsync` method is **not** calling `BuildEventTimelineAsync`, meaning any bug fix or behavioral change must be made in two places.

**Fix:** Have `GetEventsAsync` delegate to `BuildEventTimelineAsync` after the ownership check:

```csharp
// ✅ GetEventsAsync — single path, no duplication
public async Task<List<NotificationEventDto>> GetEventsAsync(Guid tenantId, Guid id)
{
    var notification = await _notificationRepo.GetByIdAndTenantAsync(id, tenantId);
    if (notification == null) return new List<NotificationEventDto>();

    return await BuildEventTimelineAsync(notification);  // reuse shared logic
}
```

---

### 10. `ComputeEffectiveAccessAsync` Is a 300+ Line Method

**File:** `apps/services/identity/Identity.Infrastructure/Services/EffectiveAccessService.cs`

The `ComputeEffectiveAccessAsync` method is approximately 300 lines long and covers:
1. Loading tenant entitlements
2. Checking TenantAdmin status
3. Loading direct product grants
4. Loading group memberships and inherited grants
5. Computing the effective product set
6. Applying legacy default access fallback
7. Loading and resolving roles (direct + inherited + TenantAdmin auto-grant)
8. Computing permissions via `ResolvePermissionsAsync`

Each of these is a distinct sub-concern. When a regression occurs (e.g., wrong roles computed), it is hard to isolate which stage introduced the bug.

**Fix:** Extract each logical stage into small, clearly-named private methods:

```csharp
// ✅ Method structure becomes readable at a glance
private async Task<EffectiveAccessResult> ComputeEffectiveAccessAsync(Guid tenantId, Guid userId, CancellationToken ct)
{
    var context = await LoadAccessContextAsync(tenantId, userId, ct);
    var products = ResolveEffectiveProducts(context);
    var roles    = await ResolveEffectiveRolesAsync(context, products, ct);
    var (permissions, permissionSources) = await ResolvePermissionsAsync(...);

    return BuildResult(products, roles, permissions, ...);
}
```

---

## 🟢 LOW

---

### 11. `GetPrimaryOrgMembershipAsync` — Deeply Nested Two-Chain Include

**File:** `apps/services/identity/Identity.Infrastructure/Repositories/UserRepository.cs`

```csharp
// ❌ Two independent 4-level Include chains — potential for very wide JOIN result sets
.Include(m => m.Organization)
    .ThenInclude(o => o.OrganizationProducts)
        .ThenInclude(op => op.Product)
            .ThenInclude(p => p.ProductRoles)
                .ThenInclude(pr => pr.OrgTypeRules)
                    .ThenInclude(r => r.OrganizationType)
.Include(m => m.Organization)
    .ThenInclude(o => o.OrganizationTypeRef)
```

This query is called on every login path to get org context for the JWT. A 4-level eagerly-loaded chain generates a large JOIN result that hydrates many related entities even if only `org_id` and `org_type` are ultimately needed for the JWT claim.

**Fix:** Use a projection `Select()` to fetch only the fields actually needed for JWT generation rather than hydrating the full navigation tree:

```csharp
// ✅ Project directly to what the JWT needs — single flat query
.Select(m => new
{
    OrgId                = m.Organization.Id,
    OrgType              = m.Organization.OrgType,
    OrganizationTypeId   = m.Organization.OrganizationTypeId,
    ProviderMode         = m.Organization.ProviderMode,
})
.FirstOrDefaultAsync(ct);
```

---

### 12. Migration Recovery Guard as 100+ Lines of Raw SQL in `Program.cs`

**File:** `apps/services/identity/Identity.Api/Program.cs`

The startup guard that detects and heals a partially-applied migration (`AddTenantPermissionCatalog`) is implemented as 100+ lines of inline raw SQL and `DbConnection` commands directly in `Program.cs`. While functionally sound, this:
- Makes the startup sequence harder to read and reason about
- Mixes infrastructure migration concerns into API bootstrapping code
- Cannot be unit-tested in isolation
- Will be replicated if more partial-migration guards are ever needed

**Fix:** Extract to a `StartupMigrationGuard` or `DatabaseStartupValidator` class with a single `TryHealAsync(IServiceProvider)` method called from `Program.cs`. This keeps startup lean and the guard independently testable.

---

## What's Done Well ✅

- **`EffectiveAccessService` caches results** using `AccessVersion` as the cache key — correct and efficient. A version increment automatically busts stale entries.
- **`ReferralPerformanceCalculator` separates pure computation from DB loading** — `EffectiveAccessService` and `ReferralPerformanceCalculator` are both static/pure computation classes that are independently testable without EF.
- **`AsNoTracking()` used consistently on read-only queries** throughout CareConnect, Flow, and Identity repositories — correct and avoids unnecessary change-tracking overhead.
- **`HashSet<string>` used instead of `List.Contains()` for membership checks** in `EffectiveAccessService` (e.g., `entitlementSet`, `effectiveProductSet`) — correct O(1) vs. O(n) choice.
- **`useNavBadges` cleans up its `setInterval` correctly** — `return () => clearInterval(id)` is present, preventing memory leaks on unmount.
- **`JsonSerializerOptions` cached as `static readonly` in `WorkflowRuleEvaluator` and `NotificationsProducerClient`** — the pattern exists and is used in several places, just not applied consistently everywhere.
- **`AuditEventQueryService` runs paginated query and time-range aggregate in parallel** — noted explicitly in a comment and correctly implemented.
- **`IMemoryCache` with bounded size (`Size = 1`)** used in `EffectiveAccessService` — prevents unbounded cache growth.
- **Cancellation tokens threaded consistently** through all async paths in all services reviewed.
