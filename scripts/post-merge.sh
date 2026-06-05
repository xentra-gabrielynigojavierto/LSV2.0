#!/bin/bash
set -e

echo "=== Post-merge setup ==="

echo "Installing frontend dependencies..."
# Cap Node.js heap to avoid SIGABRT in memory-constrained environment.
# Three-tier fallback: frozen → regular → ignore-scripts (packages already cached).
export NODE_OPTIONS="--max-old-space-size=512"

pnpm_install() {
  if pnpm install --frozen-lockfile 2>/dev/null; then
    return 0
  fi
  echo "  (frozen-lockfile failed, retrying without it...)"
  if pnpm install 2>/dev/null; then
    return 0
  fi
  echo "  (retry failed, falling back to --ignore-scripts...)"
  pnpm install --ignore-scripts
}

pnpm_install

echo "Building .NET services (restore + build)..."
# Build one project at a time and apply aggressive memory limits to avoid OOM
# crashes in the constrained Replit build environment.
export DOTNET_GCConserveMemory=9
export DOTNET_CLI_TELEMETRY_OPTOUT=1
# Disable MSBuild node reuse so each build releases memory fully before the next
export MSBUILDDISABLENODEREUSE=1
# Cap the GC heap to ~400 MB per build process
export DOTNET_GCHeapHardLimit=419430400

# build_project: restore + build with one retry on transient failure.
# The platform can CANCEL a build under concurrent-merge resource pressure;
# a single retry recovers without failing the entire post-merge run.
build_project() {
  local proj="$1"
  echo "  -> building $proj"
  local attempt=0
  while true; do
    attempt=$((attempt + 1))
    if dotnet restore "$proj" --verbosity quiet \
       && dotnet build "$proj" --no-restore --verbosity quiet -maxcpucount:1 -nodeReuse:false; then
      return 0
    fi
    if [ "$attempt" -ge 2 ]; then
      echo "  ERROR: $proj failed after $attempt attempt(s)" >&2
      return 1
    fi
    echo "  (attempt $attempt failed, retrying after 5s...)"
    sleep 5
  done
}

build_project apps/services/liens/Liens.Api/Liens.Api.csproj
build_project apps/services/identity/Identity.Api/Identity.Api.csproj
build_project apps/services/documents/Documents.Api/Documents.Api.csproj
build_project apps/services/careconnect/CareConnect.Api/CareConnect.Api.csproj
build_project apps/services/notifications/Notifications.Api/Notifications.Api.csproj
build_project apps/gateway/Gateway.Api/Gateway.Api.csproj

echo "=== Post-merge setup complete ==="
