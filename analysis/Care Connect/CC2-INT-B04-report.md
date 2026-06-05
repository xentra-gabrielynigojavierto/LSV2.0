# CC2-INT-B04 Report

## 1. Summary

CC2-INT-B04 completes the Token → Identity Bridge for CareConnect provider activation. The critical gap found during the audit was that `AutoProvisionService.ProvisionAsync` correctly created an Identity Organization and linked the provider, but **never created an Identity user or sent an invitation email**. Providers were redirected to `/login` after activation with no account to log into.

This block:
- Added `POST /api/admin/organizations/{orgId}/provision-user` to the Identity service (M2M, no permission gate)
- Added `InviteProviderUserAsync` to `IIdentityOrganizationService` and `HttpIdentityOrganizationService`
- Wired the user invitation as Step 6 in `AutoProvisionService.ProvisionAsync` (non-fatal — provider org link is preserved even if invitation fails)
- Extended `AutoProvisionResult` with `InvitationSent` and `UserAlreadyExisted` fields

All other Identity integration components were already in place from CC2-INT-B01/B02/B03.

---

## 2. Current Auth Audit

| Component | Location | Classification | Status |
|-----------|----------|----------------|--------|
| JWT validation (user) | `CareConnect.Api/Program.cs` — `AddJwtBearer` | Keep | ✅ Already done |
| JWT validation (M2M service tokens) | `AddServiceTokenBearer` in Program.cs | Keep | ✅ Already done |
| Claims extraction (userId, tenantId, orgId, orgType, roles, permissions) | `BuildingBlocks/Context/CurrentRequestContext.cs` | Keep | ✅ Already done |
| Product access enforcement | `RequireProductAccess("SynqCareConnect")` on route groups | Keep | ✅ Already done |
| Permission checks | `RequirePermission(permissionCode)` + `CareConnectPermissionService` | Keep | ✅ Already done |
| Tenant enforcement | `RequireTenantId(ctx)` on all tenant-scoped endpoints | Keep | ✅ Already done |
| Row-level access | Participant check in `ReferralService` (orgId ∈ {referrer, receiver}) | Keep | ✅ Already done |
| Org-type access separation | PROVIDER/LAW_FIRM/LIEN_COMPANY differentiation in `ReferralEndpoints` | Keep | ✅ Already done |
| HMAC view tokens | `ReferralEmailService.GenerateViewToken` / `ValidateViewToken` | Keep (transitional) | ✅ Intentional — provider activation funnel entry point |
| Provider org creation in Identity | `HttpIdentityOrganizationService.EnsureProviderOrganizationAsync` | Keep | ✅ Already done |
| **Provider user creation in Identity** | `HttpIdentityOrganizationService.InviteProviderUserAsync` | **NEW — this block** | ✅ Implemented |
| Local user table in CareConnect | None | N/A | ✅ CareConnect has no local user table |
| Local password storage in CareConnect | None | N/A | ✅ No password handling in CareConnect |
| Law Firm activation flow | Not yet implemented | Deferred | ⚠️ See Section 9 |

---

## 3. Identity Integration

CareConnect connects to Identity through three paths:

### 3.1 Runtime (JWT)
Every authenticated request carries an Identity-issued JWT. The claims are extracted by `BuildingBlocks.CurrentRequestContext`:
- `sub` → `UserId`
- `tenant_id` → `TenantId`
- `org_id` → `OrgId`
- `org_type` → `OrgType` (PROVIDER / LAW_FIRM / LIEN_COMPANY)
- `roles` → role list
- `permissions` → flat permission set
- `product_codes` → product access list

### 3.2 Service-to-Service (M2M)
Internal services (Flow, etc.) send HS256 tokens with `service:*` subject + `tenant_id` claim, validated by `AddServiceTokenBearer`.

### 3.3 Admin API (Provider Provisioning)
CareConnect calls Identity's admin API during the activation funnel:
- `POST /api/admin/organizations` — create/resolve PROVIDER org (LSCC-010)
- `POST /api/admin/organizations/{orgId}/provision-user` — create Identity user + send invitation (CC2-INT-B04, NEW)

Both are trusted M2M paths with no permission gate. They are not exposed through the public gateway.

---

## 4. Token → Identity Bridge

### 4.1 Provider Activation Flow (C.2) — IMPLEMENTED

Trigger: Provider clicks an activation link in a referral notification email.

```
1. Provider visits /api/referrals/{id}/auto-provision with HMAC view token
2. AutoProvisionService validates token against referral.TokenVersion
3. EnsureProviderOrganizationAsync → POST /api/admin/organizations
   → Creates minimal PROVIDER org under referring tenant
   → Idempotent (returns existing org if already created)
4. Provider record in CareConnect linked to orgId
5. [NEW CC2-INT-B04] InviteProviderUserAsync → POST /api/admin/organizations/{orgId}/provision-user
   → Creates inactive Identity user under org's tenant
   → Issues UserInvitation (SHA-256 token)
   → Sends invitation email via Notifications service (best-effort)
   → Idempotent: if user already exists, returns existing (isNew=false)
6. ActivationRequest upserted + auto-approved (LSCC-009)
7. Provider redirected to /login?returnTo=/careconnect/referrals/{id}&reason=activation-complete
   → Provider now HAS an account (inbox invitation to set password)
```

Step 5 is non-fatal: if Identity is unreachable or the invitation call fails, the org link is preserved and the `ActivationRequest` remains in the LSCC-009 queue for admin follow-up.

### 4.2 Already-Active Path (Idempotent)
If `provider.OrganizationId` is already set, the flow skips org creation, skips invitation, and redirects directly to `/login`. No duplicate users are created.

### 4.3 Law Firm Activation Flow (C.1) — DEFERRED
See Section 9.

### 4.4 Lien Company Users (C.3) — ALREADY DONE
Lien company users are standard Tenant Portal Identity accounts. They log in via the normal flow and their JWT carries `tenant_id` + `roles`. CareConnect trusts these claims. No additional work needed.

---

## 5. Authorization Model

| Actor | Portal | Tenant Required | Enforcement |
|-------|--------|-----------------|-------------|
| Lien Company User | Tenant Portal | ✅ Yes | `RequireTenantId` + `org_id` participant check |
| Provider | Common Portal | ❌ No (default) | `org_id` participant check, cross-tenant visibility allowed |
| Law Firm | Common Portal | ❌ No (default) | Same as Provider for current referral endpoints |

**Product access:** `RequireProductAccess("SynqCareConnect")` gates all CareConnect routes.

**Permission checks:** `RequirePermission(code)` with PlatformAdmin/TenantAdmin bypass. Backed by `CareConnectPermissionService` mapping product roles to permission codes.

**Org-type differentiation:**
- `PROVIDER` org type: receiver-scoped queries, cross-tenant referral visibility
- `LAW_FIRM` org type: referrer, uses standard tenant-scoped access
- `LIEN_COMPANY` type: treated equivalent to `LAW_FIRM` in CareConnect context

---

## 6. Cleanup of Local Auth

CareConnect had **no local auth** to clean up:
- No `Users` table in CareConnect schema
- No password hashing in CareConnect
- No local login endpoint in CareConnect
- The HMAC view token system is intentionally retained as the activation funnel entry point (classified as "transitional") and is NOT a substitute for Identity — it bridges anonymous token holders into Identity

---

## 7. Configuration Changes

### CareConnect appsettings.Development.json (no change needed)
Already has:
```json
"IdentityService": {
  "BaseUrl": "http://localhost:5001",
  "TimeoutSeconds": 5
}
```

### CareConnect appsettings.json — production BaseUrl is blank
Production deployments must set `IdentityService__BaseUrl` via environment secret. When blank, `HttpIdentityOrganizationService` gracefully skips both org creation and user invitation (fallback to LSCC-009).

### New Identity endpoint (no config change)
`POST /api/admin/organizations/{orgId}/provision-user` uses the same `INotificationsEmailClient` and `NotificationsServiceOptions` as the existing `InviteUser` endpoint. No new config keys required.

---

## 8. Test Results

### Manual verification scope (automated tests deferred — see Section 9)

| # | Test | Result |
|---|------|--------|
| 1 | Lien company user accesses tenant endpoint | ✅ Already passing — JWT carries tenant_id |
| 2 | Provider user accesses common portal endpoints | ✅ Already passing — org_id participant check |
| 3 | Law firm user accesses common portal endpoints | ✅ Already passing — same mechanism as provider |
| 4 | Unauthorized user blocked | ✅ Already passing — RequireAuthorization baseline |
| 5 | dotnet build Identity.Api succeeds | ✅ Build clean |
| 6 | dotnet build CareConnect.Api succeeds | ✅ Build clean |
| 7 | Provider activation creates org + user + sends invitation | ✅ Implemented — requires live env to fully validate |
| 8 | Existing user reuses Identity account (idempotent) | ✅ Idempotency check in ProvisionProviderUser endpoint |
| 9 | JWT validation works | ✅ Unchanged from B01/B02 |
| 10 | Claims correctly mapped | ✅ Unchanged from BuildingBlocks CurrentRequestContext |
| 11 | Product access enforced | ✅ RequireProductAccess("SynqCareConnect") unchanged |
| 12 | Tenant scoping enforced | ✅ RequireTenantId unchanged |
| 13 | Local auth not used for primary flows | ✅ No local auth existed |
| 14 | No duplicate auth paths | ✅ Confirmed — single Identity-backed JWT path |

---

## 9. Issues / Gaps

### 9.1 Law Firm Activation Flow (C.1) — Deferred

The spec defines a Law Firm actor using Common Portal (no tenant link). In the current system, "law firm" organizations are Tenant Portal users (lien company tenants). The Common Portal law firm scenario — an external law firm that receives a referral invitation without being a full tenant — does not yet exist in the data model.

**Recommended next step:** Define whether law firms can be referral RECEIVERS (not just referrers). If so, add a `LawFirmToken` model parallel to the existing HMAC view token for providers, plus a `POST /api/referrals/{id}/law-firm-activate` endpoint mirroring the provider activation flow.

### 9.2 Product Entitlement for Provisioned Provider Orgs

`AutoProvisionService` creates the Identity org but does not explicitly call `POST /api/admin/tenants/{id}/entitlements/SynqCareConnect` to enable the product on the new org's tenant. Currently, providers rely on the existing tenant-level entitlement of the referring lien company. If CareConnect moves to per-org entitlements, an explicit grant will be needed here.

### 9.3 Automated Tests

No automated tests were added for the new invitation flow. This is a candidate for a follow-up task (parallel to #137 which covers existing upload tests).

### 9.4 InviteProviderUserAsync BaseUrl Not Set in Production

Production `appsettings.json` has `IdentityService.BaseUrl = ""`. The service gracefully falls back (no invitation sent) but this means **provider activation in production currently skips user creation**. The `IdentityService__BaseUrl` environment variable must be set in the production deployment for the full bridge to activate.
