# LS-NOTIF-SMS-024 — Cross-Channel Governance Federation and Unified Communications Governance Topology

**Status:** COMPLETED  
**Migration:** `20260513000000_AddGovernanceFederation`  
**Depends on:** LS-NOTIF-SMS-017 through LS-NOTIF-SMS-023  
**Build:** ✅ Succeeded — 0 errors (warnings pre-existing: NU1902/MailKit, CS7095, CS8669 snapshot nullability)

---

## 1. Initial Codebase Analysis

### 1.1 Existing Channel Model
- `NotificationChannel` enum (Enums.cs): `Email`, `Sms`, `Push`, `InApp`
- String aliases: `"email"`, `"sms"`, `"push"`, `"in-app"` / `"inapp"`
- No `webhook` enum value — added as governance channel type string only (not mutating the enum)
- `ProviderWebhookLog`, `TenantChannelProviderSetting` exist in domain but use string-based channel type
- `ChannelType` string field already used in `SmsOperationalEscalationPolicy` and `SmsOperationalAlertEscalation`

### 1.2 SMS Governance Architecture (LS-017 through LS-023)
- **Entities**: `SmsGovernancePack`, `SmsGovernanceRule`, `SmsGovernanceRuleVersion`, `SmsGovernanceRulePackVersion`, `SmsGovernanceRuleMatchMetric`, `SmsGovernanceReleasePackage`, `SmsGovernanceReleaseItem`, `SmsGovernanceRolloutPlan`, `SmsGovernanceRolloutStage`, `SmsGovernanceTenantCohort`, `SmsGovernanceTenantRulePackAssignment`, `SmsGovernanceTenantOverlay`, `SmsGovernanceTenantAssignmentAuditEvent`, approval entities
- **Resolver**: `SmsGovernanceRuleResolver` — already has `ISmsGovernanceTenantResolutionService` injection (LS-023). Extension point: after `BuildFinalRuleSet()`, if federation enabled and channel scope exists, topology resolver enriches effective rules.
- **Resolution context**: `GovernanceResolutionContext(NotificationId, TemplateId, RolloutPlanId, ReleasePackageId, EvaluationContext, NowUtc)` — topology resolver accepts this as-is
- **Table prefix**: `ntf_SmsGovernance*` for SMS, `ntf_Governance*` for cross-channel federation

### 1.3 Existing Options Pattern
All governance options in `Notifications.Application/Options/`:
- `SmsGovernanceOptions`, `SmsGovernanceDynamicOptions`, `SmsGovernanceVersioningOptions`, `SmsGovernanceAnalyticsOptions`, `SmsGovernanceReleaseManagementOptions`, `SmsGovernanceRolloutsOptions`, `SmsGovernanceTenantScopingOptions`

### 1.4 DI Registration Location
`Notifications.Infrastructure/DependencyInjection.cs` — sequential registration, LS-023 services registered before LS-024 federation services (correct dependency order).

### 1.5 API Routing Pattern
Base: `/notifications/v1/admin/sms/governance/{feature}`  
New federation base: `/notifications/v1/admin/governance/` (drops `sms/` — cross-channel)  
Auth: `BuildingBlocks.Authorization.Policies.AdminOnly`

### 1.6 Control Center Governance UI
Pages: `apps/control-center/src/app/notifications/sms-governance/`  
New page: `/notifications/governance/federation` (new top-level governance section)  
Lib: `apps/control-center/src/lib/governance-federation-api.ts`

### 1.7 Existing Analytics Pattern
`ISmsGovernanceAnalyticsService` — `GovernanceAnalyticsQuery`, methods for metrics/summary/decisions/policy stats.  
Federation analytics reuse these patterns, adding channel dimension.

---

## 2. Files Added

### Domain (`Notifications.Domain/`)
- `GovernanceChannelScope.cs` — channel participation registry entity. Fields: `ChannelType`, `ScopeMode`, `Enabled`, `Priority`, `Description`. Scope modes: `isolated_channel`, `inherited_channel`, `federated_shared`, `tenant_federated`, `rollout_federated`.
- `GovernanceFederatedRulePack.cs` — federation mapping between a rule pack and a channel. Fields: `RulePackId`, `ChannelType`, `FederationGroup`, `TenantId`, `Enabled`, `Priority`, `EffectiveFrom/To`. Methods: `Activate()`, `Disable()`, `IsEffective(DateTime)`.
- `GovernanceFederationOverlay.cs` — non-destructive in-memory governance modifier. Fields: `ChannelType`, `TenantId`, `RulePackId`, `RuleId`, `OverlayType`, `OverlayState`, `OverlayJson`, `Priority`. Methods: `Activate()`, `Disable()`, `IsEffective(DateTime)`, `HasSensitiveContent()`. Overlay types: add_rule, disable_rule, suppress_rule, override_severity, override_pattern, override_metadata, override_classification, channel_override, tenant_channel_override.
- `GovernanceFederationAuditEvent.cs` — append-only audit trail. Fields: `TenantId`, `ChannelType`, `FederationGroup`, `EntityType`, `EntityId`, `EventType`, `PreviousState`, `NewState`, `Actor`, `Reason`, `MetadataJson`. 15 event type constants.

### Application (`Notifications.Application/`)
- `Options/GovernanceFederationOptions.cs` — `GovernanceFederation` config section. 9 settings including `Enabled`, `DefaultScopeMode`, `FailOpenOnFederationError`, `EnableCrossChannelOverlays`, `MaxFederatedPacksPerChannel`.
- `Interfaces/IGovernanceFederationService.cs` — 9 interface methods + 22 request/result/query record types. Full request model for channel scopes, federated packs, overlays.
- `Interfaces/IGovernanceTopologyResolver.cs` — 3 interface methods (`ResolveTopologyAsync`, `ResolveEffectiveRulesAsync`, `ExplainTopologyAsync`) + 12 record types for topology graph, effective rules, and step-by-step explanation.
- `Interfaces/IGovernanceFederationAnalyticsService.cs` — 4 interface methods + analytics result records: `TopologyAnalyticsResult`, `ChannelGovernanceAnalyticsResult`, `FederatedPackAnalyticsResult`, `CrossChannelRolloutAnalyticsResult`.

### Infrastructure (`Notifications.Infrastructure/`)
- `Data/Configurations/GovernanceChannelScopeConfiguration.cs` — MySQL: `char(36)` PK, `varchar(50)` channel/scope, `tinyint(1)` enabled, 3 indexes.
- `Data/Configurations/GovernanceFederatedRulePackConfiguration.cs` — MySQL: 5 indexes including `(ChannelType, Enabled, Priority)`, `(TenantId, ChannelType, Enabled)`, effective window.
- `Data/Configurations/GovernanceFederationOverlayConfiguration.cs` — MySQL: 5 indexes covering channel/tenant/type/effective combinations.
- `Data/Configurations/GovernanceFederationAuditEventConfiguration.cs` — MySQL: 4 indexes, no updates (append-only).
- `Data/Migrations/20260513000000_AddGovernanceFederation.cs` — creates all 4 tables with 17 total indexes, 2 Up/Down methods, full rollback.
- `Services/GovernanceFederationService.cs` — `IGovernanceFederationService` implementation. Channel scope CRUD, federated pack federation/disable, federation overlay create/activate/disable. Writes audit events to `ntf_GovernanceFederationAuditEvents`. Validates: duplicate scopes (409-style), effective date ranges, pack existence, sensitive overlay content (blocks password/token/secret/bearer/apikey).
- `Services/GovernanceTopologyResolver.cs` — `IGovernanceTopologyResolver` implementation. 5-step resolution: global packs → channel-federated packs → tenant LS-023 resolution → tenant-federated packs → federation overlays. In-memory overlay application (non-destructive). Rollout override metadata threading. Returns `GovernanceTopologyGraph` with per-layer pack lists, overlay summaries, rule counts, warnings. `ExplainTopologyAsync` returns numbered step-by-step explanation. Uses `SmsGovernanceRulePack` for global packs (`Status == "active" && Enabled == true`), `SmsGovernanceRule.Enabled` for rule counts.
- `Services/GovernanceFederationAnalyticsService.cs` — `IGovernanceFederationAnalyticsService` implementation. Topology analytics: total/enabled scopes, federated packs, overlays, audit events, per-channel breakdown. Channel analytics: federated pack counts, global pack counts, tenant coverage, top pack stats. Cross-channel rollout analytics: active rollouts via `RolloutStates.ActiveStates`, active channel types.

### API (`Notifications.Api/`)
- `Endpoints/GovernanceFederationEndpoints.cs` — 14 endpoints under `/notifications/v1/admin/governance/`. All require `AdminOnly`. Endpoints: GET/POST/PUT channel-scopes, GET/POST federated-rule-packs + /disable, GET/POST federation-overlays + /activate + /disable, GET topology + topology/explain, GET federation/audit, GET federation/analytics.

### Control Center (`apps/control-center/`)
- `src/lib/governance-federation-api.ts` — typed API client. 6 channel types, 5 scope modes, 9 overlay types. `buildGovernanceFederationApi(token)` factory with methods for all 14 endpoints. Color helpers: `channelBadgeColor`, `scopeModeBadgeColor`, `overlayStateBadgeColor`.
- `src/app/notifications/governance/federation/page.tsx` — Server Component dashboard. 4 KPI stat cards, SMS topology graph (scope mode + 6 metric tiles), channel scopes table, federated rule packs table, federation overlays table, recent audit trail, architectural enforcement note. All data loaded with `Promise.allSettled` (graceful degradation). Requires `PlatformAdmin`.

---

## 3. Files Modified

| File | Change |
|------|--------|
| `Notifications.Infrastructure/Data/NotificationsDbContext.cs` | +4 DbSet properties, +4 ApplyConfiguration calls |
| `Notifications.Infrastructure/Data/Migrations/NotificationsDbContextModelSnapshot.cs` | +4 entity model blocks at end |
| `Notifications.Infrastructure/DependencyInjection.cs` | +`GovernanceFederationOptions` bind, +3 scoped services |
| `Notifications.Api/appsettings.json` | +`GovernanceFederation` config section (9 keys) |
| `Notifications.Api/Program.cs` | +`app.MapGovernanceFederationEndpoints()` |

---

## 4. Database Schema Changes

### New Tables (prefix: `ntf_Governance*`)

**`ntf_GovernanceChannelScopes`** (5 columns + audits)
- PK: `Id char(36)`
- `ChannelType varchar(50)`, `ScopeMode varchar(50)`, `Enabled tinyint(1)`, `Priority int`
- Indexes: `(ChannelType, Enabled)`, `(ScopeMode, Enabled)`, `(Priority)`

**`ntf_GovernanceFederatedRulePacks`** (11 columns + audits)
- `RulePackId char(36)`, `ChannelType varchar(50)`, `FederationGroup varchar(200)`, `TenantId char(36)`, `Enabled tinyint(1)`, `Priority int`, `EffectiveFrom/To datetime(6)`
- Indexes: `(ChannelType, Enabled, Priority)`, `(RulePackId, ChannelType)`, `(TenantId, ChannelType, Enabled)`, `(FederationGroup, Enabled)`, `(EffectiveFrom, EffectiveTo)`

**`ntf_GovernanceFederationOverlays`** (13 columns + audits)
- `TenantId char(36)`, `ChannelType varchar(50)`, `RulePackId char(36)`, `RuleId char(36)`, `OverlayType varchar(50)`, `OverlayState varchar(50)`, `OverlayJson varchar(4000)`, `Priority int`, `Enabled tinyint(1)`, `EffectiveFrom/To datetime(6)`
- Indexes: `(ChannelType, Enabled, Priority)`, `(TenantId, ChannelType, Enabled)`, `(RulePackId, ChannelType)`, `(RuleId, ChannelType)`, `(OverlayType, Enabled)`

**`ntf_GovernanceFederationAuditEvents`** (11 columns, append-only)
- `TenantId char(36)`, `ChannelType varchar(50)`, `FederationGroup varchar(200)`, `EntityType varchar(100)`, `EntityId char(36)`, `EventType varchar(100)`, `PreviousState/NewState varchar(100)`, `Actor varchar(200)`, `Reason varchar(1000)`, `MetadataJson varchar(4000)`
- Indexes: `(TenantId, CreatedAt)`, `(ChannelType, CreatedAt)`, `(EventType, CreatedAt)`, `(EntityType, EntityId)`

**Total new indexes: 17**

---

## 5. API Changes

### New Endpoints (14)

| Method | Path | Description |
|--------|------|-------------|
| GET | `/notifications/v1/admin/governance/channel-scopes` | List channel scopes (filtered) |
| POST | `/notifications/v1/admin/governance/channel-scopes` | Create channel scope |
| PUT | `/notifications/v1/admin/governance/channel-scopes/{id}` | Update channel scope |
| GET | `/notifications/v1/admin/governance/federated-rule-packs` | List federated packs |
| POST | `/notifications/v1/admin/governance/federated-rule-packs` | Federate a rule pack to a channel |
| POST | `/notifications/v1/admin/governance/federated-rule-packs/{id}/disable` | Disable federated pack |
| GET | `/notifications/v1/admin/governance/federation-overlays` | List overlays |
| POST | `/notifications/v1/admin/governance/federation-overlays` | Create federation overlay |
| POST | `/notifications/v1/admin/governance/federation-overlays/{id}/activate` | Activate overlay |
| POST | `/notifications/v1/admin/governance/federation-overlays/{id}/disable` | Disable overlay |
| GET | `/notifications/v1/admin/governance/topology` | Full topology graph for channel |
| GET | `/notifications/v1/admin/governance/topology/explain` | Step-by-step topology explanation |
| GET | `/notifications/v1/admin/governance/federation/audit` | Audit trail (paginated) |
| GET | `/notifications/v1/admin/governance/federation/analytics` | Analytics dashboard data |

All endpoints: `AdminOnly` policy.  
Query params on list endpoints: `page`, `pageSize` (bounded 1–200).

---

## 6. SMS Backward Compatibility Guarantee

- All LS-017 through LS-023 behaviors preserved — zero mutation of existing paths.
- `SmsGovernanceRuleResolver` unchanged — topology resolver enrichment is opt-in (requires `GovernanceFederation:Enabled=true` and a channel scope for `sms` to exist).
- `GovernanceResolutionContext` record unchanged — topology resolver accepts it as-is for `RolloutPlanId`/`ReleasePackageId` threading.
- `ISmsGovernanceTenantResolutionService.ResolveEffectiveRulePacksAsync(Guid? tenantId, context, ct)` signature unchanged.
- Federation topology resolver wraps LS-023 calls in try/catch — resolution degrades gracefully if tenant resolution fails.
- SMS global packs queried with correct guard: `Status == "active" && Enabled == true` (no `IsActive` property).
- `SmsGovernanceRolloutPlan` active state via `RolloutStates.ActiveStates.Contains(r.RolloutState)` (not `.Status`).

---

## 7. Topology Resolution Order

```
1. Global governance packs (existing LS-019 path)
   └─ SmsGovernanceRulePacks WHERE TenantId IS NULL AND Enabled AND Status='active'
   
2. Channel-federated packs (new LS-024)
   └─ GovernanceFederatedRulePacks WHERE ChannelType=? AND Enabled AND TenantId IS NULL AND effective
   
3. Tenant-assigned packs from LS-023 (SMS only)
   └─ ISmsGovernanceTenantResolutionService.ResolveEffectiveRulePacksAsync → AssignedPackIds
   
4. Tenant-scoped federated packs (new LS-024)
   └─ GovernanceFederatedRulePacks WHERE TenantId=? AND ChannelType=? AND effective
   
5. Federation overlays (in-memory, non-destructive)
   └─ GovernanceFederationOverlays WHERE enabled AND active AND effective
   └─ Applied in-memory: disable, suppress, override_severity, add_rule
```

---

## 8. Validation Results

| Check | Result |
|-------|--------|
| `dotnet build Notifications.Api` | ✅ Build succeeded — 0 errors |
| `dotnet build Notifications.Infrastructure` | ✅ Build succeeded — 0 errors |
| Pre-existing warnings preserved | ✅ NU1902/MailKit, CS7095, CS8669 snapshot (all pre-existing) |
| New errors introduced | ✅ None |
| Workflow restart | ✅ Application running, no notification service errors in logs |
| API endpoint mapping | ✅ `app.MapGovernanceFederationEndpoints()` in Program.cs |
| DI registration order | ✅ LS-023 services registered before LS-024 (correct dependency order) |
| `SmsGovernanceRulePack.IsActive` compile error | ✅ Fixed → `Enabled && Status == "active"` |
| `SmsGovernanceRolloutPlan.Status` compile error | ✅ Fixed → `RolloutStates.ActiveStates.Contains(r.RolloutState)` |

---

## 9. Known Gaps / Limitations

- **Email, Push, Webhook, InApp enforcement**: Channel scopes and federated rule packs for these channels record topology orchestration intent. Active rule enforcement requires per-channel rule engine implementations analogous to `SmsGovernanceRuleEngine` — planned future features. SMS governance (LS-017 through LS-023) remains fully enforced and backward compatible.
- **Time-series analytics**: `GovernanceFederationAnalyticsService` returns live DB aggregate counts. Historical time-series analytics (like SMS `SmsGovernanceRuleMatchMetric`) require per-channel delivery instrumentation not yet implemented for non-SMS channels.
- **SmsGovernanceRuleResolver enrichment**: Optional topology enrichment in `SmsGovernanceRuleResolver` is deliberately not wired — the resolver already gets tenant-resolution from LS-023. Federation topology is resolved independently via the `IGovernanceTopologyResolver` API endpoints rather than during message-send resolution, avoiding SMS path latency impact.

---

## 10. Recommended Next Steps

- Add per-channel rule engines for Email, Push, Webhook (analogous to `SmsGovernanceRuleEngine`)
- Wire topology resolver into Email/Push delivery pipelines once enforcement exists
- Add time-series match metrics for non-SMS channels via `GovernanceFederationAuditEvent`
- Add federation group-based rollout targeting in LS-022 rollout UI (Control Center)
- Consider topology caching (`GovernanceFederation:CacheTopology=true`) once traffic warrants it
