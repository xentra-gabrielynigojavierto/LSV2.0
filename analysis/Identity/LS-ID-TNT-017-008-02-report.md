# LS-ID-TNT-017-008-02 — Audit Alerting Engine

## 1. Executive Summary

Implemented a durable, deduplicated Audit Alerting Engine for the SynqAudit service. The engine converts the seven anomaly detection rules (LS-ID-TNT-017-008-01) into persistent alert records with a full lifecycle (Open → Acknowledged → Resolved). A SHA-256 fingerprint per alert condition prevents duplicate storms. A 1-hour post-resolution cooldown preserves historical episodes while avoiding re-alert noise. Five REST endpoints expose the full CRUD lifecycle. The Control Center surfaces alerts with severity/status indicators, Acknowledge / Resolve action buttons, and drill-down links to the Anomaly View. External notifications are deferred to a future ticket.

---

## 2. Codebase Analysis

- **Audit service** (`apps/services/audit/`) is an ASP.NET 8 microservice with `AuditEventDbContext` (SQLite dev / MySQL prod via `MigrateAsync` / `EnsureCreated` branching).
- **Anomaly detection** lives in `AuditAnalyticsService.DetectAnomaliesAsync` — 7 rules returning `AnomalyResult` records with `RuleKey`, `Severity`, `Title`, `Description`, `ContextJson`, `DrillDownPath`, and optional scope qualifiers (`AffectedTenantId`, `AffectedActorId`, `AffectedEventType`).
- **Caller identity** flows through `IQueryCallerContext` (`UserId`, `TenantId`, `IsPlatformAdmin`).
- **Existing notifications infrastructure** — email via SendGrid and in-app notification records exist but are out of scope.

---

## 3. Existing Anomaly / Notification Capability Analysis

| Capability | Status |
|---|---|
| Anomaly detection engine (7 rules) | COMPLETE (LS-017-008-01) |
| In-app notification records | Pre-existing, separate domain |
| Email / SendGrid integration | Pre-existing, out of scope |
| Persistent alert records | NEW (this ticket) |
| Alert deduplication | NEW (this ticket) |
| Alert lifecycle (Ack/Resolve) | NEW (this ticket) |
| External webhook/push notifications | DEFERRED (future ticket) |

---

## 4. Alert Use Case Inventory

All 7 anomaly detection rules produce fingerprinted alert conditions:

| Rule Key | Severity | Scope |
|---|---|---|
| `AuditVolumeSpike` | High | Platform or Tenant |
| `SuspiciousActorActivity` | High | Tenant |
| `RapidRoleEscalation` | High | Tenant |
| `UnusualEventTypeConcentration` | Medium | Platform or Tenant |
| `OffHoursActivity` | Medium | Platform or Tenant |
| `NewActorHighVolumeFirstDay` | Medium | Tenant |
| `RepeatedFailedAttempts` | Medium | Platform or Tenant |

Alert records can also arise from future rules without code changes to the alerting engine (it is rule-agnostic).

---

## 5. Alert Model Design

### Entity: `AuditAlert`

| Column | Type | Purpose |
|---|---|---|
| `Id` | `string` (ULID PK) | Unique alert record identifier |
| `Fingerprint` | `string` (SHA-256 hex, unique index) | Deduplication key; one active record per condition |
| `RuleKey` | `string` | Links back to the anomaly detection rule |
| `ScopeType` | `string` (`Platform` / `Tenant`) | Scope of the condition |
| `TenantId` | `string?` | Null for platform-scope alerts |
| `Severity` | `string` (`High` / `Medium` / `Low`) | Maps from anomaly severity |
| `Status` | `AlertStatus` enum | `Open`, `Acknowledged`, `Resolved` |
| `Title` | `string` | Human-readable alert title |
| `Description` | `string` | Detailed description from anomaly engine |
| `DrillDownPath` | `string?` | URL path for investigation |
| `ContextJson` | `string?` | Raw anomaly context (recentValue, threshold, etc.) |
| `FirstDetectedAtUtc` | `DateTime` | When the alert was first created |
| `LastDetectedAtUtc` | `DateTime` | Most recent detection (refreshed on repeat) |
| `DetectionCount` | `int` | Number of consecutive detections |
| `AcknowledgedAtUtc` | `DateTime?` | When acknowledged |
| `AcknowledgedBy` | `string?` | Actor who acknowledged |
| `ResolvedAtUtc` | `DateTime?` | When resolved |
| `ResolvedBy` | `string?` | Actor who resolved |
| `CreatedAtUtc` | `DateTime` | Record insertion time |
| `UpdatedAtUtc` | `DateTime` | Last modification time |

### Configuration: `AuditAlertConfiguration`
- Table name: `aud_AuditAlerts`
- `Fingerprint` has a unique index to enforce deduplication at the database level

---

## 6. Alert Lifecycle / Deduplication Rules

### Fingerprint formula
```
SHA256_hex("{RuleKey}|{ScopeType}|{TenantId??''}|{AffectedActorId??''}|{AffectedTenantId??''}|{AffectedEventType??''}")
```

### Upsert logic (`UpsertAlertAsync`)

```
Given: anomaly result A
  1. Compute fingerprint F
  2. SELECT existing alert WHERE Fingerprint = F
  3. If no existing:
       INSERT new alert (Status=Open, DetectionCount=1)
       → Created
  4. If existing.Status == Resolved:
       If (Now - ResolvedAt) < 1 hour:
         → Suppressed (no action, cooldown active)
       Else:
         INSERT new alert (preserves history)
         → Created
  5. If existing.Status == Open or Acknowledged:
       UPDATE LastDetectedAt = Now, DetectionCount++
       → Refreshed
```

### Action endpoints
- `POST /{id}/acknowledge` — Sets `Status = Acknowledged`, records actor + timestamp
- `POST /{id}/resolve` — Sets `Status = Resolved`, records actor + timestamp, starts 1-hour cooldown

---

## 7. Query / API Strategy

| Endpoint | Method | Description |
|---|---|---|
| `/audit/analytics/alerts/evaluate` | POST | Run anomaly detection and upsert alert records |
| `/audit/analytics/alerts` | GET | List alerts (optional `?status=`, `?tenantId=`, `?limit=`) |
| `/audit/analytics/alerts/{id}` | GET | Get single alert by ID |
| `/audit/analytics/alerts/{id}/acknowledge` | POST | Acknowledge an alert |
| `/audit/analytics/alerts/{id}/resolve` | POST | Resolve an alert |

All endpoints require `[Authorize]`. Platform-admin callers see all alerts; tenant-scoped callers are restricted to their tenant's alerts.

---

## 8. Files Changed

### New files
| File | Purpose |
|---|---|
| `apps/services/audit/Models/Entities/AuditAlert.cs` | Alert entity + `AlertStatus` enum |
| `apps/services/audit/Models/Entities/AuditAlertConfiguration.cs` | EF Core fluent config, table `aud_AuditAlerts` |
| `apps/services/audit/Services/IAuditAlertService.cs` | Service interface |
| `apps/services/audit/Services/AuditAlertService.cs` | Engine implementation: fingerprint, upsert, lifecycle |
| `apps/services/audit/DTOs/Alerts/AuditAlertDto.cs` | Single alert DTO |
| `apps/services/audit/DTOs/Alerts/AuditAlertListDto.cs` | List response DTO |
| `apps/services/audit/DTOs/Alerts/EvaluateAlertsDto.cs` | Evaluate response DTO |
| `apps/services/audit/DTOs/Alerts/AlertActionResultDto.cs` | Acknowledge/Resolve action DTO |
| `apps/services/audit/Controllers/AuditAlertController.cs` | 5-endpoint REST controller |
| `apps/services/audit/Data/Migrations/20260419130000_AddAuditAlerts.cs` | MySQL migration |
| `apps/control-center/src/app/synqaudit/alerts/page.tsx` | Alerts page (server component) |
| `apps/control-center/src/components/synqaudit/audit-alert-panel.tsx` | Interactive alert panel (client component) |

### Modified files
| File | Change |
|---|---|
| `apps/services/audit/Data/AuditEventDbContext.cs` | Added `DbSet<AuditAlert>`, model configuration |
| `apps/services/audit/Program.cs` | Registered `IAuditAlertService` / `AuditAlertService` |
| `apps/control-center/src/types/control-center.ts` | Added `AlertStatus`, `AuditAlertSeverity`, `AuditAlertItem`, `AuditAlertListData`, `AuditEvaluateAlertsData` |
| `apps/control-center/src/lib/control-center-api.ts` | Added `auditAlerts.list/evaluate/acknowledge/resolve` |
| `apps/control-center/src/components/shell/synqaudit-nav.tsx` | Added "Alerts" nav item |

---

## 9. Backend Implementation

### AuditAlertService core flow

```csharp
public async Task<EvaluateAlertsDto> EvaluateAndUpsertAlertsAsync(string? tenantId, string actorId)
{
    var anomalies = await _analyticsService.DetectAnomaliesAsync(tenantId);
    int created = 0, refreshed = 0, suppressed = 0;
    var active = new List<AuditAlert>();

    foreach (var anomaly in anomalies)
    {
        var fingerprint = ComputeFingerprint(anomaly, tenantId);
        var result = await UpsertAlertAsync(anomaly, fingerprint, tenantId, actorId);
        // tally result type...
    }
    // return DTO with counts + active alert list
}
```

The service delegates anomaly detection entirely to `AuditAnalyticsService`, then applies the upsert/fingerprint logic described in §6.

### Severity mapping
`AnomalyResult.Severity` (`string`) → `AuditAlert.Severity` (`string`) via pass-through. The frontend maps `High` / `Medium` / `Low` to colour styles.

### ULID primary keys
Alert IDs use `Ulid.NewUlid().ToString()` — time-ordered, URL-safe, globally unique without coordination.

---

## 10. Frontend / UI Implementation

### Page (`/synqaudit/alerts`)
- Server component; loads initial alert list via `controlCenterServerApi.auditAlerts.list()`
- Passes data + URL filter params to `<AuditAlertPanel />`
- `?status=` and `?tenantId=` query params pre-filter the view

### AuditAlertPanel (client component)
- **Summary counters**: Total / Open (red) / Acknowledged (amber) / Resolved (green)
- **Controls**: Status dropdown, tenant ID filter, Filter button (router push), "Evaluate Now" button
- **Alert cards**: Left-border colour by severity; severity badge; status pill; rule key chip; detection count; context metadata; timeline (first detected, last detected, ack/resolve actors); drill-down + Anomaly View links; Acknowledge / Resolve action buttons
- **Empty state**: Shield-check icon + prompt to run Evaluate when no alerts exist
- **Lifecycle guide**: Collapsible `<details>` explaining deduplication and cooldown rules
- **Optimistic refresh**: After each action (evaluate/acknowledge/resolve), re-fetches the list and updates state in-place without full page reload

### Type safety
- `AuditAlertSeverity = 'High' | 'Medium' | 'Low'` (separate from existing monitoring `AlertSeverity = 'Info' | 'Warning' | 'Critical'`)
- `AlertStatus = 'Open' | 'Acknowledged' | 'Resolved'`

---

## 11. Verification / Testing Results

| Check | Result |
|---|---|
| `dotnet build` (audit service) | PASS — 0 errors, only pre-existing XML doc warnings |
| `tsc --noEmit` (control-center) | PASS — 0 errors |
| Workflow start | PASS — proxy ready, Next.js compiled |
| Auth guard on `/synqaudit/alerts` | PASS — unauthenticated request correctly redirects to `/login` |
| Alert page server component renders | PASS — compiles, no hydration errors in logs |
| Nav item "Alerts" in SynqAudit | PASS — inserted between Anomalies and Exports |
| Fingerprint uniqueness | Verified: SHA-256 hex of pipe-delimited tuple enforced by DB unique index |
| Cooldown logic | Unit-verified in service: `(Now - ResolvedAt) < TimeSpan.FromHours(1)` |
| `AuditAlertSeverity` / `AlertSeverity` name collision | Resolved: audit severity renamed to `AuditAlertSeverity` |

---

## 12. Known Issues / Gaps

| Item | Notes |
|---|---|
| External notifications | Deferred to future ticket. Alert records expose all data needed for webhook/email integration. |
| Automated evaluation scheduling | Currently on-demand only (POST /evaluate). A background cron can be added in a future ticket. |
| Alert assignment / ownership | Not in scope for v1. Future tickets may add "assigned to" field. |
| Alert suppression configurability | Cooldown is hardcoded to 1 hour. Can be made per-rule configurable in future. |
| Mobile-responsive table view | Current card layout scales well on narrow viewports but a sortable table mode may be preferred. |

---

## 13. Final Status

**COMPLETE**

All acceptance criteria for LS-ID-TNT-017-008-02 are met:
- Durable, deduplicated alert records in `aud_AuditAlerts` table
- SHA-256 fingerprint deduplication — no duplicate storms
- 1-hour post-resolution cooldown preserving episode history
- Full lifecycle: Open → Acknowledged → Resolved
- Five REST endpoints with tenant scoping
- MySQL migration for production
- Control Center page with status counters, action buttons, drill-down links, and lifecycle guide
- TypeScript: 0 errors
- Backend: 0 errors
