# SUP-TNT-01 Report — External Customer Model

## 1. Codebase Analysis

### Ticket creation flow
- `TicketService.CreateAsync` stores requester info as plain strings on the ticket: `RequesterUserId`, `RequesterName`, `RequesterEmail`
- No external customer concept exists; requester is whoever submits the ticket (typically a tenant user via session)
- Tenant context is resolved via `ITenantContext` → `TenantContext` (reads `X-Tenant-Id` from JWT claims)

### Current creator fields on SupportTicket
| Field           | Type    | Notes                    |
|-----------------|---------|--------------------------|
| RequesterUserId | string? | Identity user ID (opt.)  |
| RequesterName   | string? | Display name (opt.)      |
| RequesterEmail  | string? | Email (opt.)             |
| CreatedByUserId | string? | Agent who opened (opt.)  |

### DB schema structure
- MySQL (via Pomelo EF Core provider), snake_case column names
- All entities follow: `ToTable`, `HasKey`, property-level configuration, named indexes
- Unique index convention: prefix `ux_`, other indexes: `ix_`
- GUIDs stored as `char(36)` with `ascii_general_ci` collation
- Strings: `varchar(N)` with `utf8mb4` charset
- Booleans: `tinyint(1)`

### Existing service pattern
- Interface + implementation in `Services/`
- Constructor injection via DI
- `AddScoped<IFoo, Foo>()` registration in `Program.cs`

### Test project
- `Support.Tests/` present, integration-test style using `SupportApiFactory`
- No unit tests currently for isolated service logic

---

## 2. Data Model

### ExternalCustomer entity (new)

| Field     | Type                    | Constraints              | Notes                              |
|-----------|-------------------------|--------------------------|------------------------------------|
| Id        | Guid                    | PK                       | char(36), ascii_general_ci         |
| TenantId  | string                  | required, max 64         | Tenant isolation boundary          |
| Email     | string                  | required, max 320        | Normalized to lowercase            |
| Name      | string?                 | optional, max 200        |                                    |
| Status    | ExternalCustomerStatus  | required                 | Enum: Active / Inactive            |
| CreatedAt | DateTime                | required, UTC            |                                    |

**Unique index**: `(TenantId, Email)` — same email in two tenants = two distinct records

### ExternalCustomerStatus enum
```
Active
Inactive
```

### Table
`support_external_customers`

---

## 3. Repository Layer

**Interface**: `IExternalCustomerRepository`
- `Task<ExternalCustomer?> GetByEmailAsync(string tenantId, string email, CancellationToken ct)`
- `Task<ExternalCustomer> CreateAsync(ExternalCustomer customer, CancellationToken ct)`

**Implementation**: `ExternalCustomerRepository`
- Constructor-injected `SupportDbContext`
- `GetByEmailAsync`: normalized lowercase lookup, tenant-scoped
- `CreateAsync`: adds to context and saves

---

## 4. Service Layer

**Interface**: `IExternalCustomerService`
- `Task<ExternalCustomer> ResolveOrCreateAsync(string tenantId, string email, string? name, CancellationToken ct)`

**Implementation**: `ExternalCustomerService`
- If exists (by tenantId + email) → return existing record
- If not → create new customer with `Status = Active`
- Email normalized to lowercase on ingestion
- No Identity Service calls made
- No authentication/session handling

---

## 5. Schema Changes

New migration: `AddExternalCustomers`
- Creates `support_external_customers` table
- Unique index: `ux_support_external_customers_tenant_email`
- Index: `ix_support_external_customers_tenant`, `ix_support_external_customers_email`, `ix_support_external_customers_status`

---

## 6. Tenant Isolation Validation

- All queries are scoped by `TenantId` — same email in two tenants produces two independent rows
- `GetByEmailAsync` filters on `(TenantId, email.ToLower())` — no cross-tenant leakage
- No foreign keys to identity tables
- No authentication fields stored

---

## 7. Impact Analysis

| Component         | Impact |
|-------------------|--------|
| Ticket creation   | None — existing `RequesterUserId/Name/Email` fields unchanged |
| Assignment        | None   |
| Comments          | None   |
| Audit             | None   |
| Notifications     | None   |
| Existing tests    | None   |
| API endpoints     | None — no endpoints added |
| UI                | None — no UI in this block |

---

## 8. Build Results

| Target           | Result | Errors | Warnings |
|------------------|--------|--------|----------|
| Support.Api      | PASS   | 0      | 11 (pre-existing MSB3277 + CS0618) |
| Support.Tests    | PASS   | 0      | 12 (pre-existing CS8604 + others)  |

Migration generated: `20260425020044_AddExternalCustomers`
- Table `support_external_customers` created
- Unique index `ux_support_external_customers_tenant_email` on `(tenant_id, email)`
- Indexes: `ix_support_external_customers_tenant`, `ix_support_external_customers_email`, `ix_support_external_customers_status`

---

## 9. Known Gaps / Deferred Items

1. **API exposure** — deferred to future block (SUP-TNT-02 or similar)
2. **UI** — deferred per task spec
3. **Ticket linkage** — external customer is not yet linked to ticket requester; that wiring is deferred
4. **UpdatedAt** — no updated_at field (not in spec; simplest model)
5. **Name update** — `ResolveOrCreateAsync` does not update name if customer already exists with different name (deferred)

---

## 10. Final Readiness Assessment

**READY.** All done criteria met:

| Criterion | Status |
|-----------|--------|
| ExternalCustomer entity exists | PASS — `Domain/ExternalCustomer.cs` |
| Unique (TenantId + Email) enforced | PASS — `ux_support_external_customers_tenant_email` in migration |
| Repository implemented | PASS — `IExternalCustomerRepository` + `ExternalCustomerRepository` |
| Service implemented | PASS — `IExternalCustomerService` + `ExternalCustomerService` |
| No API exposure | PASS — no endpoints added |
| No changes to existing ticket behavior | PASS — additive only |
| Tenant isolation preserved | PASS — all queries scoped by `TenantId` |
| Support.Api build passes | PASS — 0 errors |
| Support.Tests build passes | PASS — 0 errors |
| Report complete | PASS — this document |
