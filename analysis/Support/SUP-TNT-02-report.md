# SUP-TNT-02 Report — Ticket Ownership + Visibility

_Status: COMPLETE_

---

## 1. Codebase Analysis

### SupportTicket Entity (Domain/SupportTicket.cs)
Existing requester fields:
- `RequesterUserId` (string?, max64) — identity user
- `RequesterName` (string?, max200) — display name
- `RequesterEmail` (string?, max320) — email
- `CreatedByUserId` (string?, max64) — actor who created ticket via API

No ownership or visibility concept exists. All tickets are implicitly internal.

### TicketService (Services/TicketService.cs)
- `CreateAsync`: sets RequesterUserId/Name/Email directly from request; defaults to tenant userId as actor
- `ListAsync`: filters by TenantId, paged, supports status/priority/severity/source/product/category/search/assigned filters
- `GetAsync`: fetches by Id + TenantId (tenant-scoped)
- `UpdateAsync`: updates ticket fields
- `AssignAsync`: changes assignment; emits audit + notification

### DTOs (Dtos/TicketDtos.cs)
- `CreateTicketRequest`: Title (req), Description?, Priority, Severity?, Category, Source, RequesterUserId?, RequesterName?, RequesterEmail?, ProductCode?, TenantId? (dev-only body fallback)
- `UpdateTicketRequest`: all optional
- `TicketResponse`: mirrors entity fields

### Validators (Validators/TicketValidators.cs)
- Email format validation already exists for RequesterEmail
- Title required, max 200; description max 8000

### Tenant Scoping
- All queries guarded by `TenantId == tenantId`
- `RequireTenant()` enforces resolved tenant on list/get/update/assign
- `ResolveTenantId()` for create (dev/test body fallback allowed)

### ExternalCustomer (from SUP-TNT-01)
- `IExternalCustomerService.ResolveOrCreateAsync(tenantId, email, name?)` — find-or-create by tenant+email
- Already registered as `AddScoped` in Program.cs
- Unique index: `ux_support_external_customers_tenant_email`

### Audit / Notification
- `TryPublishAsync` + `TryAuditAsync` are fire-and-forget safe (exceptions logged, not rethrown)
- `BuildTicketAudit` builds `SupportAuditEvent` from ticket + metadata dict
- Notification recipients include RequesterEmail channel already

---

## 2. Current Ticket Ownership Model

All tickets are implicitly internal:
- No `RequesterType` field
- No `VisibilityScope` field
- No `ExternalCustomerId` field
- External customer concept does not exist in tickets

---

## 3. New Ticket Ownership / Visibility Model

### New Enums
```
TicketRequesterType: InternalUser | ExternalCustomer
TicketVisibilityScope: Internal | CustomerVisible
```

### New SupportTicket Fields
| Field              | Type                       | Default        | Nullable |
|--------------------|----------------------------|----------------|----------|
| RequesterType      | TicketRequesterType (enum) | InternalUser   | No       |
| ExternalCustomerId | Guid                       | null           | Yes      |
| VisibilityScope    | TicketVisibilityScope (enum)| Internal      | No       |

### Rules
- Existing/new agent-created tickets: RequesterType=InternalUser, VisibilityScope=Internal, ExternalCustomerId=null
- Customer-linked tickets (when optional path used): RequesterType=ExternalCustomer, ExternalCustomerId=resolved, RequesterEmail=normalized email, VisibilityScope=CustomerVisible

### FK Decision
No hard FK from support_tickets.external_customer_id → support_external_customers.id.
Rationale: matches existing soft-reference pattern in codebase; avoids cross-table cascade issues; allows ExternalCustomer record to be managed independently. The relationship is enforced at service layer.

---

## 4. Schema Changes

### New Columns (support_tickets table)
| Column               | SQL Type       | Default        | Null |
|----------------------|----------------|----------------|------|
| requester_type       | varchar(20)    | 'InternalUser' | No   |
| external_customer_id | char(36)       | null           | Yes  |
| visibility_scope     | varchar(20)    | 'Internal'     | No   |

### New Indexes
- `ix_support_tickets_tenant_ext_customer` on (tenant_id, external_customer_id)
- `ix_support_tickets_tenant_requester_type` on (tenant_id, requester_type)
- `ix_support_tickets_tenant_visibility` on (tenant_id, visibility_scope)

### Migration
`AddTicketOwnershipFields` — ALTER TABLE adds columns with defaults; no rows broken.

---

## 5. DTO / API Compatibility

### CreateTicketRequest
Added optional fields (nullable, no change to existing required fields):
- `ExternalCustomerEmail` (string?) — triggers external customer path
- `ExternalCustomerName` (string?) — optional display name for new customers

Existing create payloads remain fully valid — no new required fields.

### TicketResponse
Added new read-only fields (always populated with defaults for existing tickets):
- `RequesterType` (TicketRequesterType)
- `ExternalCustomerId` (Guid?)
- `VisibilityScope` (TicketVisibilityScope)

Additive change — existing clients ignore unknown fields.

---

## 6. Service Logic Changes

### CreateAsync
- Default path (no ExternalCustomerEmail): RequesterType=InternalUser, VisibilityScope=Internal — **zero behavior change**
- Optional external path (ExternalCustomerEmail provided):
  - Calls `IExternalCustomerService.ResolveOrCreateAsync(tenantId, email, name)`
  - Sets ExternalCustomerId, RequesterType=ExternalCustomer, VisibilityScope=CustomerVisible
  - Sets RequesterEmail = normalized email, RequesterName = name (if provided)
  - Fails validation if email invalid (handled at validator layer)
- Audit metadata extended: requester_type, external_customer_id, visibility_scope

### New ITicketService Methods
- `ListByExternalCustomerAsync(tenantId, externalCustomerId, page, pageSize)` — internal service method for customer-scoped ticket list (for SUP-TNT-03)

---

## 7. Query / Visibility Behavior

- Existing `ListAsync` and `GetAsync` unchanged — agents see all tenant tickets as before
- New `TicketListQuery.ExternalCustomerId` filter (optional) — narrows list to tickets linked to a specific external customer
- New `TicketListQuery.VisibilityScope` filter (optional) — for future customer portal query filtering
- New `ITicketService.ListByExternalCustomerAsync` method — scoped to tenantId+externalCustomerId — preparation for SUP-TNT-03 customer portal
- No public customer-facing endpoints added

---

## 8. Audit / Notification Alignment

- `BuildTicketAudit` metadata dict extended: `requester_type`, `external_customer_id`, `visibility_scope`
- Notification payload extended: `requester_type`, `external_customer_id`
- No new event types; `TicketCreated` event carries the extra metadata safely
- `TryAuditAsync` / `TryPublishAsync` are already fire-and-forget — no failure risk

---

## 9. Tenant Isolation Validation

- `ListByExternalCustomerAsync` filters by BOTH tenantId AND externalCustomerId — cross-tenant leak impossible
- `GetAsync` already guards tenantId — unchanged
- External customer resolution uses `ResolveOrCreateAsync(tenantId, email)` — tenant-scoped by design (SUP-TNT-01)
- No cross-tenant query paths introduced

---

## 10. Backward Compatibility Validation

| Scenario | Result |
|----------|--------|
| Existing create call (no new fields) | RequesterType=InternalUser, VisibilityScope=Internal — identical behavior |
| Existing list call | All filters still work; new fields returned but additive |
| Existing get call | Same query; new fields in response (additive) |
| Existing tickets in DB after migration | requester_type='InternalUser', visibility_scope='Internal', external_customer_id=null |
| Same email, different tenants | Two isolated ExternalCustomer records (SUP-TNT-01 guarantee) |

---

## 11. Files Created / Changed

| File | Change |
|------|--------|
| `Domain/Enums.cs` | Added TicketRequesterType, TicketVisibilityScope enums |
| `Domain/SupportTicket.cs` | Added RequesterType, ExternalCustomerId, VisibilityScope fields |
| `Data/SupportDbContext.cs` | Added entity config + indexes for new fields |
| `Dtos/TicketDtos.cs` | CreateTicketRequest: 2 optional fields; TicketResponse: 3 new fields + From() |
| `Services/TicketService.cs` | Injected IExternalCustomerService; extended CreateAsync; extended ListAsync query; added ListByExternalCustomerAsync |
| `Validators/TicketValidators.cs` | ExternalCustomerEmail email validation |
| `Data/Migrations/..._AddTicketOwnershipFields.cs` | New migration |
| `Data/Migrations/SupportDbContextModelSnapshot.cs` | Auto-updated |
| `analysis/SUP-TNT-02-report.md` | This file |

---

## 12. Build / Test Results

| Target         | Result | Errors | Warnings |
|----------------|--------|--------|----------|
| Support.Api    | PASS   | 0      | 11 (pre-existing CS0618 + MSB3277) |
| Support.Tests  | PASS   | 0      | 12 (pre-existing CS8604 + others) |

Migration generated: `20260425022101_AddTicketOwnershipFields`
- Columns added to `support_tickets`: `requester_type` (varchar20, not null, default ''), `external_customer_id` (char36, nullable), `visibility_scope` (varchar20, not null, default '')
- Indexes created: `ix_support_tickets_tenant_ext_customer`, `ix_support_tickets_tenant_requester_type`, `ix_support_tickets_tenant_visibility`
- Down() correctly drops all three columns and all three indexes
- No FK introduced (soft reference by design — documented in §3)

---

## 13. Known Gaps / Deferred Items

1. **Public customer API** — deferred to SUP-TNT-03
2. **Customer portal UI** — deferred
3. **Customer authentication** — deferred, no Identity integration
4. **SLA / Comms / email reply** — not in scope
5. **Ticket requester upgrade** — if a ticket is initially InternalUser and later linked to an ExternalCustomer, no migration path defined yet
6. **ExternalCustomerName update on existing customer** — ResolveOrCreateAsync does not update name if customer already exists (SUP-TNT-01 known gap)

---

## 14. Final Readiness Assessment

**READY.** All done criteria met:

| Criterion | Status |
|-----------|--------|
| Ticket ownership fields added safely | PASS — 3 new optional/defaulted fields on SupportTicket |
| Existing tickets default to InternalUser/Internal | PASS — EF defaults + C# property defaults |
| Existing create flow unchanged | PASS — no new required fields; default code path unmodified |
| Existing list/detail flows unchanged | PASS — new filters are additive/optional |
| ExternalCustomerId can be associated without breaking APIs | PASS — optional path triggered only by ExternalCustomerEmail field |
| Customer-specific internal query support exists | PASS — ListByExternalCustomerAsync, ExternalCustomerId filter in ListAsync |
| Tenant isolation preserved | PASS — all queries scope by tenantId; ListByExternalCustomerAsync enforces both tenantId AND externalCustomerId |
| No public customer APIs added | PASS — no new endpoints |
| No customer UI added | PASS |
| No Identity dependency introduced | PASS |
| No Comms/email/SLA/Task logic introduced | PASS |
| Support.Api build passes | PASS — 0 errors |
| Support.Tests build passes | PASS — 0 errors |
| /analysis/SUP-TNT-02-report.md complete | PASS — this document |
