# LS-NOTIF-SMS-018 — SMS Template Governance, Content Classification, and Delivery Compliance Enforcement

**Status:** IN PROGRESS  
**Date:** 2026-05-10  
**Service:** `apps/services/notifications`  
**Control Center:** `apps/control-center/src/`

---

## 1. Initial Codebase Analysis

### Existing Template Infrastructure

The Notifications service already has a general-purpose template system (`Template` / `TemplateVersion` entities, `ntf_Templates` / `ntf_TemplateVersions` tables) that handles multi-channel content storage and rendering. This system is **not SMS-governance-aware** — it has no approval lifecycle, content classification, prohibited-content enforcement, or governance decision persistence.

| Existing Entity | Table | Purpose |
|---|---|---|
| `Template` | `ntf_Templates` | Multi-channel templates (email, SMS, push, webhook) |
| `TemplateVersion` | `ntf_TemplateVersions` | Immutable body/subject versions; IsPublished flag |

LS-018 adds a parallel **SMS-specific governance layer** that does not replace or modify these entities.

### Existing Notification Payload Fields

| Field | Notes |
|---|---|
| `TemplateId` | FK to `Template.Id` — set during `SubmitAsync` |
| `TemplateKey` | String key — preserved for audit/logging |
| `RenderedSubject` | Fully rendered subject — set before `ExecuteSendLoopAsync` |
| `RenderedBody` | Fully rendered body — set before `ExecuteSendLoopAsync` |

### Template Rendering Behavior

`ITemplateRenderingService.Render()` is called in `SubmitAsync` (line ~1680) with Handlebars-style `{{variable}}` tokens. The rendered body is persisted on `Notification.RenderedBody` before the send loop starts.

### Governance Evaluation Insertion Points

The LS-017 governance block sits inside `ExecuteSendLoopAsync` after routing engine selection (~line 933). LS-018 template governance inserts **immediately after** the LS-017 governance block (~line 985), operating on the already-rendered `RenderedBody`.

### Existing Governance Patterns (LS-017)

- Service interface in `Notifications.Application.Interfaces`
- Options class in `Notifications.Application.Options`
- Scoped DI registration in `DependencyInjection.cs`
- Endpoints as static class in `Notifications.Api/Endpoints/`
- EF configs in `Notifications.Infrastructure/Data/Configurations/`
- Migration naming: `YYYYMMDDNNNNNN_Description`

### Existing Admin Endpoint Conventions

- MinimalAPI `MapGroup("/v1/admin/sms/...")` with `.RequireAuthorization(Policies.AdminOnly)`
- Direct `NotificationsDbContext` injection for list/read endpoints
- Service injection for mutation endpoints
- Responses never expose: raw phones, credentials, SettingsJson, webhook URLs

---

## 2. Existing Notification Payload/Template Findings

- `RenderedBody` contains the fully-rendered SMS text including all variable substitutions
- `TemplateKey` is a string key that maps to the `Template` table — not all SMS messages use a template key (inline body allowed)
- Variable rendering uses `{{varName}}` syntax via `ITemplateRenderingService`
- Rendered body is set at submit-time, before the send loop — LS-018 validates it at send-time before provider execution

---

## 3. Existing Governance Evaluation Findings

Send flow after LS-017 integration:
```
ContactEnforcement → RecipientIntelligence/Suppression (LS-016)
→ ExecuteSendLoopAsync
  → Routing engine (LS-014/015)
  → LS-017 governance pre-send
  → [LS-018 template governance ← NEW]
  → Body extraction (RenderedBody/RenderedSubject)
  → Provider send loop
```

Retry flow:
```
SmsRetrySuppressionService (LS-016)
→ LS-017 governance retry
→ [LS-018 template governance retry ← NEW]
→ ExecuteSendLoopAsync
```

---

## 4. Files Added

| File | Purpose |
|---|---|
| `Notifications.Domain/SmsTemplate.cs` | SMS template registry entity |
| `Notifications.Domain/SmsTemplateVersion.cs` | Immutable template version entity |
| `Notifications.Domain/SmsTemplateGovernanceDecision.cs` | Governance decision persistence |
| `Notifications.Application/Interfaces/ISmsTemplateGovernanceService.cs` | Service interface + request/result types |
| `Notifications.Application/Options/SmsTemplateGovernanceOptions.cs` | Feature options |
| `Notifications.Infrastructure/Data/Configurations/SmsTemplateConfiguration.cs` | EF config |
| `Notifications.Infrastructure/Data/Configurations/SmsTemplateVersionConfiguration.cs` | EF config |
| `Notifications.Infrastructure/Data/Configurations/SmsTemplateGovernanceDecisionConfiguration.cs` | EF config |
| `Notifications.Infrastructure/Data/Migrations/20260512000003_AddSmsTemplateGovernance.cs` | DB migration |
| `Notifications.Infrastructure/Services/SmsTemplateGovernanceService.cs` | Governance engine |
| `Notifications.Api/Endpoints/SmsTemplateGovernanceEndpoints.cs` | 11 admin endpoints |
| `apps/control-center/src/lib/sms-templates-api.ts` | Control Center API client |
| `apps/control-center/src/components/sms-template-governance/template-governance-panel.tsx` | Control Center UI |
| `apps/control-center/src/app/notifications/sms-templates/page.tsx` | Control Center page |

---

## 5. Files Modified

| File | Change |
|---|---|
| `Notifications.Infrastructure/Data/NotificationsDbContext.cs` | +3 DbSets, +3 ApplyConfiguration |
| `Notifications.Infrastructure/DependencyInjection.cs` | +ISmsTemplateGovernanceService Scoped + options |
| `Notifications.Api/Program.cs` | +MapSmsTemplateGovernanceEndpoints |
| `Notifications.Api/appsettings.json` | +SmsTemplateGovernance section |
| `Notifications.Infrastructure/Services/NotificationService.cs` | +template governance evaluation blocks |
| `Notifications.Infrastructure/Data/Migrations/NotificationsDbContextModelSnapshot.cs` | +3 entity snapshots |

---

## 6. Database/Schema/Config Changes

### Tables

**`ntf_SmsTemplates`**
- TenantId nullable (null = global), TemplateKey unique per scope
- Status: draft/pending_review/approved/rejected/archived
- ContentClassification: transactional/operational/escalation/compliance/marketing_restricted/prohibited
- Indexes: (TenantId, TemplateKey), (Status, Enabled), (ContentClassification)

**`ntf_SmsTemplateVersions`**
- Immutable once ApprovalStatus = approved
- Indexes: (TemplateId, VersionNumber), (ApprovalStatus), (ApprovedAt)

**`ntf_SmsTemplateGovernanceDecisions`**
- No raw phone persistence
- DecisionType: allow/warn/block/review_required
- ReasonCode: template_not_found/template_not_approved/prohibited_content/invalid_variables/classification_mismatch/marketing_restricted/unsafe_payload/governance_evaluation_error
- Indexes: (TenantId, CreatedAt), (DecisionType, CreatedAt), (ReasonCode, CreatedAt), (TemplateId)

### Migration
`20260512000003_AddSmsTemplateGovernance`

### Config
```
SmsTemplateGovernance:
  Enabled: true
  RequireApprovedTemplates: true
  FailOpenOnEvaluationError: true
  MaxTemplateLength: 1600
  MaxVariableCount: 50
  AllowInlineUntemplatedMessages: false
  RestrictedCategories: [marketing_restricted, prohibited]
```

---

## 7. API/Interface Changes

11 admin endpoints under `/v1/admin/sms/templates` (PlatformAdmin required):

| Method | Route | Description |
|---|---|---|
| GET | `/` | List templates (filters: tenantId, status, classification) |
| GET | `/{id}` | Get template detail |
| POST | `/` | Create template |
| PUT | `/{id}` | Update template metadata |
| POST | `/{id}/submit-review` | Transition draft → pending_review |
| POST | `/{id}/approve` | Approve pending_review version |
| POST | `/{id}/reject` | Reject with reason |
| GET | `/{id}/versions` | List all versions |
| POST | `/{id}/versions` | Create new draft version |
| GET | `/governance-decisions` | List governance decisions (audit) |
| POST | `/evaluate-test` | Dry-run evaluation |

---

## 8. Governance Evaluation Pipeline

### Classification Rules (local/deterministic — no AI/ML/external APIs)

| Category | Trigger Patterns |
|---|---|
| `transactional` | otp, verification code, account alert, password reset, login, delivery, order confirmation |
| `operational` | system notification, incident, maintenance, service alert, reminder |
| `escalation` | escalation, paging, critical alert, on-call, urgent incident |
| `compliance` | compliance, regulatory, legal notice, required notice |
| `marketing_restricted` | promotional, discount, offer, deal, sale, campaign, subscribe, unsubscribe |
| `prohibited` | banned phrases, spam-like patterns, telecom-risk wording |

### Variable Validation

- Detects unresolved `{{token}}` patterns in rendered body
- Enforces `MaxVariableCount` on schema
- Enforces `MaxTemplateLength` on rendered body
- Blocks delivery when unresolved tokens detected

### Prohibited Content Patterns

- Repeated URL patterns (spam-like)
- Known telecom-fraud phrases
- Casino/gambling semantics
- Mass-blast keywords

### Decision Semantics

| Decision | Producer Execution | Notification State |
|---|---|---|
| allow | continues | unchanged |
| warn | continues with log | unchanged |
| block | stopped | dead-letter |
| review_required | stopped | dead-letter |

---

## 9. Known Gaps / Issues

- None identified.

---

## 10. Recommended Next Steps

- Add tenant-configurable prohibited phrase lists (LS-NOTIF-SMS-019)
- Add template usage analytics aggregation (scheduled background task)
- Add template preview/test-render endpoint with variable substitution
