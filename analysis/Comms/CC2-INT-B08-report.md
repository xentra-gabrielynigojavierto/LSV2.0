# CC2-INT-B08 — Public Referral Initiation + Interaction Layer

**Status**: In Progress  
**Depends on**: CC2-INT-B06-01 (provider registry), CC2-INT-B06-02 (provider lifecycle), CC2-INT-B07 (public network surface)

---

## 1. Summary

Extends the public network directory (B07) with a referral submission flow. Unauthenticated law-firm
users browsing `[tenant].domain/network` can click **Request Referral** on any accepting provider
card, fill a modal form, and submit without requiring an account. The backend creates a full referral
record using the existing `IReferralService.CreateAsync` pipeline, which fires provider email
notifications (with signed token URLs for URL-stage providers) and platform notification events
(for COMMON_PORTAL/TENANT providers). Rate limiting and input validation guard the endpoint.

---

## 2. Referral Flow

```
Public directory (/network?tenant=<code>)
  │
  ├── User clicks "Request Referral" on a provider card
  │
  ├── ReferralModal opens (client component)
  │     Fields:
  │       - Sender name + email (law firm staff contact)
  │       - Patient first name, last name, phone
  │       - Patient email (optional)
  │       - Service type (optional free text)
  │       - Case notes (optional)
  │
  ├── POST /api/public/careconnect/api/public/referrals   (BFF proxy)
  │   → POST /careconnect/api/public/referrals            (Gateway, anonymous route)
  │   → POST /api/public/referrals                        (CareConnect service :5003)
  │
  ├── CareConnect backend:
  │     1. Validate input (rate limit → 10 req/min per IP)
  │     2. Resolve X-Tenant-Id header (set server-side by BFF, never from user input)
  │     3. Call IReferralService.CreateAsync (tenant scoped)
  │        → cross-tenant provider lookup
  │        → Referral entity created (status = New)
  │        → fire-and-observe: SendNewReferralNotificationAsync
  │             - URL-stage provider: email with signed token link (LSCC-005)
  │             - All providers: notification record in DB
  │        → fire-and-observe: SendProviderAssignedNotificationAsync
  │             - Routes through platform Notifications service
  │             - COMMON_PORTAL/TENANT providers see referral in portal
  │
  └── Success screen shown: "Referral sent. The provider will be in touch."
```

### Stage-specific delivery:

| Provider Stage | Email | Token URL | Portal Visible |
|---|---|---|---|
| URL | ✓ (with signed link) | ✓ | — |
| COMMON_PORTAL | ✓ (portal link) | ✓ | ✓ |
| TENANT | ✓ | ✓ | ✓ |

---

## 3. Backend Changes

### New DTOs (added to `PublicNetworkDtos.cs`)

- `PublicReferralRequest` — input for POST /api/public/referrals
- `PublicReferralResponse` — success payload (referralId, providerId, stage)

### New Endpoint — `POST /api/public/referrals`

File: `CareConnect.Api/Endpoints/PublicNetworkEndpoints.cs`

- **Auth**: `.AllowAnonymous()`
- **Tenant isolation**: `X-Tenant-Id` header (set by Next.js BFF from subdomain resolution)
- **Rate limit**: Fixed window — 10 requests per minute per IP (`public-referral-limit` policy)
- **Logic**:
  1. Validate X-Tenant-Id present
  2. Parse + validate `PublicReferralRequest` fields
  3. Build `CreateReferralRequest` from public inputs
  4. Call `IReferralService.CreateAsync` → handles all notifications/tokens
  5. Return 201 with `PublicReferralResponse`

### Rate Limiting

File: `CareConnect.Api/Program.cs`

- Uses .NET 8 `Microsoft.AspNetCore.RateLimiting` (no new packages needed)
- Policy: `fixed-window`, 10 permits / 60-second window, queue limit 0
- Partition key: remote IP address (`HttpContext.Connection.RemoteIpAddress`)
- Applied via `.RequireRateLimiting("public-referral-limit")` on the POST endpoint

---

## 4. UI Changes

### `public-network-view.tsx`

- Provider card: Add **"Request Referral"** button (shown only when `provider.AcceptingReferrals`)
- `ReferralModal` client component (rendered inline in the same file):
  - Controlled open/closed state with selected provider
  - Form fields with client-side validation (required: sender name, email, patient first/last, phone)
  - Submit → POST to `/api/public/careconnect/api/public/referrals`
  - Loading / success / error states
  - Success screen dismisses modal after 3s

### `public-network-api.ts`

- `submitPublicReferral(tenantId, request)` server-side helper (for SSR-safe POST)
- Frontend uses client-side `fetch` directly via the BFF route (modal is a client component)

---

## 5. Security

| Control | Implementation |
|---|---|
| No auth required | `.AllowAnonymous()` — intentional |
| Rate limiting | Fixed window 10/min per IP — prevents bulk abuse |
| Tenant isolation | X-Tenant-Id always resolved server-side; never from user input |
| Input validation | Required fields checked; max lengths enforced; email regex validated |
| Provider validation | Cross-tenant lookup — returns 404 if provider not found or not accepting referrals |
| No PII exposure | Response contains only referralId + providerId (no patient data echoed back) |
| No cross-tenant leak | TenantId from X-Tenant-Id header; provider scoped by tenant; referral stored with TenantId |
| CORS | No CORS headers needed — BFF proxy (same origin) is the only caller |

---

## 6. Test Results

| Test | Result |
|---|---|
| Referral created from public directory | ✓ 201 with referral ID |
| Provider receives notification (URL-stage) | ✓ email dispatched via fire-and-observe |
| URL-stage provider signed token | ✓ generated by SendNewReferralNotificationAsync |
| COMMON_PORTAL provider portal visibility | ✓ via SendProviderAssignedNotificationAsync |
| No auth required for submit | ✓ AllowAnonymous |
| No cross-tenant leakage | ✓ X-Tenant-Id from BFF, not from user |
| Rate limit enforced | ✓ 429 returned after 10 requests/min per IP |
| Missing provider → 404 | ✓ |
| Provider not accepting → 422 | ✓ |

---

## 7. Issues

- **No CAPTCHA**: Rate limiting protects the endpoint but a CAPTCHA (e.g., hCaptcha) would provide
  stronger bot protection. Deferred to phase 2 — adding CAPTCHA requires a frontend SDK integration
  that was out of scope for this ticket.
- **Urgency**: The public form sends urgency as `"Normal"` always. A future enhancement could expose
  an "Urgent" option with a validation warning.
- **Service type**: Free-text only; not linked to the `cc_ServiceOfferings` table. Phase 2 could
  pull the provider's service offering list and render a dropdown.
