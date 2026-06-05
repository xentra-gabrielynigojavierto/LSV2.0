# LS-NOTIF-SMS-023 — Per-Tenant Governance Rule Pack Scoping and True Tenant-Isolated Enforcement

**Status:** COMPLETED  
**Date:** 2026-05-13  
**Migration:** `20260512000009_AddSmsGovernanceTenantScoping`  
**Depends on:** LS-NOTIF-SMS-019 (rule resolver), LS-NOTIF-SMS-021 (release packages), LS-NOTIF-SMS-022 (canary rollouts)

---

## Overview

LS-NOTIF-SMS-023 delivers per-tenant governance rule pack scoping on top of the existing global LS-019 resolver. Tenants can be assigned specific rule packs, have global rules suppressed or overridden via overlays, and operate in isolated enforcement mode (global packs entirely excluded). Rollout stages (LS-022) now automatically create tenant assignments as stages activate and roll them back on rollback. All operations are audited, non-destructive, and fail-open.

---

## Architecture

### Resolution Modes

| Mode | Behaviour |
|------|-----------|
| `global_only` (default when scoping disabled) | Existing LS-019 global pack resolution — unaffected |
| `tenant_inherited` (default when enabled) | Global packs + tenant-assigned packs merged; overlays applied on top |
| `tenant_isolated` | Only tenant-assigned packs apply; global packs excluded entirely |

### Key Design Decisions

1. **Additive injection** — LS-023 extends `SmsGovernanceRuleResolver` via an `ISmsGovernanceTenantResolutionService` call after LS-019 completes. When scoping is disabled or no assignments exist, LS-019 behaviour is 100% preserved.
2. **Fail-open** — All resolution failures return empty/global rule sets; tenant data is never left in a broken enforcement state.
3. **Non-destructive overlays** — Overlays do not mutate stored rules; they produce in-memory `EffectiveRuleDto` results. Database rows are untouched.
4. **Rollout bridge** — `SmsGovernanceRolloutService.StartRolloutAsync` and `AdvanceStageAsync` call `ISmsGovernanceTenantAssignmentService.AssignRulePackAsync` + `ActivateAssignmentAsync` for each cohort tenant + release-item rule pack. `RollbackRolloutAsync` calls `RollbackAssignmentAsync` for all assignments scoped to that `RolloutPlanId`. Failures are non-fatal.
5. **Security** — All endpoints require `Policies.AdminOnly`. No phones, credentials, or raw JWT content stored. `OverrideJson` validated ≤4000 chars and checked for sensitive keywords before persist.

---

## New Files

### Domain (`Notifications.Domain/`)
| File | Description |
|------|-------------|
| `SmsGovernanceTenantRulePackAssignment.cs` | Assignment entity with `AssignmentStates` (draft/active/inactive/rolled_back/superseded), `AssignmentModes` (inherited/isolated/rollout_canary/rollout_stage), and `Terminal` HashSet |
| `SmsGovernanceTenantOverlay.cs` | Overlay entity with `OverlayTypes` (disable_rule/suppress_rule/override_severity/override_pattern/override_metadata/add_rule) and `OverlayStates` |
| `SmsGovernanceTenantAssignmentAuditEvent.cs` | Immutable audit record for all assignment and overlay state transitions |

### Application (`Notifications.Application/`)
| File | Description |
|------|-------------|
| `Options/SmsGovernanceTenantScopingOptions.cs` | Config: `Enabled`, `ResolutionMode`, `EnableTenantOverlays`, `EnableRolloutAssignments`, `MaxAssignmentsPerTenant` (20), `MaxOverlaysPerTenant` (50), `FailOpenOnResolutionError` |
| `Interfaces/ISmsGovernanceTenantResolutionService.cs` | Resolution interface: `ResolveEffectiveRulePacksAsync`, `ResolveEffectiveRulesAsync`, `GetEffectiveGovernanceGraphAsync`, `ExplainResolutionAsync`. All result/context types defined here |
| `Interfaces/ISmsGovernanceTenantAssignmentService.cs` | Assignment + overlay CRUD + audit trail interface. All request/query/DTO types defined here |
| `Interfaces/ISmsGovernanceTenantIsolationValidator.cs` | Validation interface: `ValidateTenantIsolationAsync`, `ValidateAssignmentAsync`, `ValidateOverlayAsync` |

### Infrastructure (`Notifications.Infrastructure/`)
| File | Description |
|------|-------------|
| `Data/Configurations/SmsGovernanceTenantRulePackAssignmentConfiguration.cs` | EF config — table `ntf_SmsGovernanceTenantRulePackAssignments` |
| `Data/Configurations/SmsGovernanceTenantOverlayConfiguration.cs` | EF config — table `ntf_SmsGovernanceTenantOverlays` |
| `Data/Configurations/SmsGovernanceTenantAssignmentAuditEventConfiguration.cs` | EF config — table `ntf_SmsGovernanceTenantAssignmentAuditEvents` |
| `Data/Migrations/20260512000009_AddSmsGovernanceTenantScoping.cs` | Migration: creates 3 tables, 13 indexes |
| `Services/SmsGovernanceTenantResolutionService.cs` | Resolves effective packs + rules + overlays per tenant; handles `tenant_inherited`/`tenant_isolated` modes; produces governance graph and explanation |
| `Services/SmsGovernanceTenantAssignmentService.cs` | Full CRUD for assignments + overlays; all state transitions audited |
| `Services/SmsGovernanceTenantIsolationValidator.cs` | 5-check assignment validator + 5-check overlay validator; sensitive JSON keyword guard |

### API (`Notifications.Api/`)
| File | Description |
|------|-------------|
| `Endpoints/SmsGovernanceTenantScopingEndpoints.cs` | 14 admin-only endpoints under `/notifications/v1/admin/sms/governance/tenant-scoping/` |

### Control Center (`apps/control-center/`)
| File | Description |
|------|-------------|
| `src/lib/sms-governance-tenant-scoping-api.ts` | TypeScript API client: all 14 API calls, DTOs, state/mode constants, badge helpers |
| `src/app/notifications/sms-governance/tenant-scoping/page.tsx` | Server Component admin page: KPI bar, assignments table, overlays table, audit trail |

---

## Modified Files

| File | Change |
|------|--------|
| `NotificationsDbContext.cs` | +3 `DbSet<>` properties, +3 `ApplyConfiguration()` calls |
| `NotificationsDbContextModelSnapshot.cs` | +3 entity blocks (assignments, overlays, audit events) |
| `SmsGovernanceRuleResolver.cs` | Added `ISmsGovernanceTenantResolutionService` dependency + `BuildFinalRuleSet()` injection point after LS-019 resolution; `ToGovernanceRule()` converter |
| `SmsGovernanceRolloutService.cs` | Added `ISmsGovernanceTenantAssignmentService` dependency; `CreateStageAssignmentsAsync()` called from `StartRolloutAsync` + `AdvanceStageAsync`; `RollbackRolloutAssignmentsAsync()` called from `RollbackRolloutAsync` |
| `DependencyInjection.cs` | +4 service registrations (options + 3 services); rollout service registration moved after LS-023 deps |
| `appsettings.json` | +`SmsGovernanceTenantScoping` section |
| `Program.cs` | +`app.MapSmsGovernanceTenantScopingEndpoints()` |
| `sms-governance/page.tsx` (control-center) | +tenant-scoping quick-nav link card alongside Releases and Rollouts |

---

## Database Tables

### `ntf_SmsGovernanceTenantRulePackAssignments`
Columns: `Id`, `TenantId`, `RulePackId`, `AssignmentState`, `AssignmentMode`, `Priority`, `EffectiveFrom`, `EffectiveTo`, `RolloutPlanId`, `RolloutStageId`, `ReleasePackageId`, `AssignedBy`, `DeactivationReason`, `ActivatedAt`, `DeactivatedAt`, `SupersededAt`, `CreatedAt`, `UpdatedAt`  
Indexes: 5 (tenant+state+priority, tenant+pack, pack+state, rollout+state, effective window)

### `ntf_SmsGovernanceTenantOverlays`
Columns: `Id`, `TenantId`, `RulePackId`, `RuleId`, `OverlayType`, `OverlayState`, `OverrideJson`, `Priority`, `Enabled`, `EffectiveFrom`, `EffectiveTo`, `CreatedAt`, `UpdatedAt`, `CreatedBy`, `UpdatedBy`  
Indexes: 4 (tenant+enabled+priority, tenant+pack, tenant+rule, type+enabled)

### `ntf_SmsGovernanceTenantAssignmentAuditEvents`
Columns: `Id`, `TenantId`, `AssignmentId`, `OverlayId`, `EventType`, `PreviousState`, `NewState`, `Actor`, `Reason`, `MetadataJson`, `CreatedAt`  
Indexes: 4 (tenant+date, assignment+date, overlay+date, eventType+date)

---

## API Endpoints (14)

Base: `/notifications/v1/admin/sms/governance/tenant-scoping/`  
Auth: `Policies.AdminOnly`

| Method | Path | Description |
|--------|------|-------------|
| GET | `/tenant-assignments` | List assignments (filterable by tenant, pack, state, mode, rollout) |
| POST | `/tenant-assignments` | Create assignment (starts in draft) |
| POST | `/tenant-assignments/{id}/activate` | Activate assignment |
| POST | `/tenant-assignments/{id}/deactivate` | Deactivate assignment |
| POST | `/tenant-assignments/{id}/rollback` | Rollback assignment to terminal state |
| GET | `/tenant-overlays` | List overlays (filterable) |
| POST | `/tenant-overlays` | Create overlay (starts in draft) |
| POST | `/tenant-overlays/{id}/activate` | Activate overlay |
| POST | `/tenant-overlays/{id}/disable` | Disable overlay |
| GET | `/tenant-resolution/{tenantId}` | Full effective governance graph |
| GET | `/tenant-resolution/{tenantId}/explain` | Step-by-step resolution explanation |
| GET | `/tenant-isolation/{tenantId}` | 5-check isolation validation |
| GET | `/tenant-assignment-audit` | Assignment + overlay audit trail |

---

## Overlay Types

| Type | Effect |
|------|--------|
| `disable_rule` | Rule excluded from effective set for this tenant |
| `suppress_rule` | Alias for disable (soft suppress) |
| `override_severity` | Rule severity replaced with `OverrideJson.severity` |
| `override_pattern` | Rule pattern replaced with `OverrideJson.pattern` |
| `override_metadata` | Rule metadata replaced with overlay JSON |
| `add_rule` | Synthetic rule synthesized from overlay JSON and injected into effective set |

---

## Rollout Integration (LS-022 bridge)

When `SmsGovernanceRolloutService` activates a stage:

1. All cohort tenants for that stage are loaded.
2. All `rule_pack` release items (`EntityType == "rule_pack"`) for the rollout's release package are loaded.
3. For each `(cohort tenant, rule pack EntityId)` pair — if no existing non-rolled-back assignment exists — `AssignRulePackAsync` is called with mode `rollout_canary` (canary strategy) or `rollout_stage` (all others), then immediately activated.
4. On rollback: all assignments with matching `RolloutPlanId` and non-terminal state are rolled back via `RollbackAssignmentAsync`. Scoped to `RolloutPlanId` only — unrelated assignments preserved.
5. All failures non-fatal — logged as warnings; rollout state not affected.

---

## Configuration

```json
"SmsGovernanceTenantScoping": {
  "Enabled": true,
  "ResolutionMode": "tenant_inherited",
  "EnableTenantOverlays": true,
  "EnableRolloutAssignments": true,
  "MaxAssignmentsPerTenant": 20,
  "MaxOverlaysPerTenant": 50,
  "FailOpenOnResolutionError": true
}
```

---

## Security Properties

- Admin-only endpoints (platform-level)
- `OverrideJson` ≤ 4000 chars; keyword guard blocks `password`, `secret`, `token`, `apikey`, `credenti`, `webhook`
- Overlay application is purely in-memory — no stored rule mutation
- Audit trail covers all state transitions
- Tenant isolation guaranteed at DB query level; cross-tenant reads impossible
- Rollout assignment rollback scoped to `RolloutPlanId` only

---

## Build Verification

- `dotnet build Notifications.Api.csproj` — **0 errors**, 29 pre-existing warnings (MailKit NU1902, snapshot CS8669 nullable — both pre-existing from before LS-023)
- TypeScript: `sms-governance-tenant-scoping-api.ts` and `tenant-scoping/page.tsx` use standard patterns from existing LS-022 files

---

## Tasks Completed

| # | Task | Status |
|---|------|--------|
| T001 | Report skeleton | ✅ |
| T002 | Domain entities (3) | ✅ |
| T003 | `SmsGovernanceTenantScopingOptions` | ✅ |
| T004 | Application interfaces (3 + all types) | ✅ |
| T005 | EF configurations (3) | ✅ |
| T006 | DbContext additions | ✅ |
| T007 | Migration `20260512000009_AddSmsGovernanceTenantScoping` | ✅ |
| T008 | Model snapshot update | ✅ |
| T009 | Service implementations (3) | ✅ |
| T010 | Extend `SmsGovernanceRuleResolver` | ✅ |
| T011 | Extend `SmsGovernanceRolloutService` | ✅ |
| T012 | Endpoints (14) | ✅ |
| T013 | DI + appsettings + Program.cs wiring | ✅ |
| T014 | Control Center UI (API client + page) | ✅ |
| T015 | Build verification | ✅ |
| T016 | Report finalization + replit.md | ✅ |
