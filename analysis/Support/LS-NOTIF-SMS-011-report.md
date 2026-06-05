# LS-NOTIF-SMS-011 — SMS External Escalation and Incident Notification Integrations

## 1. Initial Codebase Analysis

Feature builds entirely inside the Notification Service boundary. No Control Center UI. No SMS sends.
All implementation follows existing LS-NOTIF-SMS-010 patterns.

## 2. Existing SMS Alert Architecture Findings

- `SmsOperationalAlert` domain entity in `Notifications.Domain/SmsOperationalAlert.cs`
- `SmsOperationalAlertEvaluator` evaluates 8 threshold rules and calls `UpsertAlertAsync` which creates or updates active alerts
- `ISmsOperationalAlertRepository` provides CRUD + dedup finders
- `SmsOperationalAlertWorker` (BackgroundService, disabled by default, 60s stagger) drives periodic evaluation
- `SmsAlertEndpoints` provides 6 PlatformAdmin endpoints under `/v1/admin/sms/alerts`
- Alert dedup key: (AlertType, TenantId, Provider, ProviderConfigId)
- No credentials, phone numbers, or provider payloads stored in alerts

## 3. Existing Notification Service Delivery/Channel Findings

- `IEmailProviderAdapter` (SendGrid) — `EmailSendPayload` / `EmailSendResult` with `ProviderFailure.Retryable`
- `InternalEmailService` wraps `IEmailProviderAdapter`, registered as scoped
- `ISmsProviderAdapter` (Twilio) — **NOT used** for escalation (would create recursive loops)
- Internal notifications have no in-app push channel; email is the safe internal channel
- Named HTTP clients: `"SendGrid"`, `"Twilio"` — adding `"EscalationWebhook"` for Teams/Slack

## 4. Existing Worker/Retry Pattern Findings

- `BackgroundService` with `IServiceScopeFactory` + scoped service resolution per cycle
- `configuration.GetValue("ENV_VAR", defaultValue)` for all config
- Disabled by default (`SMS_ALERTS_ENABLED=false`, `SMS_RECONCILIATION_ENABLED=false`)
- 60-second startup stagger to avoid boot contention
- Bounded batch size, fault-tolerant loop, `OperationCanceledException` exits cleanly

## 5. Existing External HTTP/Webhook Client Pattern Findings

- `services.AddHttpClient("name")` + `IHttpClientFactory.CreateClient("name")`
- `TwilioAdapter` / `SendGridAdapter` show the pattern: bounded timeout, 2xx success, non-2xx failure
- Used for `"Twilio"` and `"SendGrid"` named clients; adding `"EscalationWebhook"` named client

## 6. Existing Audit/Event Findings

- `LegalSynq.AuditClient` registered via `services.AddAuditEventClient(configuration)`
- Audit calls are fire-and-forget / non-blocking in existing services
- Not integrated into escalation (LS-NOTIF-SMS-011 scope; future enhancement)

## 7. Existing Admin Authorization Findings

- `Policies.AdminOnly` requires `Roles.PlatformAdmin`
- Applied via `.RequireAuthorization(Policies.AdminOnly)` on endpoint groups
- `ClaimsPrincipal` used for `sub` / `email` operator identity extraction
- All escalation admin APIs follow this same pattern

## 8. Existing Migration/Schema Safety-Net Findings

- Migrations named `yyyyMMddHHmmss_Name.cs` in `Notifications.Infrastructure/Data/Migrations/`
- `SeedMigrationHistoryIfNeededAsync` seeds `__EFMigrationsHistory` with `INSERT IGNORE`
- `EnsureNotificationsSchemaColumnsAsync` uses raw ADO.NET + `INFORMATION_SCHEMA` for safety-net DDL
- Model snapshot at `NotificationsDbContextModelSnapshot.cs` must be updated for EF CLI compatibility
- New migration: `20260510000002_AddSmsEscalation`

## 9. Files Added

### Domain
- `Notifications.Domain/SmsOperationalEscalationPolicy.cs` — escalation policy entity
- `Notifications.Domain/SmsOperationalAlertEscalation.cs` — escalation attempt/history entity

### Application
- `Notifications.Application/DTOs/SmsEscalationDtos.cs` — all DTOs (policy, escalation, queries, requests, responses)
- `Notifications.Application/Interfaces/ISmsOperationalEscalationPolicyRepository.cs`
- `Notifications.Application/Interfaces/ISmsOperationalAlertEscalationRepository.cs`
- `Notifications.Application/Interfaces/ISmsAlertEscalationService.cs`
- `Notifications.Application/Interfaces/ISmsAlertEscalationMessageBuilder.cs`
- `Notifications.Application/Interfaces/ISmsAlertEscalationChannelAdapter.cs`

### Infrastructure
- `Notifications.Infrastructure/Data/Configurations/SmsEscalationPolicyConfiguration.cs`
- `Notifications.Infrastructure/Data/Configurations/SmsAlertEscalationConfiguration.cs`
- `Notifications.Infrastructure/Repositories/SmsEscalationPolicyRepository.cs`
- `Notifications.Infrastructure/Repositories/SmsAlertEscalationRepository.cs`
- `Notifications.Infrastructure/Services/SmsAlertEscalationMessageBuilder.cs`
- `Notifications.Infrastructure/Services/SmsAlertEscalationChannelAdapters.cs` (Internal/Email, Teams, Slack)
- `Notifications.Infrastructure/Services/SmsAlertEscalationService.cs`
- `Notifications.Infrastructure/Workers/SmsAlertEscalationRetryWorker.cs`
- `Notifications.Infrastructure/Data/Migrations/20260510000002_AddSmsEscalation.cs`

### API
- `Notifications.Api/Endpoints/SmsEscalationEndpoints.cs` — 9 admin endpoints under `/v1/admin/sms/alerts`

## 10. Files Modified

- `Notifications.Infrastructure/Data/NotificationsDbContext.cs` — 2 DbSets + 2 ApplyConfiguration
- `Notifications.Infrastructure/DependencyInjection.cs` — repos, services, workers, http client
- `Notifications.Infrastructure/Services/SmsOperationalAlertEvaluator.cs` — escalation hook post-evaluation
- `Notifications.Infrastructure/Data/Migrations/NotificationsDbContextModelSnapshot.cs` — updated for EF CLI
- `Notifications.Api/Program.cs` — endpoint mapping + safety-net DDL + migration history seed

## 11. Database/Schema/Config Changes

### New tables
- `ntf_SmsEscalationPolicies` — policy records (target stored as `text`, masked in API responses)
- `ntf_SmsAlertEscalations` — attempt history (TargetMasked only; raw Target never stored here)

### New indexes
- `IX_SmsEscalationPolicies_Enabled_AlertType` — enabled policy lookup
- `IX_SmsAlertEscalations_AlertId` — foreign-key-style lookup by alert
- `IX_SmsAlertEscalations_Status_NextRetryAt` — retry worker polling
- `IX_SmsAlertEscalations_AlertId_PolicyId_PayloadHash` — dedup check

### New config / env vars (all disabled by default)
- `SMS_ALERT_ESCALATION_ENABLED=false`
- `SMS_ALERT_ESCALATION_RETRY_ENABLED=false`
- `SMS_ALERT_ESCALATION_RETRY_INTERVAL_MINUTES=5`
- `SMS_ALERT_ESCALATION_RETRY_BATCH_SIZE=50`
- `SMS_ALERT_ESCALATION_HTTP_TIMEOUT_SECONDS=10`

## 12. API/Interface Changes

### New endpoints under `/v1/admin/sms/alerts` (all require PlatformAdmin)
- `GET  /policies` — list escalation policies (target masked)
- `GET  /policies/{id}` — get policy by ID (target masked)
- `POST /policies` — create policy
- `PUT  /policies/{id}` — update policy
- `POST /policies/{id}/disable` — soft-disable policy
- `GET  /escalations` — list escalation history (filters: alertId, policyId, status, channelType, severity, from, to)
- `GET  /escalations/summary` — aggregate counts by status + channel
- `GET  /escalations/{id}` — single escalation (target masked)
- `POST /escalations/{id}/retry` — manually retry failed/pending escalation

## 13. Validation/Testing Performed

- Build succeeds: `dotnet build ... 0 Error(s)` (MailKit NU1902 + MSB3277 pre-existing warnings only)
- Schema: two new tables, all indexes created via migration + safety-net DDL
- Target masking verified in mapper: email → `a***@domain`, webhook → `https://host/***`
- Dedup/cooldown: `FindRecentDuplicateAsync` prevents spam within policy cooldown window
- Global disabled: when `SMS_ALERT_ESCALATION_ENABLED=false`, `EscalateAlertAsync` logs and returns immediately
- No SMS sends: no `ISmsProviderAdapter` calls in any escalation path
- No recursive loops: `ChannelType` validation rejects "sms"; Internal/Email adapter uses email only
- Retry worker disabled by default (`SMS_ALERT_ESCALATION_RETRY_ENABLED=false`)
- 429/5xx/network timeout → `Retryable=true`; 400/401/403 → `Retryable=false`
- Webhook URL not logged; only masked target in logs

## 14. Known Gaps/Issues

- PagerDuty and Opsgenie adapters not implemented (documented as future per spec)
- Audit events (sms.alert.escalation.*) not emitted — future enhancement
- Internal notification channel is implemented as email (no in-app push exists in Notification Service)
- Retry backoff is fixed 5 minutes (not exponential); configurable via future enhancement

## 15. Recommended Next Steps

- Add PagerDuty/Opsgenie adapters as separate adapter files
- Add audit event emission in SmsAlertEscalationService (after AuditClient integration verified)
- Add Control Center UI (separate LS-NOTIF-SMS-012 feature)
- Add exponential backoff for retry scheduling
- Add per-policy notification recipient config (currently target is a flat string)
