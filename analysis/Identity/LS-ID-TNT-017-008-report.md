# LS-ID-TNT-017-008 — Audit Analytics

## 1. Executive Summary

Implements an Audit Analytics layer on top of the existing append-only audit event store. Provides aggregated operational views — volume trends, category/severity breakdowns, top actors/tenants, governance activity summaries — via a new `GET /audit/analytics/summary` backend endpoint and a dedicated `/synqaudit/analytics` frontend page in Control Center. All existing audit query, ingestion, correlation, and viewer behavior is fully preserved.

---

## 2. Codebase Analysis

### Audit service (apps/services/audit/)
- **Table:** `aud_AuditEventRecords` (MySQL 8 / SQLite in dev)  
- **Primary key:** `Id` (bigint AUTO_INCREMENT) — clustered, efficient for range scans  
- **Public id:** `AuditId` (char 36) — unique index  
- **Repository pattern:** `IAuditEventRecordRepository` → `EfAuditEventRecordRepository` — uses `IDbContextFactory<AuditEventDbContext>`  
- **Query service:** `IAuditEventQueryService.QueryAsync` — pagination + filter, no aggregation  
- **Authorization:** `IQueryAuthorizer.Authorize(caller, query)` — mutates query to enforce tenant scope  
- **No analytics endpoints exist** prior to this ticket

### Indexes relevant to analytics
| Index | Column(s) | Use case |
|-------|-----------|----------|
| `IX_AuditEventRecords_OccurredAtUtc` | OccurredAtUtc | time-range scans and GROUP BY date |
| `IX_AuditEventRecords_EventCategory` | EventCategory | category breakdown |
| `IX_AuditEventRecords_EventType` | EventType | top event types |
| `IX_AuditEventRecords_TenantId` | TenantId | tenant-scoped queries |
| `IX_AuditEventRecords_ActorId` | ActorId | top actors |
| `IX_AuditEventRecords_RecordedAtUtc` | RecordedAtUtc | secondary time range |
| `IX_AuditEventRecords_VisibilityScope` | VisibilityScope | visibility enforcement |

All analytic GROUP BY queries hit at least one indexed column. All queries are bounded by a mandatory date range to avoid full-table scans.

### Enums
- **EventCategory:** Security, Access, Business, Administrative, System, Compliance, DataChange, Integration, Performance (9 values, stored as tinyint)
- **SeverityLevel:** Debug, Info, Notice, Warn, Error, Critical, Alert (7 values, stored as tinyint)
- **ActorType:** User, ServiceAccount, System, Api, Scheduler, Anonymous, Support

---

## 3. Existing Audit Query / Storage Analysis

### Strengths for analytics
- All group dimensions are indexed — category, eventType, tenantId, actorId, occurredAtUtc
- EF Core's `GroupBy` translates to SQL `GROUP BY` via Pomelo for MySQL
- Mandatory date-range parameter prevents unbounded full-table scans
- `IDbContextFactory<AuditEventDbContext>` pattern is already established — analytics service reuses it

### Limitations / caveats
- No materialized views or pre-aggregated tables — analytics runs live against the event store
- `TagsJson` is raw JSON text — tag-based analytics not feasible without schema change
- Hourly granularity is possible but adds cost; day-level is safer for the default window
- In dev (SQLite in-memory), date arithmetic works differently — used EF Core's `EF.Functions` where needed

---

## 4. Analytics Use Case Inventory

| # | Operator Question | Grouping Field | Supported? | v1? |
|---|------------------|---------------|-----------|-----|
| 1 | How many events occurred each day? | OccurredAtUtc (date) | ✅ | ✅ |
| 2 | Which event categories are most active? | EventCategory | ✅ | ✅ |
| 3 | Which event types fire most often? | EventType | ✅ | ✅ |
| 4 | What is the severity distribution? | Severity | ✅ | ✅ |
| 5 | Which actors generate the most events? | ActorId + ActorName | ✅ | ✅ |
| 6 | Which tenants are most active? (PA only) | TenantId | ✅ | ✅ |
| 7 | How many access denials occurred? | EventType prefix "*.denied" | ✅ | ✅ (via Security category filter) |
| 8 | Governance activity? (holds, exports) | EventType prefix | ✅ | ✅ (as dedicated metric) |
| 9 | Are denial counts spiking? | time + denied filter | ✅ | partial (count, not alerting) |
| 10 | Tag-based grouping | TagsJson | ❌ | ❌ (raw JSON) |
| 11 | ML anomaly detection | — | ❌ | ❌ (out of scope) |

---

## 5. Analytics Model Design

### V1 scope: `AuditAnalyticsSummaryResponse`
Single composite endpoint (`GET /audit/analytics/summary`) returns all analytics in one round-trip:

- **VolumeByDay** — count per calendar day in requested window
- **ByCategory** — count per EventCategory (all 9 values)
- **BySeverity** — count per SeverityLevel (all 7 values)
- **TopEventTypes** — top 15 event types by count
- **TopActors** — top 10 actors by count (actorId + name)
- **TopTenants** — top 10 tenants by count (platform admin only; null otherwise)
- **TotalEvents** — total count in window
- **SecurityEventCount** — count where EventCategory = Security
- **DenialEventCount** — count where EventType contains "denied" OR ".deny" (signal)
- **GovernanceEventCount** — count where EventType starts with "audit.legal_hold" OR "audit.integrity" OR "audit.log.exported"

### Filter model
- **From, To** — required; default: last 30 days; maximum: 90 days enforced
- **TenantId** — platform admin: optional (null = all tenants); tenant caller: forced to own tenant
- **Category** — optional; filters the entire summary to one category

### Performance guardrails
- Mandatory date range (From/To enforced, max 90 days)
- All queries hit indexed columns
- Each grouping query is independent and fast
- TopActors/TopTenants: TOP 10 SQL LIMIT
- TopEventTypes: TOP 15 SQL LIMIT
- All queries use `AsNoTracking()`

---

## 6. Query / API Strategy

### Endpoint
```
GET /audit/analytics/summary
  ?from=2026-03-20T00:00:00Z
  &to=2026-04-19T23:59:59Z
  &tenantId=...   (platform admin only)
  &category=...   (optional, e.g. "Security")
```

### Authorization
- Reuses `AuthorizeQuery(probeQuery)` probe pattern from `AuditEventQueryController`
- Platform admin: may pass `tenantId` or omit for cross-tenant
- Tenant-scoped caller: `callerTenantId` overrides request `tenantId`

### Query structure
Each of the 7 analytics sub-queries runs as an independent `GroupBy` on indexed fields, bounded by the same `From/To` and `TenantId` predicate.

Scalar totals (TotalEvents, SecurityEventCount, DenialEventCount, GovernanceEventCount) use `CountAsync` with appropriate Where predicates.

---

## 7. Files Changed

### New backend files
- `DTOs/Analytics/AuditAnalyticsSummaryRequest.cs`
- `DTOs/Analytics/AuditAnalyticsSummaryResponse.cs`
- `DTOs/Analytics/` — 7 sub-DTO files (VolumeByDay, ByCategory, BySeverity, TopEventType, TopActor, TopTenant, GovernanceSummary)
- `Services/IAuditAnalyticsService.cs`
- `Services/AuditAnalyticsService.cs`
- `Controllers/AuditAnalyticsController.cs`

### Modified backend files
- `Program.cs` — register `IAuditAnalyticsService`

### New frontend files
- `apps/control-center/src/app/synqaudit/analytics/page.tsx`
- `apps/control-center/src/components/synqaudit/audit-analytics-dashboard.tsx`

### Modified frontend files
- `apps/control-center/src/types/control-center.ts` — add analytics types
- `apps/control-center/src/lib/control-center-api.ts` — add `auditAnalytics.getSummary()`
- `apps/control-center/src/lib/nav.ts` — add Analytics nav item to SYNQAUDIT section

---

## 8. Backend Implementation

*See implementation sections below — updated after code is written.*

### AuditAnalyticsService
Uses `IDbContextFactory<AuditEventDbContext>` directly. All queries are scoped by (optional) TenantId + mandatory date range. Each sub-query is a separate EF Core GroupBy or CountAsync call.

### AuditAnalyticsController
Route: `GET /audit/analytics/summary`. Reuses the same `AuthorizeQuery` + caller-context pattern as `AuditEventQueryController`. Returns `ApiResponse<AuditAnalyticsSummaryResponse>`.

---

## 9. Frontend / UI Implementation

### /synqaudit/analytics page
Server component fetches summary on load, passes to client `AuditAnalyticsDashboard` component.

#### Sections
1. **Filter bar** — date picker (from/to), category selector, optional tenant selector (platform admin)
2. **KPI cards** — Total Events, Security Events, Denials, Governance actions
3. **Volume trend** — bar/segment chart by day
4. **Category + Severity breakdown** — two side-by-side tables with percentage bars
5. **Top Event Types** — table, top 15
6. **Top Actors** — table, top 10
7. **Top Tenants** — table, top 10 (platform admin only)

#### Investigation integration
- "View in investigation" links on category breakdown rows → `/synqaudit/investigation?category=X`
- "View in investigation" links on top event type rows → `/synqaudit/investigation?eventType=X`
- "View actor" links on top actor rows → `/synqaudit/investigation?actorId=X`

---

## 10. Verification / Testing Results

- Backend build: ✅ 0 errors (2 harmless XML-doc warnings)
- Frontend tsc: ✅ 0 errors
- Application restart: ✅ serving on dev proxy
- `/synqaudit/analytics` route: ✅ auth guard active (redirects to login when unauthenticated)
- Backend endpoint `GET /audit/analytics/summary`: ✅ registered, authorization probe active
- Nav item "Analytics" visible in SYNQAUDIT section: ✅
- All 7 analytics sub-queries: ✅ EF Core GroupBy on indexed fields, bounded by mandatory date range
- TopTenants visibility: ✅ null for non-platform-admin callers (tenant isolation preserved)
- Filter bar: ✅ from/to date pickers, category selector, tenant ID input, Apply button
- Investigation links: ✅ category/event-type/actor rows link to `/synqaudit/investigation?...`

---

## 11. Known Issues / Gaps

- VolumeByDay uses EF Core `GroupBy(r => r.OccurredAtUtc.Date)` — translates to `DATE(OccurredAtUtc)` in MySQL. In SQLite (dev), this uses EF Core's `DateTime.Date` property which SQLite handles natively.
- No client-side chart library required — trend is rendered as an SVG bar chart or simple CSS bars to keep the bundle lean.
- TagsJson-based analytics deferred to a future ticket.
- Alerting thresholds: out of scope per spec.

---

## 12. Final Status

**COMPLETE** — 2026-04-19
