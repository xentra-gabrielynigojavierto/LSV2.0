# Frontend Architecture Design — LegalSynq Platform

**Framework:** Next.js 14+ (App Router)  
**Auth pattern:** JWT stored in HttpOnly cookie via Identity service login  
**API entry point:** Gateway only (port 5000 / public HTTPS)  
**Tenant model:** subdomain-resolved, not path-based  
**Date:** 2026-03-28

---

## Platform Constants (from BuildingBlocks)

```typescript
// Mirrored from BuildingBlocks.Authorization — keep in sync
export const OrgType = {
  LawFirm:   'LAW_FIRM',
  Provider:  'PROVIDER',
  Funder:    'FUNDER',
  LienOwner: 'LIEN_OWNER',
  Internal:  'INTERNAL',
} as const;

export const SystemRole = {
  PlatformAdmin: 'PlatformAdmin',
  TenantAdmin:   'TenantAdmin',
  StandardUser:  'StandardUser',
} as const;

export const ProductRole = {
  // CareConnect
  CareConnectReferrer: 'CARECONNECT_REFERRER',
  CareConnectReceiver: 'CARECONNECT_RECEIVER',
  // SynqFund
  SynqFundReferrer:        'SYNQFUND_REFERRER',
  SynqFundFunder:          'SYNQFUND_FUNDER',
  SynqFundApplicantPortal: 'SYNQFUND_APPLICANT_PORTAL',
  // SynqLien
  SynqLienSeller: 'SYNQLIEN_SELLER',
  SynqLienBuyer:  'SYNQLIEN_BUYER',
  SynqLienHolder: 'SYNQLIEN_HOLDER',
} as const;
```

---

## 1. Frontend Responsibility Model

### Frontend Is Responsible For

| Responsibility | Detail |
|---|---|
| **Session management** | Store JWT in HttpOnly cookie (set by login response). Expose decoded session to components via React Context. Never store the raw token in `localStorage`. |
| **Tenant context** | Read tenant identity (`tenantId`, `tenantCode`) from the decoded JWT. Apply tenant-specific branding (logo, colors, name) fetched from a public tenant-config endpoint on boot. |
| **Org context** | Read `orgId`, `orgType`, `productRoles` from session. Drive navigation, route visibility, and UI labeling from these values. |
| **Navigation logic** | Build the sidebar and nav groups from `orgType` + `productRoles`. Hide routes the caller has no product role for — as a UX courtesy only, not a security boundary. |
| **Optimistic feedback** | Show loading states, inline validation errors, and success toasts. Never hide backend errors or silently fail. |
| **CorrelationId logging** | Read `X-Correlation-Id` from API response headers. Surface it in error banners ("Request ID: …") to aid support. |

### Backend / Gateway Remains Responsible For

| Responsibility | Why the frontend cannot own it |
|---|---|
| **Token validation** | JWT signature and expiry — frontend has no signing key |
| **Capability enforcement** | `product_roles → capabilities` map lives on the backend; frontend never evaluates capabilities |
| **Tenant cross-check** | `JWT.tenant_id === resolved TenantId from Host` — gateway middleware |
| **Row-level access** | Org-participant checks on records (`SellingOrganizationId = orgId`, etc.) — service layer |
| **Audit logs** | Status history writes — service layer |

### Capability Trust Boundary

> The frontend **must never** gate API calls based on self-evaluated capabilities. It may hide UI elements as a convenience but must always send the request and handle a 403 response gracefully.

A backend 403 means: render an "Access denied" page with the correlation ID — not a silent no-op or redirect to login.

---

## 2. Session and Context Model

### Session Shape (TypeScript)

```typescript
// Decoded from JWT + login response envelope
export interface PlatformSession {
  // Identity
  userId:    string;          // JWT: sub
  email:     string;          // JWT: email

  // Tenant (from JWT; cross-checked by gateway against subdomain)
  tenantId:  string;          // JWT: tenant_id
  tenantCode: string;         // JWT: tenant_code

  // Organization (absent if user has no org membership)
  orgId?:    string;          // JWT: org_id
  orgType?:  OrgTypeValue;    // JWT: org_type

  // Access control (display-only; backend enforces)
  productRoles: ProductRoleValue[];  // JWT: product_roles[]
  systemRoles:  SystemRoleValue[];   // JWT: ClaimTypes.Role[]

  // Convenience flags (derived client-side)
  isPlatformAdmin: boolean;          // systemRoles.includes('PlatformAdmin')
  isTenantAdmin:   boolean;          // systemRoles.includes('TenantAdmin')
  hasOrg:          boolean;          // orgId !== undefined

  // Token lifecycle
  expiresAt: Date;           // from login response expiresAtUtc
}
```

### Tenant Branding Context (separate from session)

```typescript
export interface TenantBranding {
  tenantId:    string;
  tenantCode:  string;
  displayName: string;
  logoUrl?:    string;
  primaryColor?: string;    // hex, for CSS variable injection
  faviconUrl?: string;
}
```

### Where Each Field Originates

| Field | Source |
|---|---|
| `userId`, `email`, `tenantId`, `tenantCode`, `orgId`, `orgType` | JWT payload (decoded client-side from cookie or login response) |
| `productRoles`, `systemRoles` | JWT payload (`product_roles[]` and `ClaimTypes.Role[]` claims) |
| `isPlatformAdmin`, `isTenantAdmin`, `hasOrg` | Derived from above during session decode |
| `expiresAt` | `LoginResponse.expiresAtUtc` from Identity service |
| `TenantBranding.*` | Public `GET /identity/api/tenants/current/branding` endpoint (no auth required; keyed to subdomain) |

### React Context Providers

```
<TenantBrandingProvider>        ← loaded on boot, before auth
  <SessionProvider>             ← loaded after successful login
    <AppShell>
      <OrgContextProvider>      ← derived from session.orgId/orgType
        {children}
      </OrgContextProvider>
    </AppShell>
  </SessionProvider>
</TenantBrandingProvider>
```

---

## 3. Tenant Resolution and App Boot Flow

### Subdomain Resolution (Production)

```
User visits: https://lawfirm-alpha.legalsynq.com
                      │
                      ▼
Next.js app boots at that subdomain.
No path prefix for tenant — tenant is always implicit from Host.
                      │
                      ▼
App boot sequence:
  1. Read subdomain from window.location.hostname
     → extract: "lawfirm-alpha"
     → no tenant-specific API call needed client-side;
       all requests carry the subdomain via Host header

  2. Fetch tenant branding (anonymous, hostname-keyed by gateway)
     GET /identity/api/tenants/current/branding
     → inject CSS variables (--primary-color, --logo-url)
     → set document title = TenantBranding.displayName

  3. Check for existing session cookie (HttpOnly, sent automatically)
     → If cookie present: attempt GET /identity/api/auth/me (validate token)
       - 200 OK → decode session, restore authenticated state
       - 401 → clear session, redirect to /login

  4. If no cookie or 401: render /login page
     → TenantBranding already loaded, so login page shows correct branding
```

### Login Flow

```
User submits: { email, password }
(tenantCode is NOT sent by user — it is resolved server-side from the subdomain)

→ POST /identity/api/auth/login  { tenantCode: <from subdomain>, email, password }
  (gateway resolves subdomain → tenantCode before forwarding, OR
   the Next.js server-side action reads window.location.hostname and derives tenantCode)

← 200 { token, expiresAtUtc, user: { id, email, orgId, orgType, productRoles, ... } }

→ Store JWT in HttpOnly cookie (set by Next.js API route acting as BFF)
→ Decode JWT payload into PlatformSession
→ Redirect to /dashboard (org-aware landing page)
```

### Login Page: Tenant Code Input (Dev / Direct Access)

On `localhost:3000` there is no subdomain. The login page shows a `tenantCode` input field in development mode (`NEXT_PUBLIC_ENV = development`). In production this field is hidden and the value is derived from the hostname.

### App Boot Sequence (After Login)

```
1. Session restored from cookie → PlatformSession decoded
2. Evaluate orgType → select navigation set (see §7)
3. Evaluate productRoles → filter nav groups
4. Redirect if landing at "/" → route to first available nav item
5. TenantBranding CSS variables already injected (from step 2 of initial boot)
```

---

## 4. App Shell Architecture

### Component Hierarchy

```
<AppShell>
  ├── <TopBar>
  │     ├── TenantLogo (from TenantBranding.logoUrl)
  │     ├── OrgBadge (orgType label + org display name)
  │     ├── ProductSwitcher (tabs: active products for this org)
  │     └── UserMenu (avatar, name, sign out, tenant-admin link if TenantAdmin)
  │
  ├── <Sidebar>
  │     ├── NavGroup per product (shown only if productRole matches)
  │     │     └── NavItem per route
  │     └── AdminNavGroup (shown only if TenantAdmin or PlatformAdmin)
  │
  └── <MainContent>
        └── {children}
```

### TopBar

```typescript
// TopBar reads session context
const { orgType, orgId, tenantCode, isPlatformAdmin, isTenantAdmin } = useSession();
const branding = useTenantBranding();

// TenantLogo — always shown; falls back to tenant name text if no logoUrl
// OrgBadge — shows orgType human label:
//   LAW_FIRM → "Law Firm"
//   PROVIDER → "Provider"
//   FUNDER   → "Funder"
//   LIEN_OWNER → "Lien Owner"
// ProductSwitcher — tabs driven by active productRoles (see §7)
// UserMenu — email, Sign out, "Admin Panel" link (if TenantAdmin)
```

### Sidebar / Navigation

The sidebar renders `NavGroup` components. Each `NavGroup` maps to a product. Groups and items are derived from the session — not hardcoded per page (see §7 for the full derivation logic).

```typescript
// Sidebar composition
<Sidebar>
  {navGroups.map(group => (
    <NavGroup key={group.id} label={group.label} icon={group.icon}>
      {group.items.map(item => (
        <NavItem key={item.href} href={item.href} label={item.label} />
      ))}
    </NavGroup>
  ))}
</Sidebar>
```

### Organization Badge / Switcher Placeholder

Phase 1: display-only badge (org name + org type label). Phase 2: a full org switcher for users who are members of multiple organizations (see §10 for multi-org edge case).

```typescript
<OrgBadge orgType={session.orgType} orgName={session.orgName} />
// orgName fetched from GET /identity/api/organizations/me after login
```

### Tenant Branding Injection

```typescript
// In layout.tsx — applied once on boot
useEffect(() => {
  if (branding.primaryColor)
    document.documentElement.style.setProperty('--color-primary', branding.primaryColor);
  if (branding.faviconUrl)
    updateFavicon(branding.faviconUrl);
}, [branding]);
```

---

## 5. Actor-Specific Experience Model

### One App, Role-Aware Views — Recommended

The platform uses **one shared Next.js application** with actor-specific navigation, layouts, and page content driven by `orgType` + `productRoles`. Separate apps per actor would duplicate auth, session, shell, and API layer code and create a deployment management burden.

The injured party portal is the **only exception** — it uses a separate auth shape (no `org_id`/`org_type`) and a distinctly simpler UI. It may be deployed as a separate Next.js app on a different subdomain pattern (e.g., `portal.legalsynq.com` or `client.lawfirm-alpha.legalsynq.com`) or as a heavily isolated route group within the main app.

### Actor Experience Summary

| Actor | OrgType | Product Roles | Experience Focus |
|---|---|---|---|
| **Law Firm** | `LAW_FIRM` | `CARECONNECT_REFERRER`, `SYNQFUND_REFERRER` | Create referrals, submit fund applications, track status |
| **Provider** | `PROVIDER` | `CARECONNECT_RECEIVER`, `SYNQLIEN_SELLER` | Manage referrals received, create/list liens |
| **Funder** | `FUNDER` | `SYNQFUND_FUNDER` | Review applications, approve/deny, record disbursements |
| **Lien Owner** | `LIEN_OWNER` | `SYNQLIEN_BUYER`, `SYNQLIEN_HOLDER` | Browse marketplace, manage purchased liens, settle |
| **Injured Party** | _(no org)_ | `SYNQFUND_APPLICANT_PORTAL` | Read-only view of own application + funding status |
| **Tenant Admin** | any | any + `TenantAdmin` system role | User management, org management, product subscriptions |
| **Platform Admin** | `INTERNAL` | any | All tenants, all orgs, all records |

### Layout Selection Logic

```typescript
// In root layout.tsx
function selectLayout(session: PlatformSession): LayoutVariant {
  if (session.isPlatformAdmin) return 'platform-admin';
  if (!session.hasOrg)         return 'no-org';          // redirect to setup page
  switch (session.orgType) {
    case OrgType.LawFirm:   return 'law-firm';
    case OrgType.Provider:  return 'provider';
    case OrgType.Funder:    return 'funder';
    case OrgType.LienOwner: return 'lien-owner';
    default:                return 'generic';
  }
}
```

---

## 6. Route Strategy

### Core Rules

- **Tenant is never in the path.** It is always resolved from the subdomain (Host header).
- **Product prefix in path** — routes are organized by product, not by org type.
- **Injured party portal** lives at a clearly separated route group.

### Route Map

```
/                                   → redirect to /dashboard
/login                              → anonymous; shows tenant-branded login form
/dashboard                          → org-type-aware landing (first available product)

── CareConnect ────────────────────────────────────────────────────────
/careconnect
  /referrals                        → list (filtered by org role: sent | received)
  /referrals/new                    → create referral (CARECONNECT_REFERRER only)
  /referrals/[id]                   → detail view
  /referrals/[id]/notes             → notes panel
  /appointments                     → list (filtered by org role)
  /appointments/[id]                → detail view
  /appointments/[id]/notes          → notes panel
  /providers                        → search / map (CARECONNECT_REFERRER)
  /providers/[id]                   → provider profile + availability

── SynqFund ───────────────────────────────────────────────────────────
/fund
  /applications                     → list (law firm: submitted | funder: received)
  /applications/new                 → create application (SYNQFUND_REFERRER only)
  /applications/[id]                → detail view
  /applications/[id]/documents      → document panel
  /applications/[id]/decision       → approve/deny (SYNQFUND_FUNDER only)
  /applications/[id]/disbursements  → disburse (SYNQFUND_FUNDER only)

── SynqLien ───────────────────────────────────────────────────────────
/lien
  /marketplace                      → public browse (SYNQLIEN_BUYER)
  /marketplace/[id]                 → lien detail (pre-purchase; public within platform)
  /my-liens                         → seller inventory (SYNQLIEN_SELLER)
  /my-liens/new                     → create lien (SYNQLIEN_SELLER only)
  /my-liens/[id]                    → lien detail + manage offers
  /portfolio                        → purchased / held liens (SYNQLIEN_BUYER | HOLDER)
  /portfolio/[id]                   → lien detail + settlement panel

── Injured Party Portal ───────────────────────────────────────────────
/portal
  /login                            → portal-specific login (party_id auth)
  /my-application                   → read-only application status
  /my-application/funding           → disbursement summary

── Admin ──────────────────────────────────────────────────────────────
/admin                              → TenantAdmin or PlatformAdmin only
  /users                            → user management
  /organizations                    → org management
  /products                         → product subscriptions
  /domains                          → tenant domain management
  /tenants                          → PlatformAdmin only: all tenants
```

### Next.js App Router File Structure

```
app/
  layout.tsx                    ← root layout; TenantBrandingProvider + SessionProvider
  page.tsx                      ← redirect to /dashboard
  login/
    page.tsx
  dashboard/
    page.tsx                    ← org-type-aware landing redirect

  (platform)/                   ← route group; requires auth + org
    layout.tsx                  ← AppShell (TopBar + Sidebar)
    careconnect/
      referrals/
        page.tsx                ← list
        new/page.tsx
        [id]/page.tsx
        [id]/notes/page.tsx
      appointments/ ...
      providers/ ...
    fund/
      applications/ ...
    lien/
      marketplace/ ...
      my-liens/ ...
      portfolio/ ...

  (admin)/                      ← route group; requires TenantAdmin or PlatformAdmin
    layout.tsx
    admin/ ...

  portal/                       ← injured party; separate auth shape
    login/page.tsx
    my-application/page.tsx
    my-application/funding/page.tsx
```

---

## 7. Navigation Rules

### Derivation Logic

```typescript
function buildNavGroups(session: PlatformSession): NavGroup[] {
  const groups: NavGroup[] = [];
  const pr = session.productRoles;
  const isAdmin = session.isPlatformAdmin || session.isTenantAdmin;

  // ── CareConnect ─────────────────────────────────────────
  const ccRoles = [ProductRole.CareConnectReferrer, ProductRole.CareConnectReceiver];
  if (pr.some(r => ccRoles.includes(r))) {
    groups.push({
      id: 'careconnect', label: 'CareConnect', icon: 'HeartPulse',
      items: [
        { href: '/careconnect/referrals',   label: 'Referrals' },
        { href: '/careconnect/appointments', label: 'Appointments' },
        pr.includes(ProductRole.CareConnectReferrer)
          ? { href: '/careconnect/providers', label: 'Find Providers' }
          : null,
      ].filter(Boolean),
    });
  }

  // ── SynqFund ────────────────────────────────────────────
  const fundRoles = [ProductRole.SynqFundReferrer, ProductRole.SynqFundFunder];
  if (pr.some(r => fundRoles.includes(r))) {
    groups.push({
      id: 'fund', label: 'SynqFund', icon: 'Banknote',
      items: [
        { href: '/fund/applications', label: 'Applications' },
        pr.includes(ProductRole.SynqFundReferrer)
          ? { href: '/fund/applications/new', label: 'New Application' }
          : null,
      ].filter(Boolean),
    });
  }

  // ── SynqLien ────────────────────────────────────────────
  const lienRoles = [ProductRole.SynqLienSeller, ProductRole.SynqLienBuyer,
                     ProductRole.SynqLienHolder];
  if (pr.some(r => lienRoles.includes(r))) {
    groups.push({
      id: 'lien', label: 'SynqLien', icon: 'FileStack',
      items: [
        pr.includes(ProductRole.SynqLienBuyer)
          ? { href: '/lien/marketplace', label: 'Marketplace' } : null,
        pr.includes(ProductRole.SynqLienSeller)
          ? { href: '/lien/my-liens', label: 'My Liens' } : null,
        (pr.includes(ProductRole.SynqLienBuyer) || pr.includes(ProductRole.SynqLienHolder))
          ? { href: '/lien/portfolio', label: 'Portfolio' } : null,
      ].filter(Boolean),
    });
  }

  // ── Admin ───────────────────────────────────────────────
  if (isAdmin) {
    groups.push({
      id: 'admin', label: 'Administration', icon: 'Settings',
      items: [
        { href: '/admin/users',         label: 'Users' },
        { href: '/admin/organizations', label: 'Organizations' },
        { href: '/admin/products',      label: 'Products' },
        session.isPlatformAdmin
          ? { href: '/admin/tenants', label: 'All Tenants' } : null,
      ].filter(Boolean),
    });
  }

  return groups;
}
```

### Example Nav Sets by Actor

**LAW_FIRM** (roles: `CARECONNECT_REFERRER`, `SYNQFUND_REFERRER`):
```
CareConnect
  • Referrals
  • Appointments
  • Find Providers
SynqFund
  • Applications
  • New Application
```

**PROVIDER** (roles: `CARECONNECT_RECEIVER`, `SYNQLIEN_SELLER`):
```
CareConnect
  • Referrals
  • Appointments
SynqLien
  • My Liens
```

**FUNDER** (roles: `SYNQFUND_FUNDER`):
```
SynqFund
  • Applications
```

**LIEN_OWNER** (roles: `SYNQLIEN_BUYER`, `SYNQLIEN_HOLDER`):
```
SynqLien
  • Marketplace
  • Portfolio
```

**TenantAdmin** (any orgType + `TenantAdmin` system role):
```
[all product nav items based on productRoles]
Administration
  • Users
  • Organizations
  • Products
```

**PlatformAdmin** (system role `PlatformAdmin`):
```
[all nav groups visible]
Administration
  • Users
  • Organizations
  • Products
  • All Tenants
```

---

## 8. Frontend Data Access Layer

### API Client Shape

```typescript
// lib/api-client.ts
// All requests go through the gateway only — never directly to services

const GATEWAY_BASE = process.env.NEXT_PUBLIC_GATEWAY_URL ?? '';
// In production: '' (relative, same origin via gateway)
// In dev: 'http://localhost:5000'

interface ApiClientOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE';
  body?: unknown;
  headers?: Record<string, string>;
}

interface ApiResponse<T> {
  data: T;
  correlationId: string;
  status: number;
}

async function apiRequest<T>(
  path: string,
  options: ApiClientOptions = {}
): Promise<ApiResponse<T>> {
  const res = await fetch(`${GATEWAY_BASE}${path}`, {
    method: options.method ?? 'GET',
    credentials: 'include',   // send HttpOnly session cookie automatically
    headers: {
      'Content-Type': 'application/json',
      ...options.headers,
    },
    body: options.body ? JSON.stringify(options.body) : undefined,
  });

  const correlationId = res.headers.get('X-Correlation-Id') ?? 'unknown';

  if (!res.ok) {
    const error = await res.json().catch(() => ({ message: 'Unknown error' }));
    throw new ApiError(res.status, error.message ?? error.title, correlationId);
  }

  const data: T = await res.json();
  return { data, correlationId, status: res.status };
}

export const apiClient = {
  get:    <T>(path: string) => apiRequest<T>(path),
  post:   <T>(path: string, body: unknown) => apiRequest<T>(path, { method: 'POST', body }),
  put:    <T>(path: string, body: unknown) => apiRequest<T>(path, { method: 'PUT', body }),
  patch:  <T>(path: string, body: unknown) => apiRequest<T>(path, { method: 'PATCH', body }),
  delete: <T>(path: string) => apiRequest<T>(path, { method: 'DELETE' }),
};
```

### ApiError Class

```typescript
export class ApiError extends Error {
  constructor(
    public readonly status: number,
    message: string,
    public readonly correlationId: string
  ) {
    super(message);
    this.name = 'ApiError';
  }

  get isUnauthorized()  { return this.status === 401; }
  get isForbidden()     { return this.status === 403; }
  get isNotFound()      { return this.status === 404; }
  get isConflict()      { return this.status === 409; }
  get isServerError()   { return this.status >= 500; }
}
```

### Error Handling Convention

```typescript
// In components/pages — canonical error handling
try {
  const { data } = await apiClient.post('/careconnect/api/referrals', payload);
  showSuccessToast('Referral created');
  router.push(`/careconnect/referrals/${data.id}`);
} catch (err) {
  if (err instanceof ApiError) {
    if (err.isUnauthorized) { router.push('/login'); return; }
    if (err.isForbidden)    { showForbiddenBanner(err.correlationId); return; }
    showErrorToast(err.message, { requestId: err.correlationId });
  } else {
    showErrorToast('Unexpected error. Please try again.');
  }
}
```

### CorrelationId Surfacing

```typescript
// ForbiddenBanner component
function ForbiddenBanner({ correlationId }: { correlationId: string }) {
  return (
    <Alert variant="destructive">
      <p>You do not have permission to perform this action.</p>
      <p className="text-xs text-muted-foreground">Request ID: {correlationId}</p>
    </Alert>
  );
}
```

### Gateway-Only Rule

- All API calls use `GATEWAY_BASE` — never `localhost:5001/5002/5003` directly.
- In production, `GATEWAY_BASE = ''` — requests are relative, same-origin through the gateway's public HTTPS endpoint.
- The gateway handles CORS; the Next.js frontend does not set `Access-Control-Allow-Origin`.

---

## 9. Auth Guard and Route Protection

### Route Protection Layers

```
Anonymous routes:   /login, /portal/login, /identity/health
Authenticated:      all /(platform) routes — require valid session
Org-required:       all /(platform) routes — require session.hasOrg
Actor-specific:     specific routes require matching productRole
Admin:              all /(admin) routes — require TenantAdmin or PlatformAdmin
```

### Next.js Middleware (global guard)

```typescript
// middleware.ts — runs on every request before rendering
import { NextResponse, type NextRequest } from 'next/server';
import { decodeSessionCookie } from '@/lib/session';

const PUBLIC_PATHS = ['/login', '/portal/login', '/portal'];

export async function middleware(request: NextRequest) {
  const { pathname } = request.nextUrl;

  // Allow anonymous paths
  if (PUBLIC_PATHS.some(p => pathname.startsWith(p))) {
    return NextResponse.next();
  }

  // Validate session cookie
  const session = await decodeSessionCookie(request);
  if (!session) {
    return NextResponse.redirect(new URL('/login', request.url));
  }

  // Org-required for platform routes
  if (pathname.startsWith('/careconnect') ||
      pathname.startsWith('/fund') ||
      pathname.startsWith('/lien')) {
    if (!session.hasOrg) {
      return NextResponse.redirect(new URL('/no-org', request.url));
    }
  }

  // Admin guard
  if (pathname.startsWith('/admin')) {
    if (!session.isPlatformAdmin && !session.isTenantAdmin) {
      return NextResponse.redirect(new URL('/dashboard', request.url));
    }
  }

  return NextResponse.next();
}

export const config = {
  matcher: ['/((?!_next/static|_next/image|favicon.ico).*)'],
};
```

### Page-Level Guards (Server Components)

For actor-specific pages, use a server-side guard that redirects to `/dashboard` if the product role is absent:

```typescript
// app/(platform)/fund/applications/new/page.tsx
import { requireProductRole } from '@/lib/auth-guards';

export default async function NewApplicationPage() {
  await requireProductRole(ProductRole.SynqFundReferrer);
  return <NewApplicationForm />;
}

// lib/auth-guards.ts
export async function requireProductRole(role: ProductRoleValue) {
  const session = await getServerSession();
  if (!session?.productRoles.includes(role)) {
    redirect('/dashboard');
  }
  return session;
}
```

### Stale or Missing Session Context

| Scenario | Behavior |
|---|---|
| Cookie expired | Middleware redirects to `/login` with `?reason=expired` |
| `GET /auth/me` returns 401 | `SessionProvider` clears session, redirects to `/login` |
| Session exists but `orgId` missing | Redirect to `/no-org` — a page prompting the user to contact their administrator |
| Backend returns 401 mid-session | `apiClient` catches it and redirects to `/login` |
| Backend returns 403 | Show `ForbiddenBanner` with correlationId; do not redirect |

---

## 10. Risks and Edge Cases

### 10.1 Wrong Subdomain + Valid Token

**Risk:** A user from `firm-a.legalsynq.com` navigates to `firm-b.legalsynq.com` with their existing cookie. The token is valid but issued for Tenant A.

**Frontend behavior:** The app boots, loads `firm-b`'s branding correctly. The first authenticated API call (e.g., `GET /auth/me`) is forwarded by the gateway, which detects `JWT.tenant_id ≠ firm-b's TenantId` and returns 401. The `SessionProvider` catches the 401 → clears the cookie → redirects to `/login`.

**UX:** The login page shows `firm-b`'s branding. The user must log in to `firm-b` with `firm-b` credentials. No data from `firm-a` is exposed.

### 10.2 User With No Org Membership

**Risk:** A user is created in the Identity system but has not been assigned to any organization. `JWT.org_id` is absent.

**Frontend behavior:** Session decode sets `hasOrg = false`. The global middleware redirects all `/(platform)` route attempts to `/no-org`. The `/no-org` page shows: "Your account is not associated with an organization. Please contact your administrator." No navigation links to product areas are shown.

### 10.3 User With Multiple Org Memberships

**Risk:** A user belongs to two organizations (e.g., a consultant who works for both a law firm and a funder).

**Current Phase 1 behavior:** The Identity service returns only the user's _primary_ org membership in the JWT (`GetPrimaryOrgMembershipAsync`). The frontend shows only one org context. An `OrgBadge` displays the current org with a "Switch" placeholder (disabled in Phase 1).

**Phase 2:** An org-switcher flow calls `POST /identity/api/auth/switch-org { targetOrgId }`, which returns a new JWT for the selected org. The frontend replaces the session cookie and rebuilds the nav.

### 10.4 Backend Denies Capability Despite Frontend Nav Showing the Page

**Risk:** A user navigates to `/fund/applications/[id]/decision` (the approve/deny page for a funder). The frontend renders because `productRoles.includes('SYNQFUND_FUNDER')`, but the backend returns 403 when the user tries to approve.

**Frontend behavior:**
- The page renders normally (no frontend-side capability evaluation).
- The "Approve" button calls `POST /fund/api/applications/{id}/approve`.
- The backend returns `403 Forbidden` with a correlationId.
- The `apiClient` throws `ApiError(403)`.
- The page shows a `ForbiddenBanner`: "You do not have permission to approve this application. Request ID: abc-123."
- No redirect, no crash. The correlationId allows the admin to trace the exact backend decision.

This is the expected behavior — the frontend never hides API errors silently.

### 10.5 Injured Party Portal — Separate Auth / Session Shape

**Risk:** The injured party portal needs a distinct identity flow. The party authenticates with a `party_id`-based credential, has no `org_id`, and sees only their own application. Mixing this into the main `PlatformSession` shape creates confusion.

**Design:**
- The portal lives at `/portal/*` routes.
- It uses a **separate session shape** (`PartySession`) stored in a separate HttpOnly cookie (`portal_session`).
- The `/portal/login` page calls `POST /identity/api/auth/party-login { partyCode, dob }` (future endpoint).
- `PartySession`:
  ```typescript
  interface PartySession {
    partyId:  string;
    email?:   string;
    tenantId: string;
    expiresAt: Date;
    productRoles: ['SYNQFUND_APPLICANT_PORTAL'];
  }
  ```
- The portal middleware checks the `portal_session` cookie only — ignoring the main `platform_session`.
- Portal pages call `/fund/api/applications/mine` → filtered by `ApplicantPartyId` from JWT `party_id` claim.
- None of the main app shell (Sidebar, OrgBadge, ProductSwitcher) is rendered in the portal layout.
- The portal layout uses the same `TenantBranding` provider (same subdomain resolution, same CSS variables).

### 10.6 Session Cookie on Subdomain vs. Root Domain

**Risk:** If the session cookie is set on `legalsynq.com` (root domain), it is shared across all tenants' subdomains — a catastrophic security failure.

**Mitigation:**
- The Next.js BFF sets the cookie with `Domain` omitted (defaults to the exact origin subdomain only, e.g., `firm-a.legalsynq.com`).
- Cookies do NOT use `Domain=.legalsynq.com` (wildcard) — that would share sessions across tenants.
- In development (`localhost`), cookies are `SameSite=Lax; Secure=false`.
- In production: `SameSite=Strict; Secure=true; HttpOnly=true`.

---

*Document status: Design complete. Implementation sequencing: (1) Next.js project scaffold with App Router + TailwindCSS → (2) Session + TenantBranding providers → (3) API client + error handling → (4) Global middleware guard → (5) AppShell (TopBar + Sidebar) → (6) Login page with tenant-aware branding → (7) CareConnect route group → (8) SynqFund route group → (9) SynqLien route group → (10) Admin route group → (11) Injured party portal (separate session shape).*
