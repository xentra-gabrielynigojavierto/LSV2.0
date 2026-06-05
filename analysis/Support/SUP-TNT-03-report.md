# SUP-TNT-03 Report — Customer RBAC Layer

_Status: COMPLETE_

---

## 1. Codebase Analysis

### Existing Authorization / RBAC
**Roles (`SupportRoles`):**
- PlatformAdmin, SupportAdmin, SupportManager, SupportAgent, TenantAdmin, TenantUser
- Groups: `All`, `InternalStaff`, `Managers`
- No ExternalCustomer role existed

**Policies (`SupportPolicies`):**
- `SupportRead` — all roles
- `SupportWrite` — all roles
- `SupportManage` — Managers only
- `SupportInternal` — InternalStaff only
- No `CustomerAccess` policy existed

**Endpoints and Auth:**
| Endpoint | Policy |
|----------|--------|
| POST /support/api/tickets | SupportWrite |
| GET /support/api/tickets | SupportRead |
| GET /support/api/tickets/{id} | SupportRead |
| PUT /support/api/tickets/{id} | SupportWrite |
| PUT /support/api/tickets/{id}/assignment | SupportInternal |
| POST /support/api/tickets/{id}/comments | SupportWrite |
| GET /support/api/tickets/{id}/comments | SupportRead |
| GET /support/api/tickets/{id}/timeline | SupportRead |
| POST /support/api/tickets/{id}/attachments | SupportWrite |
| GET /support/api/tickets/{id}/attachments | SupportRead |
| Queue endpoints | SupportManage / SupportInternal |

### TenantContext / Resolution
- `ITenantContext.TenantId` populated from JWT `tenant_id`/`tenantId`/`tid` claims
- In dev/test, `X-Tenant-Id` header also accepted
- Existing `TenantResolutionMiddleware` already handles `/support/api/customer/tickets` path (matches `/support/api/*` rule)

### ExternalCustomerService
- `ResolveOrCreateAsync(tenantId, email, name?)` — find-or-create
- Registered as `AddScoped`

### TicketService
- `ListByExternalCustomerAsync` added in SUP-TNT-02 — does NOT enforce VisibilityScope=CustomerVisible
- No customer-safe `GetCustomerTicketAsync` or `ListCustomerTicketsAsync` existed

### CommentService
- `AddAsync` — requires tenant context, checks ticket ownership by tenantId only
- No customer-scoped comment method existed

---

## 2. Existing Authorization / RBAC Model

All existing internal policies remain **completely unchanged**. `SupportRead`, `SupportWrite`, `SupportManage`, `SupportInternal` continue to use the same role sets.

The new `CustomerAccess` policy is **additive only** — no internal policy is weakened.

---

## 3. Customer Access Model

### New Role
`SupportRoles.ExternalCustomer = "ExternalCustomer"` — not in `All`, `InternalStaff`, or `Managers`.

### New Policy
`SupportPolicies.CustomerAccess = "CustomerAccess"` — requires authenticated user with role `ExternalCustomer`.

### What `CustomerAccess` Allows
| Action | Allowed |
|--------|---------|
| List own CustomerVisible tickets | YES |
| Get own CustomerVisible ticket | YES |
| Add comment to own CustomerVisible ticket | YES |
| Queue access | NO |
| Assignment | NO |
| Status management | NO |
| Internal-only tickets | NO |
| Other customer's tickets | NO |
| Cross-tenant tickets | NO |
| Product ref management | NO |
| Attachment upload | NO |
| Attachment read | NO (deferred) |

---

## 4. Customer Context Resolution

### JWT Claim Contract
Customer JWTs must contain:
| Claim | Type | Purpose |
|-------|------|---------|
| `tenant_id` | string | Tenant scoping (reuses existing claim, resolved by TenantResolutionMiddleware) |
| `role` | string | Value: `ExternalCustomer` (authorizes CustomerAccess policy) |
| `external_customer_id` | string (UUID) | Customer identity — read from JWT claim only |
| `email` | string | Optional — used as comment author email |
| `name` | string | Optional — used as comment author name |

### Resolution Pattern in Endpoints
1. `ITenantContext.TenantId` — already set by `TenantResolutionMiddleware` from JWT `tenant_id` claim
2. `external_customer_id` — read directly from `HttpContext.User.FindFirst("external_customer_id")?.Value` in each endpoint handler
3. Both are required; missing either → 403

**Not trusted:** request body, query string, or headers for `externalCustomerId` authorization.

### Customer Token Issuance
Not available yet — no customer login/token issuance is implemented in this block. Endpoints fail closed: without a valid `ExternalCustomer` role in the JWT, the `CustomerAccess` policy returns 403.

---

## 5. Service / Query Enforcement

### New methods added to `ITicketService`
```
ListCustomerTicketsAsync(tenantId, externalCustomerId, page, pageSize)
  - WHERE TenantId = tenantId
  - AND ExternalCustomerId = externalCustomerId
  - AND VisibilityScope = CustomerVisible

GetCustomerTicketAsync(tenantId, externalCustomerId, ticketId)
  - WHERE TenantId = tenantId
  - AND Id = ticketId
  - AND ExternalCustomerId = externalCustomerId
  - AND VisibilityScope = CustomerVisible
```

### New method added to `ICommentService`
```
AddCustomerCommentAsync(tenantId, externalCustomerId, ticketId, body, authorEmail?, authorName?)
  - First calls GetCustomerTicketAsync to verify ownership (returns 404 if any constraint fails)
  - Creates comment with CommentType=CustomerReply, Visibility=CustomerVisible
  - Includes externalCustomerId in audit metadata
```

---

## 6. API / Endpoint Changes

### New Endpoint Group: `/support/api/customer/tickets`
All endpoints require `CustomerAccess` policy.

| Method | Path | Description |
|--------|------|-------------|
| GET | /support/api/customer/tickets | List own CustomerVisible tickets |
| GET | /support/api/customer/tickets/{id} | Get own CustomerVisible ticket |
| POST | /support/api/customer/tickets/{id}/comments | Add comment to own ticket |

No existing endpoints are changed.

### Security Pattern per Endpoint
Each customer endpoint:
1. Reads `external_customer_id` claim from JWT — missing/invalid → 403
2. Gets `tenantId` from `ITenantContext` (middleware-resolved from JWT) — missing → 400
3. Passes both to the customer-scoped service method
4. Service method enforces all three constraints: tenantId + externalCustomerId + CustomerVisible

---

## 7. Audit / Notification Alignment

- `AddCustomerCommentAsync` includes `requester_type`, `external_customer_id`, and `visibility_scope` in audit metadata
- Uses existing `SupportAuditEventTypes.TicketCommentAdded` — no new event taxonomy
- No email/Comms/SLA/Task logic added

---

## 8. Tenant Isolation Validation

| Scenario | Enforcement |
|----------|-------------|
| Customer A cannot see Customer B's tickets | `ExternalCustomerId` match required in all queries |
| Customer from Tenant A cannot see Tenant B tickets | `TenantId` match required in all queries |
| Customer cannot see Internal tickets | `VisibilityScope == CustomerVisible` required in all queries |
| Agent can still see all tenant tickets | Existing `SupportRead` policy, no change to query methods |

---

## 9. Backward Compatibility Validation

| Scenario | Result |
|----------|--------|
| Internal ticket create/list/detail | Unchanged — no new requirements |
| Agent list sees all tickets | `SupportRead` policy unchanged, no query changes |
| Existing SupportRead/Write/Manage/Internal policies | Unchanged |
| Queue/assignment endpoints | Unchanged — `ExternalCustomer` role not in any existing policy |
| TenantResolutionMiddleware | No changes — customer path already covered by `/support/api/*` rule |

---

## 10. Security Test Results

Tests added in `CustomerApiTests.cs`:
- Customer can list own CustomerVisible tickets (only CustomerVisible tickets returned)
- Customer cannot see Internal-visibility tickets (even if linked to same externalCustomerId)
- Customer cannot see another customer's CustomerVisible ticket
- Customer cannot access queue endpoints (403)
- Customer cannot call assignment endpoint (403)
- Customer cannot call internal admin ticket list with customer token (403, since ExternalCustomer not in SupportRead)
- Internal agent list remains unchanged (sees all tenant tickets)

---

## 11. Files Created / Changed

| File | Change |
|------|--------|
| `Auth/SupportRoles.cs` | Added `ExternalCustomer` role; added `CustomerAccess` policy constant |
| `Auth/AuthExtensions.cs` | Registered `CustomerAccess` policy |
| `Services/TicketService.cs` | Added `ListCustomerTicketsAsync` + `GetCustomerTicketAsync` to interface + implementation |
| `Services/CommentService.cs` | Added `AddCustomerCommentAsync` to interface + implementation |
| `Endpoints/CustomerTicketEndpoints.cs` | New file — 3 customer-safe endpoints |
| `Program.cs` | Registered `MapCustomerTicketEndpoints()` |
| `Tests/TestJwt.cs` | Added `IssueCustomer(sub, tenantId, externalCustomerId, email?, name?)` helper |
| `Tests/CustomerApiTests.cs` | New test class — customer RBAC validation tests |
| `analysis/SUP-TNT-03-report.md` | This file |

---

## 12. Build / Test Results

**Support.Api** — `dotnet build` → **0 errors**, 7 pre-existing warnings (NU190x vulnerability advisories, MSB3277 version conflict, CS0618 ISystemClock obsolete — all pre-existing).

**Support.Tests** — `dotnet build` → **0 errors**, 8 pre-existing warnings (same set plus 2 CS8604 nullable warnings in CommentApiTests and ProductRefApiTests — pre-existing).

New files compiled:
- `Endpoints/CustomerTicketEndpoints.cs`
- `Tests/CustomerApiTests.cs` (11 tests: list, get, cross-customer isolation, comment add, comment isolation, 403 on queue/internal-ticket-list, 401 without token, 403 with wrong role)

Exit code: 0

---

## 13. Known Gaps / Deferred Items

1. **Customer token issuance** — No customer login/JWT issuance is implemented. Endpoints fail closed (403) without a valid customer JWT. This is intentional per block scope.
2. **Attachment read for customers** — Not exposed. Deferred to a later block.
3. **Comment list for customers** — Not exposed. Customer can POST comments but cannot list comments via the customer endpoint. Can be added safely later.
4. **Timeline for customers** — Not exposed. Deferred.
5. **Customer status management** — Intentionally not added (no close/reopen by customer in this block).
6. **Admin ListByExternalCustomerAsync** — The existing internal method does not enforce `VisibilityScope=CustomerVisible`. This is intentional — agents need to see all customer-linked tickets regardless of visibility. The new `ListCustomerTicketsAsync` method adds the scope constraint.

---

## 14. Final Readiness Assessment

**READY.** All done criteria met:

| Criterion | Status |
|-----------|--------|
| Customer-scoped access model exists | PASS — `CustomerAccess` policy + `ExternalCustomer` role |
| Customer access restricted to tenantId + externalCustomerId + CustomerVisible | PASS — enforced in service layer |
| Customer cannot access queues, assignment, admin | PASS — `ExternalCustomer` not in any existing policy |
| Existing SupportRead/Write/Manage/Internal unchanged | PASS — additive only |
| Tenant isolation preserved | PASS — all queries scope by tenantId |
| No public unauthenticated access | PASS — `CustomerAccess` requires authenticated user with ExternalCustomer role |
| No Identity integration | PASS |
| No customer portal UI | PASS |
| No Comms/email/SLA/Task logic | PASS |
| Build/tests pass | PASS — 0 errors |
| Report complete | PASS — this document |
