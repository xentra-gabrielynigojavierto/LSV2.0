# LS-NOTIF-SMS-010 — SMS Operational Alerting and Threshold Rules

**Feature:** SMS Operational Alerting and Threshold Rules  
**Service:** Notification Service (`apps/services/notifications`)  
**Status:** COMPLETE  
**Date:** 2026-05-10  

---

## Overview

Implements a self-contained, rule-based SMS operational alerting system inside the Notification Service boundary. A background worker periodically evaluates 8 threshold rules against `ntf_NotificationAttempts`, creates/updates persisted alert records in the new `ntf_SmsOperationalAlerts` table, and exposes 6 admin-only endpoints for alert management.

No Control Center UI is part of this feature (as spec'd). No SMS sends, retries, reconciliation calls, or provider interactions are made by any alert component.

---

## Files Created

### Domain
| File | Description |
|------|-------------|
| `Notifications.Domain/SmsOperationalAlert.cs` | Entity with classification, scope, threshold context, lifecycle, and audit fields. |

### Application
| File | Description |
|------|-------------|
| `Notifications.Application/DTOs/SmsAlertDtos.cs` | `SmsAlertQuery`, `SmsAlertDto`, `SmsAlertListResult`, `SmsAlertSummaryDto`, `SmsAlertResolveRequest`, `SmsAlertSuppressRequest`, `SmsAlertEvaluationResult`. |
| `Notifications.Application/Interfaces/ISmsOperationalAlertRepository.cs` | Repository interface: list, summary, getById, findActive, findRecentlyResolved, create, update, resolve, suppress. |
| `Notifications.Application/Interfaces/ISmsOperationalAlertEvaluator.cs` | Evaluator interface: `EvaluateAsync(windowStart, windowEnd, ct)`. |

### Infrastructure
| File | Description |
|------|-------------|
| `Notifications.Infrastructure/Data/Configurations/SmsOperationalAlertConfiguration.cs` | EF Core config: table `ntf_SmsOperationalAlerts`, 3 composite indexes. |
| `Notifications.Infrastructure/Repositories/SmsOperationalAlertRepository.cs` | Full CRUD + dedup finders. Never reads credentials/phone numbers. |
| `Notifications.Infrastructure/Services/SmsOperationalAlertEvaluator.cs` | Evaluates all 8 rules in one in-memory pass over a minimal attempt projection. |
| `Notifications.Infrastructure/Workers/SmsOperationalAlertWorker.cs` | `BackgroundService`, disabled by default (`SMS_ALERTS_ENABLED=false`), 60s startup stagger. |
| `Notifications.Infrastructure/Data/Migrations/20260510000001_AddSmsOperationalAlerts.cs` | EF migration: `CreateTable ntf_SmsOperationalAlerts` + 3 indexes. |

### API
| File | Description |
|------|-------------|
| `Notifications.Api/Endpoints/SmsAlertEndpoints.cs` | 6 endpoints under `/v1/admin/sms/alerts`, all `RequireAuthorization(Policies.AdminOnly)`. |

---

## Files Modified

| File | Change |
|------|--------|
| `Notifications.Infrastructure/Data/NotificationsDbContext.cs` | Added `DbSet<SmsOperationalAlert>` + `ApplyConfiguration(new SmsOperationalAlertConfiguration())`. |
| `Notifications.Infrastructure/DependencyInjection.cs` | Registered `ISmsOperationalAlertRepository`, `ISmsOperationalAlertEvaluator`; added `AddHostedService<SmsOperationalAlertWorker>()`. |
| `Notifications.Api/Program.cs` | Added `app.MapSmsAlertEndpoints()`, `ntf_SmsOperationalAlerts` safety-net DDL, and `20260510000001_AddSmsOperationalAlerts` to migration history seed. |
| `Notifications.Infrastructure/Data/Migrations/NotificationsDbContextModelSnapshot.cs` | Added `SmsOperationalAlert` entity block. |

---

## Database Schema

### `ntf_SmsOperationalAlerts`

| Column | Type | Notes |
|--------|------|-------|
| `Id` | `char(36)` PK | GUID |
| `AlertType` | `varchar(100)` NOT NULL | Rule code |
| `Severity` | `varchar(20)` DEFAULT 'warning' | "warning" \| "critical" |
| `TenantId` | `char(36)` NULL | Scoped tenant or null for platform |
| `Provider` | `varchar(100)` NULL | e.g. "twilio" |
| `ProviderConfigId` | `char(36)` NULL | Scoped provider config |
| `MetricValue` | `decimal(18,6)` | Observed value that triggered alert |
| `ThresholdValue` | `decimal(18,6)` | Configured threshold breached |
| `Message` | `text` | Human-readable summary |
| `EvaluationWindowStart` | `datetime(6)` | UTC start of evaluation window |
| `EvaluationWindowEnd` | `datetime(6)` | UTC end of evaluation window |
| `Status` | `varchar(20)` DEFAULT 'active' | "active" \| "resolved" \| "suppressed" |
| `OccurrenceCount` | `int` DEFAULT 1 | Incremented per evaluation cycle |
| `FirstObservedAt` | `datetime(6)` | Creation time |
| `LastObservedAt` | `datetime(6)` | Most recent matching evaluation |
| `ResolvedAt` | `datetime(6)` NULL | When resolved |
| `ResolvedBy` | `varchar(255)` NULL | Operator sub/email |
| `ResolutionNote` | `text` NULL | Optional operator note (≤1000 chars) |
| `SuppressedUntil` | `datetime(6)` NULL | Suppress re-alerting until |
| `CreatedAt` | `datetime(6)` | |
| `UpdatedAt` | `datetime(6)` | |

**Indexes:**
- `IX_SmsOperationalAlerts_Status_LastObservedAt` — primary operational query
- `IX_SmsOperationalAlerts_AlertType_Status_Scope` — deduplication lookup
- `IX_SmsOperationalAlerts_TenantId_Status_CreatedAt` — tenant-scoped view

---

## Alert Rules

| Code | Scope | Metric | Default Warning | Default Critical | Min Attempts |
|------|-------|--------|----------------|-----------------|-------------|
| `sms.failure_rate_high` | Platform | (failed+dead_letter)/total | 10% | 25% | 10 |
| `sms.dead_letter_spike` | Platform | count(dead_letter) | 5 | 20 | — |
| `sms.retry_spike` | Platform | count(retrying) | 10 | 30 | — |
| `sms.reconciliation_failure_rate_high` | Platform | vendor_lookup_failed/reconciled | 20% | 40% | 5 |
| `sms.provider_degraded` | Per-provider | (failed+dead_letter)/total | 15% | 30% | 10 |
| `sms.provider_config_failure_spike` | Platform | count(ProviderConfigFailedOutcomes) | 3 | 10 | — |
| `sms.tenant_anomaly` | Per-tenant | (failed+dead_letter)/total | 20% | 40% | 10 |
| `sms.reconciliation_stale` | Platform | count(never-reconciled, old) | 10 | 50 | — |

All thresholds are configurable via environment variables (e.g. `SMS_ALERT_FAILURE_RATE_WARNING`).

---

## API Endpoints

All endpoints require `PlatformAdmin` role.

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/v1/admin/sms/alerts` | Paginated list (filterable by status, severity, alertType, tenantId, provider, providerConfigId, from, to) |
| `GET` | `/v1/admin/sms/alerts/summary` | Aggregate counts (active/resolved/suppressed, critical/warning breakdown, activeByType) |
| `GET` | `/v1/admin/sms/alerts/{id}` | Single alert detail |
| `POST` | `/v1/admin/sms/alerts/{id}/resolve` | Resolve active alert; records resolvedBy (JWT sub) + optional note |
| `POST` | `/v1/admin/sms/alerts/{id}/suppress` | Suppress alert for 1–10080 minutes (up to 7 days) |
| `POST` | `/v1/admin/sms/alerts/evaluate` | Manually trigger one evaluation cycle (windowMinutes param, default 60) |

---

## Worker Configuration

| Variable | Default | Description |
|----------|---------|-------------|
| `SMS_ALERTS_ENABLED` | `false` | Must set to `true` to enable worker |
| `SMS_ALERTS_EVALUATION_INTERVAL_MINUTES` | `15` | How often to evaluate |
| `SMS_ALERTS_WINDOW_MINUTES` | `60` | How far back each evaluation looks |
| `SMS_ALERT_COOLDOWN_MINUTES` | `60` | Re-alerting cooldown after resolution |

---

## Deduplication Logic

1. **Active alert found** → increment `OccurrenceCount`, refresh `LastObservedAt`, update metrics/message/severity. If suppressed and `SuppressedUntil` has not expired, skip.
2. **No active alert, recently resolved within cooldown** → skip (counted as `AlertsSuppressed`).
3. **No active, no recent resolved** → create new alert.

---

## Security

- All endpoints guarded by `Policies.AdminOnly` (`PlatformAdmin` role).
- No credentials, phone numbers, `RecipientJson`, `CredentialsJson`, or `SettingsJson` are read or stored anywhere in the alert pipeline.
- Evaluator fetches a minimal 9-field projection (Status, FailureCategory, ReconciliationAttemptCount, LastReconciliationOutcome, Provider, ProviderConfigId, ProviderOwnershipMode, TenantId, CreatedAt).
- Worker is disabled by default; must be explicitly enabled.

---

## Data Flow

```
[SmsOperationalAlertWorker]  (every 15 min by default)
        ↓
[SmsOperationalAlertEvaluator.EvaluateAsync(windowStart, windowEnd)]
        ↓
  1. Fetch minimal projection of ntf_NotificationAttempts (Channel='sms', in window)
  2. Apply 8 rules in-memory
  3. For each breach:
       FindActiveAlertAsync → update OccurrenceCount
       FindRecentlyResolvedAlertAsync → skip (cooldown)
       CreateAsync → new alert
        ↓
  ntf_SmsOperationalAlerts

[Admin API: /v1/admin/sms/alerts/*]
  ← reads ntf_SmsOperationalAlerts (read-only except resolve/suppress)
  ← POST /evaluate triggers one manual cycle
```
