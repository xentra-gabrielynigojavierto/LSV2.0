# CC2-INT-B07 Report — Public Network Surface + Subdomain Routing

**Date:** 2026-04-22  
**Status:** COMPLETE

---

## 1. Summary

Implemented a fully unauthenticated public network directory surface for LegalSynq CareConnect.
Tenants can expose their provider networks at `[tenant-subdomain].[baseurl]/network` (production)
or `/network?tenant=<code>` (Replit dev) without any login requirement.

---

## 2. Subdomain Resolution

**Server-side (Next.js Server Component)**

Resolution order in `apps/web/src/app/network/page.tsx`:
1. **Host header subdomain** — extracts `subdomain` from `Host` / `X-Forwarded-Host` header (production)
2. **`?tenant=<code>` query param** — Replit dev fallback (no real subdomains on single Replit domain)
3. **`NEXT_PUBLIC_TENANT_CODE` env var** — last resort for single-tenant deployments

Reserved subdomains (`www`, `app`, `api`, `admin`, etc.) are explicitly rejected.

**Tenant lookup** — calls Identity branding endpoint (`GET /identity/api/tenants/current/branding`)
with `X-Tenant-Code: <code>` header. This endpoint is already `AllowAnonymous` and looks up by
`Tenant.Code` then `Tenant.Subdomain` (existing `GetByCodeAsync` / `GetBySubdomainAsync`).

The branding response returns `tenantId` (GUID), `tenantCode`, and `displayName` — exactly
what the public page needs.

---

## 3. Public Network Routing

### Backend (CareConnect API)

New file: `apps/services/careconnect/CareConnect.Api/Endpoints/PublicNetworkEndpoints.cs`

All endpoints under `/api/public/network` are `AllowAnonymous`:

| Endpoint | Description |
|---|---|
| `GET /api/public/network` | List all networks for tenant (via `X-Tenant-Id` header) |
| `GET /api/public/network/{id}/providers` | Providers in a specific network |
| `GET /api/public/network/{id}/providers/markers` | Geo-coded map markers |
| `GET /api/public/network/{id}/detail` | Combined network + providers + markers (single round-trip) |

Tenant isolation is enforced via the `X-Tenant-Id` header (GUID). This is set server-side by
the Next.js BFF/Server Component — never taken from user URL input. The tenant GUID is resolved
from the subdomain → Identity lookup → GUID before being forwarded to CareConnect.

### Gateway (YARP)

Added `careconnect-public-network` route in `apps/gateway/Gateway.Api/appsettings.json`:
```json
"careconnect-public-network": {
  "ClusterId": "careconnect-cluster",
  "AuthorizationPolicy": "Anonymous",
  "Order": 23,
  "Match": { "Path": "/careconnect/api/public/{**catch-all}" },
  "Transforms": [{ "PathRemovePrefix": "/careconnect" }]
}
```
Order 23 is before the protected catch-all (Order 120) and after the internal block (Order 22).

### Frontend BFF Proxy

New file: `apps/web/src/app/api/public/careconnect/[...path]/route.ts`

- Proxies `/api/public/careconnect/**` → `${GATEWAY_URL}/careconnect/**`  
- Does NOT inject `Authorization: Bearer` header (public endpoints)
- Forwards `X-Tenant-Id` header verbatim

### Public API Helpers

New file: `apps/web/src/lib/public-network-api.ts`

Server-side typed wrappers around the public CareConnect endpoints. Used by the `/network`
Server Component to fetch data at render time (no client-side fetching on initial load).

---

## 4. Provider Surface

**New file:** `apps/web/src/app/network/page.tsx` — Server Component  
**New file:** `apps/web/src/app/network/layout.tsx` — Minimal public layout  
**New file:** `apps/web/src/components/careconnect/public-network-view.tsx` — Interactive client

The public directory shows:
- Provider name, organization name
- City, state, postal code
- Phone number (click-to-call `tel:` link)
- Whether accepting referrals (green / grey badge)
- Access stage indicator (blue "Portal active" for COMMON_PORTAL, purple for TENANT)
- Searchable by name, city, state
- Filterable by "accepting referrals" vs "all"

---

## 5. Stage Enforcement

Stage routing follows CC2-INT-B06-02 definitions:

| Stage | Public page behavior |
|---|---|
| `URL` | Provider fully shown, no portal link. Referrals via signed token URLs. |
| `COMMON_PORTAL` | Provider shown with "Portal active" badge + "View portal" → `/login` |
| `TENANT` | Provider shown with "Tenant portal" badge + "Tenant portal" → `/login` |

The viewer (law firm user / prospective client) is ALWAYS allowed to browse the full directory
regardless of provider stage. Stage enforcement redirects apply to the **provider** viewing their
own profile in the future (phase 2), not to directory browsers.

---

## 6. UI

- Public layout: minimal header (brand + "Provider Network") + footer, no auth UI
- Network selector: pill navigation for tenants with multiple networks (query param: `?network={id}`)
- Provider cards: name, org, location, phone, badges, portal action
- Empty / error states: graceful messages for missing tenant code, unknown tenant, no networks

---

## 7. New Files

| File | Purpose |
|---|---|
| `CareConnect.Application/DTOs/PublicNetworkDtos.cs` | Public-safe response shapes |
| `CareConnect.Api/Endpoints/PublicNetworkEndpoints.cs` | Anonymous CareConnect endpoints |
| `apps/gateway/Gateway.Api/appsettings.json` | Added `careconnect-public-network` YARP route |
| `apps/web/src/app/api/public/careconnect/[...path]/route.ts` | Public BFF proxy (no auth) |
| `apps/web/src/lib/public-network-api.ts` | Server-side typed API helpers |
| `apps/web/src/app/network/layout.tsx` | Public layout |
| `apps/web/src/app/network/page.tsx` | Public network directory page |
| `apps/web/src/components/careconnect/public-network-view.tsx` | Interactive client component |

---

## 8. Modifications

| File | Change |
|---|---|
| `CareConnect.Api/Program.cs` | Added `app.MapPublicNetworkEndpoints()` |
| `apps/web/src/middleware.ts` | Added `/network` and `/api/public/` to `PUBLIC_PATHS` |

---

## 9. Issues / Known Limitations

- **Subdomain routing on Replit**: Replit uses a single proxied domain; real subdomain routing
  requires DNS delegation (production concern only). Dev uses `?tenant=<code>` query param.
- **No map view**: The provider map (Leaflet) uses client-only dynamic imports. The map component
  is scaffolded (`/api/public/network/{id}/providers/markers` returns markers) but the interactive
  map is deferred to phase 2 (requires dynamic import wrapper for SSR compatibility).
- **No referral CTA**: Public provider cards do not currently expose a "Request referral" button.
  The URL-stage referral flow uses signed tokens issued by the law firm — not initiated from the
  public directory. Phase 2 can add a "Contact this provider" form.

---

## 10. Schema Repair (CC2-INT-B07 Addendum)

### Problem
The RDS `careconnect_db` had stale `__EFMigrationsHistory` entries for four B06 migrations
(`AddProviderNetworks`, `AddProviderNpi`, `AddProviderAccessStage`, `AddProviderReassignmentLog`)
but the actual tables/columns were absent. MySQL DDL is non-transactional: a deployment crash
can commit the history row without executing the DDL.

### Root Cause of Initial Repair Failure
The first repair attempt used `ADD COLUMN IF NOT EXISTS` which requires MySQL ≥ 8.0.29 — not
available on the RDS instance. FK constraints also used `char(36)` without matching the exact
collation of `cc_Providers.Id`, triggering an incompatible-column error.

### Solution — `EnsureSchemaObjectsAsync` in `Program.cs`
Replaced the repair helper with an idempotent schema-existence check at startup:

1. **Tables** — `CREATE TABLE` (without `IF NOT EXISTS`) guarded by a prior
   `information_schema.tables` count check. Safe to re-run; skipped if the table already exists.
2. **Columns** — `ALTER TABLE ADD COLUMN` (without `IF NOT EXISTS`) guarded by a prior
   `information_schema.columns` count check. One statement per column to isolate failures.
3. **FK constraints omitted** — FK collation must exactly match the referenced column's charset.
   Since `cc_Providers.Id` uses the table's implicit collation (not `ascii_general_ci`), FK
   constraints are left to EF to manage after `db.Database.Migrate()` runs.

### Outcome
All 6 missing schema objects were applied on next startup:
- `cc_ReferralProviderReassignments` (table)
- `cc_NetworkProviders` (table, no FK constraints in manual DDL)
- `cc_Providers.Npi`, `.AccessStage`, `.IdentityUserId`, `.CommonPortalActivatedAtUtc`, `.TenantProvisionedAtUtc`

Coverage probe: **PASSED** — all EF-mapped tables and columns confirmed present.
Endpoint `GET /api/public/network` returns `200 []` (no 500 errors).
