#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

# shellcheck source=scripts/_startup-helpers.sh
source "$ROOT/scripts/_startup-helpers.sh"

NEXT_BIN=""
# NOTE: node_modules/.bin/next is a pnpm shell shim (#!/bin/sh), NOT a JS file.
# Running it with `node shell-shim` causes an immediate SyntaxError.
# Always resolve to the real JS entrypoint at next/dist/bin/next.
for candidate in \
  "$ROOT/node_modules/next/dist/bin/next" \
  "$ROOT/node_modules/.pnpm/next@15.5.15"*/node_modules/next/dist/bin/next \
  "$(npm root 2>/dev/null)/next/dist/bin/next"; do
  # Expand glob — skip if candidate still contains a wildcard
  case "$candidate" in *\**) continue ;; esac
  if [ -f "$candidate" ]; then
    NEXT_BIN="$candidate"
    break
  fi
done
if [ -z "$NEXT_BIN" ]; then
  NEXT_BIN="$(which next 2>/dev/null || echo next)"
fi

echo "====== LegalSynq production startup ======"
echo "[next] Using: $NEXT_BIN"

# ── Startup probe timeouts ─────────────────────────────────────────────────────
# Override these env vars to tune probe deadlines without editing this script.
#   PROBE_TIMEOUT_NODEJS — seconds to wait for Node.js services (default 60)
#   PROBE_TIMEOUT_DOTNET — seconds to wait for .NET services    (default 90)
PROBE_TIMEOUT_NODEJS="${PROBE_TIMEOUT_NODEJS:-60}"
PROBE_TIMEOUT_DOTNET="${PROBE_TIMEOUT_DOTNET:-90}"

# ── Health-check probe helper ──────────────────────────────────────────────────
# Polls <scheme>://<host>:<port><path> in the background for up to $deadline
# seconds.  Logs a clear WARNING if the service never responds.
# Used for both .NET and Node.js services.
_probe_svc() {
  local label="$1" port="$2" path="$3" pid="${4:-}" deadline="${5:-90}"
  (
    echo "[$label] Waiting for $path on :$port..."
    local elapsed=0
    while [ "$elapsed" -lt "$deadline" ]; do
      # If we have a PID and the process is no longer alive, warn immediately
      # rather than polling for the rest of the 90-second window.
      if [ -n "$pid" ] && ! kill -0 "$pid" 2>/dev/null; then
        echo "[$label] WARNING: $label process (pid $pid) has exited — service crashed before becoming healthy"
        exit 1
      fi
      if curl -sf "http://127.0.0.1:${port}${path}" >/dev/null 2>&1; then
        echo "[$label] $path healthy after ${elapsed}s"
        exit 0
      fi
      sleep 5
      elapsed=$((elapsed + 5))
    done
    echo "[$label] WARNING: $path on :$port did not respond within ${deadline}s — $label may be unhealthy"
  ) &
}

NEXT_INTERNAL_PORT=3050
echo "[web] Starting Next.js on :$NEXT_INTERNAL_PORT (internal)"
(cd "$ROOT/apps/web" && NEXT_PUBLIC_ENV=production NEXT_PUBLIC_TENANT_CODE= GATEWAY_URL=http://127.0.0.1:5010 \
  CC_COMMON_PORTAL_HOSTNAME="${CC_COMMON_PORTAL_HOSTNAME:-careconnect-demo.legalsynq.com}" \
  node "$NEXT_BIN" start -p "$NEXT_INTERNAL_PORT") &
PID_WEB=$!

echo "[proxy] Starting prod proxy on :5000 → :$NEXT_INTERNAL_PORT"
NEXT_INTERNAL_PORT=$NEXT_INTERNAL_PORT PROXY_PORT=5000 node "$ROOT/scripts/dev-proxy.js" &
PID_PROXY=$!

echo "[control-center] Starting Next.js on :5004"
(cd "$ROOT/apps/control-center" && GATEWAY_URL=http://127.0.0.1:5010 MONITORING_SOURCE=service node "$NEXT_BIN" start -p 5004) &
PID_CC=$!

echo "[dotnet] Starting .NET services"
# shellcheck source=scripts/lib/dotnet-helpers.sh
source "$ROOT/scripts/lib/dotnet-helpers.sh"
# shellcheck source=scripts/lib/flow-preflight.sh
source "$ROOT/scripts/lib/flow-preflight.sh"

if command -v dotnet &>/dev/null; then
  (
    set +e
    export ASPNETCORE_ENVIRONMENT=Production

    # ── Single source of truth for all .NET services ─────────────────────────
    # Add a new service by appending its .csproj path here.  The DLL check,
    # build step, and post-build verification all derive from this list
    # automatically — no other section needs updating for the build pipeline.
    # Services are listed in launch order: backend services first, Gateway last
    # so it only begins routing once all upstream services are started.
    # New services should be inserted before Gateway.Api.csproj.
    BUILD_PROJECTS=(
      "$ROOT/apps/services/identity/Identity.Api/Identity.Api.csproj"
      "$ROOT/apps/services/fund/Fund.Api/Fund.Api.csproj"
      "$ROOT/apps/services/careconnect/CareConnect.Api/CareConnect.Api.csproj"
      "$ROOT/apps/services/documents/Documents.Api/Documents.Api.csproj"
      "$ROOT/apps/services/audit/PlatformAuditEventService.csproj"
      "$ROOT/apps/services/notifications/Notifications.Api/Notifications.Api.csproj"
      "$ROOT/apps/services/liens/Liens.Api/Liens.Api.csproj"
      "$ROOT/apps/services/flow/backend/src/Flow.Api/Flow.Api.csproj"
      "$ROOT/apps/services/monitoring/Monitoring.Api/Monitoring.Api.csproj"
      "$ROOT/apps/services/task/Task.Api/Task.Api.csproj"
      "$ROOT/apps/services/tenant/Tenant.Api/Tenant.Api.csproj"
      "$ROOT/apps/services/support/Support.Api/Support.Api.csproj"
      "$ROOT/apps/gateway/Gateway.Api/Gateway.Api.csproj"
    )

    # Derives the expected Release output DLL from a .csproj path.
    # Uses the same convention as the launch_svc helper above.
    dll_for_csproj() {
      local csproj="$1"
      echo "$(dirname "$csproj")/bin/Release/net10.0/$(basename "$csproj" .csproj).dll"
    }

    need_build=0
    for csproj in "${BUILD_PROJECTS[@]}"; do
      dll="$(dll_for_csproj "$csproj")"
      if [ ! -f "$dll" ]; then
        echo "[dotnet] Missing binary: $dll"
        need_build=1
      fi
    done

    if [ "$need_build" -eq 1 ]; then
      echo "[dotnet] One or more binaries missing — building all services now..."
      for csproj in "${BUILD_PROJECTS[@]}"; do
        svc_name="$(basename "$csproj" .csproj)"
        echo "[dotnet] Building $svc_name..."
        dotnet build "$csproj" --configuration Release --verbosity minimal \
          || { echo "[dotnet] ERROR: $svc_name build failed — aborting"; exit 1; }
      done

      for csproj in "${BUILD_PROJECTS[@]}"; do
        dll="$(dll_for_csproj "$csproj")"
        if [ ! -f "$dll" ]; then
          echo "[dotnet] ERROR: binary not produced after build: $dll — aborting"
          exit 1
        fi
      done
    else
      echo "[dotnet] All service binaries present — skipping build"
    fi

    # ── Flow env-var pre-flight ───────────────────────────────────────────
    # Warn on missing required keys and set SKIP_FLOW so the operator gets a
    # clear message before services are launched. Flow is skipped gracefully
    # rather than aborting all other services.
    # Implementation lives in scripts/lib/flow-preflight.sh so it can be
    # sourced independently by the shell-script test suite.
    check_flow_env_vars

    echo "[dotnet] Launching services..."
    # Derived from BUILD_PROJECTS — every service that was built is launched.
    # Per-service env vars (e.g. ASPNETCORE_URLS) are set via a case statement;
    # the catch-all (*) ensures any new entry in BUILD_PROJECTS is launched
    # automatically even if it has no special configuration.
    # SVC_PIDS / SVC_NAMES are populated here and consumed by the crash-monitor
    # immediately below the loop.
    SVC_PIDS=()
    SVC_NAMES=()
    PID_IDENTITY="" PID_FUND="" PID_CARECONNECT="" PID_DOCUMENTS=""
    PID_AUDIT="" PID_NOTIFICATIONS="" PID_LIENS="" PID_GATEWAY="" PID_FLOW="" PID_MONITORING="" PID_TASK=""
    PID_SUPPORT=""

    # ── Resolve portal URL / domain once — used by Identity, CareConnect, Support ──
    # PortalBaseUrl → PORTAL_BASE_URL secret/env if set; otherwise derived from
    #   the first value in REPLIT_DOMAINS (the Replit-assigned deployment domain).
    # PortalBaseDomain → hostname only (no scheme), used for subdomain link building.
    if [ -n "${PORTAL_BASE_URL:-}" ]; then
      case "${PORTAL_BASE_URL}" in
        http://*|https://*) _portal_url="${PORTAL_BASE_URL}" ;;
        *) _portal_url="https://${PORTAL_BASE_URL}" ;;
      esac
      _portal_domain="$(echo "${_portal_url}" | sed 's|^https\?://||' | cut -d'/' -f1)"
    else
      _portal_domain="$(echo "${REPLIT_DOMAINS:-localhost:3050}" | cut -d',' -f1)"
      _portal_url="https://${_portal_domain}"
    fi

    for csproj in "${BUILD_PROJECTS[@]}"; do
      svc_name="$(basename "$csproj" .csproj)"
      # _svc_label_for is defined in scripts/_startup-helpers.sh and maps the
      # .csproj basename to the human-readable name used in log / error messages.
      _svc_label="$(_svc_label_for "$svc_name")"
      case "$svc_name" in
        PlatformAuditEventService)
          launch_svc "$_svc_label" "$csproj" env ASPNETCORE_URLS=http://0.0.0.0:5007
          PID_AUDIT=$! ;;
        Notifications.Api)
          launch_svc "$_svc_label" "$csproj" env ASPNETCORE_URLS=http://0.0.0.0:5008
          PID_NOTIFICATIONS=$! ;;
        Flow.Api)
          if [ "$SKIP_FLOW" -eq 1 ]; then
            echo "[flow] Skipping Flow API launch due to missing required env vars"
            continue
          fi
          launch_svc "$_svc_label" "$csproj" env ASPNETCORE_URLS=http://0.0.0.0:5012
          PID_FLOW=$! ;;
        Monitoring.Api) launch_svc "$_svc_label" "$csproj"; PID_MONITORING=$! ;;
        Task.Api)      launch_svc "$_svc_label" "$csproj"; PID_TASK=$! ;;
        Tenant.Api)    launch_svc "$_svc_label" "$csproj"; PID_TENANT=$! ;;
        Support.Api)
          # Jwt:SigningKey is read from the Jwt__SigningKey Replit secret (env var).
          # Notifications are forwarded to the Notifications service on :5008.
          # ServiceTokens signing uses FLOW_SERVICE_TOKEN_SECRET, which AddServiceTokenIssuer
          # reads directly from the environment — no explicit config override needed here.
          # FileStorage: Local mode writes uploads to ./data/support-uploads
          # relative to the Support.Api working directory.
          # Audit: Http mode forwards compliance events to the Audit service (port 5007).
          launch_svc "$_svc_label" "$csproj" env \
            "Support__Notifications__Enabled=true" \
            "Support__Notifications__Mode=Http" \
            "Support__Notifications__BaseUrl=http://localhost:5008" \
            "Support__Notifications__PortalBaseUrl=${_portal_url}" \
            "Support__Notifications__PortalBaseDomain=${_portal_domain}" \
            "Support__FileStorage__Mode=Local" \
            "Support__FileStorage__LocalRootPath=./data/support-uploads" \
            "Support__Audit__Enabled=true" \
            "Support__Audit__Mode=Http"
          PID_SUPPORT=$! ;;
        Gateway.Api)   launch_svc "$_svc_label" "$csproj"; PID_GATEWAY=$! ;;
        Identity.Api)
          # NotificationsService:BaseUrl, :PortalBaseUrl, and :PortalBaseDomain must
          # all be non-empty in Production (Program.cs startup guard).
          # BaseUrl → internal Notifications service (always port 5008).
          # _portal_url / _portal_domain resolved before the loop (see above).
          launch_svc "$_svc_label" "$csproj" env \
            "NotificationsService__BaseUrl=http://localhost:5008" \
            "NotificationsService__PortalBaseUrl=${_portal_url}" \
            "NotificationsService__PortalBaseDomain=${_portal_domain}"
          PID_IDENTITY=$! ;;
        Fund.Api)      launch_svc "$_svc_label" "$csproj"; PID_FUND=$! ;;
        CareConnect.Api)
          launch_svc "$_svc_label" "$csproj" env \
            "IdentityService__BaseUrl=http://localhost:5001" \
            "TenantService__BaseUrl=http://localhost:5005" \
            "TenantService__ProvisioningToken=${TenantService__ProvisioningToken:-}" \
            "AppBaseUrl=${_portal_url}" \
            "AppBaseDomain=${_portal_domain}" \
            "ReferralToken__Secret=${FLOW_SERVICE_TOKEN_SECRET:-}"
          PID_CARECONNECT=$! ;;
        Documents.Api) launch_svc "$_svc_label" "$csproj"; PID_DOCUMENTS=$! ;;
        Liens.Api)     launch_svc "$_svc_label" "$csproj"; PID_LIENS=$! ;;
        *)             launch_svc "$_svc_label" "$csproj" ;;
      esac
      # $! is the PID of the dotnet process just backgrounded by launch_svc
      SVC_PIDS+=("$!")
      SVC_NAMES+=("$_svc_label")
    done

    echo "[dotnet] Service launch complete"

    # ── Fast crash-detection ──────────────────────────────────────────────────
    # Poll every 0.5 s for up to 5 s.  Runs entirely in the background so
    # healthy startups are not slowed down at all.  A crashed service is
    # reported within 0.5 s of its exit — no fixed wait required.
    # _run_crash_monitor is defined in scripts/_startup-helpers.sh.
    # It polls SVC_PIDS / SVC_NAMES for 5 s (10 × 0.5 s) and exits the
    # subshell with code 1 if any service crashes in that window.
    ( _run_crash_monitor ) &

    # ── Health-check probes for all .NET services ─────────────────────────
    # Each probe polls its /health (or /healthz for Flow) endpoint for up to
    # $PROBE_TIMEOUT_DOTNET seconds (default 90) after launch (.NET cold-start
    # + build can take that long).  Set PROBE_TIMEOUT_DOTNET in the environment
    # to override without editing this script.
    # All probes run in the background so they do not block each other or the
    # wait below.  A clear WARNING is logged if a service does not respond
    # within the deadline.
    # _probe_svc is defined at the top of this script and inherited here.

    _probe_svc "Identity"      5001 /health   "${PID_IDENTITY:-}"      "$PROBE_TIMEOUT_DOTNET"
    _probe_svc "Fund"          5002 /health   "${PID_FUND:-}"          "$PROBE_TIMEOUT_DOTNET"
    _probe_svc "CareConnect"   5003 /health   "${PID_CARECONNECT:-}"   "$PROBE_TIMEOUT_DOTNET"
    _probe_svc "Documents"     5006 /health   "${PID_DOCUMENTS:-}"     "$PROBE_TIMEOUT_DOTNET"
    _probe_svc "Audit"         5007 /health   "${PID_AUDIT:-}"         "$PROBE_TIMEOUT_DOTNET"
    _probe_svc "Notifications" 5008 /health   "${PID_NOTIFICATIONS:-}" "$PROBE_TIMEOUT_DOTNET"
    _probe_svc "Liens"         5009 /health   "${PID_LIENS:-}"         "$PROBE_TIMEOUT_DOTNET"
    _probe_svc "Gateway"       5010 /health   "${PID_GATEWAY:-}"       "$PROBE_TIMEOUT_DOTNET"
    _probe_svc "Flow"          5012 /healthz  "${PID_FLOW:-}"          "$PROBE_TIMEOUT_DOTNET"
    _probe_svc "Monitoring"    5015 /health   "${PID_MONITORING:-}"    "$PROBE_TIMEOUT_DOTNET"
    _probe_svc "Task"          5016 /health        "${PID_TASK:-}"          "$PROBE_TIMEOUT_DOTNET"
    _probe_svc "Tenant"        5005 /health        "${PID_TENANT:-}"        "$PROBE_TIMEOUT_DOTNET"
    _probe_svc "Support"       5017 /support/api/health "${PID_SUPPORT:-}"  "$PROBE_TIMEOUT_DOTNET"

    wait
  ) &
  PID_DOTNET=$!
else
  echo "[dotnet] WARNING: dotnet SDK not found — .NET services will not start"
  PID_DOTNET=""
fi

echo "[artifacts] Starting on :5020"
(
  cd "$ROOT/artifacts/api-server"
  ARTIFACTS_PORT=5020 NODE_ENV=production \
    node_modules/.bin/ts-node --transpile-only src/server.ts
) &
PID_ARTIFACTS=$!

# ── Health-check probes for all Node.js services ──────────────────────────────
# Each probe polls a liveness endpoint in the background for up to
# $PROBE_TIMEOUT_NODEJS seconds (default 60).  Node.js processes (Next.js,
# Express) typically start well under 30s, so the default deadline surfaces
# failures faster than the 90s used for .NET.  Set PROBE_TIMEOUT_NODEJS in
# the environment to override without editing this script.
# A clear WARNING is logged if the service does not respond within the deadline.
_probe_svc "Web"       3050 /api/health "$PID_WEB"       "$PROBE_TIMEOUT_NODEJS"
_probe_svc "Proxy"     5000 /health     "$PID_PROXY"     "$PROBE_TIMEOUT_NODEJS"
_probe_svc "CC"        5004 /api/health "$PID_CC"        "$PROBE_TIMEOUT_NODEJS"
_probe_svc "Artifacts" 5020 /api/health "$PID_ARTIFACTS" "$PROBE_TIMEOUT_NODEJS"

ALL_PIDS="$PID_WEB $PID_PROXY $PID_CC $PID_ARTIFACTS"
[ -n "$PID_DOTNET" ] && ALL_PIDS="$ALL_PIDS $PID_DOTNET"

cleanup() {
    kill $ALL_PIDS 2>/dev/null || true
    wait 2>/dev/null || true
}
trap cleanup EXIT INT TERM

# Wait for all background services and propagate failures visibly.
# PID_DOTNET is appended last to ALL_PIDS, so `wait` returns its exit code
# when it is the last process to be waited on.  We capture that code and emit
# a clear diagnostic so operators do not have to guess why the script failed.
set +e
wait $ALL_PIDS
_startup_ec=$?
set -e
if [ "$_startup_ec" -ne 0 ]; then
  if [ -n "$PID_DOTNET" ]; then
    echo "[dotnet] ERROR: The .NET services subshell exited with code $_startup_ec — one or more .NET services crashed at launch. Review the output above for details."
  else
    echo "[startup] ERROR: A service exited with code $_startup_ec. Review the output above for details."
  fi
  exit "$_startup_ec"
fi
