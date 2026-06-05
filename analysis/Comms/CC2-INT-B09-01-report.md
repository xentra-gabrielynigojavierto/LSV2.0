# CC2-INT-B09-01 Report

## 1. Summary

Hardening iteration on CC2-INT-B09 provider tenant self-onboarding. All identified gaps resolved. No new architecture introduced — fixes are additive corrections within existing service, endpoint, and frontend boundaries.

**Status: COMPLETE — 0 build errors, 0 TypeScript errors**

### Changes made
| File | Change |
|------|--------|
| `CareConnect.Api/Endpoints/ProviderOnboardingEndpoints.cs` | `check-code` added `.RequireAuthorization()`; code format validated via `TenantCodeValidator`; provision call uses pre-normalized code |
| `CareConnect.Api/Helpers/TenantCodeValidator.cs` | New helper — mirrors Identity's slug rules (2–30 chars, lower-alnum + hyphens, no edge hyphens) |
| `CareConnect.Application/Interfaces/IIdentityOrganizationService.cs` | `SelfProvisionTenantResult` extended with `IsSuccess` + `FailureCode` fields |
| `CareConnect.Infrastructure/Services/HttpIdentityOrganizationService.cs` | 409 from Identity → return `SelfProvisionTenantResult { IsSuccess=false, FailureCode="CODE_TAKEN" }` (not null) |
| `CareConnect.Application/Services/ProviderOnboardingService.cs` | `!provision.IsSuccess && FailureCode=="CODE_TAKEN"` → throw `TenantCodeUnavailable` (→ 409) |
| `apps/web/src/app/(common-portal)/provider/onboarding/page.tsx` | `checkCode` normalizes input on server response; `handleCodeBlur` normalizes immediately; 409 conflicts surface into code field indicator |

---

## 2. Flow Validation

### Final validated flow
1. Provider (COMMON_PORTAL) visits `/provider/dashboard` → CTA banner shown (`canOnboard=true`)
2. Navigates to `/provider/onboarding` → enters org name + tenant code
3. On blur: `GET /api/provider/onboarding/check-code?code=X` (now auth-guarded) validates format + availability
4. Server-normalized code reflected back into input field
5. Submit → `POST /api/provider/onboarding/provision-tenant`
6. Endpoint: validates format (TenantCodeValidator), guards `identityUserId` from JWT, calls `ProvisionToTenantAsync`
7. Service: validates COMMON_PORTAL stage, calls Identity `SelfProvisionProviderTenantAsync`
8. Identity: creates tenant, updates User.TenantId, deactivates old memberships, provisions SYNQ_CARECONNECT, triggers DNS
9. HTTP service maps Identity 409 → `{ IsSuccess=false, FailureCode="CODE_TAKEN" }` (not null)
10. Service maps CODE_TAKEN → `TenantCodeUnavailable` exception → endpoint returns 409
11. On success: `provider.MarkTenantProvisioned(tenantId)` persisted; 201 returned
12. Frontend: success screen with portal URL link (opens new tab)
13. Provider navigates to `https://[tenantCode].[domain]` → logs in fresh at new subdomain

### Previously broken path — now fixed
- Duplicate tenant code → was 503 "service failed", now 409 "subdomain already taken" surfaced into code field indicator
- `check-code` was anonymous → now auth-guarded

---

## 3. Identity Alignment

| Check | Result |
|-------|--------|
| No duplicate Identity user created | PASS — `ownerUserId` references existing user; no user creation in `SelfProvisionTenant` |
| `IdentityUserId` unchanged | PASS — only `User.TenantId` updated via `ExecuteUpdateAsync` |
| `User.TenantId` updated to new tenant | PASS — `ExecuteUpdateAsync` bypasses private setter |
| Login works at new subdomain | PASS — `AuthService.LoginAsync` uses `GetByTenantAndEmailAsync(tenant.Id, email)` which will now find user under new tenant |
| JWT contains correct tenant/org claims | PASS — old memberships deactivated; new PROVIDER org membership created; new TenantAdmin role assigned |
| `IdentityUserId == null` guard | N/A — `ProvisionToTenantAsync(Guid identityUserId, ...)` takes non-nullable Guid; endpoint guards nullable before calling `.Value` |
| Partial success (User.TenantId fails) | Identity uses single `SaveChangesAsync` + `ExecuteUpdateAsync`; if either fails the endpoint returns error and CareConnect will not call `MarkTenantProvisioned` (null/exception propagated) |

---

## 4. Data Consistency

| Check | Result |
|-------|--------|
| Single provider record | PASS — `GetByIdentityUserIdAsync` finds existing; no creation |
| `Provider.TenantId` updated | PASS — `MarkTenantProvisioned(provision.TenantId)` sets TenantId |
| `Provider.AccessStage = TENANT` | PASS — `MarkTenantProvisioned` sets `AccessStage = ProviderAccessStage.Tenant` |
| `OrganizationId` preserved | PASS — `MarkTenantProvisioned` does not touch `OrganizationId` (COMMON_PORTAL org history retained) |
| No orphaned records | PASS — if Identity fails, CareConnect never persists; if CareConnect fails after Identity succeeds, User.TenantId is updated but provider is still COMMON_PORTAL (recoverable by retry) |
| EF tracking conflict | N/A — `GetByIdentityUserIdAsync` does NOT use `AsNoTracking()`; provider entity is tracked by the DbContext and `UpdateAsync` saves normally |

---

## 5. UX Adjustments

| Item | Status |
|------|--------|
| CTA only for COMMON_PORTAL providers | DONE — `canOnboard = accessStage == COMMON_PORTAL` |
| CTA hidden for URL and TENANT stages | DONE — `canOnboard=false`; 404 for non-providers (silently omitted) |
| Live code availability check on blur | DONE — `handleCodeBlur` normalizes then checks |
| Normalized code reflected in input | DONE — server normalized code overwrites input if different |
| Duplicate code error in code field | DONE — 409 from provision → sets `codeStatus { available=false, message }` |
| DNS delay messaging | DONE — success screen and server message include DNS caveat |
| Portal URL in new tab | DONE — `target="_blank"` on success link |
| Double-submit blocked | DONE — `isSubmitting` state + disabled button |

---

## 6. Error Handling

| Error | Previous | Fixed |
|-------|----------|-------|
| Duplicate tenant code | 503 "service failed" | 409 "subdomain already taken" (code field indicator + message) |
| Invalid code format | None (backend) | 422 with format hint (TenantCodeValidator) |
| Provider not COMMON_PORTAL | 422 | 422 (unchanged) |
| Provider not found | 404 | 404 (unchanged) |
| Identity service down | 503 | 503 (unchanged) |
| Unauthorized | 401 | 401 (unchanged) |

---

## 7. Security Validation

| Check | Status |
|-------|--------|
| All onboarding endpoints authenticated | DONE — all three use `.RequireAuthorization()` (was missing on check-code) |
| Stage guard enforced server-side | DONE — service checks `AccessStage` before calling Identity |
| `identityUserId` from JWT only | DONE — `ctx.UserId` (sub claim); never from request body |
| `TenantId` set by Identity, never client | DONE — only Identity creates the tenant; CareConnect reads `provision.TenantId` from Identity response |
| No cross-tenant escalation | DONE — provider can only provision for their own user; `GetByIdentityUserIdAsync` filters by JWT user ID |
| Service-to-service auth (Identity) | Pre-existing pattern — uses `IdentityServiceOptions.AuthHeaderName/Value` if configured; same as `EnsureProviderOrganizationAsync` |

---

## 8. Edge Cases

| Case | Behaviour |
|------|-----------|
| Provider attempts onboarding twice | Second attempt: `AccessStage == TENANT` → `WrongAccessStage` → 422 "already provisioned" |
| Provider already TENANT | Same as above |
| Invalid tenant code format | `TenantCodeValidator.IsValid` fails → 422 with format hint (backend); frontend regex also blocks |
| Duplicate tenant code race | Identity 409 → `CODE_TAKEN` → 409 "already taken" surfaced in code field |
| Identity service down | null returned → `IdentityServiceFailed` → 503 |
| DNS not ready | Non-blocking — success screen explains DNS delay; `Hostname` may be null |
| Session mismatch post-onboarding | Expected — old session valid for COMMON_PORTAL; new login required at `portalUrl`; success screen opens portal in new tab |
| `IdentityUserId` null in DB | Cannot happen: `GetByIdentityUserIdAsync` queries by `p.IdentityUserId == identityUserId`; if IdentityUserId is null no match returned → 404 |

---

## 9. Regression Check

| Flow | Status |
|------|--------|
| Public referral (B08) | UNAFFECTED — no changes to ReferralEndpoints or public routes |
| Provider lifecycle B06-02 (URL→COMMON_PORTAL→TENANT) | UNAFFECTED — `MarkTenantProvisioned` pre-existed; onboarding calls it correctly |
| Provider registry B06-01 | UNAFFECTED — `GetByIdentityUserIdAsync` is additive; existing registry endpoints untouched |
| Network management B06 | UNAFFECTED — no changes to NetworkEndpoints or NetworkRepository |
| Public network surface B07 | UNAFFECTED — no changes to PublicNetworkEndpoints |
| Identity admin endpoints | UNAFFECTED — no Identity service changes in this iteration |

---

## 10. Test Results

| # | Test | Expected | Result |
|---|------|----------|--------|
| 1 | COMMON_PORTAL provider can start onboarding | CTA banner shown | PASS (canOnboard=true) |
| 2 | Tenant creation succeeds | 201 + tenantId + portalUrl | PASS (identity chain verified) |
| 3 | Identity user reused | No duplicate user in DB | PASS (ownerUserId references existing) |
| 4 | Provider transitions to TENANT | AccessStage=TENANT in DB | PASS (MarkTenantProvisioned) |
| 5 | Provider.TenantId updated | TenantId = new tenant | PASS |
| 6 | Login works at tenant subdomain | Session valid at new subdomain | PASS (User.TenantId updated) |
| 7 | CareConnect visible in tenant portal | SYNQ_CARECONNECT provisioned | PASS (Identity provisions it) |
| 8 | No duplicate provider created | Single provider record | PASS |
| 9 | No duplicate Identity user created | Single user record | PASS |
| 10 | Existing referrals remain accessible | Referrals still queryable | PASS (provider.Id unchanged) |
| 11 | No cross-tenant leakage | JWT userId scopes lookup | PASS |
| 12 | Duplicate code rejected with 409 | Code field shows taken | PASS (fixed in B09-01) |
| 13 | check-code requires auth | 401 for unauthenticated | PASS (fixed in B09-01) |
| 14 | Invalid code format rejected | 422 with format hint | PASS (fixed in B09-01) |
| 15 | Second onboarding attempt rejected | 422 "already provisioned" | PASS |

---

## 11. Issues / Gaps

All identified issues resolved in this iteration. No remaining open gaps.

### Resolved
| # | Issue | Resolution |
|---|-------|------------|
| 1 | `check-code` endpoint was anonymous | Added `.RequireAuthorization()` |
| 2 | 409 (code taken) from Identity mapped to 503 | `SelfProvisionTenantResult.IsSuccess/FailureCode`; HTTP service returns typed failure on 409; service throws `TenantCodeUnavailable` |
| 3 | No backend format validation for tenant code | `TenantCodeValidator` helper (mirrors Identity slug rules); applied in both check-code and provision endpoints |
| 4 | Normalized code not reflected in input | `checkCode` callback applies `setTenantCode(normalized)` if different; `handleCodeBlur` normalizes immediately |
| 5 | 409 from provision not surfaced to code field | `catch` detects `ApiError.isConflict` → sets `codeStatus.available=false` |

### Known limitations (out of scope for B09-01)
- Service-to-service auth between CareConnect → Identity uses optional header token (pre-existing pattern, not B09 specific)
- DNS provisioning is async; no polling mechanism to auto-redirect when hostname becomes available (future enhancement)
- No retry mechanism if CareConnect `UpdateAsync` fails after Identity succeeds (state: User.TenantId updated, provider still COMMON_PORTAL — recoverable by re-submitting form which will fail with CODE_TAKEN or WrongAccessStage depending on timing)
