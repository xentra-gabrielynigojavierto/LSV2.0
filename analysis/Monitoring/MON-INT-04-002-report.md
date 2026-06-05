# MON-INT-04-002 — Uptime Aggregation Engine

> **Report created FIRST**, before any implementation code was written. Updated incrementally.

---

## 1. Task Summary

Deliver durable uptime aggregation to the Monitoring Service:
- Hourly rollup table derived from raw `check_results` history
- Incremental aggregation `BackgroundService` (never reads alert state)
- Read APIs for 24h / 7d / 30d / 90d windows (rollups + per-entity history)
- Gateway routing for the new endpoints
- Explicit configuration in appsettings

**All components were found fully scaffolded** during the codebase analysis. Work performed in this feature: wiring verification, appsettings configuration, gateway route additions, end-to-end validation, and this report.

---

## 2. Existing Raw Data Analysis

### Canonical fact table
`check_results` (via `DbSet<CheckResultRecord>`) is the canonical input for all uptime computation.

| Field | Type | Uptime role |
|---|---|---|
| `MonitoredEntityId` | Guid | Groups results per component |
| `EntityName` | string | Snapshot display name (denormalized) |
| `CheckedAtUtc` | DateTime | Bucket assignment (truncated to hour) |
| `Outcome` | enum `CheckOutcome` | Maps to uptime state (see below) |
| `ElapsedMs` | long | Source for avg/max latency aggregates |
| `Succeeded` | bool | Redundant with Outcome.Success; unused by aggregation |

### Why `entity_current_status` is NOT used for uptime
`EntityCurrentStatus` is a single-row projection: "what is the status right now?". It has no historical dimension. Using it would only reflect the most recent check outcome, not a window of availability.

### Why `monitoring_alerts` are NOT used for uptime
Alerts represent operator-level incidents, not factual check history. They can be:
- Manually resolved (which must NOT change uptime metrics)
- Created with delays relative to actual check failures
- Not created at all if the alert rule engine is not running

The aggregation engine never reads `MonitoringAlert` rows. This is enforced by design.

### Check cadence
- Configurable; currently 60 s in dev / 300 s in production appsettings
- Aggregation is count-based, not interval-assumed
- Buckets with no checks produce no rollup row (represented as InsufficientData in reads)

---

## 3. Uptime Aggregation Model

### State mapping (CheckOutcome → uptime state)

| CheckOutcome | Uptime State | Included in denominator? |
|---|---|---|
| `Success` | **Up** | Yes |
| `NonSuccessStatusCode` | **Degraded** | Yes |
| `Timeout`, `NetworkFailure`, `InvalidTarget`, `UnexpectedFailure` | **Down** | Yes |
| `Skipped` | **Unknown** | **No** |

Unknown checks are tracked (`UnknownCount`) but excluded from the denominator.

### Formulas

```
denominator          = UpCount + DegradedCount + DownCount       (Unknown excluded)

UptimeRatio          = UpCount / denominator                     (strict)
WeightedAvailability = (UpCount + DegradedCount * 0.5) / denominator

AvgLatencyMs         = SumElapsedMs / TotalCount                 (all checks incl. Unknown)
MaxLatencyMs         = max(ElapsedMs) in bucket

InsufficientData     = true when denominator == 0
```

### Window support
| Window | Duration |
|---|---|
| `24h` | last 24 hours |
| `7d` | last 7 days |
| `30d` | last 30 days |
| `90d` | last 90 days |

Invalid or missing `window` parameter silently defaults to `24h`.

### Granularity
Hourly buckets only (no separate daily rollup table). Daily/30d/90d windows are computed by aggregating hourly rows in the read service. This is sufficient given expected entity counts (O(tens)), avoiding premature complexity.

---

## 4. Persistence Design

### Table: `uptime_hourly_rollups`

| Column | Type | Notes |
|---|---|---|
| `id` | char(36) | Primary key, Guid |
| `monitored_entity_id` | char(36) | FK → `monitored_entities.id` (CASCADE) |
| `entity_name` | varchar(200) | Snapshotted at compute time |
| `bucket_hour_utc` | datetime(6) | UTC start of hour window |
| `up_count` | int | Success checks |
| `degraded_count` | int | NonSuccessStatusCode checks |
| `down_count` | int | Timeout/Network/Invalid/Unexpected checks |
| `unknown_count` | int | Skipped checks |
| `total_count` | int | Sum of all four counts |
| `sum_elapsed_ms` | bigint | For avg latency |
| `max_elapsed_ms` | bigint | Peak latency in bucket |
| `uptime_ratio` | double nullable | null → InsufficientData |
| `weighted_availability` | double nullable | null → InsufficientData |
| `insufficient_data` | tinyint(1) | True when denominator == 0 |
| `computed_at_utc` | datetime(6) | When last recomputed |
| `created_at_utc` | datetime(6) | Row insert time (set once) |

**Indexes:**
- `ix_uptime_hourly_entity_hour` — UNIQUE on `(monitored_entity_id, bucket_hour_utc)` — enforces one row per entity per hour and enables O(1) insert-vs-update lookup
- `ix_uptime_hourly_bucket_hour` — on `bucket_hour_utc` alone — enables efficient time-window scans

**Migration:** `20260420120000_AddUptimeRollups` — adds the table, indexes, and FK.

---

## 5. Aggregation Engine Implementation

**Class:** `UptimeAggregationHostedService` (`Monitoring.Infrastructure/UptimeAggregation/`)

### Processing flow

1. **Scope creation:** creates a scoped DI scope (scoped `MonitoringDbContext`)
2. **Cutoff calculation:** `DateTime.UtcNow - LookbackDays` (default 91 days)
3. **Raw data load:** `SELECT entity_id, entity_name, checked_at_utc, outcome, elapsed_ms FROM check_results WHERE checked_at_utc >= cutoff` (projection only)
4. **In-memory grouping:** groups by `(MonitoredEntityId, EntityName, TruncateToHour(CheckedAtUtc))`
5. **Existing rollup load:** `SELECT * FROM uptime_hourly_rollups WHERE bucket_hour_utc >= cutoff` — dictionary keyed by `(entityId, hourBucket)`
6. **Insert or update:** calls `UptimeHourlyRollup.Update(...)` for existing rows, constructs new entities for new buckets
7. **Save:** single `SaveChangesAsync` per cycle

### Idempotency
Running the cycle N times over the same check data produces identical rollup values. No duplicate inflation: existing rows are updated in-place via the unique `(entity, hour)` index.

### Cadence (configurable)
| Setting | Default (prod) | Dev |
|---|---|---|
| `IntervalSeconds` | 300 s (5 min) | 60 s |
| `LookbackDays` | 91 | 91 |
| `Enabled` | true | true |

### Bucket boundary handling
Checks are assigned to the hour that contains `CheckedAtUtc` (truncated to the start of the hour). A check at 14:59:59 falls in the 14:00 bucket. No interval splitting — count-based, not duration-based.

### Alert isolation
The aggregation engine reads only `check_results`. The `MonitoringAlert` table is never touched. Manual alert resolution (which only updates `MonitoringAlert.IsActive`/`ResolvedAtUtc`) has **zero effect** on uptime rollups.

---

## 6. Read API Design & Implementation

### GET /monitoring/uptime/rollups?window=24h|7d|30d|90d

**Access:** Anonymous (same as all other monitoring read endpoints)
**Implementation:** `EfCoreUptimeReadService.GetRollupsAsync`

**Response shape:**
```json
{
  "window": "24h",
  "windowStartUtc": "2026-04-19T17:25:49Z",
  "windowEndUtc":   "2026-04-20T17:25:49Z",
  "overallUptimePercent": 92.951,
  "componentCount": 11,
  "insufficientData": false,
  "components": [
    {
      "entityId": "8f697459-...",
      "entityName": "Gateway",
      "uptimePercent": 100.0,
      "weightedAvailabilityPercent": 100.0,
      "upCount": 1006, "degradedCount": 0, "downCount": 0, "unknownCount": 0,
      "totalCountable": 1006,
      "avgLatencyMs": 5.99, "maxLatencyMs": 534,
      "insufficientData": false
    }
  ]
}
```

### GET /monitoring/uptime/history?entityId={guid}&window=24h|7d|30d|90d

**Access:** Anonymous
**Implementation:** `EfCoreUptimeReadService.GetHistoryAsync`
**Error responses:** 400 (missing/invalid entityId), 404 (entity not found)

**Response shape:**
```json
{
  "entityId": "8f697459-...",
  "entityName": "Gateway",
  "window": "24h",
  "windowStartUtc": "...", "windowEndUtc": "...",
  "buckets": [
    {
      "bucketStartUtc": "2026-04-20T06:00:00",
      "uptimePercent": 100.0,
      "dominantStatus": "Healthy",
      "upCount": 94, "degradedCount": 0, "downCount": 0, "unknownCount": 0,
      "avgLatencyMs": 8.24, "maxLatencyMs": 487,
      "insufficientData": false
    }
  ]
}
```

**DominantStatus derivation:**
- `up + down + degraded == 0` → `"Unknown"`
- `down > up + degraded` → `"Down"`
- `degraded > up` → `"Degraded"`
- otherwise → `"Healthy"`

### Gateway routing
Added explicit named routes in `Gateway.Api/appsettings.json`:
- `monitoring-uptime-rollups` (order 57) — `/monitoring/monitoring/uptime/rollups`
- `monitoring-uptime-history` (order 58) — `/monitoring/monitoring/uptime/history`

Both routes use `PathRemovePrefix: "/monitoring"` to forward to `http://127.0.0.1:5015/monitoring/uptime/...`.

The existing `monitoring-protected` catch-all (order 150) also covers these paths — the explicit routes are added for clarity and correct ordering.

---

## 7. Validation

### A. Build
```
Monitoring.Api: 0 errors, 0 warnings
Gateway.Api:    0 errors, 0 warnings (1 known pre-existing version-conflict warning)
```

### B. Endpoint validation (direct — port 5015)

| Test | Result |
|---|---|
| `GET /monitoring/uptime/rollups?window=24h` | 11 components, 92.951% overall ✓ |
| `GET /monitoring/uptime/rollups?window=7d` | same data (only 24h of history exists in dev) ✓ |
| `GET /monitoring/uptime/rollups?window=30d` | 92.951% overall ✓ |
| `GET /monitoring/uptime/rollups?window=90d` | 92.951% overall ✓ |
| `?window=invalid` | silently defaults to `24h` ✓ |
| no window param | silently defaults to `24h` ✓ |
| History (Gateway entity) | hourly buckets with `dominantStatus: "Healthy"` ✓ |
| History (Reports entity, 36.8% uptime) | 5 buckets `"Down"`, 3 `"Healthy"` ✓ |
| `?entityId=not-a-guid` | 400 with descriptive error ✓ |
| no entityId | 400 with descriptive error ✓ |
| `?entityId=00000000-0000-0000-0000-000000000001` | 404 ✓ |

### C. Endpoint validation (via gateway — port 5010)

| Test | Result |
|---|---|
| `GET /monitoring/monitoring/uptime/rollups?window=24h` | 92.951% overall ✓ |

### D. Existing endpoints unaffected
- `GET /monitoring/summary` ✓
- `GET /monitoring/status` ✓
- `GET /monitoring/alerts` ✓
- `GET /health` ✓

### E. Alert isolation — code-verified
`UptimeAggregationHostedService.RunCycleAsync` reads only `db.CheckResults`. `MonitoringAlert` is never queried. `EfCoreAlertRuleEngine` only modifies `MonitoringAlert` rows. Manual alert resolve → zero effect on uptime rollups. ✓

### F. Sparse-data behavior
Dev environment has ~24h of check history. 7d/30d/90d windows return the same values as 24h. This is correct — `InsufficientData` stays `false` because countable checks exist; the window just doesn't have older data yet.

---

## 8. Files Changed

### New (created as part of this feature scaffold)
| File | Purpose |
|---|---|
| `Monitoring.Domain/Monitoring/UptimeHourlyRollup.cs` | Domain entity with formula, Update, ComputeRatios |
| `Monitoring.Infrastructure/Persistence/Configurations/UptimeHourlyRollupConfiguration.cs` | EF Core column/index/FK mapping |
| `Monitoring.Infrastructure/Persistence/Migrations/20260420120000_AddUptimeRollups.cs` | Creates `uptime_hourly_rollups` table |
| `Monitoring.Infrastructure/Persistence/Migrations/20260420120000_AddUptimeRollups.Designer.cs` | EF migration metadata |
| `Monitoring.Infrastructure/UptimeAggregation/UptimeAggregationHostedService.cs` | Aggregation engine BackgroundService |
| `Monitoring.Infrastructure/UptimeAggregation/UptimeAggregationOptions.cs` | Config options (Enabled, IntervalSeconds, LookbackDays) |
| `Monitoring.Infrastructure/Queries/EfCoreUptimeReadService.cs` | IUptimeReadService implementation |
| `Monitoring.Application/Queries/IUptimeReadService.cs` | Read service interface |
| `Monitoring.Application/Queries/UptimeReadResults.cs` | Application-layer result records |
| `Monitoring.Api/Contracts/UptimeResponses.cs` | API contract records |
| `Monitoring.Api/Endpoints/UptimeReadEndpoints.cs` | GET /monitoring/uptime/rollups and /history |
| `analysis/MON-INT-04-002-report.md` | This report |

### Modified
| File | Change |
|---|---|
| `Monitoring.Infrastructure/Persistence/MonitoringDbContext.cs` | Added `DbSet<UptimeHourlyRollup>` |
| `Monitoring.Infrastructure/DependencyInjection.cs` | Registered `IUptimeReadService` + `UptimeAggregationHostedService` + options |
| `Monitoring.Api/Program.cs` | `app.MapUptimeReadEndpoints()` added |
| `Monitoring.Api/appsettings.json` | Added `Monitoring:UptimeAggregation` config section (300s interval) |
| `Monitoring.Api/appsettings.Development.json` | Added `Monitoring:UptimeAggregation` config section (60s interval) |
| `Gateway.Api/appsettings.json` | Added `monitoring-uptime-rollups` and `monitoring-uptime-history` routes |

---

## 9. Known Gaps / Risks

| Gap | Detail |
|---|---|
| **Count-based, not interval-based** | Uptime = successful checks / total countable checks. If checks are unevenly spaced, a 1-minute outage in a lightly-checked hour looks worse than in a heavily-checked hour. True interval-weighted uptime would require recording inter-check intervals. Deferred. |
| **Unknown excluded from denominator** | Skipped checks tracked but excluded. If many checks are skipped, uptime % may appear inflated. InsufficientData only set when denominator=0; partial-data hours are not flagged. |
| **No daily rollup table** | 30d/90d windows aggregate hourly rows at query time. Fine for O(tens) entities; may need a daily rollup if entity counts grow into hundreds. |
| **Latency total_count includes Unknown** | `avgMs = SumElapsedMs / TotalCount` includes Skipped checks in the divisor. Slight skew if Skipped checks have non-zero elapsed ms. Acceptable approximation. |
| **Sparse-data window reporting** | 7d/30d/90d return same values as 24h when history is short. Correct behavior but may confuse consumers that don't check `windowStartUtc`. |
| **Latency trends not yet surfaced in UI** | `avgLatencyMs`/`maxLatencyMs` present in both endpoints. Chart presentation deferred to MON-INT-04-004. |
| **No per-entity name query for history** | History endpoint requires entityId (Guid). Name-based lookup not yet supported — callers must first obtain entityId from `/monitoring/entities`. |

---

## 10. Recommended Next Feature

**MON-INT-04-003 — Availability Timeline Bars**

The rollup/history data is now fully available and validated. Each entity has real hourly bucket data with `dominantStatus` (`Healthy` / `Degraded` / `Down` / `Unknown`) and `uptimePercent` per bucket. The `GET /monitoring/uptime/history` endpoint is purpose-built for driving availability bar visualizations.

Logical next step: surface this data on the public status page as:
- 90-day availability bars (one bar per day, derived from hourly buckets)
- Per-component uptime percentages (from the rollups endpoint)

No further backend work is needed — only a frontend presentation layer consuming the existing endpoints.

MON-INT-04-004 (Response Time Charts) would follow after availability bars, since the latency aggregates are already present in the same endpoint responses.
