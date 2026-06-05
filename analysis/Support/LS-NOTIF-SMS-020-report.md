# LS-NOTIF-SMS-020 Implementation Report
# Governance Versioning, Bulk Rule Import, and Rule Effectiveness Analytics

**Feature:** LS-NOTIF-SMS-020  
**Status:** COMPLETE  
**Started:** 2026-05-12  
**Completed:** 2026-05-12  
**Service:** Notifications (port 5008)  
**Control Center:** apps/control-center

---

## 1. Summary

LS-NOTIF-SMS-020 extends LS-019 Dynamic Governance Rules with:

1. **Immutable version snapshots** — every mutation (create/update/disable/rollback/import) produces an immutable `SmsGovernanceRuleVersion` or `SmsGovernanceRulePackVersion` record.
2. **Rollback** — any entity can be restored to any previous version. Rollback creates a new version (ChangeType=rollback); no history is ever deleted.
3. **Bulk JSON import** — all-or-nothing transactional import of rule pack bundles with full pre-flight validation. Dry-run supported.
4. **Export** — filtered JSON export of all packs, rules, and optionally profiles.
5. **Rule match instrumentation** — `SmsGovernanceRuleEngine` records aggregate daily metrics via a nullable `ISmsGovernanceMatchRecorder?` (fire-and-forget; never blocks delivery pipeline).
6. **Effectiveness analytics** — per-rule effectiveness (match/block/warn/review rates), per-pack effectiveness, and heuristic false-positive candidate detection.
7. **11 new admin endpoints** in `SmsGovernanceLifecycleEndpoints.cs`.
8. **Control Center Lifecycle tab** — version browser with one-click rollback, bulk import/export UI, and effectiveness analytics dashboard.

---

## 2. Architecture Decisions

### 2.1 Snapshot timing
Snapshots are taken **after** `db.SaveChangesAsync()` — the snapshot always captures the persisted state, not a pre-save in-memory projection.

### 2.2 Import atomicity
`SmsGovernanceImportService.ImportAsync` wraps all bundles in a single `BeginTransactionAsync` / `CommitAsync`. Any failure triggers `RollbackAsync` — no partial state is ever persisted.

### 2.3 Match recording
`ISmsGovernanceMatchRecorder` is registered as a scoped service and injected into `SmsGovernanceRuleEngine` as a **nullable optional parameter** (`recorder = null`). Recording is fire-and-forget (`Task.Run` + swallow exceptions). The delivery pipeline is never affected by analytics failures.

### 2.4 Analytics aggregation
`SmsGovernanceRuleMatchMetric` uses a daily window bucket model (WindowStart = midnight UTC, WindowEnd = 23:59:59.9999999). Counts are atomically incremented via `ON DUPLICATE KEY UPDATE` raw SQL — no read-modify-write races.

### 2.5 Rollback semantics
- Rollback restores **safe fields only** — never restores secrets, credentials, or PII
- Rollback always creates a new version snapshot (ChangeType = "rollback")
- Version history is append-only

### 2.6 Dual interface registration
`SmsGovernanceAnalyticsService` implements both `ISmsGovernanceAnalyticsService` and `ISmsGovernanceMatchRecorder`. Both are resolved from the same scoped instance via factory registrations in `DependencyInjection.cs`.

---

## 3. New Domain Entities

### `SmsGovernanceRuleVersion`
Table: `ntf_SmsGovernanceRuleVersions`

| Column | Type | Notes |
|---|---|---|
| Id | GUID | PK |
| RuleId | GUID | FK → SmsGovernanceRule |
| RulePackId | GUID? | Denormalized for efficient pack-level queries |
| VersionNumber | int | Sequential per rule; unique with RuleId |
| RuleSnapshotJson | mediumtext | JSON snapshot of all safe rule fields |
| ChangeType | varchar(20) | created / updated / disabled / rollback / imported |
| ChangeReason | varchar(500) | Optional human-readable reason |
| CreatedAt | datetime | UTC |
| CreatedBy | varchar(200) | Actor identifier |

Indexes: `UIX_ntf_SmsGovRuleVersions_Rule_Version`, `IX_ntf_SmsGovRuleVersions_Pack_CreatedAt`, `IX_ntf_SmsGovRuleVersions_CreatedAt`

### `SmsGovernanceRulePackVersion`
Table: `ntf_SmsGovernanceRulePackVersions`

| Column | Type | Notes |
|---|---|---|
| Id | GUID | PK |
| RulePackId | GUID | FK → SmsGovernanceRulePack |
| VersionNumber | int | Sequential per pack |
| PackSnapshotJson | mediumtext | JSON snapshot of all safe pack fields |
| IncludedRulesSnapshotJson | longtext | Optional embedded rules snapshot |
| ChangeType | varchar(20) | created / updated / disabled / rollback / imported |
| ChangeReason | varchar(500) | |
| CreatedAt | datetime | UTC |
| CreatedBy | varchar(200) | |

Indexes: `UIX_ntf_SmsGovPackVersions_Pack_Version`, `IX_ntf_SmsGovPackVersions_CreatedAt`

### `SmsGovernanceRuleMatchMetric`
Table: `ntf_SmsGovernanceRuleMatchMetrics`

| Column | Type | Notes |
|---|---|---|
| Id | GUID | PK |
| RuleId | GUID? | Nullable — supports unlinked metrics |
| RulePackId | GUID? | |
| TenantId | GUID? | |
| RuleType | varchar(40) | Denormalized for query efficiency |
| Severity | varchar(20) | |
| DecisionType | varchar(20) | allow / warn / review_required / block |
| ReasonCode | varchar(100) | |
| MatchCount | int | Total matches in window |
| BlockCount | int | Matches that resulted in block |
| WarnCount | int | Matches that resulted in warn |
| ReviewCount | int | Matches that resulted in review_required |
| AllowCount | int | Matches that resulted in allow |
| SimulationCount | int | Dry-run triggered matches |
| LiveCount | int | Live delivery triggered matches |
| WindowStart | datetime | Midnight UTC — daily bucket |
| WindowEnd | datetime | 23:59:59.9999999 UTC |
| LastMatchedAt | datetime? | Most recent match in window |
| CreatedAt / UpdatedAt | datetime | |

Indexes: `IX_ntf_SmsGovMatchMetrics_Rule_Tenant_Window`, `IX_ntf_SmsGovMatchMetrics_Pack_Window`, `IX_ntf_SmsGovMatchMetrics_Tenant_Window`, `IX_ntf_SmsGovMatchMetrics_WindowStart`

---

## 4. Application Interfaces

### `ISmsGovernanceVersioningService`
```csharp
Task SnapshotRuleAsync(Guid ruleId, string changeType, string? changeReason, string? requestedBy, CancellationToken ct)
Task SnapshotRulePackAsync(Guid rulePackId, string changeType, string? changeReason, string? requestedBy, bool includeRules, CancellationToken ct)
Task<IReadOnlyList<RuleVersionDto>> GetRuleVersionsAsync(Guid ruleId, CancellationToken ct)
Task<IReadOnlyList<RulePackVersionDto>> GetRulePackVersionsAsync(Guid rulePackId, CancellationToken ct)
Task<RollbackResult> RollbackRuleAsync(Guid ruleId, int versionNumber, string? requestedBy, string? reason, CancellationToken ct)
Task<RollbackResult> RollbackRulePackAsync(Guid rulePackId, int versionNumber, string? requestedBy, string? reason, CancellationToken ct)
```

### `ISmsGovernanceImportService`
```csharp
Task<GovernanceImportResult> ValidateImportAsync(GovernanceImportRequest request, CancellationToken ct)
Task<GovernanceImportResult> ImportAsync(GovernanceImportRequest request, CancellationToken ct)
Task<object> ExportAsync(GovernanceExportQuery query, CancellationToken ct)
```

### `ISmsGovernanceAnalyticsService`
```csharp
Task<(IReadOnlyList<RuleEffectivenessRow> Rows, int Total)> GetRuleEffectivenessAsync(GovernanceAnalyticsQuery query, CancellationToken ct)
Task<IReadOnlyList<MatchAnalyticsRow>> GetRuleMatchAnalyticsAsync(GovernanceAnalyticsQuery query, CancellationToken ct)
Task<IReadOnlyList<FalsePositiveCandidateRow>> GetFalsePositiveCandidatesAsync(GovernanceAnalyticsQuery query, CancellationToken ct)
Task<(IReadOnlyList<PackEffectivenessRow> Rows, int Total)> GetPackEffectivenessAsync(GovernanceAnalyticsQuery query, CancellationToken ct)
```

### `ISmsGovernanceMatchRecorder`
```csharp
void RecordMatches(SmsGovernanceRuleEvaluationResult result, Guid? tenantId, bool isDryRun)
```
Fire-and-forget — synchronous entry, async body, exceptions swallowed.

---

## 5. Configuration Options

### `SmsGovernanceVersioningOptions` (`SmsGovernanceVersioning`)
| Key | Default | Description |
|---|---|---|
| Enabled | true | Master switch — disables all snapshotting when false |
| IncludeRulesInPackSnapshot | true | Whether to embed rule snapshots inside pack snapshots |
| MaxSnapshotJsonBytes | 65536 | Max byte size of any single snapshot JSON |

### `SmsGovernanceAnalyticsOptions` (`SmsGovernanceAnalytics`)
| Key | Default | Description |
|---|---|---|
| Enabled | true | Master switch for match recording |
| WindowDays | 30 | Default date range for analytics queries |
| MaxResultRows | 200 | Max rows returned per analytics query |
| FalsePositiveWarnThreshold | 10 | Min warn count for FP heuristic eligibility |
| FalsePositiveLiveToSimRatio | 0.1 | Threshold below which live/sim ratio triggers FP candidate flag |

---

## 6. Endpoint Map

All endpoints require `PlatformAdmin` authorization and are prefixed with `/v1/admin/sms/governance`.

| Method | Path | Handler | Description |
|---|---|---|---|
| GET | `/rules/{id}/versions` | `GetRuleVersionsAsync` | Rule version history (newest first) |
| POST | `/rules/{id}/rollback` | `RollbackRuleAsync` | Roll rule back to a previous version |
| GET | `/rule-packs/{id}/versions` | `GetRulePackVersionsAsync` | Pack version history |
| POST | `/rule-packs/{id}/rollback` | `RollbackRulePackAsync` | Roll pack back to a previous version |
| POST | `/import/validate` | `ValidateImportAsync` | Dry-run validate import payload (no writes) |
| POST | `/import` | `ImportAsync` | Transactional bulk import (dryRun flag supported) |
| GET | `/export` | `ExportAsync` | Export rules as JSON |
| GET | `/effectiveness` | `GetRuleEffectivenessAsync` | Rule-level effectiveness analytics |
| GET | `/match-analytics` | `GetRuleMatchAnalyticsAsync` | Time-series match analytics |
| GET | `/false-positive-candidates` | `GetFalsePositiveCandidatesAsync` | Heuristic false-positive detection |
| GET | `/pack-effectiveness` | `GetPackEffectivenessAsync` | Pack-level effectiveness analytics |

### LS-019 Mutation Patches
The following existing LS-019 endpoints were updated to call versioning after each save:

| Endpoint | Version call |
|---|---|
| POST `/rule-packs` | `SnapshotRulePackAsync(pack.Id, "created", …)` |
| PUT `/rule-packs/{id}` | `SnapshotRulePackAsync(id, "updated", …)` |
| POST `/rule-packs/{id}/disable` | `SnapshotRulePackAsync(id, "disabled", …)` |
| POST `/rules` | `SnapshotRuleAsync(rule.Id, "created", …)` |
| PUT `/rules/{id}` | `SnapshotRuleAsync(id, "updated", …)` |
| POST `/rules/{id}/disable` | `SnapshotRuleAsync(id, "disabled", …)` |

---

## 7. Import Validation Rules

The import service validates all bundles before any writes occur. Errors identify bundle index, rule index, and field name.

| Check | Rule |
|---|---|
| Bundle count | At least 1 bundle required |
| `rulePack.name` | Required, non-empty |
| `rulePack.status` | Must be one of: draft, active, inactive, archived |
| `rulePack.inheritanceMode` | Must be one of: merge, override, append_only |
| `rule.name` | Required |
| `rule.ruleType` | Must be one of 7 valid types |
| `rule.severity` | Must be one of 5 valid severities |
| `rule.pattern` | Max length from `SmsGovernanceDynamicOptions.MaxPatternLength` |
| Regex rules | Blocked when `AllowRegexRules=false`; catastrophic backtracking check applied |
| `rule.metadataJson` | Must be valid JSON if provided |

---

## 8. False-Positive Detection Heuristics

Three heuristics are applied per rule in the configured date window:

1. **High warn rate** — warn count ≥ `FalsePositiveWarnThreshold` AND warn/total > 80% → "rule may be too broad"
2. **Simulation-only** — ≥5 simulation matches, 0 live matches → "never triggered in live delivery pipeline"
3. **Low live/sim ratio** — live/(live+sim) < `FalsePositiveLiveToSimRatio` with ≥10 total matches → "low live-to-simulation ratio"

FP score = `(warnRatio × 0.6) + (simRatio × 0.4)` — used for descending sort order.

---

## 9. Rule Engine Instrumentation

`SmsGovernanceRuleEngine` was updated with:
```csharp
private readonly ISmsGovernanceMatchRecorder? _recorder;

public SmsGovernanceRuleEngine(
    ...,
    ISmsGovernanceMatchRecorder? recorder = null)  // nullable optional

// After evaluation completes:
if (matchedRules.Count > 0)
    _recorder?.RecordMatches(evalResult, request.TenantId, request.IsDryRun);
```

The recorder is injected by the DI container when registered; falls back to null gracefully in test/isolated contexts.

---

## 10. EF Migration

**Migration:** `20260512000005_AddSmsGovernanceVersioningAndAnalytics`

Tables created:
- `ntf_SmsGovernanceRuleVersions` — 10 columns, 3 indexes
- `ntf_SmsGovernanceRulePackVersions` — 10 columns, 2 indexes  
- `ntf_SmsGovernanceRuleMatchMetrics` — 21 columns, 4 indexes (unique: RuleId+TenantId+WindowStart)

Model snapshot updated with all 3 entities.

---

## 11. Control Center UI

**Page:** `/notifications/sms-dynamic-rules`

A "Lifecycle & Analytics" tab was added alongside the existing "Rule Management" tab (URL: `?tab=lifecycle`).

### New files
| File | Purpose |
|---|---|
| `apps/control-center/src/lib/sms-governance-lifecycle-api.ts` | TypeScript API client for all LS-020 endpoints |
| `apps/control-center/src/components/sms-dynamic-rules/governance-lifecycle-panel.tsx` | Client component with 3 sub-tabs |

### Lifecycle tab sub-tabs

**Version History & Rollback**
- Entity type selector (Rule / Rule Pack)
- UUID input → loads full version timeline
- Table shows: version number, change type badge, reason, timestamp, actor
- Click-to-select rollback target with reason field → confirm and execute

**Import / Export**
- JSON textarea with "Load example" helper
- Dry-run checkbox (checked by default)
- Validate button (calls `/import/validate`)
- Import button (calls `/import`)
- Inline error list with bundle/rule/field context
- Export button → downloads timestamped JSON file

**Effectiveness Analytics**
- "Load Analytics" fetches all 3 analytics endpoints in parallel (resilient: partial results OK)
- Rules sub-tab: match counts, block/warn/review split, block%, live vs sim
- Packs sub-tab: per-pack match totals, active rule count, block rate
- False Positive Candidates: heuristic explanations, FP score, warn/live/sim breakdown

---

## 12. Security Considerations

- No raw phone numbers are stored in version snapshots or analytics records
- No message content is stored — only aggregate counts per rule/window
- Snapshot serialization explicitly excludes any potential secrets (uses a safe record type with only domain-level fields)
- Import regex patterns are validated for catastrophic backtracking before persistence
- All endpoints require `PlatformAdmin` authorization
- Analytics `ON DUPLICATE KEY UPDATE` uses parameterized raw SQL — no string interpolation

---

## 13. Build Verification

Final build: **0 errors**

Pre-existing warnings retained from prior implementations (NU1902 MailKit advisory, CS8669 nullable annotations in auto-generated model snapshot, CS7095 constant filter in worker).

Fixed as part of this implementation:
- `SmsGovernanceSimulationService.cs:49` — `Guid?` to `Guid` implicit conversion (added `?? Guid.Empty`)
- `SmsGovernanceEndpoints.cs` — missing `using BuildingBlocks.Authorization`
- `SmsTemplateGovernanceEndpoints.cs` — missing `using BuildingBlocks.Authorization`

---

## 14. Files Changed / Created

### New — Notifications service
| File | Description |
|---|---|
| `Notifications.Domain/SmsGovernanceRuleVersion.cs` | Domain entity |
| `Notifications.Domain/SmsGovernanceRulePackVersion.cs` | Domain entity |
| `Notifications.Domain/SmsGovernanceRuleMatchMetric.cs` | Domain entity |
| `Notifications.Application/Interfaces/ISmsGovernanceVersioningService.cs` | Service interface + DTOs |
| `Notifications.Application/Interfaces/ISmsGovernanceImportService.cs` | Service interface + DTOs |
| `Notifications.Application/Interfaces/ISmsGovernanceAnalyticsService.cs` | Service interface + DTOs |
| `Notifications.Application/Interfaces/ISmsGovernanceMatchRecorder.cs` | Recorder interface |
| `Notifications.Application/Options/SmsGovernanceVersioningOptions.cs` | Config options |
| `Notifications.Application/Options/SmsGovernanceAnalyticsOptions.cs` | Config options |
| `Notifications.Infrastructure/Data/Configurations/SmsGovernanceRuleVersionConfiguration.cs` | EF config |
| `Notifications.Infrastructure/Data/Configurations/SmsGovernanceRulePackVersionConfiguration.cs` | EF config |
| `Notifications.Infrastructure/Data/Configurations/SmsGovernanceRuleMatchMetricConfiguration.cs` | EF config |
| `Notifications.Infrastructure/Data/Migrations/20260512000005_AddSmsGovernanceVersioningAndAnalytics.cs` | EF migration |
| `Notifications.Infrastructure/Services/SmsGovernanceVersioningService.cs` | Implementation |
| `Notifications.Infrastructure/Services/SmsGovernanceImportService.cs` | Implementation |
| `Notifications.Infrastructure/Services/SmsGovernanceAnalyticsService.cs` | Implementation (dual interface) |
| `Notifications.Api/Endpoints/SmsGovernanceLifecycleEndpoints.cs` | 11 new endpoints |

### Modified — Notifications service
| File | Change |
|---|---|
| `Notifications.Infrastructure/Data/NotificationsDbContext.cs` | +3 DbSets, +3 ApplyConfiguration |
| `Notifications.Infrastructure/DependencyInjection.cs` | +2 options, +4 service registrations |
| `Notifications.Infrastructure/Services/SmsGovernanceRuleEngine.cs` | +nullable ISmsGovernanceMatchRecorder, fire-and-forget instrumentation |
| `Notifications.Infrastructure/Services/SmsGovernanceSimulationService.cs` | Fixed Guid? cast |
| `Notifications.Infrastructure/Data/Migrations/NotificationsDbContextModelSnapshot.cs` | +3 entity blocks |
| `Notifications.Api/Program.cs` | +MapSmsGovernanceLifecycleEndpoints() |
| `Notifications.Api/appsettings.json` | +SmsGovernanceVersioning + SmsGovernanceAnalytics sections |
| `Notifications.Api/Endpoints/SmsGovernanceDynamicRuleEndpoints.cs` | +ISmsGovernanceVersioningService DI + 6 snapshot calls |
| `Notifications.Api/Endpoints/SmsGovernanceEndpoints.cs` | +using BuildingBlocks.Authorization |
| `Notifications.Api/Endpoints/SmsTemplateGovernanceEndpoints.cs` | +using BuildingBlocks.Authorization |

### New — Control Center
| File | Description |
|---|---|
| `apps/control-center/src/lib/sms-governance-lifecycle-api.ts` | Full TypeScript API client (types + functions) |
| `apps/control-center/src/components/sms-dynamic-rules/governance-lifecycle-panel.tsx` | Lifecycle UI panel (3 sub-tabs) |

### Modified — Control Center
| File | Change |
|---|---|
| `apps/control-center/src/app/notifications/sms-dynamic-rules/page.tsx` | +GovernanceLifecyclePanel, page-level tab bar |
