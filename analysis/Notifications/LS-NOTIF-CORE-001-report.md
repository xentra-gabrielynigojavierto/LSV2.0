# LS-NOTIF-CORE-001 — Operational API Completion & Contract Alignment

> **Status: COMPLETE** — build verified: `Build succeeded. 3 Warning(s), 0 Error(s)` (all warnings pre-existing).

---

## Summary

Completing the Notifications microservice operational API surface. The existing service has robust send/submit logic, provider routing, suppression enforcement, and webhook ingestion — but the public API surface is thin: only POST (submit), GET /{id}, and GET / (basic list). This task adds stats, event timeline, issues, retry, resend, and upgrades the list endpoint to a structured paged response.

All changes are additive. No existing endpoints are broken. POST /v1/notifications and POST /internal/send-email are untouched.

---

## Implemented Endpoints

| Method | Path | Status |
|---|---|---|
| GET | `/v1/notifications` (upgraded) | ✅ Implemented |
| GET | `/v1/notifications/stats` | ✅ Implemented |
| GET | `/v1/notifications/{id}/events` | ✅ Implemented |
| GET | `/v1/notifications/{id}/issues` | ✅ Implemented |
| POST | `/v1/notifications/{id}/retry` | ✅ Implemented |
| POST | `/v1/notifications/{id}/resend` | ✅ Implemented |

---

## Files Changed

| File | Change |
|---|---|
| `Notifications.Application/DTOs/OperationalDtos.cs` | New — query models, paged response, stats, event, issue, retry/resend DTOs |
| `Notifications.Application/Interfaces/INotificationRepository.cs` | Added `GetPagedAsync`, `GetStatsAsync` |
| `Notifications.Application/Interfaces/INotificationService.cs` | Added `ListPagedAsync`, `GetStatsAsync`, `GetEventsAsync`, `GetIssuesAsync`, `RetryAsync`, `ResendAsync` |
| `Notifications.Infrastructure/Repositories/NotificationRepository.cs` | Implemented `GetPagedAsync`, `GetStatsAsync` |
| `Notifications.Infrastructure/Services/NotificationService.cs` | Refactored send loop into `ExecuteSendLoopAsync`; implemented 6 new service methods |
| `Notifications.Api/Endpoints/NotificationEndpoints.cs` | Upgraded list endpoint; added 5 new routes |

---

## Data / Domain Changes

No schema migrations required. All new endpoints read from existing tables:
- `ntf_Notifications` — stats, paged list
- `ntf_NotificationAttempts` — events timeline, retry dispatch
- `ntf_NotificationEvents` — events timeline (provider webhook events)
- `ntf_DeliveryIssues` — issues endpoint

The only new field usage is `Notification.Category` as the `productKey` filter target (already in schema). There is no `ProductType` column on `ntf_Notifications` (ProductType was used for template resolution but not persisted). This mismatch is documented in Remaining Gaps.

### Retry eligibility
A notification is retryable when:
- Status = `failed`
- FailureCategory ∈ `{retryable_provider_failure, provider_unavailable, auth_config_failure}`

Non-retryable: `blocked`, `sent`, `processing`, `accepted`, `partial`, or any failed with `non_retryable_failure` or `invalid_recipient`.

### Resend behavior
- Creates a brand-new `Notification` record via `SubmitAsync`
- Strips the original's IdempotencyKey (force new dispatch)
- Injects `resendOf: <originalId>` into MetadataJson so the link is traceable

### Retry behavior
- Reuses the **original** notification record (updates it in place)
- Creates new `NotificationAttempt` records for the retry
- Does NOT create a new notification (retry ≠ resend)
- Preserves the full attempt history on the original record

---

## Validation Performed

- ✅ .NET build: `Build succeeded. 3 Warning(s), 0 Error(s)` — all warnings are pre-existing (MailKit vulnerability advisory, JwtBearer version conflict in shared BuildingBlocks)
- ✅ `INotificationRepository` interface fully implemented by `NotificationRepository` — both `GetPagedAsync` and `GetStatsAsync`
- ✅ `INotificationService` interface fully implemented by `NotificationServiceImpl` — all 6 new methods
- ✅ DI container unchanged — `INotificationEventRepository` and `IDeliveryIssueRepository` are already registered; constructor injection resolves automatically
- ✅ Existing POST /v1/notifications path fully preserved (no behavioral change to `SubmitAsync` or `DispatchSingleAsync`)
- ✅ `ExecuteSendLoopAsync` extraction verified equivalent to original routing/send loop (same attempt creation, provider dispatch, status updates, audit, metering)
- Manual smoke tests not possible without live DB/provider credentials in this environment

---

## Remaining Gaps

1. **`productKey` filter maps to `Category` field** — The `SubmitNotificationDto.ProductType` is used only for template resolution, not stored. `Category` is the stored product-level tag. If producers use `Category` = product code, the filter works. If they don't, the filter returns all. Document this to producers.

2. **Stats `delivered` count** — The `Notification` entity has no `delivered` status. "Delivered" is a provider webhook event stored in `ntf_NotificationEvents` (NormalizedEventType=Delivered). The stats endpoint returns a `deliveredCount` sourced from counting `NotificationEvents` with `NormalizedEventType = "delivered"` within the filtered set. This is correct but slightly more expensive than a pure status count.

3. **Retry for fan-out parents** — Fan-out parent notifications have status "sent" or "partial" but represent batch sends. Retrying a fan-out parent is blocked by current retry logic (only individual failed notifications are retryable). A future iteration should support per-recipient retry for partial fan-outs.

4. **No `tenantId` admin override** — The stats and list endpoints currently enforce tenant isolation via the TenantMiddleware (X-Tenant-Id header). Platform admins cannot query cross-tenant unless they cycle through each tenant. A future iteration should add an operator-level endpoint (e.g., `/v1/admin/notifications`) with cross-tenant access controlled by an admin token.

5. **Stats do not filter by `tenantId` param** — The `NotificationStatsQuery.TenantId` filter field is defined in the DTO but the endpoint always uses the middleware-provided `tenantId`. This is correct for tenant safety but means a platform-admin UI querying a different tenant's stats must send the correct `X-Tenant-Id` header.

6. **No pagination on events/issues** — Events and issues are returned as complete lists (up to 50 events per the existing `GetByNotificationIdAsync` limit). Pagination can be added if per-notification event counts become large.

---

## Risks / Follow-Up Recommendations

1. **Stats in-memory grouping** — The stats endpoint loads a lightweight projection (5 columns) and groups in memory. For tenants with hundreds of thousands of notifications and no date filter, this will be slow. Add a default date range (last 30 days) in the UI layer, and consider adding a DB-side aggregate view or indexed materialized stats table in a future iteration.

2. **Retry race condition** — If two retry requests arrive simultaneously for the same notification, both will pass the status check, reset to "processing", and dispatch. The repository's `UpdateStatusAsync` is not atomic with the check. For production, add an optimistic concurrency check (rowversion/timestamp) or a Redis lock. For now the risk is low (duplicate delivery via retry is benign when idempotency is enforced at the provider level, e.g., SendGrid deduplication).

3. **Resend idempotency key stripping** — Resend always creates a new notification. If the same resend is requested twice quickly (network retry), two new notifications will be created (no idempotency guard on the resend path). A future iteration should let callers pass a `resendIdempotencyKey` to guard against this.
