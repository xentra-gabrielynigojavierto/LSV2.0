# LS-NOTIF-SMS-019 — Tenant Custom Governance Rules, Dynamic Policy Packs, and Compliance Rule Management

**Status:** COMPLETE  
**Date:** 2026-05-11  
**Service:** `apps/services/notifications`  
**Control Center:** `apps/control-center/src/`

---

## 1. Initial Codebase Analysis

### Existing Governance Infrastructure

| Feature | Entities | Tables | Service |
|---|---|---|---|
| LS-017: Governance Policies | `SmsGovernancePolicy`, `SmsGovernanceDecision` | `ntf_SmsGovernancePolicies`, `ntf_SmsGovernanceDecisions` | `SmsGovernancePolicyService` |
| LS-018: Template Governance | `SmsTemplate`, `SmsTemplateVersion`, `SmsTemplateGovernanceDecision` | `ntf_SmsTemplates`, `ntf_SmsTemplateVersions`, `ntf_SmsTemplateGovernanceDecisions` | `SmsTemplateGovernanceService` |
| LS-019: Dynamic Rule Packs | NEW | NEW | NEW |

### LS-017 Key Patterns
- `ISmsGovernancePolicyService`: 3 evaluation methods (PreSend/Retry/Escalation)
- Evaluation requests carry `TenantId`, `NotificationId`, no raw phones
- `SmsGovernanceEvaluationResult.ShouldBlock` = decisionType is "block" or "review_required"
- Results include `Metadata` dict (safe operational fields only)
- Admin endpoints under `/v1/admin/sms/governance/policies`, PlatformAdmin required
- `SmsGovernancePolicy.PolicyJson` stores structured config JSON — no secrets

### LS-018 Key Patterns
- `SmsTemplateGovernanceService.EvaluateAsync` pipeline:
  1. Template registry + approval check
  2. Variable token validation (unresolved `{{token}}` detection)
  3. Content length check
  4. Static content classification (6 categories, 70+ keywords, local only)
  5. Restricted category enforcement
  6. Classification mismatch warn
- `SmsTemplateGovernanceService.ClassifyContent` — deterministic, no external APIs
- Decision persisted via `PersistDecision` (no raw phones, no credentials)

### LS-019 Integration Point
Dynamic rules evaluate **after** LS-018 static checks in `EvaluateAsync`. `ISmsGovernanceRuleEngine` is injected into `SmsTemplateGovernanceService`. Final decision = stricter of (static result, dynamic rule result).

### Existing Prohibited Content Patterns (LS-018 static — preserved)
- 70+ keyword arrays: TransactionalKeywords, OperationalKeywords, EscalationKeywords, ComplianceKeywords, MarketingRestrictedKeywords, ProhibitedKeywords
- URL spam pattern (≥3 URLs → prohibited)
- All kept in `SmsTemplateGovernanceService`; LS-019 dynamic rules extend these

### Control Center Governance UI (LS-017)
- Page: `/notifications/sms-governance/page.tsx`
- Fetches 5 APIs in `Promise.allSettled`, passes to `GovernancePanel`
- Auth: reads `platform_session` cookie as Bearer token
- Component: `src/components/sms-governance/governance-panel.tsx`

### Existing API Client Patterns
- `sms-governance-api.ts` uses `/api/notifications/v1/admin/sms/governance` prefix (client-side through BFF rewrite)
- `sms-governance/page.tsx` fetches server-side via `CONTROL_CENTER_API_BASE` with Bearer token

---

## 2. Existing LS-017 Governance Policy Findings

- Policy types: quiet_hours, geographic_restriction, rate_limit, provider_governance, retry_governance, escalation_guardrail
- PolicyJson validated as parseable JSON on create/update
- Decisions persisted for non-allow + noteworthy-allow (emergency override)
- `SmsGovernancePolicyService` is sealed, injected as scoped `ISmsGovernancePolicyService`

---

## 3. Existing LS-018 Template Governance Findings

- `SmsTemplateGovernanceService` is `sealed partial class` (required by `[GeneratedRegex]`)
- `ClassifyContent` is `public string` method — called from LS-019 rule engine for classification_override rules
- `ValidateVariablesAsync` is `public async Task<(bool Passed, List<string> Errors)>` — LS-019 variable_rules can extend it
- `ISmsTemplateGovernanceService` interface needs one new method: `EvaluateWithDynamicRulesAsync` (or engine injected into service)

---

## 4. Existing Content Classification / Prohibited Content Findings

Static (LS-018, preserved unchanged):
- `ProhibitedKeywords`: 10 phrases (lottery, casino, mlm, bulk sms, etc.)
- `MarketingRestrictedKeywords`: 27 phrases (discount, promo, act now, etc.)
- URL spam: 3+ URLs → prohibited
- `UrlRegex()` [GeneratedRegex]
- `UnresolvedTokenRegex()` [GeneratedRegex]
- `VariableTokenRegex()` [GeneratedRegex]

Dynamic (LS-019, new):
- Tenant-configurable prohibited phrases persisted in `ntf_SmsGovernanceRules`
- Tenant-configurable restricted regex patterns with safety controls
- Classification override rules
- Variable rules extending LS-018 variable validation
- Link/domain rules
- Delivery restriction rules
- Escalation rules

---

## 5. Existing Control Center Governance UI Findings

| File | Description |
|---|---|
| `src/app/notifications/sms-governance/page.tsx` | LS-017 governance dashboard (policies, decisions, summary) |
| `src/components/sms-governance/governance-panel.tsx` | LS-017 main UI component |
| `src/lib/sms-governance-api.ts` | LS-017 client-side API types |
| `src/app/notifications/sms-templates/page.tsx` | LS-018 template governance |
| `src/components/sms-template-governance/template-governance-panel.tsx` | LS-018 components |
| `src/lib/sms-templates-api.ts` | LS-018 API client (server-side) |

LS-019 adds `/notifications/sms-dynamic-rules` page + `sms-dynamic-rules-api.ts` + components.

---

## 6. Files Added

| File | Purpose |
|---|---|
| `Notifications.Domain/SmsGovernanceRulePack.cs` | Rule pack entity |
| `Notifications.Domain/SmsGovernanceRule.cs` | Individual rule entity |
| `Notifications.Domain/SmsComplianceProfile.cs` | Compliance profile entity |
| `Notifications.Domain/SmsComplianceProfileAssignment.cs` | Tenant/profile assignment entity |
| `Notifications.Application/Interfaces/ISmsGovernanceRuleResolver.cs` | Resolver interface + models |
| `Notifications.Application/Interfaces/ISmsGovernanceRuleEngine.cs` | Engine interface + request/result types |
| `Notifications.Application/Interfaces/ISmsGovernanceSimulationService.cs` | Simulation interface |
| `Notifications.Application/Options/SmsGovernanceDynamicOptions.cs` | Feature options |
| `Notifications.Infrastructure/Data/Configurations/SmsGovernanceRulePackConfiguration.cs` | EF config |
| `Notifications.Infrastructure/Data/Configurations/SmsGovernanceRuleConfiguration.cs` | EF config |
| `Notifications.Infrastructure/Data/Configurations/SmsComplianceProfileConfiguration.cs` | EF config |
| `Notifications.Infrastructure/Data/Configurations/SmsComplianceProfileAssignmentConfiguration.cs` | EF config |
| `Notifications.Infrastructure/Data/Migrations/20260512000004_AddSmsGovernanceDynamicRules.cs` | DB migration |
| `Notifications.Infrastructure/Services/SmsGovernanceRuleResolver.cs` | Inheritance resolver |
| `Notifications.Infrastructure/Services/SmsGovernanceRuleEngine.cs` | Dynamic rule engine |
| `Notifications.Infrastructure/Services/SmsGovernanceSimulationService.cs` | Governance simulation |
| `Notifications.Api/Endpoints/SmsGovernanceDynamicRuleEndpoints.cs` | 14 admin endpoints |
| `apps/control-center/src/lib/sms-dynamic-rules-api.ts` | Control Center API client |
| `apps/control-center/src/components/sms-dynamic-rules/dynamic-rules-panel.tsx` | Control Center UI components |
| `apps/control-center/src/app/notifications/sms-dynamic-rules/page.tsx` | Control Center page |

---

## 7. Files Modified

| File | Change |
|---|---|
| `Notifications.Infrastructure/Data/NotificationsDbContext.cs` | +4 DbSets +4 ApplyConfiguration |
| `Notifications.Infrastructure/DependencyInjection.cs` | +3 Scoped services + options |
| `Notifications.Api/Program.cs` | +MapSmsGovernanceDynamicRuleEndpoints |
| `Notifications.Api/appsettings.json` | +SmsGovernanceDynamic section |
| `Notifications.Infrastructure/Services/SmsTemplateGovernanceService.cs` | +ISmsGovernanceRuleEngine nullable injection + Step 7 dynamic evaluation |
| `Notifications.Application/Interfaces/ISmsTemplateGovernanceService.cs` | +IsDryRun + TemplateBody fields to SmsTemplateGovernanceRequest |
| `Notifications.Infrastructure/Data/Migrations/NotificationsDbContextModelSnapshot.cs` | +4 entity snapshots |

---

## 8. Database/Schema/Config Changes

### Tables

**`ntf_SmsGovernanceRulePacks`**
- TenantId nullable (null = global/platform)
- Status: draft/active/inactive/archived
- InheritanceMode: merge/override/append_only
- Indexes: (TenantId, Enabled, Priority), (Status, Enabled), (EffectiveFrom, EffectiveTo)

**`ntf_SmsGovernanceRules`**
- RulePackId FK (logical, no FK constraint for perf)
- RuleType: prohibited_phrase/restricted_pattern/classification_override/variable_rule/link_rule/delivery_restriction/escalation_rule
- Severity: allow/warn/review_required/block/override_allowed
- Pattern: phrase/regex/variable name/domain — max 500 chars
- Indexes: (RulePackId, Enabled, Priority), (RuleType, Enabled), (Severity)

**`ntf_SmsComplianceProfiles`**
- TenantId nullable
- EnforcementMode: permissive/standard/strict
- DefaultRulePackIdsJson: JSON array of rule pack IDs to include
- Indexes: (TenantId, Enabled), (EnforcementMode)

**`ntf_SmsComplianceProfileAssignments`**
- TenantId (not nullable — must target a specific tenant)
- Scope: tenant/provider/template_category/escalation
- Indexes: (TenantId, Scope, Enabled), (ProfileId)

### Migration
`20260512000004_AddSmsGovernanceDynamicRules`

### Config
```
SmsGovernanceDynamic:
  Enabled: true
  FailOpenOnEvaluationError: true
  MaxPatternLength: 500
  RegexTimeoutMs: 200
  MaxRulesPerEvaluation: 200
  PersistAllowDecisions: false
  AllowRegexRules: true
```

---

## 9. API/Interface Changes

14 admin endpoints under `/v1/admin/sms/governance/` extensions (PlatformAdmin required):

| Method | Route | Description |
|---|---|---|
| GET | `/rule-packs` | List rule packs |
| GET | `/rule-packs/{id}` | Get rule pack detail |
| POST | `/rule-packs` | Create rule pack |
| PUT | `/rule-packs/{id}` | Update rule pack |
| POST | `/rule-packs/{id}/disable` | Disable rule pack |
| GET | `/rules` | List rules (filter by packId, type, severity) |
| POST | `/rules` | Create rule (validates type, severity, pattern safety) |
| PUT | `/rules/{id}` | Update rule |
| POST | `/rules/{id}/disable` | Disable rule |
| GET | `/profiles` | List compliance profiles |
| POST | `/profiles` | Create compliance profile |
| POST | `/profiles/{profileId}/assignments` | Assign profile to tenant |
| PUT | `/profiles/{id}` | Update profile |
| POST | `/simulate` | Governance simulation (dry-run, no SMS) |
| GET | `/rule-analytics` | Rule match analytics |

---

## 10. UI/Route Changes

New page: `/notifications/sms-dynamic-rules`
- Rule packs table
- Rules editor
- Compliance profiles
- Simulation panel (dry-run)
- No raw phone display, no credentials

---

## 11. Validation

### Rule Engine
- prohibited_phrase: case-insensitive match, whole-word optional
- restricted_pattern: safe regex with timeout, catastrophic pattern rejection
- classification_override: matches classification label, overrides routing
- variable_rule: disallowed/required variable enforcement
- link_rule: URL domain allowlist/blocklist
- delivery_restriction: content-category or time-of-day restrictions
- escalation_rule: escalation context guards

### Integration
- Dynamic block → dead-letter (same as LS-018 block behavior)
- Dynamic warn → log + proceed (same as LS-018 warn behavior)
- LS-018 block still blocks even if dynamic says allow
- LS-017 governance evaluated independently (not replaced)
- Simulation returns full trace without sending SMS
- Regression: zero dynamic rules → behavior identical to LS-018-only

---

## 12. Known Gaps / Issues

- Escalation rule integration with LS-017 escalation_guardrail documented as future (too high risk for pipeline coupling)
- Delivery restriction rules currently evaluated in template governance only; LS-017 pre-send integration is future work
- No real-time rule pack hot-reload (requires service restart or cache TTL)

---

## 13. Recommended Next Steps

- Add per-tenant prohibited phrase import (CSV/bulk upload endpoint)
- Add rule match heatmap aggregation (background worker)
- Add escalation_rule ↔ LS-017 escalation_guardrail integration
- Add delivery_restriction ↔ LS-017 pre-send integration
- Add rule version history (immutable version snapshots)
