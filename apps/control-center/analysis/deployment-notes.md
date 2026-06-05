# LegalSynq Control Center — Deployment Notes

> Last updated: Step 28 — Post-Launch Enhancements  
> Port: **5004**  
> Environment: Replit / NixOS

---

## Port Layout

| Service           | Port |
|-------------------|------|
| Next.js web app   | 5000 |
| API Gateway       | 5010 |
| Identity service  | 5001 |
| Fund service      | 5002 |
| CareConnect       | 5003 |
| **Control Center**| **5004** |

---

## Admin Credentials (dev seeds only)

| Tenant        | Email                    | Password       | Role           |
|---------------|--------------------------|----------------|----------------|
| LEGALSYNQ     | admin@legalsynq.com      | Admin1234!     | PlatformAdmin  |
| HARTWELL      | margaret@hartwell.law    | hartwell123!   | TenantAdmin    |

> **Do not use these credentials in production.** Rotate before any external exposure.

---

## Environment Variables

All values are managed via Replit Secrets and referenced in `src/lib/env.ts`.

| Variable                        | Purpose                                          | Required |
|---------------------------------|--------------------------------------------------|----------|
| `ConnectionStrings__CareConnectDb` | PostgreSQL for CareConnect service           | Yes      |
| `ConnectionStrings__FundDb`     | PostgreSQL for Fund service                      | Yes      |
| `ConnectionStrings__IdentityDb` | PostgreSQL for Identity service                  | Yes      |
| `CONTROL_CENTER_API_BASE`       | Base URL for backend API calls (default: Gateway)| Prod only|
| `NEXT_PUBLIC_APP_VERSION`       | Displayed version string (optional)              | No       |
| `NEXT_PUBLIC_ANALYTICS_KEY`     | Analytics provider write key (optional)          | No       |

---

## Docker

A production `Dockerfile` is included at `apps/control-center/Dockerfile`.

```bash
# Build
docker build -t legalsynq-control-center:latest -f apps/control-center/Dockerfile .

# Run
docker run -p 5004:5004 \
  -e CONTROL_CENTER_ORIGIN=https://control.example.com \
  legalsynq-control-center:latest
```

The image uses **Node 22 Alpine**, multi-stage build, and runs as a non-root user.

---

## CI / CD

`.github/workflows/control-center-ci.yml` runs on every push to `main` and on PRs:
- TypeScript type check (`tsc --noEmit`)
- Build (`next build`)
- Planned: Playwright smoke tests

---

## Health Check

```
GET /api/health
```

- Unauthenticated (whitelisted in `middleware.ts` as a `PUBLIC_PATH`)
- Returns `200 OK` with JSON body when healthy
- Performs a `HEAD` request to the configured API gateway with a 2 s timeout
- Returns `503 Service Unavailable` if the gateway is unreachable

```json
{
  "status": "ok",
  "service": "control-center",
  "timestamp": "2026-03-29T06:00:00.000Z",
  "apiGateway": "reachable"
}
```

---

## Cookie Reference

| Cookie                  | Purpose                              | HttpOnly | SameSite |
|-------------------------|--------------------------------------|----------|----------|
| `platform_session`      | Authenticated session (JWT)          | Yes      | Strict   |
| `cc_tenant_context`     | Active tenant scope (impersonation)  | No       | Strict   |
| `cc_impersonation`      | User impersonation session           | Yes      | Strict   |

---

## Analytics

`src/lib/analytics.ts` exports `track()`, `identifyUser()`, and `resetUser()`.

- **Dev**: events are logged to the browser console as `[CC Analytics] event.name {...}`
- **Prod**: add your SDK call inside the `TODO` block in each function

`AnalyticsProvider` is mounted once in `layout.tsx` and fires `page.view` on every route change via `usePathname()`.

---

## Security Hardening (completed in Step 27)

- `X-Frame-Options: SAMEORIGIN` — prevents clickjacking
- `X-Content-Type-Options: nosniff`
- `Referrer-Policy: strict-origin-when-cross-origin`
- `Permissions-Policy: camera=(), microphone=()`
- `Content-Security-Policy` — strict, inline scripts blocked; nonces for legitimate inline JS
- Session cookies: `httpOnly`, `sameSite: Strict`, `secure` in production

---

## Known Limitations / TODOs

| Area                | Status               | Notes                                           |
|---------------------|----------------------|-------------------------------------------------|
| Create Tenant       | UI placeholder       | POST /identity/api/admin/tenants not wired      |
| Tenant Actions      | Stub (800ms mock)    | Wire BFF proxy in app/api/identity/admin        |
| User Actions        | Stub (800ms mock)    | Same                                            |
| Support case update | Stub                 | Wire PATCH endpoint in BFF                      |
| Impersonation       | Fully wired          | Uses cc_impersonation cookie                    |
| Tenant context      | Fully wired          | Uses cc_tenant_context cookie                   |
| Analytics provider  | Console logging only | Replace TODO in analytics.ts with real SDK      |

---

## Troubleshooting Guide

### App won't start — port 5004 already in use

```bash
lsof -ti :5004 | xargs kill -9 2>/dev/null || true
```

Then restart the workflow.

---

### "Failed to compile" — TypeScript errors after pulling changes

```bash
cd apps/control-center
node /home/runner/workspace/node_modules/typescript/bin/tsc --noEmit
```

This gives line-level details. Common causes:
- Missing `'use client'` directive on a component that uses hooks
- Import path typo in `@/...` aliases
- New component prop added to interface but not passed at call site

---

### Authentication loop — keeps redirecting to `/login`

1. Check the `platform_session` cookie is being set after login.
2. Verify `NEXTAUTH_SECRET` (or your session secret) is set in Replit Secrets.
3. Ensure `middleware.ts` matcher does not exclude the login page from public paths.
4. Check `src/lib/auth-guards.ts` — `requirePlatformAdmin()` calls `redirect('/login')` if no session.

---

### "Module not found" for `@/...` imports

Alias is configured in `tsconfig.json`:
```json
{ "paths": { "@/*": ["./src/*"] } }
```
and in `next.config.js` (or `next.config.mjs`). If you add a new path alias, update both.

---

### 401 / 403 from backend API calls (on real endpoints)

1. Check that the gateway is running on the correct port (`5010`).
2. Verify the `Authorization: Bearer <token>` header is forwarded by the BFF proxy route.
3. Look for `CONTROL_CENTER_API_BASE` in Replit Secrets — must be set to the gateway URL in production.
4. The mock API stubs in `control-center-api.ts` always succeed — errors only appear after real endpoints are wired.

---

### Impersonation banner does not appear

- Check the `cc_impersonation` cookie exists and is not expired.
- Ensure `getImpersonation()` in `src/lib/auth.ts` can parse the cookie value.
- The impersonation banner is rendered by `CCShell` — ensure the page uses `CCShell` as its outer wrapper.

---

### Tenant context banner stuck / not clearing

- Call the `EXIT /api/cc/context` endpoint (or clear the `cc_tenant_context` cookie) to exit context.
- The `TenantContextBanner` renders a "Clear context" button that calls the exit action.
- If the cookie persists: check `sameSite` and `domain` settings match the Replit preview domain.

---

### Loading skeletons show full-screen white flash on first load

This happens if the root `layout.tsx` has no default background. Verify `<body className="antialiased bg-gray-50">` is present in `src/app/layout.tsx`. The loading shells also set `bg-gray-50` on their root div.

---

### Confirm dialogs are not trapping focus in some screen readers

The current implementation auto-focuses the Cancel button and handles Escape. A full focus-trap library (e.g. `focus-trap-react`) can be added for stricter WCAG 2.1 AA compliance:

```bash
pnpm add focus-trap-react --filter=control-center --legacy-peer-deps
```

Then wrap the dialog panel:
```tsx
import FocusTrap from 'focus-trap-react';
<FocusTrap>
  <div role="dialog" ...>...</div>
</FocusTrap>
```

---

### Analytics events not appearing in the provider dashboard

1. Check the browser console for `[CC Analytics]` log lines — if present, the `track()` function is called correctly.
2. Confirm your analytics SDK is initialised before `track()` is called (use the `useEffect` in `AnalyticsProvider`).
3. Add the provider API key to `NEXT_PUBLIC_ANALYTICS_KEY` in Replit Secrets.
4. Verify `NODE_ENV === 'production'` — in dev, events are only console-logged, not sent to the provider.

---

### Error boundary not resetting after "Try again"

`reset()` calls `router.refresh()` internally (Next.js). If the error persists after clicking "Try again":
- The underlying data fetch may be failing continuously — check the API gateway health.
- Add a `key` prop to the error-boundary parent if you need to force a full re-mount.

---

_End of deployment notes._
