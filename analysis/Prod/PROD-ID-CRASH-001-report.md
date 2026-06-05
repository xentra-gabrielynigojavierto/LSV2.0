# PROD-ID-CRASH-001 — Identity Service Startup Crash in Production

## Status
**RESOLVED** — fix applied to `scripts/run-prod.sh`

## Symptom
All login attempts return 502. Users cannot authenticate in production.

## Root Cause
`Identity.Api` crashes at `Program.cs:42` during startup with:

```
System.InvalidOperationException: NotificationsService:BaseUrl is not configured.
Set this value so the Identity service can dispatch invitation and password-reset emails.
   at Program.<Main>$(String[] args)
```

The startup guard at lines 39–50 of `Identity.Api/Program.cs` throws if either of two
config values is absent in non-Development environments:

| Config key | Purpose |
|---|---|
| `NotificationsService:BaseUrl` | Internal HTTP endpoint of the Notifications service |
| `NotificationsService:PortalBaseUrl` | Public portal URL used in invite / reset links |

Neither value was being passed to the identity process in `scripts/run-prod.sh`. The
`ASPNETCORE_ENVIRONMENT=Production` export (line 82) triggers the guard, while no
`NotificationsService__*` env vars were propagated.

## Evidence
- Deployment log: `Unhandled exception. System.InvalidOperationException: NotificationsService:BaseUrl is not configured.`
- Deployment log: `Identity process (pid 93) has exited — service crashed before becoming healthy`
- All downstream services: `Connection refused (localhost:5001)` — gateway cannot route `/login`

## Fix
In `run-prod.sh`, the `Identity.Api` case statement was changed from:

```bash
Identity.Api)  launch_svc "$_svc_label" "$csproj"; PID_IDENTITY=$! ;;
```

to:

```bash
Identity.Api)
  _portal_url="${PORTAL_BASE_URL:-https://$(echo "${REPLIT_DOMAINS:-localhost:3050}" | cut -d',' -f1)}"
  launch_svc "$_svc_label" "$csproj" env \
    "NotificationsService__BaseUrl=http://localhost:5008" \
    "NotificationsService__PortalBaseUrl=${_portal_url}"
  PID_IDENTITY=$! ;;
```

### Why each value
- **`NotificationsService__BaseUrl=http://localhost:5008`** — the Notifications service is always
  launched on port 5008 in production (see `Notifications.Api` case in the same loop).
- **`NotificationsService__PortalBaseUrl`** — derived from `PORTAL_BASE_URL` env var (set via
  Replit secrets for custom-domain deployments) with a fallback to the first value in
  `REPLIT_DOMAINS` (the Replit-assigned deployment domain). This ensures the identity service
  can start even without a custom domain, and operators can override it for branded links.

## Operator Notes
- Set `PORTAL_BASE_URL` (production env var) to the public tenant-portal hostname, e.g.
  `https://demo.legalsynq.com`, so invitation and password-reset links use the correct domain.
- `NotificationsService__BaseUrl` is internal only — never expose this directly to clients.
