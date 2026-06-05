#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
NODE="/nix/store/51gywl5jn4nna7al9waj142pw4vfhy0k-nodejs-22.19.0/bin/node"

echo "====== LegalSynq dev startup ======"

# Start Next.js on an internal port; the proxy on :5000 gates requests
# until the cold-compile race condition is resolved (HTTP 200 on /login).
NEXT_INTERNAL_PORT=3050
echo "[web] Starting Next.js on :$NEXT_INTERNAL_PORT (internal)"
# Both apps/web and apps/control-center specify Next.js 15.5.15.
# Use the pnpm store binary directly — the root node_modules/next is a stub.
WEB_NEXT_BIN="$ROOT/node_modules/.pnpm/next@15.5.15_react-dom@18.3.1_react@18.3.1__react@18.3.1/node_modules/next/dist/bin/next"
if [ ! -f "$WEB_NEXT_BIN" ]; then
  WEB_NEXT_BIN="$(find "$ROOT/node_modules/.pnpm" -path "*/next@15.5*/node_modules/next/dist/bin/next" 2>/dev/null | head -1)"
fi
if [ -z "$WEB_NEXT_BIN" ] || [ ! -f "$WEB_NEXT_BIN" ]; then
  echo "[web] WARNING: Could not find Next.js 15.5.x binary in pnpm store"
  WEB_NEXT_BIN="$ROOT/node_modules/next/dist/bin/next"
fi
echo "[web] Using next binary: $WEB_NEXT_BIN"
# Pin apps/web/node_modules/next → pnpm store 15.5.15 so webpack does not fall
# back to the root node_modules/next (a different version that lacks shared/lib/utils).
PNPM_NEXT15="$ROOT/node_modules/.pnpm/next@15.5.15_react-dom@18.3.1_react@18.3.1__react@18.3.1/node_modules/next"
WEB_NM="$ROOT/apps/web/node_modules"
if [ -d "$PNPM_NEXT15" ]; then
  mkdir -p "$WEB_NM"
  rm -rf "$WEB_NM/next"
  ln -s "$PNPM_NEXT15" "$WEB_NM/next"
  mkdir -p "$WEB_NM/.bin"
  rm -f "$WEB_NM/.bin/next"
  ln -s "../next/dist/bin/next" "$WEB_NM/.bin/next"
  echo "[web] Pinned node_modules/next → 15.5.15"
fi
(cd "$ROOT/apps/web" && GATEWAY_URL=http://localhost:5010 \
  CC_COMMON_PORTAL_HOSTNAME="${CC_COMMON_PORTAL_HOSTNAME:-careconnect-demo.legalsynq.com}" \
  exec "$NODE" "$WEB_NEXT_BIN" dev -p "$NEXT_INTERNAL_PORT") &
PID_WEB=$!

echo "[proxy] Starting dev proxy on :5000 → :$NEXT_INTERNAL_PORT"
NEXT_INTERNAL_PORT=$NEXT_INTERNAL_PORT PROXY_PORT=5000 "$NODE" "$ROOT/scripts/dev-proxy.js" &
PID_PROXY=$!

# Start Control Center — port 5004
# Both apps/web and apps/control-center use Next.js 15.5.15.
# Pin the CC's node_modules/next to 15.5.15 so webpack uses the correct version.
echo "[control-center] Starting Next.js on :5004"
CC_NM="$ROOT/apps/control-center/node_modules"
if [ -d "$PNPM_NEXT15" ]; then
  rm -rf "$CC_NM/next"
  ln -s "$PNPM_NEXT15" "$CC_NM/next"
  rm -f "$CC_NM/.bin/next"
  ln -s "../next/dist/bin/next" "$CC_NM/.bin/next"
  echo "[control-center] Pinned node_modules/next → 15.5.15"
fi
CC_NEXT_BIN="$ROOT/node_modules/.pnpm/next@15.5.15_react-dom@18.3.1_react@18.3.1__react@18.3.1/node_modules/next/dist/bin/next"
if [ ! -f "$CC_NEXT_BIN" ]; then
  # Fallback: search pnpm store for any next@15.5.x binary
  CC_NEXT_BIN="$(find "$ROOT/node_modules/.pnpm" -path "*/next@15.5*/node_modules/next/dist/bin/next" 2>/dev/null | head -1)"
fi
if [ -z "$CC_NEXT_BIN" ] || [ ! -f "$CC_NEXT_BIN" ]; then
  echo "[control-center] WARNING: Could not find Next.js 15.5.x binary, falling back to root binary"
  CC_NEXT_BIN="$ROOT/node_modules/next/dist/bin/next"
fi
echo "[control-center] Using next binary: $CC_NEXT_BIN"
(cd "$ROOT/apps/control-center" && GATEWAY_URL=http://localhost:5010 MONITORING_SOURCE=service exec "$NODE" "$CC_NEXT_BIN" dev -p 5004) &
PID_CC=$!

# Restore, build, and start .NET services all in background
(
  dotnet restore "$ROOT/LegalSynq.sln" --verbosity quiet
  # The full solution build can OOM on constrained hosts. Add || true so the
  # subshell continues and services launch from their cached (pre-built) binaries.
  dotnet build  "$ROOT/LegalSynq.sln" --no-restore --configuration Debug --verbosity quiet \
    || echo "[build] LegalSynq.sln build error (possibly OOM) — continuing with cached binaries"
  dotnet build "$ROOT/apps/services/documents/Documents.Api/Documents.Api.csproj" --configuration Debug --verbosity quiet \
    || echo "[build] Documents.Api build error — continuing with cached binary"
  # Flow service has its own solution (separate boundary, separate DB).
  # Build only the API project — not the full Flow.sln — so test-project
  # NuGet packages (never restored by LegalSynq.sln) don't block the build.
  dotnet restore "$ROOT/apps/services/flow/backend/src/Flow.Api/Flow.Api.csproj" --verbosity quiet
  dotnet build   "$ROOT/apps/services/flow/backend/src/Flow.Api/Flow.Api.csproj" --no-restore --configuration Debug --verbosity quiet \
    || echo "[build] Flow.Api build error — continuing with cached binary"
  # Reports service has its own project boundary (not in LegalSynq.sln).
  dotnet restore "$ROOT/apps/services/reports/src/Reports.Api/Reports.Api.csproj" --verbosity quiet
  dotnet build   "$ROOT/apps/services/reports/src/Reports.Api/Reports.Api.csproj" --no-restore --configuration Debug --verbosity quiet \
    || echo "[build] Reports.Api build error — continuing with cached binary"
  # Audit service has its own project boundary (not in LegalSynq.sln).
  dotnet build   "$ROOT/apps/services/audit/PlatformAuditEventService.csproj" --configuration Debug --verbosity quiet \
    || echo "[build] Audit build error — continuing with cached binary"
  # Support service has its own solution boundary (not in LegalSynq.sln).
  dotnet restore "$ROOT/apps/services/support/Support.Api/Support.Api.csproj" --verbosity quiet
  dotnet build   "$ROOT/apps/services/support/Support.Api/Support.Api.csproj" --no-restore --configuration Debug --verbosity quiet \
    || echo "[build] Support.Api build error — continuing with cached binary"
  # Identity.Api can OOM inside the full solution build on constrained hosts.
  # Build it separately here with conservative GC settings so the binary always
  # reflects the latest source (including the "role" claim fix in JwtTokenService).
  # Falls back to the cached binary if this also fails.
  echo "[identity] Building Identity.Api with conservative memory settings..."
  DOTNET_GCConserveMemory=9 \
    dotnet build "$ROOT/apps/services/identity/Identity.Api/Identity.Api.csproj" \
    --no-restore --configuration Debug --verbosity quiet -maxcpucount:1 \
    || echo "[identity] Build error — will run from cached binary"
  NotificationsService__BaseUrl=http://localhost:5008 \
    NotificationsService__PortalBaseUrl=http://localhost:3050 \
    dotnet run --no-build --project "$ROOT/apps/services/identity/Identity.Api/Identity.Api.csproj" &
  dotnet run --no-build --project "$ROOT/apps/services/fund/Fund.Api/Fund.Api.csproj" &
  AppBaseUrl="${PORTAL_BASE_URL:-https://demo.legalsynq.com}" \
    AppBaseDomain="${Route53__BaseDomain:-demo.legalsynq.com}" \
    TenantService__BaseUrl=http://localhost:5005 \
    TenantService__ProvisioningToken="${TenantService__ProvisioningToken:-}" \
    dotnet run --no-build --project "$ROOT/apps/services/careconnect/CareConnect.Api/CareConnect.Api.csproj" &
  dotnet run --no-build --project "$ROOT/apps/services/liens/Liens.Api/Liens.Api.csproj" &
  ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://0.0.0.0:5007 dotnet run --no-build --project "$ROOT/apps/services/audit/PlatformAuditEventService.csproj" &
  ASPNETCORE_ENVIRONMENT=Development dotnet run --no-build --project "$ROOT/apps/services/documents/Documents.Api/Documents.Api.csproj" &
  ASPNETCORE_ENVIRONMENT=Development dotnet run --no-build --project "$ROOT/apps/services/notifications/Notifications.Api/Notifications.Api.csproj" &
  dotnet run --no-build --project "$ROOT/apps/services/comms/Comms.Api/Comms.Api.csproj" &
  ASPNETCORE_ENVIRONMENT=Development dotnet run --no-build --project "$ROOT/apps/services/flow/backend/src/Flow.Api/Flow.Api.csproj" &
  # Monitoring service: use 'dotnet run' (not --no-build) so the runtime build
  # step resolves any binary-path mismatch between solution-build and standalone
  # run. A restart wrapper logs crashes visibly and retries once after a delay.
  (
    for attempt in 1 2; do
      echo "[monitoring] Starting Monitoring service (attempt $attempt)..."
      ASPNETCORE_ENVIRONMENT=Development \
        dotnet run --project "$ROOT/apps/services/monitoring/Monitoring.Api/Monitoring.Api.csproj" \
        2>&1 | sed 's/^/[monitoring] /' || true
      if [ "$attempt" -lt 2 ]; then
        echo "[monitoring] Service exited on attempt $attempt; restarting in 15s..."
        sleep 15
      fi
    done
    echo "[monitoring] Monitoring service exited after 2 attempts."
  ) &
  ASPNETCORE_ENVIRONMENT=Development dotnet run --no-build --project "$ROOT/apps/services/reports/src/Reports.Api/Reports.Api.csproj" &
  ASPNETCORE_ENVIRONMENT=Development dotnet run --no-build --project "$ROOT/apps/services/task/Task.Api/Task.Api.csproj" &
  # Tenant.Api also needs a separate low-memory build to pick up the "role" RoleClaimType fix.
  echo "[tenant] Building Tenant.Api with conservative memory settings..."
  DOTNET_GCConserveMemory=9 \
    dotnet build "$ROOT/apps/services/tenant/Tenant.Api/Tenant.Api.csproj" \
    --no-restore --configuration Debug --verbosity quiet -maxcpucount:1 \
    || echo "[tenant] Build error — will run from cached binary"
  ASPNETCORE_ENVIRONMENT=Development dotnet run --no-build --project "$ROOT/apps/services/tenant/Tenant.Api/Tenant.Api.csproj" &
  # Support service — port 5017, standalone MySQL, JWT via Authentication:Jwt:SymmetricKey.
  # Authentication__Jwt__SymmetricKey is sourced from Jwt__SigningKey (the same secret used
  # by the gateway) so tokens minted by the platform validate correctly in Support.
  # Falls back to the matching dev key when no secret is set in the environment.
  ASPNETCORE_ENVIRONMENT=Development \
    Authentication__Jwt__SymmetricKey="${Jwt__SigningKey:-dev-only-signing-key-minimum-32-chars-long!}" \
    dotnet run --no-build --project "$ROOT/apps/services/support/Support.Api/Support.Api.csproj" &
  dotnet run --no-build --project "$ROOT/apps/gateway/Gateway.Api/Gateway.Api.csproj" &
  wait
) &
PID_DOTNET=$!

# Start artifacts API server — port 5020
echo "[artifacts] Starting on :5020"
(
  cd "$ROOT/artifacts/api-server"
  ARTIFACTS_PORT=5020 NODE_ENV=development \
    node_modules/.bin/ts-node-dev --respawn --transpile-only src/server.ts
) &
PID_ARTIFACTS=$!

cleanup() {
    kill "$PID_WEB" "$PID_PROXY" "$PID_CC" "$PID_DOTNET" "$PID_ARTIFACTS" 2>/dev/null || true
    wait 2>/dev/null || true
}
trap cleanup EXIT INT TERM

wait "$PID_WEB" "$PID_PROXY" "$PID_CC" "$PID_DOTNET" "$PID_ARTIFACTS"
