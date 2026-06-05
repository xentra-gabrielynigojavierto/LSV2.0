# LS-NOTIF-SMS-021 Implementation Report
# Governance Approval Workflow, Multi-Stage Change Control, and Governance Release Management

**Feature:** LS-NOTIF-SMS-021  
**Status:** COMPLETED  
**Started:** 2026-05-12  
**Completed:** 2026-05-12  
**Service:** Notifications (port 5008)  
**Control Center:** apps/control-center

---

## 1. Initial Codebase Analysis

### 1.1 Service Structure
- `Notifications.Domain` — domain entities (all governance entities live here)
- `Notifications.Application` — interfaces + options (typed config)
- `Notifications.Infrastructure` — EF DbContext (MySQL/Pomelo), migrations, configurations, services, workers
- `Notifications.Api` — Minimal API endpoints, Program.cs

### 1.2 EF Conventions
- Table prefix: `ntf_`
- MySQL (Pomelo EF 8.x)
- Per-entity `IEntityTypeConfiguration<T>` classes in `Data/Configurations/`
- `NotificationsDbContext.ApplyConfigurationsFromAssembly(...)` registers all configs
- Migrations: sequential timestamp names `YYYYMMDD000000_DescriptiveName`
- Latest prior migration: `20260512000005_AddSmsGovernanceVersioningAndAnalytics`

### 1.3 Endpoint Conventions
- Minimal APIs via static extension methods `MapXxx()`
- PlatformAdmin auth: `using BuildingBlocks.Authorization;` + `.RequireAuthorization(Policies.AdminOnly)`
- Program.cs maps all endpoint groups sequentially at the bottom

### 1.4 Worker Conventions
- `BackgroundService` pattern
- `IServiceScopeFactory` for scoped service resolution
- Enabled/disabled via typed options (default disabled where not traffic-critical)
- 90-second startup stagger to avoid boot contention
- `AddHostedService<T>()` registered in `DependencyInjection.cs`

### 1.5 LS-019 Findings (Dynamic Rules)
- Entities: `SmsGovernanceRulePack`, `SmsGovernanceRule`, `SmsComplianceProfile`, `SmsComplianceProfileAssignment`
- Key services: `ISmsGovernanceRuleResolver`, `ISmsGovernanceRuleEngine`, `ISmsGovernanceSimulationService`
- Tables: `ntf_SmsGovernanceRulePacks`, `ntf_SmsGovernanceRules`, `ntf_SmsComplianceProfiles`, `ntf_SmsComplianceProfileAssignments`

### 1.6 LS-020 Findings (Versioning/Import/Analytics)
- Entities: `SmsGovernanceRuleVersion`, `SmsGovernanceRulePackVersion`, `SmsGovernanceRuleMatchMetric`
- Key services: `ISmsGovernanceVersioningService`, `ISmsGovernanceImportService`, `ISmsGovernanceAnalyticsService`, `ISmsGovernanceMatchRecorder`
- LS-021 activation calls `ISmsGovernanceVersioningService.SnapshotRulePackAsync`/`SnapshotRuleAsync` when mutating rule/pack entities
- Rollback history from LS-020 remains immutable under LS-021

### 1.7 Control Center Findings
- Existing route: `/notifications/sms-dynamic-rules` (Rule Management + Lifecycle tabs)
- New route: `/notifications/sms-governance/releases` (dedicated release management page)
- API clients: `sms-governance-lifecycle-api.ts`, `sms-dynamic-rules-api.ts`
- Pattern: Server Component page + Client Component panel

---

## 2. Architecture Decisions

### 2.1 Release State Machine
```
draft → pending_review (submit-review)
pending_review → approved (all approval stages pass)
pending_review → rejected (any stage rejects)
approved → scheduled (schedule with future date)
approved → active (activate immediately)
approved → archived (abandon)
scheduled → active (worker activates when due OR immediate call)
scheduled → archived (abandon scheduled)
active → superseded (new active release replaces it)
active → archived (explicit archive)
rejected → archived (abandon rejected)
rejected → draft (resubmit as fresh draft)
activation_failed → archived (abandon failed)
activation_failed → draft (retry from draft)
```

### 2.2 Approval Workflow
- Multi-stage ordered: Stage 1 must complete before Stage 2 is opened
- Stage defined by `ApprovalStage` int (1-based ordering)
- Required approvals: minimum approvals per stage (default 1)
- Rejection at any stage → release moves to `rejected`; all pending requests cancelled
- Final stage approved → release moves to `approved`
- Approval decisions are append-only (`SmsGovernanceApprovalDecision`)
- All transitions create `SmsGovernanceReleaseAuditEvent` entries (append-only audit trail)

### 2.3 Activation Semantics
- For release items with `ActionType = activate`: enables the referenced rule pack/rule/profile
- Calls `ISmsGovernanceVersioningService.SnapshotRulePackAsync`/`SnapshotRuleAsync` when entities are mutated
- Failure: marks release `activation_failed`, leaves existing active governance intact
- Previously active release moves to `superseded` on successful activation
- Uses EF transaction where practical

### 2.4 Scheduled Activation Worker
- `SmsGovernanceReleaseActivationWorker` — disabled by default (consistent with other non-critical workers)
- Polls every `SmsGovernanceReleasesManagement:ScheduledActivationPollMinutes` minutes (default 5)
- Max batch: 10 scheduled releases per cycle
- Fire-and-forget per release; individual activation failures do not abort the cycle
- 90-second startup delay to avoid boot contention

### 2.5 Security
- No raw phone numbers stored in release/approval tables
- No message content stored
- No credentials or secrets in release/approval/audit tables
- `MetadataJson`/`EntitySnapshotJson` explicitly exclude sensitive fields
- All 13 endpoints require `PlatformAdmin` policy (`Policies.AdminOnly`)
- Approval decisions carry `ApprovedBy` string (username/ID) for accountability

---

## 3. Database Schema

### New Tables
| Table | Entity | Description |
|---|---|---|
| `ntf_SmsGovernanceReleasePackages` | `SmsGovernanceReleasePackage` | Release package metadata + state machine |
| `ntf_SmsGovernanceReleaseItems` | `SmsGovernanceReleaseItem` | Grouped governance changes within a release |
| `ntf_SmsGovernanceApprovalRequests` | `SmsGovernanceApprovalRequest` | Per-stage approval request tracking |
| `ntf_SmsGovernanceApprovalDecisions` | `SmsGovernanceApprovalDecision` | Append-only approval/rejection decisions |
| `ntf_SmsGovernanceReleaseAuditEvents` | `SmsGovernanceReleaseAuditEvent` | Append-only release lifecycle audit trail |

### Key Indexes
- `ntf_SmsGovernanceReleasePackages`: Status, TenantId, ScheduledActivationAtUtc (for worker query)
- `ntf_SmsGovernanceReleaseItems`: ReleasePackageId + EntityType + EntityId (unique)
- `ntf_SmsGovernanceApprovalRequests`: ReleasePackageId + ApprovalStage (composite)
- `ntf_SmsGovernanceApprovalDecisions`: ApprovalRequestId, DecidedBy
- `ntf_SmsGovernanceReleaseAuditEvents`: ReleasePackageId + OccurredAtUtc

### Migration
`20260512000006_AddSmsGovernanceReleaseManagement`

---

## 4. API Endpoints (13, all PlatformAdmin)

| Method | Path | Description |
|---|---|---|
| GET | `/v1/admin/sms/governance/releases` | List releases (paginated, filterable by status) |
| GET | `/v1/admin/sms/governance/releases/{id}` | Get release detail with items + approvals |
| POST | `/v1/admin/sms/governance/releases` | Create new draft release |
| POST | `/v1/admin/sms/governance/releases/{id}/items` | Add governance item to release |
| DELETE | `/v1/admin/sms/governance/releases/{id}/items/{itemId}` | Remove item from draft release |
| POST | `/v1/admin/sms/governance/releases/{id}/submit-review` | Submit draft for multi-stage review |
| POST | `/v1/admin/sms/governance/releases/{id}/approve` | Approve current approval stage |
| POST | `/v1/admin/sms/governance/releases/{id}/reject` | Reject release at current stage |
| POST | `/v1/admin/sms/governance/releases/{id}/schedule` | Schedule approved release for future activation |
| POST | `/v1/admin/sms/governance/releases/{id}/activate` | Activate approved release immediately |
| POST | `/v1/admin/sms/governance/releases/{id}/archive` | Archive release (any terminal-eligible state) |
| GET | `/v1/admin/sms/governance/releases/{id}/audit` | Get append-only audit trail for a release |
| GET | `/v1/admin/sms/governance/approvals/pending` | Get all pending approval requests |

---

## 5. Files Added

### Notifications Service — New
- `Notifications.Domain/SmsGovernanceReleasePackage.cs`
- `Notifications.Domain/SmsGovernanceReleaseItem.cs`
- `Notifications.Domain/SmsGovernanceApprovalRequest.cs`
- `Notifications.Domain/SmsGovernanceApprovalDecision.cs`
- `Notifications.Domain/SmsGovernanceReleaseAuditEvent.cs`
- `Notifications.Application/Options/SmsGovernanceReleaseManagementOptions.cs`
- `Notifications.Application/Interfaces/ISmsGovernanceReleaseService.cs`
- `Notifications.Application/Interfaces/ISmsGovernanceApprovalWorkflowService.cs`
- `Notifications.Infrastructure/Data/Configurations/SmsGovernanceReleasePackageConfiguration.cs`
- `Notifications.Infrastructure/Data/Configurations/SmsGovernanceReleaseItemConfiguration.cs`
- `Notifications.Infrastructure/Data/Configurations/SmsGovernanceApprovalRequestConfiguration.cs`
- `Notifications.Infrastructure/Data/Configurations/SmsGovernanceApprovalDecisionConfiguration.cs`
- `Notifications.Infrastructure/Data/Configurations/SmsGovernanceReleaseAuditEventConfiguration.cs`
- `Notifications.Infrastructure/Data/Migrations/20260512000006_AddSmsGovernanceReleaseManagement.cs`
- `Notifications.Infrastructure/Services/SmsGovernanceReleaseService.cs`
- `Notifications.Infrastructure/Services/SmsGovernanceApprovalWorkflowService.cs`
- `Notifications.Infrastructure/Workers/SmsGovernanceReleaseActivationWorker.cs`
- `Notifications.Api/Endpoints/SmsGovernanceReleaseEndpoints.cs`

### Control Center — New
- `apps/control-center/src/lib/sms-governance-release-api.ts`
- `apps/control-center/src/components/sms-governance/governance-release-panel.tsx`
- `apps/control-center/src/app/notifications/sms-governance/releases/page.tsx`

## 6. Files Modified

- `Notifications.Infrastructure/Data/NotificationsDbContext.cs` — +5 DbSets
- `Notifications.Infrastructure/DependencyInjection.cs` — +options binding, +2 services (approval workflow before release to satisfy DI order), +worker
- `Notifications.Infrastructure/Data/Migrations/NotificationsDbContextModelSnapshot.cs` — +5 entity blocks
- `Notifications.Api/Program.cs` — +`MapSmsGovernanceReleaseEndpoints()`
- `Notifications.Api/appsettings.json` — +`SmsGovernanceReleasesManagement` section

---

## 7. Validation

### Build Results (2026-05-12)
- `Notifications.Domain` — **Build succeeded, 0 errors**
- `Notifications.Application` — **Build succeeded, 0 errors**
- `Notifications.Infrastructure` — **Build succeeded, 0 errors** (CS7095 + CS8669 nullable snapshot warnings — pre-existing pattern from LS-020)
- `Notifications.Api` — **Build succeeded, 0 errors** (MSB3277 JwtBearer version conflict — pre-existing across all services)
- `Notifications.Tests` — Build failed (CS0535 stub errors — pre-existing from LS-020 interface additions, not caused by LS-021)
- `CareConnect.Tests` — Build failed (CS7036 constructor stub errors — pre-existing, not caused by LS-021)

### Runtime
- Workflow `Start application` running — service starts without crash
- All 13 endpoints registered under `/v1/admin/sms/governance/releases` and `/v1/admin/sms/governance/approvals`
- `SmsGovernanceReleaseActivationWorker` starts but exits loop immediately (`ScheduledActivationWorkerEnabled: false` by default)

---

## 8. Known Gaps / Next Steps

- **Approver role granularity:** Current implementation accepts any PlatformAdmin as an approver at any stage. Granular approver matching (e.g., "SeniorAdmin" for Stage 2) is an extension point in `SmsGovernanceApprovalRequest.RequiredApproverRole`.
- **Cross-tenant promotion:** Promoting a global release to a specific tenant is not in scope for LS-021.
- **Full governance publish semantics:** Activation currently enables/marks entities active. Bumping effective dates or coordinated pack publication can be extended in a follow-up ticket.
- **Test stub updates:** `Notifications.Tests` stub `StubNotificationAttemptRepository` needs the 3 methods added in LS-020 (`GetStaleSmsAttemptsAsync`, `UpdateReconciliationTrackingAsync`, `UpdateCostAsync`). Pre-existing gap, not introduced by LS-021.
