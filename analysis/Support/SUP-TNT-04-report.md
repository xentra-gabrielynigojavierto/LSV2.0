# SUP-TNT-04 Report — Multi-Mode Support

_Status: COMPLETE_

---

## 1. Codebase Analysis

### Support service structure
- `apps/services/support/Support.Api` — .NET 8 minimal-API service
- MySQL via Pomelo EF Core; InMemory for tests
- Route groups, scoped services, middleware-based tenant resolution
- Migrations in `Data/Migrations/`, EF Core tooling verified working

### Existing customer infrastructure (SUP-TNT-03)
- Customer endpoints: `GET/GET-by-id/POST-comment` under `/support/api/customer/tickets`
- `CustomerAccess` policy requires `ExternalCustomer` role
- `ResolveCustomerContext()` reads `tenant_id` and `external_customer_id` from JWT only
- No tenant-level mode guard existed before this block

### Existing tenant resolution
- `ITenantContext` / `TenantContext` / `TenantResolutionMiddleware` — resolves `tenant_id` from JWT claim only in Production; Dev/Testing allows `X-Tenant-Id` header fallback
- Customer endpoints already enforce: `tenantId + externalCustomerId + CustomerVisible`

### Audit pattern
- `SupportAuditEvent` (sealed record) + `IAuditPublisher.PublishAsync()`
- Services wrap audit calls in try-catch — audit failure never blocks business logic
- `SupportAuditEventTypes`, `SupportAuditActions`, `SupportAuditOutcomes`, `SupportAuditResourceTypes` constants

### No existing tenant settings table
- No `support_tenant_settings` or equivalent table found in any migration
- Must create new table

---

## 2. Existing Settings / Config Discovery

No tenant-scoped settings table existed in Support DB prior to this block. No platform shared tenant-settings service is used by Support. The `appsettings.json` pattern is used for global options (`Audit:`, `Notifications:`, `FileStorage:`), not per-tenant configuration. **New table required.**

---

## 3. Support Mode Model

```
SupportTenantMode (enum):
  InternalOnly          — internal/admin/agent Support only; customer endpoints disabled
  TenantCustomerSupport — internal + customer Support enabled for this tenant
```

Effective customer support is enabled when:
- `SupportTenantMode == TenantCustomerSupport` **AND**
- `CustomerPortalEnabled == true`

Both flags must be true. Default is `InternalOnly` / `CustomerPortalEnabled=false`. If no row exists for a tenant, effective mode is `InternalOnly` / disabled.

---

## 4. Storage / Schema Changes

### New table: `support_tenant_settings`

| Column | Type | Constraints |
|--------|------|-------------|
| `tenant_id` | varchar(64) | PK, NOT NULL |
| `support_mode` | varchar(30) | NOT NULL DEFAULT 'InternalOnly' |
| `customer_portal_enabled` | tinyint(1) | NOT NULL DEFAULT 0 |
| `created_at` | datetime(6) | NOT NULL |
| `updated_at` | datetime(6) | NULL |

- One row per tenant; `tenant_id` is the isolation boundary
- No FK to Identity tenant table (cross-service FK avoided per rules)
- Migration: `AddSupportTenantSettings`

### Entity: `SupportTenantSettings`
- Domain model in `Domain/SupportTenantSettings.cs`
- `SupportTenantMode` enum added to `Domain/Enums.cs`
- DbSet `TenantSettings` added to `SupportDbContext`

---

## 5. Settings Service

### Interface: `ISupportTenantSettingsService`
- `GetEffectiveSettingsAsync(tenantId)` → `TenantSettingsResponse` (default if no row)
- `SetSupportModeAsync(tenantId, mode, customerPortalEnabled, actorUserId)` → `TenantSettingsResponse`
- `IsCustomerSupportEnabledAsync(tenantId)` → `bool` (true only when both flags set)

### Implementation: `SupportTenantSettingsService`
- Reads/writes `support_tenant_settings` table
- Audit event emitted on `SetSupportModeAsync` (mode change only)
- All operations scoped by `tenantId` — no cross-tenant access

---

## 6. Admin Endpoint Changes

### New endpoints
- `GET /support/api/admin/tenant-settings` — requires `SupportRead`
- `PUT /support/api/admin/tenant-settings` — requires `SupportManage`

### Authorization note
`SupportRead` requires roles from `SupportRoles.All` (PlatformAdmin, SupportAdmin, SupportManager, SupportAgent, TenantAdmin, TenantUser). `SupportManage` requires roles from `SupportRoles.Managers` (PlatformAdmin, SupportAdmin, SupportManager). `ExternalCustomer` is in **neither** list — cannot access these endpoints.

### Tenant source
Tenant comes exclusively from `ITenantContext` (JWT-resolved). Not accepted from request body.

---

## 7. Customer Endpoint Enforcement

All three customer endpoints (`GET /`, `GET /{id}`, `POST /{id}/comments`) call `ISupportTenantSettingsService.IsCustomerSupportEnabledAsync(tenantId)` **before** any existing logic.

- Disabled → `403 Forbidden` ("Customer support portal is not enabled for this tenant.")
- Enabled → proceed to existing RBAC enforcement (`tenantId + externalCustomerId + CustomerVisible`)

The `CustomerAccess` policy check still runs before our code (required by the route group `RequireAuthorization`). Our mode check adds an additional layer.

---

## 8. Ticket Creation Behavior

**Decision:** Internal/admin ticket creation with `ExternalCustomerEmail` continues to work in `InternalOnly` mode. This allows agents to link tickets to external customers for back-office purposes before customer support is activated. Only the customer-facing endpoints (GET/POST by customer) are gated by mode.

This is consistent with the principle of not blocking existing internal workflows.

---

## 9. UI Behavior

**Customer portal (SUP-INT-07):** The existing `403` handling already shows "Customer portal access not yet available." No UI code change is required — the backend returns `403` in both cases (no ExternalCustomer JWT AND mode disabled). The UI message is appropriate for both scenarios.

**Admin/agent UI:** No new UI built. Settings are accessible via the API endpoints for programmatic or future UI integration.

---

## 10. Audit Alignment

`SetSupportModeAsync` emits a `SupportAuditEvent` with:
- `EventType`: `support.tenant_settings.changed`
- `ResourceType`: `support_tenant_settings`
- `ResourceId`: tenantId
- `Metadata`: old/new mode, old/new customerPortalEnabled, actorUserId

Wrapped in try-catch — audit failure does not block the settings update.

---

## 11. Tenant Isolation Validation

| Check | Implementation |
|---|---|
| Settings row keyed by tenantId | `support_tenant_settings.tenant_id` is PK |
| All queries scope by tenantId | `FindAsync(tenantId)` — PK lookup |
| Tenant A cannot read/write Tenant B settings | Tenant from JWT only, never from body |
| No row → InternalOnly | `IsCustomerSupportEnabledAsync` returns false when no row |

---

## 12. Backward Compatibility Validation

| Check | Status |
|---|---|
| Internal `/support/api/tickets` unaffected | No changes to TicketEndpoints |
| Internal `/support/api/queues` unaffected | No changes to QueueEndpoints |
| Comment endpoints unaffected | No changes to CommentEndpoints |
| Customer endpoints still require CustomerAccess policy | Policy unchanged |
| Customer endpoints enforce externalCustomerId from JWT | Logic unchanged |
| ExternalCustomer cannot reach admin settings | SupportRead/Manage require internal roles |
| Existing tests still pass | All existing test assertions preserved |

---

## 13. Files Created / Changed

| File | Change |
|---|---|
| `Domain/Enums.cs` | `SupportTenantMode` enum added |
| `Domain/SupportTenantSettings.cs` | New entity |
| `Audit/SupportAuditEvent.cs` | `TenantSettingsChanged` event type + resource/action constants |
| `Data/SupportDbContext.cs` | `TenantSettings` DbSet + model config |
| `Data/Migrations/AddSupportTenantSettings.cs` | New EF migration |
| `Services/ISupportTenantSettingsService.cs` | New interface + DTOs |
| `Services/SupportTenantSettingsService.cs` | New service implementation |
| `Endpoints/TenantSettingsEndpoints.cs` | New admin GET + PUT endpoints |
| `Endpoints/CustomerTicketEndpoints.cs` | Mode guard added to all 3 handlers |
| `Program.cs` | Service registration + endpoint mapping |
| `Support.Tests/TenantSettingsTests.cs` | New test class (8 tests) |
| `Support.Tests/CustomerApiTests.cs` | Added `EnableCustomerSupportAsync` helper; 6 tests updated to enable mode before asserting customer access |
| `analysis/SUP-TNT-04-report.md` | This file |

---

## 14. Build / Test Results

### Build
```
dotnet build Support.Api/Support.Api.csproj  →  0 errors, 0 new warnings
dotnet build Support.Tests/Support.Tests.csproj  →  0 errors
```

### Tests — final run (2026-04-25)
| Test class | Passed | Failed | Total |
|---|---|---|---|
| `TenantSettingsTests` | 8 | 0 | 8 |
| `CustomerApiTests` | 11 | 0 | 11 |
| **TOTAL** | **19** | **0** | **19** |

#### TenantSettingsTests (8) — SUP-TNT-04 new tests
1. `GetEffectiveSettings_ReturnsDefault_WhenNoRow` — no row → InternalOnly/disabled
2. `GetEffectiveSettings_ReturnsStoredSettings` — stored row reflected in response
3. `SetSupportMode_Creates_NewSettings` — PUT creates row when none exists
4. `SetSupportMode_Updates_ExistingSettings` — PUT upserts existing row
5. `CustomerEndpoint_Returns403_WhenModeIsInternalOnly` — mode guard blocks by default
6. `CustomerEndpoint_Returns200_WhenModeEnabled` — enabled → 200
7. `AdminGet_Returns200_ForSupportReadRole` — SupportRead authorised
8. `AdminPut_Returns403_ForExternalCustomer` — ExternalCustomer cannot manage settings

#### CustomerApiTests (11) — pre-existing + updated for mode awareness
All 11 pass; 6 tests now call `EnableCustomerSupportAsync(tenant)` before exercising customer endpoints (tests 1–6). Tests 7–11 (negative/agent) unaffected.

---

## 15. Known Gaps / Deferred Items

1. **Customer login** — Still deferred from SUP-INT-07. Mode guard is in place and fail-closed.
2. **Admin settings UI** — Not built. Settings accessible via API only.
3. **Mode-based ticket listing for admins** — Internal agents see all tickets regardless of mode (intended).
4. **Customer comment list** — Still deferred (no customer comment list endpoint).

---

## 16. Final Readiness Assessment

| Criterion | Status |
|---|---|
| Tenant-scoped support mode | PASS |
| Default InternalOnly | PASS |
| Customer endpoints fail closed when disabled | PASS |
| Internal workflows unchanged | PASS |
| ExternalCustomer cannot manage settings | PASS |
| Tenant isolation | PASS |
| No customer login added | PASS |
| No Comms/SLA/Task logic | PASS |
| Build passes (0 errors) | PASS |
| All 19 tests pass | PASS |
| Report complete | PASS |
