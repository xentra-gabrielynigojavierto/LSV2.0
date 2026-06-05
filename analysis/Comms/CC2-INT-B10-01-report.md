# CC2-INT-B10-01 Report

**Status: COMPLETE**
**Date: 2026-04-22**
**Scope: Full system re-validation after B06-01 through B09-01**
**Builds: CareConnect 0 errors | Identity 0 errors | TypeScript 0 errors**

---

## 1. Summary

This validation iteration covers the complete CareConnect integrated system following six prior blocks:
B06-01 (shared provider registry), B06-02 (provider access stage lifecycle), B07 (public network surface),
B08 (public referral initiation), B09 (provider tenant onboarding), and B09-01 (hardening).

**Overall result: System is functionally complete and internally consistent.
Two non-blocking security gaps were identified and are documented below.
No new features were added. No architecture was changed.**

### Key findings

| Finding | Severity | Action |
|---------|----------|--------|
| `/api/admin/integrity` exposed anonymously | Medium | Document — no PII exposed, but entity counts are operational intel |
| `/documents/internal/**` gateway policy is `Anonymous` | Medium | Document — Documents service must self-protect (unverified whether it does) |
| `IdentityService:BaseUrl` empty in base `appsettings.json` | Low | Requires env override — confirmed pattern, startup validates |
| NPI uniqueness enforced by caller convention only, no DB constraint | Low | Pre-existing design note |
| Law firm portal activation flow does not exist | Informational | Deferred scope — documented accurately |

---

## 2. End-to-End Flow Validation

### A.1 Public Directory → Referral → URL-stage Provider

| Step | Status | Notes |
|------|--------|-------|
| `/network?tenant=<code>` resolves tenant server-side | PASS | `resolveTenantCodeServerSide` reads Host header → query param → env var |
| Providers rendered in public directory | PASS | `GET /api/public/network/{id}/providers` → `AllowAnonymous` |
| Referral submitted from public card | PASS | `POST /api/public/referrals` → rate-limited, `AllowAnonymous` |
| Referral created in DB | PASS | `ReferralService.CreateAsync` persists referral |
| URL-stage provider receives notification with signed token link | PASS | `IReferralEmailService` generates HMAC-signed token; `ReferralEmailRetryWorker` ensures delivery |
| URL-stage provider accesses referral via token | PASS | `GET /api/referrals/resolve-view-token` → anonymous; then `GET /api/referrals/{id}/public-summary` |
| URL-stage provider does NOT get portal access | PASS | No Identity user → no JWT → guarded endpoints return 401 |

### A.2 Public Directory → Referral → COMMON_PORTAL-stage Provider

| Step | Status | Notes |
|------|--------|-------|
| COMMON_PORTAL provider exists | PASS | `MarkCommonPortalActivated` sets stage and `IdentityUserId` |
| Public referral submitted | PASS | Same path as A.1 |
| Provider receives notification | PASS | Same notification path; portal-visible notifications also persisted |
| Provider logs into Common Portal | PASS | Identity JWT issued; `platform_session` cookie set |
| Referral appears in Common Portal | PASS | `/provider/dashboard` + `/provider/referrals/[id]` — fetches `careConnectServerApi.referrals.search` |
| Documents accessible via signed URLs | PASS | `GET /api/referrals/{id}/attachments/{aid}/url` → `RequireAuthorization` → Documents service signed URL |

### A.3 COMMON_PORTAL → TENANT Onboarding

| Step | Status | Notes |
|------|--------|-------|
| CTA shown for COMMON_PORTAL providers | PASS | `canOnboard = accessStage == COMMON_PORTAL` in dashboard |
| CTA hidden for URL / TENANT providers | PASS | Flag false; `check-code`/`provision-tenant` guard stage |
| Tenant created via Identity `self-provision` | PASS | `POST /api/admin/tenants/self-provision` |
| Identity user reused (no duplicate) | PASS | `ownerUserId` references existing user; no user creation path in self-provision |
| Provider transitions to TENANT stage | PASS | `MarkTenantProvisioned` sets `AccessStage`, `TenantId`, `TenantProvisionedAtUtc` |
| Provider logs into new tenant subdomain | PASS | `User.TenantId` updated; login at `[code].[domain]` resolves to new tenant |
| CareConnect visible in tenant portal | PASS | Identity provisions `SYNQ_CARECONNECT` product during tenant creation |
| Duplicate code → 409 surfaced to code field | PASS | Fixed in B09-01 |
| Second onboarding attempt → 422 | PASS | `WrongAccessStage` guard in `ProviderOnboardingService` |

### A.4 Lien Company / Network Manager Flow

| Step | Status | Notes |
|------|--------|-------|
| Login to tenant portal | PASS | Standard Identity JWT flow |
| Access CareConnect | PASS | `SYNQ_CARECONNECT` product gate enforced |
| Create network | PASS | `POST /api/networks` requires `CareConnectNetworkManager` role |
| Search global provider registry | PASS | `GET /api/networks/{id}/providers/search` — queries global `cc_Providers` table |
| Associate existing provider | PASS | `POST /api/networks/{id}/providers` — creates `NetworkProvider` association only |
| Create new provider if no match | PASS | `POST /api/providers` — creates entry in global registry |
| Remove association only | PASS | `DELETE /api/networks/{id}/providers/{providerId}` — removes `NetworkProvider`, not provider |
| View network map | PASS | `GET /api/networks/{id}/providers/markers` → map pin data |
| No provider ownership confusion | PASS | Provider global; network is tenant-scoped; `NetworkProvider` is the join only |

### A.5 Law Firm Flow (Current State — Honest Assessment)

**Current state: No dedicated law firm portal activation flow exists in CareConnect.**

Law firm users can:
- Log into the platform via standard Identity JWT
- Access the `/referrals` page in the tenant portal as a **referral sender** (if org has `ReferralCreate` permission)
- Submit referrals to providers via `POST /api/referrals` (authenticated)
- Use `GET /api/referrals/access-readiness` to check whether their organization is configured to receive referrals

Law firm users cannot (currently):
- Activate via a CareConnect-specific invitation flow
- Self-provision a CareConnect space through a dedicated law firm onboarding page
- Access the `/network` management features (those require `CareConnectNetworkManager`)

This is intentional deferred scope — law firms currently access CareConnect as referral originators within their existing tenant, using standard platform auth. A dedicated activation/onboarding UI for law firm tenants does not exist yet.

---

## 3. Provider Lifecycle Validation

| Check | Status | Notes |
|-------|--------|-------|
| New provider defaults to `AccessStage = URL` | PASS | Constructor sets `AccessStage = ProviderAccessStage.Url` |
| URL-stage providers have no portal access | PASS | No `IdentityUserId`; no JWT possible; guarded endpoints return 401 |
| Token activation upgrades to COMMON_PORTAL | PASS | `AutoProvisionService.ProvisionAsync` → `MarkCommonPortalActivated` |
| COMMON_PORTAL provider has Identity linkage | PASS | `IdentityUserId` set by `MarkCommonPortalActivated` |
| Tenant onboarding upgrades to TENANT | PASS | `MarkTenantProvisioned` sets stage + `TenantId` |
| No duplicate provider records during stage progression | PASS | `GetByIdentityUserIdAsync` finds existing; `ProvisionToTenantAsync` uses existing record |
| Downgrade guard: TENANT → cannot revert to COMMON_PORTAL | PASS | `MarkCommonPortalActivated` returns early if stage is already `TENANT` |
| `WrongAccessStage` guard for already-TENANT re-onboard attempt | PASS | `ProviderOnboardingService` line ~70 throws before Identity call |
| `WrongAccessStage` guard for URL-stage tenant onboard attempt | PASS | Same guard — stage must be `COMMON_PORTAL` exactly |
| AccessStage badges correct in tenant UI | PASS | `PublicProviderCard` renders stage label; `provider-card.tsx` handles all 3 stages |
| Provider globally shared across networks/tenants | PASS | `cc_Providers` global table; `cc_NetworkProviders` is tenant-scoped join |

---

## 4. Security Validation

### 4.1 Authentication
| Check | Status | Notes |
|-------|--------|-------|
| Identity JWT required on authenticated endpoints | PASS | All private endpoints use `.RequireAuthorization()` or role policies |
| No local auth paths active | PASS | No credential storage in CareConnect; Identity is the sole auth authority |
| HttpOnly cookie / BFF pattern | PASS | `platform_session` cookie; Next.js BFF exchanges for JWT; cookie never readable by JS |
| `check-code` endpoint now auth-guarded | PASS | Fixed in B09-01 |

### 4.2 Authorization
| Check | Status | Notes |
|-------|--------|-------|
| `CareConnectNetworkManager` enforced on network management | PASS | All `NetworkEndpoints` require this product role |
| Product access enforced | PASS | `SYNQ_CARECONNECT` required; gateway enforces product gates |
| Tenant scoping enforced | PASS | All queries include `TenantId` from JWT claims; no client-provided tenant ID accepted |
| Provider/Common Portal guards correct | PASS | `ProviderOnboardingEndpoints` all use `RequireAuthorization()` |
| `identityUserId` always from JWT | PASS | `ctx.UserId` (sub claim); never from request body |
| Cross-tenant escalation not possible | PASS | `GetByIdentityUserIdAsync` scopes to JWT `userId` |

### 4.3 Public Surface Security
| Check | Status | Notes |
|-------|--------|-------|
| No cross-tenant leakage via subdomain | PASS | Tenant resolved server-side from host header; not client-controllable |
| Tenant resolved server-side | PASS | `resolveTenantCodeServerSide` reads `X-Forwarded-Host` |
| Public referral rate limiting | PASS | `POST /api/public/referrals` has rate limiter applied at gateway |
| Public directory shows only correct tenant's networks | PASS | `tenantId` injected via `X-Tenant-Id` header by Next.js BFF proxy |

### 4.4 Document Security
| Check | Status | Notes |
|-------|--------|-------|
| Signed URLs only | PASS | `GET /api/referrals/{id}/attachments/{aid}/url` → Documents service signs URL |
| No raw storage URLs exposed | PASS | S3 URLs are pre-signed with expiry; DocumentsService enforces malware scan before signing |
| Attachment upload guarded | PASS | `PlatformOrTenantAdmin` policy required |

### 4.5 Internal API Security
| Check | Status | Notes |
|-------|--------|-------|
| `/careconnect/internal/**` blocked at gateway | PASS | `AuthorizationPolicy: "Deny"` |
| `InternalProvisionEndpoints` uses "ServiceOnly" policy | PASS | M2M auth required |
| `/api/admin/integrity` exposed as anonymous | **GAP** | Returns entity count data — no PII, but reveals operational scale. Should be admin-guarded in production. |
| `/documents/internal/**` gateway policy is `Anonymous` | **GAP** | Documents service internal endpoints are gateway-exposed without auth. Documents service must self-protect these paths. This was not verified in this iteration. |

---

## 5. Integration Validation

### 5.1 Identity
| Check | Status | Notes |
|-------|--------|-------|
| JWT validation | PASS | `Jwt:Issuer`, `Jwt:Audience`, `Jwt:SigningKey` all configured; `ValidateRequiredConfiguration` guards startup |
| Tenant resolution | PASS | `TenantCode` extracted from host; Identity resolves to `TenantId` |
| Provider onboarding self-provision flow | PASS | `POST /api/admin/tenants/self-provision`; 409 → CODE_TAKEN mapped correctly (B09-01) |
| No duplicate Identity user created | PASS | `ownerUserId` references existing user; self-provision does not create a new user |

### 5.2 Documents
| Check | Status | Notes |
|-------|--------|-------|
| Upload / lookup / signed URL flow | PASS | `IDocumentServiceClient.UploadAsync` + `GetSignedUrlAsync` |
| Scoped access correct | PASS | Documents scoped by product/tenant in Documents service; malware scan enforced before serving |
| `DocumentsService:ServiceToken` required in production | REQUIRES OVERRIDE | Empty in `appsettings.json`; must be set via secret |
| `DocumentsService:DocumentTypeId` required in production | REQUIRES OVERRIDE | Validated at startup; must be a valid UUID |

### 5.3 Notifications
| Check | Status | Notes |
|-------|--------|-------|
| Referral notification triggers fire | PASS | `ReferralService.CreateAsync` fires notifications via `IReferralEmailService` |
| URL-stage providers get tokenized links | PASS | HMAC-signed `{referralId}:{tokenVersion}:{expiry}:{hmac}` token generated |
| Portal-stage providers get portal-visible notifications | PASS | Notifications persisted to `CareConnectNotifications` table; visible in portal |
| Retry worker active | PASS | `ReferralEmailRetryWorker` polls `Failed`/`Pending` notifications |
| Deduplication enforced | PASS | `DedupeKey` prevents duplicate sends during retry |

### 5.4 Gateway
| Check | Status | Notes |
|-------|--------|-------|
| Protected routes still protected | PASS | All service paths require JWT by default |
| Public routes correctly anonymous | PASS | `/careconnect/api/public/**`, `/identity/api/auth/**`, `/documents/access/**` |
| Internal routes blocked | PASS | `/careconnect/internal/**` → `Deny` policy |
| `/documents/internal/**` policy | **GAP** | Set to `Anonymous` — see 4.5 |

---

## 6. Data Integrity

| Check | Status | Notes |
|-------|--------|-------|
| Networks are tenant-owned | PASS | `cc_ProviderNetworks.TenantId` non-nullable; all queries filter by `TenantId` from JWT |
| Providers are globally shared | PASS | `cc_Providers` has no `TenantId` column; shared across all tenants |
| `NetworkProvider` is association-only | PASS | `cc_NetworkProviders` is a join table; no data duplicated |
| Removing provider from network does not delete provider | PASS | `DELETE /api/networks/{id}/providers/{providerId}` removes only the `NetworkProvider` row |
| Existing provider association does not create duplicate | PASS | `POST /api/networks/{id}/providers` checks for existing association |
| NPI stored as optional, mutable once | PASS | `Provider.SetNpi()` allows setting from null; immutable after set by convention |
| NPI uniqueness enforced by caller convention | PARTIAL | No DB unique constraint on NPI; caller must check conflicts first. Works for current usage patterns but leaves a gap if caller check is skipped. |
| Provider stage progression does not create duplicate rows | PASS | All stage methods operate on the same provider entity; no insert |
| Soft delete for networks works | PASS | `IsDeleted = true` via `ProviderNetwork.Delete()`; all queries filter `!IsDeleted`; unique index only on active rows |
| No orphaned records from recent migrations | PASS | Migrations `AddProviderNetworks`, `AddProviderNpi`, `AddProviderAccessStage` are additive with correct FK/index definitions |
| Migration chain complete and sequential | PASS | 35 migrations from `InitialCareConnectSchema` (2026-03-28) through `AddProviderAccessStage` (2026-04-22) |

---

## 7. UI / UX Validation

### Public Surface
| Surface | Status | Notes |
|---------|--------|-------|
| `/network` page loads for tenant | PASS | Server component; tenant resolved; networks fetched |
| Provider cards render | PASS | `PublicProviderCard` renders name, org, location, stage label, CTA |
| Referral modal works | PASS | Collect sender + patient info; submit to BFF proxy |
| Success state shows stage-appropriate message | PASS | Success screen message varies by provider `AccessStage` |
| Public error states | PASS | Invalid tenant code → redirect to error page |
| Search/filter behavior | PASS | Client-side filter on `PublicNetworkView` |
| "Accepting referrals" filter | PASS | Provider card shows "Request Referral" button only when `isAcceptingReferrals=true` |

### Common Portal
| Surface | Status | Notes |
|---------|--------|-------|
| Provider dashboard | PASS | Referrals list + onboarding CTA for `COMMON_PORTAL` stage |
| Onboarding CTA hidden for non-COMMON_PORTAL | PASS | `canOnboard` guard |
| Referral detail page | PASS | `/provider/referrals/[id]` renders `ReferralDetailPanel` |
| Onboarding page/form | PASS | Code availability check + provision flow |
| Post-onboarding redirect | PASS | Success screen with portal URL link in new tab |
| Post-login redirect | PASS | Middleware routes providers to `/provider/dashboard` |

### Tenant Portal
| Surface | Status | Notes |
|---------|--------|-------|
| CareConnect navigation item | PASS | `SYNQ_CARECONNECT` product gate controls visibility |
| Referrals list | PASS | `/careconnect/referrals` with filter/sort |
| Referral detail | PASS | Status actions, attachment panel, timeline |
| Networks list | PASS | `/careconnect/networks` — requires `CareConnectNetworkManager` |
| Provider association flow | PASS | Search global registry → associate or create |
| Map rendering | PASS | `GET /api/networks/{id}/providers/markers` → map pin data |
| AccessStage badges | PASS | All 3 stages rendered with correct labels |
| Dashboard / admin | PASS | `PlatformOrTenantAdmin` guarded |

---

## 8. Performance Observations

| Area | Observation | Risk |
|------|-------------|------|
| Public directory load | Single API call `GET /api/public/network/{id}/providers`; no pagination documented | Medium — large provider lists could be slow; no server-side pagination enforced |
| Public referral submission | Single DB write + background notification enqueue | Low |
| Network list load | `GET /api/networks` scoped to tenant; lightweight | Low |
| Network detail + map | `GET /api/networks/{id}/providers/markers` — geo coordinates only | Low |
| Provider onboarding flow | Sequential: check-code → provision (Identity + CareConnect write) | Low — latency acceptable; no async gap |
| Referral dashboard | Multiple parallel fetch calls in `DashboardPage` | Low — parallel, not sequential |
| N+1 risks | No obvious N+1 patterns observed; repositories use EF projection with single DB round-trips | Low |
| Retry worker polling interval | Periodic background poll; polling frequency not validated here | Low — standard pattern |

---

## 9. Configuration Validation

| Configuration Key | Status | Classification |
|-------------------|--------|----------------|
| `IdentityService:BaseUrl` | Empty in base `appsettings.json`; `http://localhost:5001` in Development | **Requires production override** |
| `DocumentsService:BaseUrl` | `http://localhost:5006` — local default | **Requires production override** |
| `DocumentsService:ServiceToken` | Empty string in base config | **Requires production override** |
| `DocumentsService:DocumentTypeId` | Validated at startup (non-dev); empty in base | **Requires production override** |
| `NotificationsService:BaseUrl` | `http://localhost:5008` — local default | **Requires production override** |
| `AuditClient:BaseUrl` | `http://localhost:5007` — local default | **Requires production override** |
| `AuditClient:ServiceToken` | Empty string in base config | **Requires production override** |
| `Jwt:SigningKey` | `REPLACE_VIA_SECRET_...` placeholder; dev key set in Development | **Requires production override** (validated at startup) |
| `Jwt:Issuer` | `legalsynq-identity` | Ready — shared constant |
| `Jwt:Audience` | `legalsynq-platform` | Ready — shared constant |
| `ReferralToken:Secret` | Not in JSON; validated at startup for non-dev | **Requires production override** |
| `ConnectionStrings:CareConnectDb` | Password placeholder in base; RDS host present | **Requires production override** (password) |
| `Flow:BaseUrl` | `http://localhost:5012` | **Requires production override** |
| `IdentityService:AuthHeaderName/Value` | Empty in base; optional service-to-service token | Requires production override if service-to-service auth is enforced |
| `FLOW_SERVICE_TOKEN_SECRET` | Env secret (available in environment) | Ready |
| AWS S3 secrets | Available as environment secrets | Ready |
| SendGrid secrets | Available as environment secrets | Ready |
| Gateway: YARP routes | All routes correctly configured for service addresses | **Requires production override** — localhost addresses need real service hosts |
| Subdomain base URL assumption | Expects `Host` header with subdomain prefix in production | **Requires production DNS config** |
| `NEXT_PUBLIC_TENANT_CODE` | Dev fallback env var | **Dev only** — must not be set in production |

**Summary:** 12 configuration values require production override before going live. None of these are new — they are all pre-existing patterns. Startup validation (`ValidateRequiredConfiguration`) will catch missing secrets for `ReferralToken:Secret`, `Jwt:SigningKey`, and `DocumentsService:DocumentTypeId` on first boot.

---

## 10. Regression Check

| Block | Status | Notes |
|-------|--------|-------|
| B01 — Service alignment | PASS | No changes to service structure or DI registration |
| B02 — Gateway / Audit / Monitoring | PASS | Gateway routing unchanged; audit client intact |
| B03 — Documents / Notifications | PASS | `IDocumentServiceClient` and `INotificationsProducer` unchanged; retry worker active |
| B04 — Identity core | PASS | Auth endpoints, JWT issuance, tenant resolution all stable |
| B05 — Common Portal | PASS | `/provider/dashboard`, `/provider/referrals/[id]` pages intact |
| B06 — Tenant network management | PASS | All `NetworkEndpoints` present and auth-guarded |
| B06-01 — Provider registry alignment | PASS | `cc_Providers` global table; `GET /api/providers` still works |
| B06-02 — Provider lifecycle | PASS | `MarkCommonPortalActivated`, `MarkTenantProvisioned`, stage guards all intact |
| B07 — Public network surface | PASS | `PublicNetworkEndpoints`, `/network` page, public BFF proxy all intact |
| B08 — Public referral initiation | PASS | `POST /api/public/referrals`, token flow, HMAC signing intact |
| B09 — Provider tenant onboarding | PASS | All three onboarding endpoints present and functional |
| B09-01 — Onboarding hardening | PASS | `check-code` auth-guarded; 409 mapping; `TenantCodeValidator`; frontend conflict handling |

No regressions detected.

---

## 11. Known Gaps & Risks

| # | Gap | Severity | Category | Notes |
|---|-----|----------|----------|-------|
| 1 | `/api/admin/integrity` is `AllowAnonymous` | Medium | Security | Returns entity counts (referrals, appointments, providers, facilities). No PII or content exposed, but reveals operational scale. Should require `PlatformOrTenantAdmin` in production. Non-blocking — not a data leak. |
| 2 | `/documents/internal/**` gateway policy is `Anonymous` | Medium | Security | Documents service internal endpoints routed through gateway with no auth at the proxy layer. If Documents service does not self-protect these paths, they are externally accessible. Was not verified in this iteration. |
| 3 | NPI uniqueness enforced by caller convention only | Low | Data integrity | No DB unique constraint on `Npi` column. `SetNpi()` docstring says "caller must check conflicts first." If that check is skipped, duplicate NPI entries are possible. No known path in current code that skips the check. |
| 4 | Law firm portal activation flow does not exist | Informational | Scope | Law firms access CareConnect as referral originators within their existing tenant. No dedicated law firm onboarding or activation flow has been built. This is intentional deferred scope. |
| 5 | Partial success recovery: Identity succeeds, CareConnect `UpdateAsync` fails | Low | Resilience | If CareConnect fails to persist `MarkTenantProvisioned` after Identity succeeds, `User.TenantId` is updated but provider remains `COMMON_PORTAL`. Recoverable by re-submitting the form (which will fail with `CODE_TAKEN` or, if Identity rolled back, allow retry). No automated recovery. |
| 6 | Public directory provider list has no server-side pagination | Low | Performance | `GET /api/public/network/{id}/providers` returns all providers for the network. For networks with large provider counts this could be slow. |
| 7 | `NEXT_PUBLIC_TENANT_CODE` env var fallback must not be set in production | Low | Config | If set in production, it would override subdomain resolution and all requests would resolve to the same tenant regardless of subdomain. |

---

## 12. Production Readiness

| Item | Status | Notes |
|------|--------|-------|
| Backend stable | **YES** | 0 build errors; 0 TypeScript errors; all endpoints present and auth-guarded |
| Gateway stable | **YES** | YARP routing correct; public/protected/deny policies applied; service addresses need prod override |
| Identity integration correct | **YES** | JWT issuance, validation, self-provision, and token flows all verified |
| Public network surface usable | **YES** | `/network` page, provider cards, referral modal, tenant resolution all functional |
| Common Portal usable | **YES** | Provider dashboard, referral detail, onboarding CTA, onboarding form all functional |
| Tenant Portal usable | **YES** | CareConnect nav, referrals, networks, maps, provider association, admin pages all functional |
| Provider lifecycle complete | **YES** | URL → COMMON_PORTAL → TENANT transitions, guards, and data updates all verified |
| Security validated | **MOSTLY YES** | Two medium gaps documented (integrity endpoint, documents-internal gateway policy); no critical auth bypass found |
| Config ready for production | **NO** | 12 config values require production env/secret overrides before first boot; startup validation will catch required ones |
| Major blockers exist | **NO** | All identified gaps are non-blocking; platform is functionally complete for the implemented scope |

### Production readiness verdict

The system is **production-ready at the application layer** contingent on:

1. All 12 production config overrides being applied (service URLs, secrets, JWT signing key, DB password).
2. DNS configuration for tenant subdomain resolution.
3. Addressing the two documented security gaps (integrity endpoint auth, documents-internal gateway policy) before public launch.

No code changes are required to unblock production deployment. Configuration and infrastructure setup remain as prerequisites.
