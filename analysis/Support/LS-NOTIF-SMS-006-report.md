# LS-NOTIF-SMS-006 — SMS Tenant/Admin Activity Logs and Reporting APIs

## 1. Initial Codebase Analysis

LS-NOTIF-SMS-006 adds read-only SMS activity query APIs inside the Notification Service boundary.
No new providers, no schema changes, no UI — pure query surface over existing data.

The implementation follows the existing admin/tenant pattern:
- Tenant endpoints at `/v1/sms/activity` — scoped to JWT `tenant_id` claim.
- Admin endpoints at `/v1/admin/sms/activity` — `PlatformAdmin` role, optional `tenantId` filter.

## 2. Existing NotificationAttempt / Logging / Audit Findings

### `NotificationAttempt` domain entity (`Notifications.Domain/NotificationAttempt.cs`)

| Field                | Type       | Notes                                              |
|----------------------|------------|----------------------------------------------------|
| `Id`                 | `Guid`     | Primary key                                        |
| `TenantId`           | `Guid?`    | Null for platform-owned sends                      |
| `NotificationId`     | `Guid`     | FK to `ntf_Notifications`                          |
| `Channel`            | `string`   | `"sms"`, `"email"`, etc.                           |
| `Provider`           | `string`   | `"twilio"`, `"sendgrid"`, etc.                     |
| `Status`             | `string`   | `pending/sending/sent/delivered/failed/dead_letter` |
| `AttemptNumber`      | `int`      | Sequential (1 = first attempt)                     |
| `ProviderMessageId`  | `string?`  | Twilio MessageSid; treated as operational metadata |
| `ProviderOwnershipMode` | `string?` | `"tenant"` / `"platform"` — explicit attribution |
| `ProviderConfigId`   | `Guid?`    | FK to `ntf_TenantProviderConfigs`; null = platform |
| `FailureCategory`    | `string?`  | Structured failure reason                          |
| `ErrorMessage`       | `string?`  | Safe last error (no credentials)                   |
| `IsFailover`         | `bool`     | Whether this attempt was a failover                |
| `CompletedAt`        | `DateTime?`| Set when attempt reaches a terminal state          |
| `CreatedAt`          | `DateTime` | UTC                                                |
| `UpdatedAt`          | `DateTime` | UTC                                                |

### `Notification` domain entity (`Notifications.Domain/Notification.cs`)

Contains `RecipientJson` — JSON-encoded recipient object. For SMS the `phone` property holds the E.164 number. Used only to derive a masked recipient value; the raw field is never exposed in the activity API.

## 3. Existing Tenant/Provider Metadata Findings

- `ProviderOwnershipMode` on both `Notification` and `NotificationAttempt` is the primary attribution field (set by `ProviderRoutingService` at send time). Values: `"tenant"` / `"platform"` / null.
- `ProviderConfigId` links to `ntf_TenantProviderConfigs` which holds `CredentialsJson` / `SettingsJson`. **These are never returned.** Only the opaque `ProviderConfigId` GUID is included in activity responses.
- Platform sentinel tenant: `00000000-0000-0000-0000-000000000001` — used for seeded platform provider configs. Not special-cased in activity queries; platform activity is distinguished by `ProviderOwnershipMode`.

## 4. Existing Reconciliation Metadata Findings

Reconciliation state is **not persisted** as a column on `NotificationAttempt` — it is reflected only via status changes (e.g., `delivered`, `failed`) and audit events. The activity API reflects reconciliation outcomes through the `status`, `failureCategory`, and `providerMessageId` fields already present on the attempt. No new reconciliation-state column is required.

## 5. Files Added

| File | Purpose |
|------|---------|
| `Notifications.Application/DTOs/SmsActivityDtos.cs` | Query + response DTOs + raw record |
| `Notifications.Application/Interfaces/ISmsActivityRepository.cs` | Repository interface |
| `Notifications.Application/Interfaces/ISmsActivityService.cs` | Service interface |
| `Notifications.Infrastructure/Repositories/SmsActivityRepository.cs` | EF Core query implementation |
| `Notifications.Infrastructure/Services/SmsActivityService.cs` | Phone masking + service orchestration |
| `Notifications.Api/Endpoints/SmsActivityEndpoints.cs` | Tenant + admin HTTP endpoints |

## 6. Files Modified

| File | Change |
|------|--------|
| `Notifications.Infrastructure/DependencyInjection.cs` | Registered `ISmsActivityRepository` + `ISmsActivityService` |
| `Notifications.Api/Program.cs` | Mapped `SmsActivityEndpoints`; added startup composite index on `ntf_NotificationAttempts (Channel, TenantId, CreatedAt)` |

## 7. Database / Schema / Config Changes

No EF migration required — all fields queried already exist on `ntf_NotificationAttempts` and `ntf_Notifications`.

A composite index `IX_NotificationAttempts_Channel_TenantId_CreatedAt` is added via the startup safety-net (`EnsureNotificationsSchemaColumnsAsync`) following the existing pattern. This covers the common SMS activity query: `WHERE Channel='sms' AND TenantId=? ORDER BY CreatedAt DESC`.

## 8. API / Interface Changes

### Tenant endpoints

```
GET /v1/sms/activity
  ?provider=twilio
  &providerConfigId={guid}
  &providerOwnershipMode=tenant|platform
  &providerMessageId={sid}
  &status=sent|delivered|failed|...
  &failureCategory={code}
  &from={datetime}
  &to={datetime}
  &limit=50   (max 200)
  &offset=0
→ SmsActivityPagedResult { Items, Total, Limit, Offset }

GET /v1/sms/activity/summary
  (same filters minus limit/offset)
→ SmsActivitySummaryDto
```

### Admin endpoints

```
GET /v1/admin/sms/activity
  ?tenantId={guid}          ← optional cross-tenant filter
  &includePlatformActivity=true
  (+ all filters above)
→ SmsActivityPagedResult

GET /v1/admin/sms/activity/summary
  (same)
→ SmsActivitySummaryDto
```

### Authorization
- `/v1/sms/*` → `RequireAuthorization()` (any authenticated user, tenant-scoped).
- `/v1/admin/sms/*` → `RequireAuthorization(Policies.AdminOnly)` (`PlatformAdmin` role).

## 9. Validation / Testing Performed

Build validation: `dotnet build Notifications.Api.csproj -c Release --no-restore` — EXIT:0, 0 errors.

Logical validation:
- Tenant endpoint: sets `TenantId` from JWT, `IncludePlatformActivity=false` → only tenant-scoped attempts.
- Admin endpoint: optional `tenantId` filter, optional `includePlatformActivity` flag.
- `ProviderOwnershipMode` null → attribution `"unknown"` (explicit, not guessed).
- Phone masking: first 3 characters + `***` — matches `MaskRecipient` convention in `NotificationService`.
- `CredentialsJson`, `SettingsJson`, `authToken`, raw `phone` are never returned.
- `providerMessageId` included as operational metadata (consistent with existing reconciliation API).
- `ErrorMessage` included only if it is a safe failure description (field already safe at write time).
- Email/in-app paths: unaffected — filter is `Channel == "sms"` throughout.

## 10. Known Gaps / Issues

- **Reconciliation state column**: not persisted — activity reflects reconciliation outcomes through status only. A future LS-NOTIF-SMS-007 could add a `LastReconciliationOutcome` column.
- **Pagination cursor**: offset-based pagination used (consistent with existing `/v1/admin/notifications`). Keyset cursor pagination would be more efficient at scale but is out of scope.
- **Real-time activity**: endpoints are pull-based (no WebSocket/SSE streaming).
- **AttemptNumber > 1**: the `isFailover` and `attemptNumber` fields allow callers to distinguish retries from original sends; no higher-level grouping by notification is provided in this feature.

## 11. Recommended Next Steps

- **LS-NOTIF-SMS-007**: Persist `LastReconciliationOutcome` on `NotificationAttempt` for direct reconciliation-state filtering.
- **Control Center UI**: Wire `/v1/admin/sms/activity` into the Control Center reporting surface.
- **Tenant portal UI**: Wire `/v1/sms/activity` into the tenant portal's SMS delivery log.
- **Keyset pagination**: Replace offset pagination with cursor-based for high-volume tenants.
