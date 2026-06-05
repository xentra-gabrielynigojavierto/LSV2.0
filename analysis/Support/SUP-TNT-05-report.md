# SUP-TNT-05 Report — API Hardening

_Status: COMPLETE_

---

## 1. Codebase Analysis

### Service overview
- .NET 8 minimal-API service, MySQL via Pomelo EF Core
- Serilog request logging, OpenTelemetry + Prometheus metrics
- FluentValidation for most request DTOs
- Split middleware pipeline: `UseAuthentication → TenantResolutionMiddleware → UseAuthorization`

### Existing strengths
- JWT authentication fail-closed at startup: rejects misconfigured issuer/audience/signing-key combinations
- `MapInboundClaims = false` — claim names preserved verbatim (`sub`, `role`, `tenant_id`)
- Clock-skew limited to 30 seconds
- `TenantResolutionMiddleware` reads tenant exclusively from JWT in Production
- 5 well-scoped authorization policies; `ExternalCustomer` role isolated to `CustomerAccess` only
- Mode gate (`IsCustomerSupportEnabledAsync`) checked on all three customer endpoints before any ticket data access
- Ownership triple-constraint (tenantId + externalCustomerId + CustomerVisible) enforced at service layer
- Swagger gated to Development environment only
- GUID route constraint `{id:guid}` on all ticket/comment endpoints

---

## 2. Attack Surface Assessment

### Customer-facing endpoints (internet-exposed)
| Endpoint | Auth | RBAC | Mode gate | Ownership | Gap (pre-hardening) |
|---|---|---|---|---|---|
| `GET /support/api/customer/tickets` | JWT | CustomerAccess | ✓ | tenantId+custId | No page/pageSize validation; no rate limit |
| `GET /support/api/customer/tickets/{id}` | JWT | CustomerAccess | ✓ | tenantId+custId | No rate limit |
| `POST /support/api/customer/tickets/{id}/comments` | JWT | CustomerAccess | ✓ | tenantId+custId | No body length limit; no validator; no rate limit |

### Internal/admin endpoints
| Endpoint group | Auth | Accessible by ExternalCustomer? |
|---|---|---|
| `GET/PUT /support/api/admin/tenant-settings` | SupportRead / SupportManage | No — excluded from both role arrays |
| `/support/api/tickets/*` | SupportRead / SupportWrite | No |
| `/support/api/queues/*` | SupportManage | No |
| `/support/api/tickets/{id}/comments/*` | SupportRead / SupportWrite | No |
| `/support/api/tickets/{id}/attachments/*` | SupportWrite | No |

### Unauthenticated endpoints
| Endpoint | Notes |
|---|---|
| `GET /support/api/health` | Standard health check — no sensitive data |
| `GET /support/api/metrics` | Prometheus scraping — should be network-restricted at infra layer |

### Input vectors assessed
| Vector | Location | Pre-hardening status |
|---|---|---|
| `page` query param | Customer list | Not validated (accepts negative/zero) |
| `page_size` query param | Customer list | Not validated (no upper cap) |
| Comment `body` | POST body | Only whitespace check; no length cap |
| Route `{id:guid}` | All ticket/comment paths | Constrained by ASP.NET route parser — safe |
| JWT claims | All endpoints | Validated by JwtBearer middleware |

### Missing protections (pre-hardening)
1. No rate limiting on any endpoint
2. `page` and `pageSize` not validated (negative pages, huge page sizes possible)
3. `CustomerAddCommentRequest.Body` has no length cap (no FluentValidation wired)
4. No global exception handler — uncaught exceptions could surface stack traces
5. No security headers (`X-Content-Type-Options`, `X-Frame-Options`, `X-XSS-Protection`)
6. CORS is `AllowAnyOrigin` — acceptable for internal microservice behind gateway, documented

---

## 3. Input Validation Hardening

### Customer list endpoint
- `page` must be ≥ 1; returns 400 with RFC 7807 ValidationProblem if violated
- `pageSize` must be 1–100 inclusive; returns 400 if outside range

### Customer comment endpoint
- Body validated via new `CustomerAddCommentRequestValidator` (FluentValidation):
  - `NotEmpty()` — existing whitespace check now in validator
  - `MaximumLength(8000)` — aligns with `CreateCommentRequestValidator` for internal agents

### Implementation
- New `Validators/CustomerCommentValidator.cs` — `CustomerAddCommentRequestValidator`
- `CustomerTicketEndpoints.cs` list handler: page/pageSize validation before mode check
- `CustomerTicketEndpoints.cs` comment handler: inject and invoke `IValidator<CustomerAddCommentRequest>`

---

## 4. Rate Limiting Strategy

### Scope
Applied exclusively to the `CustomerAccess` route group
(`/support/api/customer/*`). Internal/admin endpoints are NOT rate-limited — their access is already restricted to platform staff via JWT role.

### Policy
- Algorithm: Fixed window
- Limit: **60 requests per 60-second window** per unique `external_customer_id` (JWT claim)
- Fallback key: remote IP address (for unauthenticated requests reaching the limiter before auth rejects)
- Queue: 0 (immediate reject, no queuing)
- Response: HTTP 429 with `Retry-After: 60` header and RFC 7807-style problem body

### Configuration
`Support:RateLimit:CustomerPermitLimit` (default: 60) and
`Support:RateLimit:CustomerWindowSeconds` (default: 60) can be overridden
without code changes (used in tests to set a low limit for verification).

### Middleware position
`UseRateLimiter()` runs after `UseAuthentication()` so the `external_customer_id` claim is available for key selection, and before `UseAuthorization()` so abusive traffic is rejected cheaply.

---

## 5. Payload Constraints

- Comment body capped at **8 000 characters** via FluentValidation — aligns with `CreateCommentRequestValidator` for internal staff
- `page` and `pageSize` on list endpoint constrained to 1–100 — prevents oversized DB queries from unauthenticated-but-token-holding callers

---

## 6. Error Handling Standardization

### Global exception handler
Added `app.UseExceptionHandler(...)` before `UseAuthentication()`:
- Returns HTTP 500 with RFC 7807 body: `{ title, status, correlationId }`
- No stack traces, no exception messages, no SQL errors in response body

### Existing error responses
All handlers already use `Results.Problem(statusCode, title)` or
`Results.ValidationProblem(...)` (RFC 7807 ProblemDetails), with one exception:
`Results.BadRequest(new { error = ex.Message })` for `InvalidStatusTransitionException`
on internal endpoints — this is acceptable since the message is safe
(enum transition description, not a stack trace).

### Customer endpoint responses
| Status | Scenario | Body format |
|---|---|---|
| 400 | Invalid page/pageSize or invalid comment body | RFC 7807 ValidationProblem |
| 401 | Missing or invalid JWT | JwtBearer default (minimal details) |
| 403 | Wrong role or mode disabled | RFC 7807 Problem |
| 404 | Ticket not found in tenant scope | Empty 404 (no ownership leak) |
| 429 | Rate limit exceeded | RFC 7807-style `{ title, status, detail, correlationId }` |
| 500 | Unhandled exception | `{ title, status, correlationId }` — no internal details |

---

## 7. Security Headers

Applied via new `SecurityHeadersMiddleware` early in the pipeline:

| Header | Value | Rationale |
|---|---|---|
| `X-Content-Type-Options` | `nosniff` | Prevents MIME sniffing |
| `X-Frame-Options` | `DENY` | Prevents clickjacking |
| `X-XSS-Protection` | `0` | Disables legacy XSS filter (modern browsers ignore it; `0` is the correct modern setting) |
| `Server` | (removed) | Suppresses ASP.NET/Kestrel fingerprinting |

`Strict-Transport-Security` and `Content-Security-Policy` are handled at the API
gateway layer and are intentionally not duplicated here.

### CORS
CORS remains `AllowAnyOrigin` — this is an internal microservice behind the API gateway and does not serve browser traffic directly. Requests originate from the Next.js BFF server (not browser). Documented as acceptable per current architecture.

---

## 8. Logging / Observability

### Security-relevant structured log events added to customer endpoints:

| Event | Level | Fields |
|---|---|---|
| Rate limit exceeded (OnRejected) | Warning | tenantId, customerId, path, remoteIp |
| Invalid page/pageSize | Debug | page, pageSize |
| Mode disabled (customer 403) | (existing Serilog request log) | path, status |

### Existing coverage
- `UseSerilogRequestLogging()` captures every request: method, path, status, elapsed ms
- `TenantResolutionMiddleware` logs `Debug` when tenant not resolved

### PII policy
- No comment body content logged
- No ticket content logged
- `external_customer_id` is a GUID (non-PII)
- `tenantId` is a GUID or code (non-PII)

---

## 9. Endpoint Exposure Audit

### Customer endpoints
- ✅ JWT required (401 without token)
- ✅ `CustomerAccess` policy enforces `ExternalCustomer` role (403 if wrong role)
- ✅ `TenantResolutionMiddleware` enforces tenant from JWT in Production
- ✅ Mode gate: `IsCustomerSupportEnabledAsync` checked before any data access
- ✅ Ownership: service layer enforces tenantId + externalCustomerId + CustomerVisible triple
- ✅ Route GUID constraint on `{id:guid}` — malformed IDs return 400 before handler
- ✅ Rate limiting added (60 req/min per external_customer_id)
- ✅ Input validation on page, pageSize, comment body

### Internal endpoints
- ✅ `SupportRead`/`SupportWrite`/`SupportManage`/`SupportInternal` — `ExternalCustomer` is absent from all of these role arrays
- ✅ No debug/test endpoints exposed
- ✅ Swagger only in Development
- ✅ `X-Tenant-Id` header ignored in Production

### Health / metrics
- `/support/api/health` — anonymous, returns `Healthy`. No sensitive data.
- `/support/api/metrics` — anonymous, Prometheus format. **Should be network-restricted at infrastructure layer** (not exposed through public gateway). Documented as known gap.

---

## 10. Validation Results

| Check | Result |
|---|---|
| Customer endpoints reject invalid page (< 1) | ✅ 400 ValidationProblem |
| Customer endpoints reject excessive pageSize (> 100) | ✅ 400 ValidationProblem |
| Comment body empty/whitespace rejected | ✅ 400 ValidationProblem |
| Comment body over 8000 chars rejected | ✅ 400 ValidationProblem |
| Comment body exactly 8000 chars accepted | ✅ Not 400 (mode gate fires at 403; length validation passed) |
| Unauthorized access returns 401 | ✅ (existing, regression-tested) |
| Wrong role returns 403 | ✅ (existing, regression-tested) |
| Mode disabled returns 403 | ✅ (existing, regression-tested) |
| Cross-customer access blocked | ✅ (existing, regression-tested) |
| Rate limit exceeded returns 429 | ✅ 429 with Retry-After header |
| Security headers present | ✅ X-Content-Type-Options, X-Frame-Options, X-XSS-Protection |
| No stack traces on 500 | ✅ global exception handler returns safe body |
| Internal endpoints unaffected | ✅ all 19 pre-existing tests pass |

---

## 11. Build / Test Results

### Build
```
dotnet build Support.Api  →  0 errors  (3 NU warnings — pre-existing, not introduced by this work)
dotnet build Support.Tests  →  0 errors
```

### Tests — final run (all classes, no-build)
| Test class | Passed | Failed | Total | Notes |
|---|---|---|---|---|
| `TenantSettingsTests` | 8 | 0 | 8 | Pre-existing (SUP-TNT-04) |
| `CustomerApiTests` | 11 | 0 | 11 | Pre-existing (SUP-TNT-04) |
| `HardeningTests` | 11 | 0 | 11 | New — validation + security-header coverage |
| `RateLimitTests` | 3 | 0 | 3 | New — 429 + Retry-After + per-customer isolation |
| **TOTAL** | **33** | **0** | **33** | |

`HardeningTests` covers:
- `CustomerList_PageZero_Returns400`
- `CustomerList_NegativePage_Returns400`
- `CustomerList_PageSizeZero_Returns400`
- `CustomerList_PageSizeTooLarge_Returns400`
- `CustomerList_MaxPageSize_PassesValidation`
- `CustomerComment_EmptyBody_Returns400`
- `CustomerComment_WhitespaceOnlyBody_Returns400`
- `CustomerComment_BodyExceedsMaxLength_Returns400`
- `CustomerComment_BodyAtMaxLength_PassesValidation`
- `SecurityHeaders_PresentOnCustomerListResponse`
- `SecurityHeaders_PresentOnHealthEndpoint`

`RateLimitTests` covers (via `RateLimitTestFactory` — permit limit=3):
- `CustomerEndpoint_Returns429_WhenPermitLimitExceeded`
- `CustomerEndpoint_429Response_HasRetryAfterHeader`
- `DifferentCustomers_HaveIndependentRateLimits`

---

## 12. Known Gaps / Deferred Items

1. **Metrics endpoint unauthenticated** — `/support/api/metrics` is publicly accessible on the service port. Should be restricted at infrastructure/gateway level (not expose port 5017 to the public internet directly). Not fixed here — gateway handles routing.
2. **CORS wide-open** — intentional for service-to-service; gateway handles public CORS. Acceptable in current architecture.
3. **`page`/`pageSize` on internal ticket list** — internal agents hold trusted JWTs; no abuse concern. No cap added here per spec ("do not apply aggressive rate limiting to internal endpoints").
4. **Prometheus metrics auth** — Prometheus scraping is standard unauthenticated. Infrastructure-level network policy is the correct mitigation.
5. **No customer login/token issuance** — out of scope per spec; tracked separately.

---

## 13. Final Readiness Assessment

| Criterion | Status |
|---|---|
| Rate limiting on customer endpoints (60 req/min) | PASS |
| Input validation: page ≥ 1 | PASS |
| Input validation: pageSize 1–100 | PASS |
| Input validation: comment body length ≤ 8000 | PASS |
| Input validation: comment body required | PASS |
| Global exception handler — no stack traces | PASS |
| Security headers applied | PASS |
| 429 response with Retry-After header | PASS |
| Internal endpoints unaffected | PASS |
| No breaking API contract changes | PASS |
| All existing tests pass | PASS |
| New hardening tests pass | PASS |
| Report complete | PASS |
