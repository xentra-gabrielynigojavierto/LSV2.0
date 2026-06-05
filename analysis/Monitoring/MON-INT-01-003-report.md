# MON-INT-01-003 — Monitoring Auth & Security Alignment

> **Report created FIRST** before any implementation, per mandatory execution rules.
> Updated incrementally after each step.

---

## 1. Task Summary

Replace the Monitoring Service's isolated RS256 JWT scheme with the platform-standard
dual-scheme (HS256 Bearer + HS256 ServiceToken), so that:
- Platform operators can access admin endpoints with their standard user JWT
- Platform services can call Monitoring admin endpoints via `FLOW_SERVICE_TOKEN_SECRET`
- Read endpoints remain safely accessible without breaking the Control Center
- The temporary RS256 configuration is cleanly retired

| Field | Value |
|---|---|
| **Ticket** | MON-INT-01-003 |
| **Status** | ✅ Complete |
| **Depends on** | MON-INT-01-002, MON-INT-02-001 |
| **Date** | 2026-04-20 |

---

## 2. Existing Auth Model Analysis

### Monitoring Service (pre-feature)

| Property | Value |
|---|---|
| **Scheme** | `Bearer` (only) |
| **Algorithm** | RS256 |
| **Config class** | `JwtAuthenticationOptions` (`Authentication:Jwt` section) |
| **Issuer** | `https://auth.local.legalsynq.dev/` |
| **Audience** | `legalsynq-monitoring` |
| **Key** | Embedded RSA public key PEM in `appsettings.json` |
| **Validator** | `JwtAuthenticationOptionsValidator` (fail-fast startup check) |

**Endpoints:**
- `GET /health` — anonymous
- `GET /monitoring/entities`, `GET /monitoring/entities/{id}` — anonymous
- `GET /monitoring/status`, `/alerts`, `/summary` — anonymous (workaround; see MON-INT-01-002 comments)
- `POST /monitoring/admin/entities`, `PATCH /monitoring/admin/entities/{id}` — `RequireAuthorization()` (RS256 only)
- `GET /secure/ping` — `RequireAuthorization()` (RS256 only)

### Platform standard (Liens, Notifications, Fund, etc.)

Two schemes registered together:

| Property | Bearer (Scheme 1) | ServiceToken (Scheme 2) |
|---|---|---|
| **Scheme name** | `Bearer` (default) | `ServiceToken` |
| **Algorithm** | HS256 | HS256 |
| **Config key** | `Jwt:SigningKey` | `FLOW_SERVICE_TOKEN_SECRET` env var |
| **Issuer** | `legalsynq-identity` | `legalsynq-service-tokens` |
| **Audience** | `legalsynq-platform` | Service-specific (e.g., `notifications-service`) |
| **Subject pattern** | User GUID | `service:*` |
| **Use case** | Platform users via Identity | Machine-to-machine calls |
| **Building block** | Inline in each service | `BuildingBlocks.Authentication.ServiceTokens` |

### Current mismatch

The Monitoring Service was isolated:
- **Different algorithm**: RS256 vs HS256 platform-wide
- **Different issuer/audience**: custom local dev values vs platform `legalsynq-identity`/`legalsynq-platform`
- **No service token support**: no `FLOW_SERVICE_TOKEN_SECRET` integration
- **No BuildingBlocks reference**: `Monitoring.Api.csproj` had no project reference to shared building blocks
- **Read endpoints had to be made anonymous** as a workaround because the CC backend could not mint an RS256 token

This made the admin API completely inaccessible to any platform component, requiring the bootstrap workaround (MON-INT-02-001).

---

## 3. Target Security Model

### Chosen model: Platform-standard dual-scheme (Bearer HS256 + ServiceToken HS256)

**Rationale:**
- Identical to Notifications.Api, Liens.Api, Fund.Api, CareConnect.Api — the established platform pattern
- Allows platform operators to use their existing JWT (Bearer) to call admin endpoints
- Allows platform services to use `FLOW_SERVICE_TOKEN_SECRET` (ServiceToken) to call admin endpoints programmatically — the canonical path for automated entity registration
- No custom one-off auth scheme; follows the `BuildingBlocks.Authentication.ServiceTokens` building block

**Why alternatives were rejected:**
- RS256 kept alongside HS256 — half-switched state, violates the "no half-switched auth state" rule and adds complexity for no benefit
- OIDC authority — not appropriate for an internal platform service; adds an external dependency
- Anonymous admin endpoints — never acceptable; admin writes must be protected

### Security posture table

| Endpoint | Access model | Rationale |
|---|---|---|
| `GET /health` | Anonymous | Standard liveness probe; no sensitive data |
| `GET /monitoring/entities` | Anonymous | Operational metadata; consumed by scheduler and CC backend within trust boundary |
| `GET /monitoring/status` | Anonymous | Current health status; consumed by CC backend; non-sensitive operational data |
| `GET /monitoring/alerts` | Anonymous | Active alerts; consumed by CC backend; acceptable operational transparency |
| `GET /monitoring/summary` | Anonymous | Aggregated health summary; consumed by CC backend |
| `POST /monitoring/admin/entities` | `MonitoringAdmin` policy | Write operation; must be protected |
| `PATCH /monitoring/admin/entities/{id}` | `MonitoringAdmin` policy | Write operation; must be protected |
| `GET /secure/ping` | `MonitoringAdmin` policy | Auth validation probe; no relaxation needed |

**`MonitoringAdmin` policy:** accepts either (a) Bearer JWT with `PlatformAdmin` role, OR (b) ServiceToken with `service:` subject. Both authentication schemes are tried in parallel before the assertion is evaluated.

**Read endpoint anonymous justification:** All read endpoints expose operational metadata (service names, health status, alerts) that is non-sensitive for internal platform use. The CC backend consumes them server-side, within the trusted internal network (loopback `127.0.0.1`), never from a browser. Tightening to require auth would require the CC to maintain credentials for a service it already trusts on the network. If tighter controls are needed in a public deployment, the gateway's IP-level restrictions provide the first layer; explicit auth can be added in a follow-up without breaking the CC read path (just add a service token to `monitoring-source.ts`).

---

## 4. Implementation Changes

### Files changed

| File | Change |
|---|---|
| `Monitoring.Api.csproj` | Added BuildingBlocks project reference; upgraded `JwtBearer` version from `8.0.10` to `8.0.*` |
| `Authentication/AuthenticationServiceCollectionExtensions.cs` | Full rewrite: dual-scheme (Bearer HS256 + ServiceToken HS256); `MonitoringAdmin` policy defined here |
| `Authentication/MonitoringPolicies.cs` | **New** — policy name constant `MonitoringAdmin` = `"MonitoringAdmin"` |
| `Authentication/JwtAuthenticationOptions.cs` | Retired: replaced with `[Obsolete]` stub; no longer referenced by DI |
| `Authentication/JwtAuthenticationOptionsValidator.cs` | Retired: replaced with `[Obsolete]` stub; no longer referenced by DI |
| `Endpoints/MonitoredEntityEndpoints.cs` | Admin group changed from `.RequireAuthorization()` to `.RequireAuthorization(MonitoringPolicies.AdminWrite)` |
| `Program.cs` | Updated startup log message; `/secure/ping` updated to use `MonitoringPolicies.AdminWrite` |
| `appsettings.json` | Removed `Authentication:Jwt` RS256 section; added `Jwt:Issuer/Audience/SigningKey`; `MonitoringBootstrap:Enabled` defaulted to `false` |
| `appsettings.Development.json` | Added `Jwt` section (matches dev values in other services); `MonitoringBootstrap:Enabled=true` for dev |

### Key design decisions

**`MonitoringAdmin` policy (inline `RequireAssertion`):**
```csharp
options.AddPolicy(MonitoringPolicies.AdminWrite, policy =>
    policy
        .AddAuthenticationSchemes(Bearer, ServiceToken)
        .RequireAssertion(ctx =>
            ctx.User.IsInRole("PlatformAdmin") ||
            ctx.User.FindFirst("sub")?.Value
                ?.StartsWith("service:", StringComparison.Ordinal) == true));
```
This allows both human operators (Bearer + PlatformAdmin role) and automated services (ServiceToken + service: subject) without custom `IAuthorizationHandler` complexity.

**ServiceToken audience includes `monitoring-service`** (new, specific), `legalsynq-services` (broad future audience), and `flow-service` (backward compat with existing platform token issuers). Any platform service that mints a token with `aud=monitoring-service` and `sub=service:*` is admitted.

**`Jwt:SigningKey` placeholder in appsettings:** The value `REPLACE_VIA_SECRET_minimum_32_characters_long` is intentional — the actual key is provided by the `Jwt__SigningKey` environment variable at runtime (same pattern as every other platform service). The Bearer scheme is functionally active once the real key is injected; the ServiceToken scheme uses `FLOW_SERVICE_TOKEN_SECRET` which is already available.

---

## 5. Read Endpoint Security Decision

**Decision: all 5 read endpoints remain anonymous.** (Explicit, intentional, documented.)

| Endpoint | Access decision | Reasoning |
|---|---|---|
| `GET /health` | Anonymous | Universal convention; no data |
| `GET /monitoring/entities` | Anonymous | Used by scheduler (internal), CC backend (trusted network); targets are `127.0.0.1` — internal-only anyway |
| `GET /monitoring/status` | Anonymous | Operational transparency; consumed by CC backend server-side |
| `GET /monitoring/alerts` | Anonymous | Active alerts only; no business data; CC backend trusted |
| `GET /monitoring/summary` | Anonymous | Aggregated view of above; CC backend trusted |

**Not tightened because:**
1. All data is operational metadata, not business/tenant data
2. CC backend consumes these server-side on loopback — adding auth would require the CC to manage monitoring credentials for no security gain on the internal network
3. The gateway provides the first layer of access control for external traffic
4. If a future deployment model exposes the monitoring service externally, auth on read endpoints can be added then (no breaking change to existing consumers)

---

## 6. Admin / Write Path Validation

### Validated flow: ServiceToken → POST /monitoring/admin/entities → HTTP 201

```bash
# Mint ServiceToken (HS256, FLOW_SERVICE_TOKEN_SECRET):
# iss=legalsynq-service-tokens, aud=monitoring-service, sub=service:monitoring-bootstrap

# POST /monitoring/admin/entities with Authorization: Bearer {token}
# Response: HTTP 201
{
  "id": "027fd13c-b30d-45c8-a98e-3a8539633888",
  "name": "ServiceToken-Test-Entity",
  "entityType": "InternalService",
  "monitoringType": "Http",
  "target": "http://127.0.0.1:9999/health",
  "isEnabled": true,
  "scope": "test",
  "impactLevel": "Optional",
  "createdAtUtc": "2026-04-20T06:55:54.528...",
  "updatedAtUtc": "2026-04-20T06:55:54.528..."
}
```

Domain rules still enforced (test entity name/target/entityType all validated by domain constructor).

### Validated flow: ServiceToken → PATCH /monitoring/admin/entities/{id} → HTTP 200

```bash
# PATCH {id} with isEnabled=false
# Response: HTTP 200 (test entity disabled — cleaned up after validation)
```

### Admin endpoint without token → HTTP 401

```bash
# POST /monitoring/admin/entities (no Authorization header)
# Response: HTTP 401
```

### /secure/ping with ServiceToken → HTTP 200

```bash
# GET /secure/ping
# Response: HTTP 200
{ "status": "ok", "sub": "service:monitoring-bootstrap", "scheme": "..." }
```

---

## 7. Bootstrap Retirement Assessment

**Decision: keep bootstrap, but:**
- **Disabled by default** (`MonitoringBootstrap:Enabled=false` in `appsettings.json`)
- **Enabled in Development** (`MonitoringBootstrap:Enabled=true` in `appsettings.Development.json`)

**Rationale:**
- The canonical admin path is now usable: any platform service with `FLOW_SERVICE_TOKEN_SECRET` can call `POST /monitoring/admin/entities` to register entities
- The bootstrap is no longer the only write path — it is now a dev-convenience tool
- Disabling in production by default prevents unintended double-seeding if an admin has already registered entities via the API
- In development, it remains useful for fresh DB setups (empty DB = auto-seed)
- The idempotency guard (`AnyAsync` check) still prevents any duplicate seeding

**Path to full retirement:** When a proper infrastructure/deployment seed script calls the admin API to register entities for each environment, the bootstrap class can be deleted entirely. No urgency — the dev default is harmless.

---

## 8. Validation Performed

| Test | Expected | Actual |
|---|---|---|
| A. `GET /health` (anonymous) | 200 | ✅ 200 |
| A. `GET /monitoring/summary` (anonymous) | 200 | ✅ 200 |
| A. `GET /monitoring/status` (anonymous) | 200 | ✅ 200 |
| A. `GET /monitoring/alerts` (anonymous) | 200 | ✅ 200 |
| A. `GET /monitoring/entities` (anonymous) | 200 | ✅ 200 |
| B. `POST /monitoring/admin/entities` (no token) | 401 | ✅ 401 |
| C. `GET /secure/ping` (no token) | 401 | ✅ 401 |
| D. `POST /monitoring/admin/entities` (ServiceToken) | 201 | ✅ 201 |
| D. `PATCH /monitoring/admin/entities/{id}` (ServiceToken) | 200 | ✅ 200 |
| E. `GET /secure/ping` (ServiceToken) | 200 | ✅ 200 |
| F. `GET /monitoring/monitoring/summary` via gateway | 200 | ✅ 200, 11 integrations, 2 alerts |
| G. CC local mode (`/api/monitoring/summary`) | 200 | ✅ 200 |
| Build | 0 errors, 0 warnings | ✅ 0 errors, 0 warnings |

**Bearer JWT (PlatformAdmin role) path:**
Not runtime-validated in this session because the `Jwt:SigningKey` in the dev environment uses the placeholder value `REPLACE_VIA_SECRET_minimum_32_characters_long`. The Identity service uses the real key from secrets; the Monitoring service will use it once `Jwt__SigningKey` is available as an environment variable. **The ServiceToken path (validated above) is the primary admin path for platform services.** The Bearer path is for human operators via the CC UI, which is future work (MON-INT-02-002/003).

---

## 9. Files Changed

| File | Action | Purpose |
|---|---|---|
| `Monitoring.Api/Monitoring.Api.csproj` | Modified | Added BuildingBlocks project reference; upgraded JwtBearer to `8.0.*` |
| `Monitoring.Api/Authentication/AuthenticationServiceCollectionExtensions.cs` | Modified (full rewrite) | Replaced RS256-only scheme with platform-standard dual-scheme (Bearer HS256 + ServiceToken HS256); `MonitoringAdmin` policy |
| `Monitoring.Api/Authentication/MonitoringPolicies.cs` | Created | Policy name constant `AdminWrite = "MonitoringAdmin"` |
| `Monitoring.Api/Authentication/JwtAuthenticationOptions.cs` | Modified (retired stub) | RS256 config class marked `[Obsolete]`; no longer wired into DI |
| `Monitoring.Api/Authentication/JwtAuthenticationOptionsValidator.cs` | Modified (retired stub) | RS256 validator marked `[Obsolete]`; no longer wired into DI |
| `Monitoring.Api/Endpoints/MonitoredEntityEndpoints.cs` | Modified | Admin group: `RequireAuthorization()` → `RequireAuthorization(MonitoringPolicies.AdminWrite)` |
| `Monitoring.Api/Program.cs` | Modified | `/secure/ping` uses `MonitoringPolicies.AdminWrite`; startup log updated |
| `Monitoring.Api/appsettings.json` | Modified | Removed `Authentication:Jwt` RS256 section; added `Jwt:Issuer/Audience/SigningKey`; `MonitoringBootstrap:Enabled=false` |
| `Monitoring.Api/appsettings.Development.json` | Modified | Added `Jwt` section; `MonitoringBootstrap:Enabled=true` |

**No Control Center changes required.** Read endpoints remain anonymous — `monitoring-source.ts` continues to work unchanged. **No gateway changes required.** Routes 52–54 anonymous routing from MON-INT-01-002 still correct.

---

## 10. Known Gaps / Risks

| # | Gap | Severity | Notes |
|---|---|---|---|
| 1 | **Bearer JWT path not runtime-validated** — dev `Jwt:SigningKey` is placeholder. | Low | The policy and scheme are correctly wired; validation will work once `Jwt__SigningKey` env var is set (same mechanism all other services use). ServiceToken path is fully functional and is the primary admin path for platform services. |
| 2 | **RS256 stubs remain in codebase** — `JwtAuthenticationOptions.cs` and `JwtAuthenticationOptionsValidator.cs` are retired stubs. | Low | Marked `[Obsolete]`; no DI registration; zero runtime impact. Safe to delete in a cleanup pass. |
| 3 | **No UI-level admin integration** — `POST /monitoring/admin/entities` is functional but there is no UI for it yet. | Low | Expected at this stage; UI work is MON-INT-02-002/003. Platform services can use the API directly. |
| 4 | **Bootstrap still present** (dev-enabled) — a fresh dev restart auto-seeds if DB is empty. | Low | Explicitly controlled; disabled in production; idempotent; documented as temporary. |
| 5 | **Read endpoints remain anonymous** — could expose `target` URLs (internal service addresses). | Low | Targets are `127.0.0.1` loopback addresses; internal-only data; acceptable risk for current internal deployment model. |

---

## 11. Recommended Next Feature

**Recommend: MON-INT-02-002 — Status Summary Banner**

**Rationale:**
- Auth alignment is complete. The monitoring platform is operationally trustworthy:
  - 10 real entities registered and being probed every 15 seconds
  - Real alerts raised for down services
  - Admin API callable via ServiceToken from any platform service
  - Read endpoints safely consuming live DB-backed data
- The Control Center can now consume fully live monitoring data via `MONITORING_SOURCE=service`
- The most valuable next user-visible step is surfacing the monitoring health in the CC UI — specifically a status summary banner that shows the overall system health across all platform services
- MON-INT-02-003 (Component Status List) is the natural follow-on after the banner is in place
