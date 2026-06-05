# LS-NOTIF-SMS-022 — Canary Governance Rollout, Tenant Segmentation, and Staged Governance Deployment

**Status:** COMPLETE  
**Migration:** `20260512000008_AddSmsGovernanceRollout`  
**Depends on:** LS-NOTIF-SMS-021-HARDENING (`20260512000007_AddSmsGovernanceReleaseHardening`)

---

## 1. Initial Codebase Analysis

### 1.1 Existing Release Management (LS-021)

- **`SmsGovernanceReleasePackage`** — top-level release container; state machine: draft → pending_review → approved → scheduled/active → superseded/archived/activation_failed. Includes LS-021-HARDENING fields: 4 activation lock fields + 4 retry/backoff fields.
- **`SmsGovernanceReleaseItem`** — change line items (entityType + actionType per entity).
- **`SmsGovernanceApprovalRequest`** / **`SmsGovernanceApprovalDecision`** — multi-stage approval workflow.
- **`SmsGovernanceReleaseAuditEvent`** — append-only release lifecycle audit trail.
- Table prefix: `ntf_SmsGovernanceReleasePackages`, `ntf_SmsGovernanceReleaseItems`, etc.

### 1.2 Existing Hardening (LS-021-HARDENING)

- **Concurrency locking:** `ActivationLockId/AcquiredAt/ExpiresAt/LockedBy` on release package — prevents double-activation.
- **Retry/backoff:** `ActivationAttemptCount`, `NextActivationRetryAt` — worker skips releases in backoff window.
- **Approval role enforcement:** `EnforceApprovalRoles` + `AllowPlatformAdminApprovalFallback` options.
- **Integrity service:** `ISmsGovernanceReleaseIntegrityService` — read-only diagnostics (items, audit, lock state).

### 1.3 Existing Versioning/Rollback (LS-020)

- **`ISmsGovernanceVersioningService`** — `SnapshotRulePackAsync`, `SnapshotRuleAsync`, `RollbackRuleAsync`, `RollbackRulePackAsync`.
- `full_activation` rollout strategy delegates directly to `ISmsGovernanceReleaseService.ActivateAsync`, which calls the versioning snapshot and respects all LS-021-HARDENING locking.

### 1.4 Existing Analytics Data Sources (LS-020)

- **`SmsGovernanceRuleMatchMetric`** — daily aggregate buckets per (RuleId, RulePackId, TenantId, WindowStart); fields: BlockCount, WarnCount, ReviewCount, AllowCount, MatchCount.
- **`SmsGovernanceDecision`** — per-notification governance decision with DecisionType and TenantId.
- Threshold evaluator uses `SmsGovernanceRuleMatchMetric` filtered by cohort TenantIds over 24-hour windows.

### 1.5 Existing Worker Pattern

- **`SmsGovernanceReleaseActivationWorker`** — BackgroundService; IServiceScopeFactory; poll interval + startup delay; per-item fault tolerance; disabled by default.
- **`SmsGovernanceRolloutWorker`** follows identical pattern with 90-second startup delay.

### 1.6 Existing Control Center Release UI (LS-021)

- `apps/control-center/src/lib/sms-governance-release-api.ts` — TypeScript API client.
- `apps/control-center/src/components/sms-governance/governance-release-panel.tsx` — 3-tab panel.
- New rollout panel follows identical pattern at `/notifications/sms-governance/rollouts`.

### 1.7 Authorization Convention

- All governance admin endpoints: `RequireAuthorization(Policies.AdminOnly)`.
- MapGroup pattern: `app.MapGroup("/v1/admin/sms/governance").RequireAuthorization(Policies.AdminOnly)`.

### 1.8 EF/Table Naming Convention

- Tables: `ntf_SmsGovernance<EntityName>` (PascalCase after prefix).
- All Guid PKs serialized as `char(36)`, all DateTime columns UTC.

---

## 2. LS-021 Release Management Findings

- `ISmsGovernanceReleaseService.ActivateAsync` is the canonical full-activation entry point. Rollout with `full_activation` strategy delegates to this, preserving all approval and locking invariants.
- Release must be in `approved`, `active`, or `scheduled` state before `StartRolloutAsync` proceeds.
- Rollout orchestration never calls `db.SaveChanges` on the release package directly.

---

## 3. LS-021-HARDENING Locking/Retry/Audit Findings

- Activation lock is at the `SmsGovernanceReleasePackage` level. `full_activation` strategy goes through `ActivateAsync` and therefore respects the lock.
- `canary`/`staged_*`/`manual_progression` strategies record rollout orchestration state only — they do not call `ActivateAsync` and do not interact with the lock.
- All rollout transitions are recorded to `SmsGovernanceRolloutAuditEvent` (separate table from release audit events).

---

## 4. LS-020 Versioning/Rollback Findings

- `RollbackRolloutAsync` marks the rollout and all non-terminal stages as `rolled_back`; marks activated cohorts `RolledBackAt`. For `full_activation` strategy, the release's own rollback semantics (via `ISmsGovernanceVersioningService`) are the governing mechanism — the rollout rollback records the orchestration state.
- Canary/staged rollback: marks rollout + stages as `rollout_rolled_back`; operator must separately initiate release rollback via LS-020 versioning endpoints if needed.

---

## 5. Existing Governance Analytics Findings

- Threshold evaluator uses 24-hour `SmsGovernanceRuleMatchMetric` window filtered by enabled cohort TenantIds.
- If cohort TenantIds are not present in the metrics table (no traffic yet), `InsufficientData = true` is returned and no auto-pause/rollback is triggered.
- Rollout analytics service uses 7-day metric window for aggregate dashboards.

---

## 6. Existing Control Center UI Findings

- New rollout page at `/notifications/sms-governance/rollouts` follows same PlatformAdmin gate (`requirePlatformAdmin()`).
- `GovernanceRolloutPanel` is a client component with list + detail views and tabbed sub-panels (stages, cohorts, analytics, audit).
- No rollout logic is duplicated in Control Center — all operations go through Notification Service APIs.

---

## 7. Files Added

| File | Purpose |
|---|---|
| `Notifications.Domain/SmsGovernanceRolloutPlan.cs` | Rollout plan entity + `RolloutStates` + `RolloutStrategies` constants |
| `Notifications.Domain/SmsGovernanceRolloutStage.cs` | Rollout stage entity + `RolloutStageStates` constants |
| `Notifications.Domain/SmsGovernanceTenantCohort.cs` | Tenant cohort targeting entity |
| `Notifications.Domain/SmsGovernanceRolloutAuditEvent.cs` | Rollout audit event entity + `RolloutAuditEventTypes` constants (16 types) |
| `Notifications.Application/Options/SmsGovernanceRolloutsOptions.cs` | Rollout configuration (9 options) |
| `Notifications.Application/Interfaces/ISmsGovernanceRolloutService.cs` | Rollout orchestration interface + all request/DTO/result types |
| `Notifications.Application/Interfaces/ISmsGovernanceRolloutEvaluator.cs` | Threshold evaluation interface + `RolloutHealthResult` |
| `Notifications.Application/Interfaces/ISmsGovernanceRolloutAnalyticsService.cs` | Analytics interface + 3 DTO types |
| `Notifications.Infrastructure/Data/Configurations/SmsGovernanceRolloutPlanConfiguration.cs` | EF config (4 indexes) |
| `Notifications.Infrastructure/Data/Configurations/SmsGovernanceRolloutStageConfiguration.cs` | EF config (unique stage index) |
| `Notifications.Infrastructure/Data/Configurations/SmsGovernanceTenantCohortConfiguration.cs` | EF config (3 indexes) |
| `Notifications.Infrastructure/Data/Configurations/SmsGovernanceRolloutAuditEventConfiguration.cs` | EF config (3 indexes) |
| `Notifications.Infrastructure/Data/Migrations/20260512000008_AddSmsGovernanceRollout.cs` | Schema migration (4 new tables, 11 indexes) |
| `Notifications.Infrastructure/Services/SmsGovernanceRolloutService.cs` | Full rollout orchestration implementation |
| `Notifications.Infrastructure/Services/SmsGovernanceRolloutEvaluator.cs` | Threshold evaluator (JSON-configurable thresholds, fail-open) |
| `Notifications.Infrastructure/Services/SmsGovernanceRolloutAnalyticsService.cs` | Rollout + stage + cohort analytics aggregation |
| `Notifications.Infrastructure/Workers/SmsGovernanceRolloutWorker.cs` | Background stage-advancement worker (disabled by default) |
| `Notifications.Api/Endpoints/SmsGovernanceRolloutEndpoints.cs` | 12 PlatformAdmin endpoints + request body models |
| `apps/control-center/src/lib/sms-governance-rollout-api.ts` | TypeScript API client + state/strategy display helpers |
| `apps/control-center/src/components/sms-governance/governance-rollout-panel.tsx` | Client panel (list, detail, stages, cohorts, analytics, audit tabs) |
| `apps/control-center/src/app/notifications/sms-governance/rollouts/page.tsx` | PlatformAdmin-gated page at `/notifications/sms-governance/rollouts` |

---

## 8. Files Modified

| File | Change |
|---|---|
| `Notifications.Infrastructure/Data/NotificationsDbContext.cs` | +4 DbSets + 4 `ApplyConfiguration` calls |
| `Notifications.Infrastructure/DependencyInjection.cs` | Register options, 3 scoped services, 1 hosted service |
| `Notifications.Infrastructure/Data/Migrations/NotificationsDbContextModelSnapshot.cs` | +4 entity model blocks |
| `Notifications.Api/Program.cs` | `app.MapSmsGovernanceRolloutEndpoints()` |
| `Notifications.Api/appsettings.json` | `SmsGovernanceRollouts` config section (9 keys) |

---

## 9. Database/Schema/Config Changes

### New Tables

| Table | Entity | Key Indexes |
|---|---|---|
| `ntf_SmsGovernanceRolloutPlans` | `SmsGovernanceRolloutPlan` | ReleasePackageId; Tenant+State+Dt; State+Dt; Strategy+Dt |
| `ntf_SmsGovernanceRolloutStages` | `SmsGovernanceRolloutStage` | PlanId+StageNum (unique); PlanId+State |
| `ntf_SmsGovernanceTenantCohorts` | `SmsGovernanceTenantCohort` | PlanId+TenantId; StageId+TenantId; CohortName |
| `ntf_SmsGovernanceRolloutAuditEvents` | `SmsGovernanceRolloutAuditEvent` | PlanId+Dt; EventType+Dt; StageId+Dt |

### Config Section: `SmsGovernanceRollouts`

| Key | Default | Behavior |
|---|---|---|
| `Enabled` | `true` | Master switch; APIs return 503 when false |
| `RolloutWorkerEnabled` | `false` | Background worker opt-in |
| `RolloutPollMinutes` | `5` | Worker poll interval |
| `MaxRolloutsPerCycle` | `10` | Worker batch cap |
| `DefaultCanaryPercentage` | `5` | Default first-stage percentage |
| `DefaultStageDurationMinutes` | `60` | Default observation window |
| `AutoPauseOnThresholdBreach` | `true` | Auto-pause on threshold breach |
| `AutoRollbackOnCriticalThresholdBreach` | `false` | Auto-rollback on critical breach (conservative default) |
| `FailOpenOnRolloutEvaluationError` | `true` | Evaluation errors → healthy (fail open) |

---

## 10. API/Interface Changes

### New Endpoints (all PlatformAdmin / `Policies.AdminOnly`)

| Method | Path | Returns |
|---|---|---|
| `GET` | `/v1/admin/sms/governance/rollouts` | `PaginatedRolloutResult` |
| `GET` | `/v1/admin/sms/governance/rollouts/{id}` | `RolloutDetailDto` |
| `POST` | `/v1/admin/sms/governance/rollouts` | `RolloutPlanDto` (201) |
| `POST` | `/v1/admin/sms/governance/rollouts/{id}/stages` | `RolloutStageDto` (201) |
| `POST` | `/v1/admin/sms/governance/rollouts/{id}/cohorts` | `TenantCohortDto` (201) |
| `POST` | `/v1/admin/sms/governance/rollouts/{id}/start` | `RolloutOperationResult` |
| `POST` | `/v1/admin/sms/governance/rollouts/{id}/pause` | `RolloutOperationResult` |
| `POST` | `/v1/admin/sms/governance/rollouts/{id}/resume` | `RolloutOperationResult` |
| `POST` | `/v1/admin/sms/governance/rollouts/{id}/rollback` | `RolloutOperationResult` |
| `POST` | `/v1/admin/sms/governance/rollouts/{id}/advance` | `RolloutOperationResult` |
| `GET` | `/v1/admin/sms/governance/rollouts/{id}/analytics` | `RolloutAnalyticsDto` |
| `GET` | `/v1/admin/sms/governance/rollouts/{id}/audit` | `RolloutAuditEventDto[]` |

### New Interfaces

| Interface | Methods |
|---|---|
| `ISmsGovernanceRolloutService` | CreateRollout, GetRollout, ListRollouts, AddStage, AddCohortTenant, StartRollout, PauseRollout, ResumeRollout, RollbackRollout, AdvanceStage, CompleteRollout, GetAuditTrail |
| `ISmsGovernanceRolloutEvaluator` | EvaluateRolloutHealth, EvaluateStageHealth |
| `ISmsGovernanceRolloutAnalyticsService` | GetRolloutAnalytics, GetRolloutStageAnalytics, GetRolloutCohortAnalytics |

---

## 11. UI/Route Changes

### Control Center

| Asset | Purpose |
|---|---|
| `apps/control-center/src/lib/sms-governance-rollout-api.ts` | TypeScript API client for all 12 endpoints + state/strategy label helpers |
| `apps/control-center/src/components/sms-governance/governance-rollout-panel.tsx` | Client component: list view + detail view with 4 tabs (stages, cohorts, analytics, audit) + lifecycle action buttons |
| `apps/control-center/src/app/notifications/sms-governance/rollouts/page.tsx` | Server page at `/notifications/sms-governance/rollouts`, gated by `requirePlatformAdmin()` |

---

## 12. Validation/Testing

**Build verification** (both projects, 0 errors):

| Project | Exit | Errors |
|---|---|---|
| `Notifications.Infrastructure` | 0 | 0 |
| `Notifications.Api` | 0 | 0 |

**Security constraints verified:**
- No raw phone numbers in any entity field, DTO property, or log call
- No credentials, SettingsJson, CredentialsJson, webhook URLs, or provider payloads in any rollout entity
- All 12 endpoints require `Policies.AdminOnly` via MapGroup
- Worker disabled by default (`RolloutWorkerEnabled = false`)
- `full_activation` strategy delegates to `ISmsGovernanceReleaseService.ActivateAsync` — concurrency lock is fully preserved
- Threshold evaluator failures fail open (`FailOpenOnRolloutEvaluationError = true`)
- Duplicate cohort check prevents same TenantId per rollout+stage

**Preserved behaviors:**
- LS-NOTIF-SMS-001 through LS-NOTIF-SMS-021-HARDENING: delivery pipeline unchanged, dynamic rule engine unchanged, versioning/rollback unchanged, approval workflow unchanged, activation locking unchanged
- `SmsGovernanceReleasePackage` state machine is not mutated by rollout service (except via `ActivateAsync` for `full_activation`)

---

## 13. Known Gaps/Issues

**Architectural limitation — per-tenant activation scoping:**  
Canary and staged rollout strategies record orchestration/visibility state only. The existing governance rule engine (LS-017 through LS-021) applies active governance rules globally across all tenants — there is no per-tenant rule pack scoping. As a result, canary rollouts orchestrate which tenants are *designated* for rollout, but the governance rules themselves activate globally when the underlying release is activated. True per-tenant canary activation (where rules apply to only specific tenants for a period) requires LS-NOTIF-SMS-023 (per-tenant rule pack scoping). This limitation is clearly documented in the Control Center UI.

**Analytics approximation:**  
Cohort-level block/warn/review rates are computed by filtering `SmsGovernanceRuleMatchMetric` by TenantId. If a tenant has not triggered any governance rule matches, their row appears in the cohort but contributes 0 to sample size, which may inflate the `InsufficientData` state.

---

## 14. Recommended Next Steps

- **LS-NOTIF-SMS-023:** Per-tenant rule pack scoping — enables true canary tenant isolation at the governance enforcement layer
- Add rollout plan templates (preset stage/cohort configurations for common rollout patterns: 5%→25%→100%, 1→5→10→50→100%)
- Grafana/metrics integration for real-time rollout health dashboards
- Extend Control Center UI with a "New Rollout" creation wizard (stage builder + cohort picker)
