# LegalSynq Control Center — Dev Guide

## What is this?

The Control Center is a standalone Next.js 14 app for platform administration.
It runs independently from the main `apps/web` tenant portal.

**Port:** `5004`
**Auth:** PlatformAdmin system role required for all pages except the homepage.
**Admin credentials (dev):** `admin@legalsynq.com` / `Admin1234!` (tenant: `LEGALSYNQ`)

---

## How to run

### Option A — via monorepo startup (recommended)

From the workspace root, the Replit workflow starts everything including the Control Center:

```bash
bash scripts/run-dev.sh
```

The Control Center starts on port 5004 automatically alongside `apps/web` (5000) and the .NET services.

### Option B — standalone (Control Center only)

```bash
cd apps/control-center
npm run dev
```

This starts only the Control Center on port 5004.
It still expects the Gateway on port 5010 for authentication (`/identity/api/auth/me`).
All other data (tenants, users, roles) is mocked and does not require backend services.

---

## Verify the app loaded

Open the app in your browser:

```
http://localhost:5004
```

You should see the landing page with the **"Control Center Running"** green badge.

In Replit, change the preview port to `5004` to view the app directly.

---

## Page reference

| URL | Description | Auth required |
|---|---|---|
| `/` | Landing page — "Control Center Running" banner | No |
| `/login` | Sign in page | No |
| `/tenants` | All tenants list | Yes — PlatformAdmin |
| `/tenants/[id]` | Tenant detail | Yes — PlatformAdmin |
| `/tenants/[id]/users` | Users for a specific tenant | Yes — PlatformAdmin |
| `/tenant-users` | All users cross-tenant | Yes — PlatformAdmin |
| `/tenant-users/[id]` | User detail | Yes — PlatformAdmin |
| `/roles` | Roles & permissions list | Yes — PlatformAdmin |
| `/roles/[id]` | Role detail with permission table | Yes — PlatformAdmin |

---

## Dependencies

Dependencies are resolved from the monorepo root `node_modules` — no separate install needed when running from the workspace root.

If running the Control Center standalone, install from the workspace root first:

```bash
cd /path/to/workspace
npm install
```

Then `cd apps/control-center && npm run dev` will find the packages via the monorepo layout.

---

## Port layout

| Service | Port |
|---|---|
| `apps/web` (tenant portal) | 5000 |
| Identity.Api | 5001 |
| Fund.Api | 5002 |
| CareConnect.Api | 5003 |
| `apps/control-center` | 5004 |
| Gateway.Api | 5010 |

---

## Architecture

- **Framework:** Next.js 14 App Router
- **Language:** TypeScript (strict)
- **Styles:** Tailwind CSS v4 (`@import "tailwindcss"`)
- **Color scheme:** Indigo
- **Auth:** Cookie-based (`platform_session`) → validates via `GET /identity/api/auth/me`
- **Data:** All CC data currently mocked in `src/lib/control-center-api.ts`

All data access goes through `controlCenterServerApi` in `src/lib/control-center-api.ts`.
Mock stubs include `// TODO` comments pointing to the exact Identity.Api admin endpoints.
Switching to live data requires one-line changes per method in that file.
