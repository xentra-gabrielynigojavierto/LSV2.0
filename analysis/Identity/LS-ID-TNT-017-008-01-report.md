# LS-ID-TNT-017-008-01 — Audit Anomaly Detection

## 1. Executive Summary

Implements a deterministic, rule-based anomaly detection layer over the existing audit event store and analytics infrastructure. Detects unusual patterns in denial rates, actor/tenant concentration, governance bursts, export spikes, and severity escalation. All rules are computed on-demand from two bounded time windows (recent 24h vs baseline prior 7 days) using indexed SQL aggregations. No ML, no background jobs, no schema changes. Results are surfaced in a new `/synqaudit/anomalies` page in Control Center with severity badges, plain-English explanations, and drill-down links into the Investigation viewer.

---

## 2. Codebase Analysis

### What LS-ID-TNT-017-008 provides (reused directly)
- `AuditAnalyticsService` — EF Core GroupBy/CountAsync over `AuditEventDbContext` via `IDbContextFactory`
- `AuditAnalyticsController` — `GET /audit/analytics/summary`; probe-based auth, caller-context isolation
- All 9 indexed columns: `OccurredAtUtc`, `EventCategory`, `EventType`, `TenantId`, `ActorId`, `Severity`, `VisibilityScope`, `CorrelationId`, `SessionId`
- Denial detection pattern: `EventType.Contains(".denied") || EventType.Contains(".deny")`
- Governance pattern: `EventType.StartsWith("audit.legal_hold" | "audit.integrity" | "audit.log.exported")`
- Auth pattern: `AuthorizeQuery(probe)` + `GetCaller()` from `HttpContext.Items`
- Tenant isolation: `callerTenantId` override for non-platform-admin callers

### What anomaly detection adds
- A second bounded window (baseline) alongside the recent window for ratio comparison
- Deterministic threshold evaluation (not statistical modelling)
- Anomaly DTO layer with explanation strings
- Dedicated `GET /audit/analytics/anomalies` endpoint (added to existing `AuditAnalyticsController`)
- New SYNQAUDIT nav entry and `/synqaudit/anomalies` page

---

## 3. Existing Analytics / Query Capability Analysis

### Time-window queries: fully feasible
EF Core `CountAsync(r => r.OccurredAtUtc >= from && r.OccurredAtUtc < to)` hits the `IX_AuditEventRecords_OccurredAtUtc` index. Two bounded window queries (recent + baseline) are cheap.

### GroupBy for Top-N: already validated in analytics
`GroupBy(r => r.ActorId)`, `GroupBy(r => r.TenantId)`, `GroupBy(r => r.EventType)` — all indexed, all already used in `AuditAnalyticsService`. Same approach reused for anomaly actor/tenant concentration.

### Denial pattern: validated
`.Contains(".denied") || .Contains(".deny")` — runs on the OccurredAtUtc+TenantId-filtered subset; acceptable for bounded windows.

### Baseline window: prior 7 days (day -8 → day -1 relative to now)
Daily average = baseline_total / 7. Ratio = recent_24h / daily_avg. Spike threshold = ratio > 3.0 for most rules.

### Fixed minimum thresholds
Rules require a minimum absolute count to avoid false positives on quiet tenants (e.g. "ratio > 3x AND recent_count > 5").

---

## 4. Anomaly Use Case Inventory

| # | Operator Question | Rule Key | Signal | Baseline | v1? |
|---|-----------------|----------|--------|----------|-----|
| 1 | Are denials spiking? | `DENIAL_SPIKE` | `.denied` / `.deny` count in 24h vs daily avg | 7-day prior | ✅ |
| 2 | Is one actor generating unusual traffic? | `ACTOR_CONCENTRATION` | Top actor % of total in 24h | Fixed threshold ≥30% + ≥20 events | ✅ |
| 3 | Is one tenant unusually active? | `TENANT_CONCENTRATION` | Top tenant % of cross-tenant total | Fixed threshold ≥40% + ≥50 events | ✅ (PA only) |
| 4 | Is governance activity bursting? | `GOVERNANCE_BURST` | Legal hold / integrity / export events in 24h vs daily avg | 7-day prior | ✅ |
| 5 | Is audit export/query volume spiking? | `EXPORT_SPIKE` | `audit.log.accessed` + `audit.log.exported` in 24h vs daily avg | 7-day prior | ✅ |
| 6 | Are Critical/Alert severity events surging? | `SEVERITY_ESCALATION` | Critical+Alert count in 24h > threshold OR >10% of total | 7-day prior | ✅ |
| 7 | Is one event type dominating? | `EVENTTYPE_CONCENTRATION` | Top event type % of total in 24h | Fixed threshold ≥50% + ≥30 events | ✅ |

---

## 5. Anomaly Rule Design

### Windows
- **Recent window**: `[now-24h, now)` — the "current" observation
- **Baseline window**: `[now-8d, now-1d)` — 7 prior calendar days for daily average (= baseline_total / 7)

### Rule specifications

#### DENIAL_SPIKE
- Signal: denial events in recent window
- Fires when: ratio (recent / daily_avg) > 3.0 AND recent_count > 5
- Severity: High
- Explanation: "Denial events in the last 24h ({recent}) are {ratio}× above the 7-day daily average ({avg:.1f})"
- Drill-down: `/synqaudit/investigation?category=Security`

#### ACTOR_CONCENTRATION
- Signal: top actor's count / total events in recent window
- Fires when: pct > 30% AND actor_count > 20
- Severity: Medium
- Explanation: "Actor {actorId} generated {pct}% of all events ({count}/{total}) in the last 24h"
- Drill-down: `/synqaudit/investigation?actorId={actorId}`

#### TENANT_CONCENTRATION (platform admin only)
- Signal: top tenant's count / total cross-tenant events in recent window
- Fires when: pct > 40% AND tenant_count > 50
- Severity: Medium
- Explanation: "Tenant {tenantId} generated {pct}% of all platform events ({count}/{total}) in the last 24h"
- Drill-down: `/synqaudit/investigation` (tenant admin would scope separately)

#### GOVERNANCE_BURST
- Signal: governance events (legal_hold, integrity, audit.log.exported) in recent window
- Fires when: ratio > 3.0 AND recent_count > 3
- Severity: Medium
- Explanation: "Governance events in the last 24h ({recent}) are {ratio}× above the 7-day daily average ({avg:.1f})"
- Drill-down: `/synqaudit/legal-holds` + `/synqaudit/integrity`

#### EXPORT_SPIKE
- Signal: `audit.log.accessed` + `audit.log.exported` in recent window
- Fires when: ratio > 3.0 AND recent_count > 5
- Severity: Medium
- Explanation: "Audit access/export events in the last 24h ({recent}) are {ratio}× above the 7-day daily average ({avg:.1f})"
- Drill-down: `/synqaudit/exports`

#### SEVERITY_ESCALATION
- Signal: Critical + Alert severity events in recent window
- Fires when: recent_count > 10 OR (recent_count > 0 AND recent_count / total_recent > 0.10)
- Severity: High
- Explanation: "Critical/Alert severity events account for {pct}% of activity in the last 24h ({count} events)"
- Drill-down: `/synqaudit/investigation?severity=Critical`

#### EVENTTYPE_CONCENTRATION
- Signal: top event type count / total events in recent window
- Fires when: pct > 50% AND top_count > 30
- Severity: Low
- Explanation: "Event type '{eventType}' dominates recent activity at {pct}% of events ({count}/{total}) in the last 24h"
- Drill-down: `/synqaudit/investigation?eventType={eventType}`

### Thresholds table (constants in service)
| Rule | Ratio threshold | Absolute min | Pct threshold |
|------|----------------|-------------|--------------|
| DENIAL_SPIKE | 3.0× | 5 | — |
| ACTOR_CONCENTRATION | — | 20 | 30% |
| TENANT_CONCENTRATION | — | 50 | 40% |
| GOVERNANCE_BURST | 3.0× | 3 | — |
| EXPORT_SPIKE | 3.0× | 5 | — |
| SEVERITY_ESCALATION | — | 10 (OR) | 10% |
| EVENTTYPE_CONCENTRATION | — | 30 | 50% |

---

## 6. Query / API Strategy

### Endpoint
```
GET /audit/analytics/anomalies
  ?tenantId=...   (platform admin only — omit for cross-tenant)
```

No date params — windows are always fixed relative to `now` (recent=24h, baseline=prior 7d). This makes anomalies always "current" and avoids stale-baseline confusion.

### Authorization
Reuses the same `AuthorizeQuery(probe)` + `GetCaller()` pattern from `AuditAnalyticsController`. Platform admins can view cross-tenant anomalies; tenant callers see only their own tenant.

### Query strategy
Each rule runs 1-2 bounded `CountAsync` or `GroupBy` queries against indexed columns. Total budget: ~10 queries per request, all with sub-second expected performance on bounded windows.

---

## 7. Files Changed

### New backend files
- `DTOs/Analytics/AuditAnomalyItem.cs`
- `DTOs/Analytics/AuditAnomalyResponse.cs`
- `DTOs/Analytics/AuditAnomalyRequest.cs`
- `Services/IAuditAnomalyService.cs`
- `Services/AuditAnomalyService.cs`

### Modified backend files
- `Controllers/AuditAnalyticsController.cs` — add `GET /audit/analytics/anomalies`
- `Program.cs` — register `IAuditAnomalyService`

### New frontend files
- `app/synqaudit/anomalies/page.tsx`
- `components/synqaudit/audit-anomaly-panel.tsx`

### Modified frontend files
- `types/control-center.ts` — anomaly types
- `lib/control-center-api.ts` — `auditCanonical.anomalies()`
- `lib/nav.ts` — Anomalies nav entry

---

## 8. Backend Implementation

### AuditAnomalyService
Uses `IDbContextFactory<AuditEventDbContext>` directly (same pattern as `AuditAnalyticsService`). Evaluates 7 rules in sequence over two bounded windows. Returns only firing rules — no anomaly = empty list. All queries scoped by `effectiveTenantId`.

### AuditAnalyticsController (extended)
New action: `GET /audit/analytics/anomalies`. Uses identical probe auth pattern. Derives `isPlatformAdmin` + `callerTenantId` from caller context.

---

## 9. Frontend / UI Implementation

### /synqaudit/anomalies page
Server component fetches anomalies with no-cache. Passes to client `AuditAnomalyPanel` component.

#### Sections
1. Summary bar — total anomalies, High/Medium/Low counts, evaluation timestamp
2. Empty state — "No anomalies detected" card when list is empty
3. Anomaly cards — one per firing rule: severity badge, title, description, metric values, drill-down link
4. Window explanation footer — documents what windows were evaluated

---

## 10. Verification / Testing Results

- Backend build: ✅ 0 errors
- Frontend tsc: ✅ 0 errors
- Application restart: ✅ serving
- `/synqaudit/anomalies` route: ✅ auth guard active
- `GET /audit/analytics/anomalies`: ✅ returns `[]` on empty store (correct empty state)
- Existing analytics/viewer/correlation: ✅ no regressions

---

## 11. Known Issues / Gaps

- Baseline windows are always fixed at 24h recent / 7d prior — no user-configurable windows in v1
- TENANT_CONCENTRATION rule is skipped for tenant-scoped callers (correct by design)
- Rules fire on absolute count AND ratio — very low-volume environments may see fewer detections
- Tag-based anomaly rules deferred (raw JSON field, not indexed)
- Alerting/notification on anomaly detection: deferred to future ticket
- No persistent anomaly history — each request recomputes current state

---

## 12. Final Status

**COMPLETE** — 2026-04-19

### Supported anomaly rules
| Rule | Trigger |
|------|---------|
| DENIAL_SPIKE | Denials in last 24h are 3× the 7-day daily average AND ≥5 |
| ACTOR_CONCENTRATION | One actor generates ≥30% of all events in last 24h AND ≥20 events |
| TENANT_CONCENTRATION | One tenant generates ≥40% of all platform events in last 24h AND ≥50 events (PA only) |
| GOVERNANCE_BURST | Governance events in last 24h are 3× daily average AND ≥3 |
| EXPORT_SPIKE | Audit access/export events in last 24h are 3× daily average AND ≥5 |
| SEVERITY_ESCALATION | Critical/Alert events in last 24h exceed 10 OR ≥10% of total |
| EVENTTYPE_CONCENTRATION | One event type ≥50% of all events in last 24h AND ≥30 |

### Computation strategy
On-demand, two bounded window queries (24h recent + 7-day prior baseline), deterministic threshold evaluation, no background jobs, no ML, no schema changes.

### Tenant isolation
Identical to analytics: `callerTenantId` scopes all queries; `TENANT_CONCENTRATION` only populated for platform admins.
