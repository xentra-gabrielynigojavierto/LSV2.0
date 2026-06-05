# LS-NOTIF-SMS-025 — Federated Cross-Channel Governance Enforcement Engines and Unified Policy Execution Runtime

**Status:** IN PROGRESS  
**Migration:** `20260513010000_AddGovernanceExecutionRuntime`  
**Depends on:** LS-NOTIF-SMS-017 through LS-NOTIF-SMS-024

---

## 1. Initial Codebase Analysis

### 1.1 Existing SMS Governance Execution Insertion Points
- `NotificationService.ExecuteSendLoopAsync` (line 854, `NotificationService.cs`)
  - **Standard governance (line 934):** `SmsGovernancePolicyService` — quiet_hours, geographic_restriction, rate_limit. SMS only.
  - **Template governance (line 989):** `SmsTemplateGovernanceService` — content approval, prohibited content. SMS only.
  - **Email send (line 1099):** `_sendGridAdapter.SendAsync` — no governance evaluation currently. **LS-025 email integration point: line 1095** (before `ProviderFailure? lastFailure = null;` and the failover loop).
  - **SMS send (line 1109):** Existing governance already runs before the loop.

### 1.2 Email Delivery Pipeline
- `ExecuteSendLoopAsync` → `_sendGridAdapter.SendAsync(EmailSendPayload { To, Subject, Body, Html })`
- `RenderedSubject` and `RenderedBody` already extracted into local vars `subject`, `body`, `html` before the failover loop
- **Integration point:** Insert governance eval at line ~1065 for email channel, before `ProviderFailure? lastFailure = null;`
- If governance blocks: set `notification.Status = "dead_letter"` or similar using existing outcome path

### 1.3 Push Delivery Pipeline
- Push channel is a **reserved placeholder** — no active provider implementation in `ExecuteSendLoopAsync`
- No `IEmailProviderAdapter`-equivalent for push
- **LS-025 approach:** Runtime and engines fully implemented; integration deferred (documented gap)

### 1.4 Webhook Delivery Pipeline
- General webhook: reserved placeholder in `ExecuteSendLoopAsync`
- Webhook provider (Teams/Slack): `SmsAlertEscalationChannelAdapters.cs` — specialized for SMS alert escalations only
- `WebhookGovernanceEnforcementEngine` will fail-open with `insufficient_context` when safe payload metadata is absent
- **LS-025 approach:** Runtime and engines fully implemented; integration deferred (documented gap)

### 1.5 Notification/NotificationAttempt Payload
- `Notification`: `RenderedSubject`, `RenderedBody`, `Channel`, `TenantId`, `TemplateId`, `TemplateKey`
- `NotificationAttempt`: `Status`, `FailureCategory` — existing dead-letter/block mechanisms available
- **PII safety:** `To` (email address/phone) never stored in governance telemetry; only IDs and safe metadata

### 1.6 Existing Governance Decision Persistence Patterns
- `SmsGovernanceDecision` — `ntf_SmsGovernanceDecisions`: policy-level decisions (quiet_hours, rate_limit, etc.)
- `SmsTemplateGovernanceDecision` — `ntf_SmsTemplateGovernanceDecisions`: content governance outcomes
- `SmsGovernanceRuleMatchMetric` — `ntf_SmsGovernanceRuleMatchMetrics`: rolling match metrics
- **LS-025 adds:** `GovernanceExecutionRecord` — `ntf_GovernanceExecutionRecords`: unified cross-channel telemetry

### 1.7 LS-NOTIF-SMS-024 IGovernanceTopologyResolver
- `ResolveTopologyAsync(GovernanceTopologyRequest) → GovernanceTopologyGraph`
- `GovernanceTopologyGraph`: ChannelType, TenantId, ScopeMode, GlobalPacks/ChannelPacks/TenantPacks/FederatedPacks (IReadOnlyList<ChannelPackSummary>), TenantOverlays/FederationOverlays, RolloutOverrides, FinalRuleCount, Warnings
- `ChannelPackSummary`: RulePackId, PackName, Source, FederationGroup, Priority, IsGlobal, IsChannelFederated, IsTenantAssigned
- `TopologyEffectiveRule`: RuleId, RuleName, RuleType, Severity, ChannelType, Source, OverrideApplied — **does NOT contain Pattern/MetadataJson**
- **LS-025 approach:** `GovernanceRuleEvaluationHelper` extracts pack IDs from graph → loads full `SmsGovernanceRule` records from DB (`ntf_SmsGovernanceRules WHERE RulePackId IN (...)`) → evaluates pattern/phrase matching locally

### 1.8 SmsGovernanceRule Key Fields
- `Id`, `RulePackId`, `Name`, `RuleType` (prohibited_phrase/restricted_pattern/classification_override/variable_rule/link_rule/delivery_restriction/escalation_rule)
- `Pattern` (literal or regex, max 500 chars), `Severity` (allow/warn/review_required/block/override_allowed)
- `Priority`, `Enabled`, `MetadataJson`, `CreatedAt`, `UpdatedAt`

### 1.9 SmsGovernanceRuleEngine Patterns Reused
- `prohibited_phrase`: case-insensitive `Contains`, optional whole-word boundary scan
- `restricted_pattern`: `Regex` with timeout (ReDoS protection), `RegexMatchTimeoutException` catch
- Severity ranking: allow(0) < warn(1) < override_allowed(2) < review_required(3) < block(4)
- Enforcement mode: permissive (block→review), strict (review→block), standard (no change)
- Fail-open: global catch returns `allow` with `rule_engine_error` reason

### 1.10 Control Center Federation UI Pattern
- Pages: `apps/control-center/src/app/notifications/governance/federation/page.tsx` (LS-024)
- `src/lib/governance-federation-api.ts` (LS-024)
- **LS-025 adds:** `/notifications/governance/runtime` page + `governance-runtime-api.ts`

---

## 2. Files Added

### Domain
- `Notifications.Domain/GovernanceExecutionRecord.cs`

### Application/Options
- `Notifications.Application/Options/GovernanceExecutionRuntimeOptions.cs`

### Application/Interfaces
- `Notifications.Application/Interfaces/IGovernanceExecutionRuntime.cs` — runtime orchestrator + all shared models (GovernanceExecutionContext, GovernanceExecutionResult, GovernanceSimulationRequest, GovernanceSimulationResult, GovernanceChannelRuntimeStatus, DecisionTypes, ReasonCodes)
- `Notifications.Application/Interfaces/IGovernanceChannelEnforcementEngine.cs`
- `Notifications.Application/Interfaces/IGovernanceExecutionTelemetryService.cs` — with GovernanceExecutionQuery, GovernanceRuntimeTelemetryQuery, GovernanceRuntimeTelemetryResult

### Infrastructure/Data/Configurations
- `Notifications.Infrastructure/Data/Configurations/GovernanceExecutionRecordConfiguration.cs`

### Infrastructure/Data/Migrations
- `Notifications.Infrastructure/Data/Migrations/20260513010000_AddGovernanceExecutionRuntime.cs`

### Infrastructure/Services
- `Notifications.Infrastructure/Services/GovernanceRuleEvaluationHelper.cs`
- `Notifications.Infrastructure/Services/GovernanceExecutionRuntime.cs`
- `Notifications.Infrastructure/Services/EmailGovernanceEnforcementEngine.cs`
- `Notifications.Infrastructure/Services/PushGovernanceEnforcementEngine.cs`
- `Notifications.Infrastructure/Services/WebhookGovernanceEnforcementEngine.cs`
- `Notifications.Infrastructure/Services/SmsGovernanceCompatibilityEngine.cs`
- `Notifications.Infrastructure/Services/GovernanceExecutionTelemetryService.cs`

### API
- `Notifications.Api/Endpoints/GovernanceRuntimeEndpoints.cs`

### Control Center
- `apps/control-center/src/lib/governance-runtime-api.ts`
- `apps/control-center/src/app/notifications/governance/runtime/page.tsx`

---

## 3. Files Modified

- `Notifications.Infrastructure/Data/NotificationsDbContext.cs` — +1 DbSet, +1 ApplyConfiguration
- `Notifications.Infrastructure/Data/Migrations/NotificationsDbContextModelSnapshot.cs` — +1 entity block
- `Notifications.Infrastructure/DependencyInjection.cs` — +options + 4 channel engines + runtime + telemetry service
- `Notifications.Api/appsettings.json` — +GovernanceExecutionRuntime section
- `Notifications.Api/Program.cs` — +MapGovernanceRuntimeEndpoints
- `Notifications.Infrastructure/Services/NotificationService.cs` — inject IGovernanceExecutionRuntime; evaluate Email channel before failover loop

---

## 4. Database / Schema Changes

### New Table: `ntf_GovernanceExecutionRecords`
- `Id char(36)` PK
- `NotificationId char(36)?`
- `AttemptId char(36)?`
- `TenantId char(36)?`
- `ChannelType varchar(50)`
- `DecisionType varchar(50)`
- `ReasonCode varchar(100)`
- `MatchedRuleIdsJson varchar(2000)?`
- `MatchedRulePackIdsJson varchar(2000)?`
- `AppliedOverlayIdsJson varchar(2000)?`
- `ContentClassification varchar(100)?`
- `TopologyResolutionStatus varchar(50)?`
- `EngineStatus varchar(50)?`
- `SafeMetadataJson varchar(2000)?`
- `IsSimulation tinyint(1)`
- `CreatedAt datetime(6)`

Indexes:
- `(ChannelType, CreatedAt)`
- `(TenantId, ChannelType, CreatedAt)` 
- `(DecisionType, CreatedAt)`
- `(NotificationId)`
- `(IsSimulation, CreatedAt)`

---

## 5. API Changes

### New Endpoints (5)
| Method | Path | Description |
|--------|------|-------------|
| GET | `/notifications/v1/admin/governance/runtime/status` | Runtime status + engine health |
| GET | `/notifications/v1/admin/governance/runtime/channels` | Per-channel runtime status |
| GET | `/notifications/v1/admin/governance/runtime/executions` | Paginated execution records |
| GET | `/notifications/v1/admin/governance/runtime/telemetry` | Aggregate telemetry |
| POST | `/notifications/v1/admin/governance/runtime/simulate` | Simulate governance evaluation |

All: `AdminOnly` policy. No raw payloads/bodies/phones/emails/credentials exposed.

---

## 6. Email Delivery Integration

LS-025 integrates governance evaluation at line ~1065 of `NotificationService.cs` for the `email` channel:
1. If `GovernanceExecutionRuntime:Enabled = false` or `EnableEmailEnforcement = false`: skip (pass-through)
2. Build `GovernanceExecutionContext` from notification (no raw recipient/body persistence)
3. Call `IGovernanceExecutionRuntime.EvaluateAsync(context, ct)`
4. If `allow` or `warn`: continue to failover loop normally
5. If `block`, `suppress`, or `review_required`: update notification status to `blocked_by_governance`, log, and return early (skip all routes)
6. Runtime failure: fail-open (log, continue)

---

## 7. Push / Webhook Integration

Push: Channel is a reserved placeholder — `PushGovernanceEnforcementEngine` returns `allow` with `insufficient_context` reason when no payload context exists. Integration deferred until push provider is implemented.

Webhook: General webhook channel is reserved — `WebhookGovernanceEnforcementEngine` returns `allow` with `insufficient_context` when no safe payload metadata. Alert-escalation webhooks use a separate specialized pipeline. Integration deferred.

---

## 8. SMS Backward Compatibility

- Existing SMS governance (`SmsGovernancePolicyService`, `SmsTemplateGovernanceService`, `SmsGovernanceRuleEngine`) is **completely unchanged**
- `SmsGovernanceCompatibilityEngine` exists for simulation/status visibility only — does NOT duplicate SMS decision persistence
- `GovernanceExecutionRuntime:EnableSmsCompatibilityRuntime = false` by default — SMS channel bypasses runtime evaluation

---

## 9. Validation Results

**Build:** `dotnet build Notifications.Api.csproj --no-restore` — **0 errors, 28 warnings (pre-existing)**

Warnings are all pre-existing (CS8669 on snapshot nullable annotations, NU1902 MailKit vulnerability, CS8600/CS8604 from SmsGovernanceTenantResolutionService, CS7095 from SmsRecipientIntelligenceWorker). No new warnings introduced by LS-025.

---

## 10. Known Gaps / Limitations

- Push governance enforcement requires push provider implementation (reserved channel)
- Webhook governance enforcement (general) requires webhook delivery pipeline implementation (reserved channel)
- Alert-escalation webhook (Teams/Slack) has its own specialized pipeline separate from the runtime
- `TopologyEffectiveRule` does not carry Pattern/MetadataJson; GovernanceRuleEvaluationHelper loads full SmsGovernanceRule records from DB to perform content evaluation

---

## 11. Recommended Next Steps

- Implement push provider and integrate `PushGovernanceEnforcementEngine` into push send path
- Implement general webhook delivery pipeline and integrate `WebhookGovernanceEnforcementEngine`
- Add per-channel match metrics analogous to `SmsGovernanceRuleMatchMetric` for Email/Push/Webhook
- Add governance runtime status to existing monitoring health probes
