# LS-NOTIF-SMS-008 — Control Center SMS Dashboard APIs

## 1. Initial Codebase Analysis

LS-NOTIF-SMS-008 adds five read-only admin dashboard aggregation endpoints inside the Notification Service. No other service is modified. No Control Center UI is built.

All work is inside `apps/services/notifications/` boundary.

## 2. Existing SMS Activity API Findings

From LS-NOTIF-SMS-006/007:
- `SmsActivityQuery` — filter model (tenantId, provider, configId, ownershipMode, status, failureCategory, from/to, reconciliation fields).
- `ISmsActivityRepository` / `SmsActivityRepository` — LEFT JOIN `NotificationAttempts ← Notifications` for RecipientJson masking; composable `BuildBaseQuery`; `QueryAsync` (paginated) + `SummarizeAsync` (aggregate counts, in-memory).
- `ISmsActivityService` / `SmsActivityService` — phone masking, DTO mapping.
- `SmsActivityEndpoints` — tenant + admin routes at `/v1/sms/activity` and `/v1/admin/sms/activity`.
- All above are preserved unchanged.

## 3. Existing NotificationAttempt/Reconciliation Tracking Findings

Fields added by LS-NOTIF-SMS-007 and available on `ntf_NotificationAttempts`:
- `LastReconciliationOutcome`, `LastReconciledAt`, `LastReconciliationErrorCode`, `LastReconciliationProviderStatus`, `LastReconciliationNormalizedStatus`, `ReconciliationAttemptCount`.

Dashboard queries use these to compute reconciliation KPIs.

## 4. Existing Admin Authorization Findings

- `Policies.AdminOnly` (from `BuildingBlocks.Authorization`) requires role `PlatformAdmin`.
- Applied as `group.RequireAuthorization(Policies.AdminOnly)` — consistent with `AdminNotificationEndpoints` and `SmsActivityEndpoints` admin group.
- `GetUserContext()` extension available from `HttpContextAuthExtensions`.

## 5. Existing Dashboard/Reporting Pattern Findings

No pre-existing SMS dashboard exists. `AdminNotificationEndpoints` covers `/v1/admin/notifications/stats` (cross-tenant notification aggregate), which is the closest pattern. Dashboard aggregation for SMS is new.

## 6. Existing Summary/Aggregation Query Findings

`SmsActivityRepository.SummarizeAsync` fetches a minimal projection (`Status`, `ProviderOwnershipMode`, `CreatedAt`, `LastReconciliationOutcome`, `ReconciliationAttemptCount`) for all matching rows and counts in-memory. This pattern is reused for dashboard aggregation. High-volume SQL GROUP BY is documented as a future optimization.

## 7. Files Added

| File | Purpose |
|------|---------|
| `Notifications.Application/DTOs/SmsDashboardDtos.cs` | Query and response DTOs for all 5 dashboard endpoints |
| `Notifications.Application/Interfaces/ISmsDashboardRepository.cs` | Repository contract |
| `Notifications.Application/Interfaces/ISmsDashboardService.cs` | Service contract |
| `Notifications.Infrastructure/Repositories/SmsDashboardRepository.cs` | EF-backed aggregation queries |
| `Notifications.Infrastructure/Services/SmsDashboardService.cs` | Service orchestrator (thin) |
| `Notifications.Api/Endpoints/SmsDashboardEndpoints.cs` | 5 admin endpoints |

## 8. Files Modified

| File | Change |
|------|--------|
| `Notifications.Infrastructure/DependencyInjection.cs` | Register `ISmsDashboardRepository` + `ISmsDashboardService` |
| `Notifications.Api/Program.cs` | `app.MapSmsDashboardEndpoints()` |

## 9. Database/Schema/Config Changes

None. All dashboard queries are read-only against existing tables. No new columns, tables, or indexes are required. Existing indexes (`IX_NotificationAttempts_Channel_TenantId_CreatedAt`, `IX_NotificationAttempts_Channel_TenantId_LastReconciliationOutcome`) created by LS-NOTIF-SMS-006/007 support dashboard filters.

## 10. API/Interface Changes

### Endpoints (all require `Policies.AdminOnly`)

| Method | Path | Description |
|--------|------|-------------|
| GET | `/v1/admin/sms/dashboard/summary` | High-level KPI aggregate |
| GET | `/v1/admin/sms/dashboard/trends` | Time-series trend data |
| GET | `/v1/admin/sms/dashboard/failures` | Failure category/error breakdown |
| GET | `/v1/admin/sms/dashboard/tenants` | Per-tenant activity breakdown |
| GET | `/v1/admin/sms/dashboard/providers` | Per-provider/config breakdown |

### Common Query Parameters

`tenantId`, `provider`, `providerConfigId`, `providerOwnershipMode`, `status`, `failureCategory`, `from`, `to`

### Trends Additional Parameter

`bucket=hour|day|week` (default: `day`). Default date range: last 30 days when From/To omitted.

### SmsDashboardSummaryDto Fields

totalAttempts, sentCount, deliveredCount, failedCount, pendingCount, processingCount, retryingCount, deadLetterCount, tenantOwnedCount, platformOwnedCount, unknownOwnershipCount, reconciledTotal, neverReconciled, reconciliationUpdated, reconciliationNoChange, reconciliationLookupFailed, reconciliationSkipped, reconciliationProviderConfigFailed, uniqueTenantCount, uniqueProviderCount, uniqueProviderConfigCount.

Note: "blocked" is omitted — `NotificationAttempt.Status` has no "blocked" value (blocking is tracked on the `Notification` entity via `BlockedByPolicy`, not on attempts).

### SmsDashboardTrendPointDto Fields

bucketStart, bucketEnd, totalAttempts, sentCount, deliveredCount, failedCount, pendingCount, reconciledTotal, reconciliationLookupFailed.

Note: reconciliation trend counts are based on `CreatedAt` bucket (when the attempt was created/sent), not `LastReconciledAt`. This is documented as a known gap; future enhancement could use `LastReconciledAt` for a separate reconciliation-timeline view.

### SmsDashboardFailureItemDto Fields

failureCategory, errorCode, count, latestOccurrenceAt.

Groups by (FailureCategory ?? "unknown", LastReconciliationErrorCode). Only rows with Status=failed/dead_letter or non-null FailureCategory are included.

### SmsDashboardTenantItemDto Fields

tenantId, totalAttempts, sentCount, deliveredCount, failedCount, pendingCount, reconciledTotal, neverReconciled, tenantOwnedCount, platformOwnedCount, latestActivityAt.

Note: tenant names are not returned — Notification Service has no local tenant name store. Control Center UI should enrich with tenant names via Identity service in a future feature.

### SmsDashboardProviderItemDto Fields

provider, providerConfigId, providerOwnershipMode, totalAttempts, sentCount, deliveredCount, failedCount, reconciledTotal, reconciliationLookupFailed, latestActivityAt.

No CredentialsJson, SettingsJson, or authToken is included.

## 11. Validation/Testing Performed

Build: `dotnet build Notifications.Api.csproj -c Release --no-restore` — EXIT:0, 0 errors.

Logical validation:
- All 5 endpoints require `Policies.AdminOnly` via group-level authorization.
- `BuildBaseQuery` always filters `Channel == "sms"` as primary predicate.
- No CredentialsJson, SettingsJson, RecipientJson, or phone numbers projected.
- Trend default range bounded at last 30 days; max bucket validated (hour/day/week only).
- Failure breakdown includes only rows with Status=failed/dead_letter or non-null FailureCategory.
- Tenant breakdown returns tenantId only (no name lookup).
- Provider breakdown returns ProviderConfigId as opaque Guid (no credentials).
- LS-NOTIF-SMS-001..007 endpoints and behavior are unchanged.
- No provider calls, no reconciliation triggers, no SMS sends from dashboard.

## 12. Known Gaps / Issues

- **Reconciliation trend uses CreatedAt**: `SmsDashboardTrendPointDto.reconciledTotal` counts attempts created in the bucket that have been reconciled, not when they were reconciled. A separate `LastReconciledAt`-bucketed view would require a second query pass.
- **In-memory aggregation**: Like LS-NOTIF-SMS-006 `SummarizeAsync`, dashboard aggregation fetches minimal projections and counts in-memory. At high SMS volume, SQL GROUP BY would be more efficient.
- **Tenant names missing**: Only `tenantId` (Guid) is returned. Control Center UI must enrich with names from Identity service.
- **"blocked" count omitted**: `NotificationAttempt.Status` has no "blocked" value. The feature spec's mention of `blockedCount` cannot be fulfilled from attempt data alone.
- **`processingCount` / `retryingCount`**: These are defined in the summary DTO and counted from attempt Status field.

## 13. Recommended Next Steps

- **Control Center UI**: Consume `/v1/admin/sms/dashboard/*` to render KPI cards, trend charts, failure breakdown tables, and tenant/provider drilldowns.
- **SQL aggregation**: Replace in-memory counting with SQL GROUP BY for production-scale performance.
- **Tenant name enrichment**: Control Center should resolve tenant names from Identity service when rendering the tenant breakdown table.
- **LastReconciledAt-based reconciliation trend**: Add optional `reconciledFrom`/`reconciledTo` parameters on the trend endpoint.
- **Export**: Add CSV/Excel export for dashboard failure and tenant breakdowns.
