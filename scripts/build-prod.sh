#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

# ── Restore pnpm-managed node_modules ─────────────────────────────────────────
# Replit's deployment pipeline runs `npm install` automatically BEFORE calling
# this script. npm does not understand pnpm's virtual-store symlink layout and
# rewrites 113+ packages in node_modules/, corrupting or replacing the
# @next/swc-linux-x64-gnu native binary and other critical packages. Running
# `pnpm install --frozen-lockfile` here undoes that damage by restoring the
# correct pnpm-managed symlink structure from the pnpm content-addressable store
# (which npm never touches — it lives at .local/share/pnpm/store/).
if command -v pnpm &>/dev/null; then
  echo "[pnpm-restore] Restoring pnpm-managed node_modules after npm install..."
  pnpm install --frozen-lockfile 2>&1 \
    && echo "[pnpm-restore] Done" \
    || echo "[pnpm-restore] WARNING: pnpm install failed — continuing with npm-managed node_modules"
else
  echo "[pnpm-restore] WARNING: pnpm not in PATH — skipping restore (npm-managed node_modules in use)"
fi

# Resolve the Next.js JS entrypoint (NOT the .bin/next shell wrapper —
# that file is a bash script and `node <bash-script>` blows up with
# "SyntaxError: missing ) after argument list", which silently failed
# every production build.
NEXT_BIN=""
for candidate in \
  "$ROOT/node_modules/next/dist/bin/next" \
  "$(npm root)/next/dist/bin/next" \
  "$(npm root -g 2>/dev/null)/next/dist/bin/next"; do
  if [ -f "$candidate" ]; then
    NEXT_BIN="$candidate"
    break
  fi
done

if [ -z "$NEXT_BIN" ]; then
  echo "ERROR: Cannot find next JS entrypoint. Installing next..."
  npm install next@15.2.9
  NEXT_BIN="$ROOT/node_modules/next/dist/bin/next"
fi

# Sanity-check: must start with the node shebang, never a bash one.
if ! head -n 1 "$NEXT_BIN" | grep -q '^#!/usr/bin/env node'; then
  echo "ERROR: $NEXT_BIN does not look like the Next.js JS entrypoint."
  exit 1
fi

echo "Using next binary: $NEXT_BIN"

# ── Pre-build cleanup ─────────────────────────────────────────────────────────
# Free disk space BEFORE the Next.js webpack build.  The GCE deploy container's
# overlay filesystem has limited headroom; if it fills up while webpack is
# writing output files, mmap-backed writes receive SIGBUS from the kernel.
# Items removed here are not needed at build time:
#   .git              ~3.3 GB — git history is never used during compilation
#   _archived         variable — stale archive dumps
#   analysis/exports  variable — local dev artefacts
#   attached_assets   variable — media uploads not needed at build time
#   Replit state      ~638 MB — agent/workspace runtime state
echo "====== Pre-build disk cleanup ======"
echo "[pre-cleanup] Removing git history..."
rm -rf "$ROOT/.git" 2>/dev/null || true
echo "[pre-cleanup] Removing archived files..."
rm -rf "$ROOT/_archived" 2>/dev/null || true
echo "[pre-cleanup] Removing analysis/exports/downloads..."
rm -rf "$ROOT/analysis" "$ROOT/exports" "$ROOT/downloads" 2>/dev/null || true
echo "[pre-cleanup] Removing attached_assets..."
rm -rf "$ROOT/attached_assets" 2>/dev/null || true
echo "[pre-cleanup] Removing Replit agent/workflow state..."
rm -rf "$ROOT/.local/state/replit" "$ROOT/.local/state/workflow-logs" "$ROOT/.local/state/scribe" 2>/dev/null || true
echo "[pre-cleanup] Done"

echo "====== Building web app ======"
# Deduplicate Next.js: pnpm may resolve two different peer-dependency variants of
# next@15.x (one with @playwright, one without), giving apps/web its own copy whose
# HtmlContext singleton differs from the one in root node_modules.  That makes
# HtmlContext.Provider (set by render.js using the root copy) invisible to Html
# (compiled into the worker bundle with the apps/web copy), causing:
#   "Error: <Html> should not be imported outside of pages/_document"
# Fix: force apps/web/node_modules/next → the same physical directory as root.
ROOT_NEXT_REAL="$(readlink -f "$ROOT/node_modules/next")"
WEB_NM="$ROOT/apps/web/node_modules"
mkdir -p "$WEB_NM"
if [ -n "$ROOT_NEXT_REAL" ] && [ -d "$ROOT_NEXT_REAL" ]; then
  WEB_NEXT_REAL="$(readlink -f "$WEB_NM/next" 2>/dev/null || true)"
  if [ "$WEB_NEXT_REAL" != "$ROOT_NEXT_REAL" ]; then
    rm -rf "$WEB_NM/next"
    ln -s "$ROOT_NEXT_REAL" "$WEB_NM/next"
    echo "[dedup] Linked apps/web/node_modules/next → $ROOT_NEXT_REAL"
  fi
fi
# Deduplicate React for apps/web: running `pnpm add` inside apps/web creates a
# separate pnpm store (apps/web/node_modules/.pnpm/) with its own React copy.
# This creates two React instances — one resolved by webpack bundles (apps/web
# local store), another used by Next.js's SSR renderer (root store) — causing:
#   "TypeError: Cannot read properties of null (reading 'useContext')"
# during static page prerendering (e.g. /404, /500 via pages/_error.js).
# Fix: force apps/web/node_modules/react → same physical directory as root.
PNPM_REACT="$ROOT/node_modules/.pnpm/react@18.3.1/node_modules/react"
PNPM_REACT_DOM="$ROOT/node_modules/.pnpm/react-dom@18.3.1_react@18.3.1/node_modules/react-dom"
WEB_REACT_REAL="$(readlink -f "$WEB_NM/react" 2>/dev/null || true)"
WEB_REACT_DOM_REAL="$(readlink -f "$WEB_NM/react-dom" 2>/dev/null || true)"
if [ -d "$PNPM_REACT" ] && [ "$WEB_REACT_REAL" != "$PNPM_REACT" ]; then
  rm -rf "$WEB_NM/react"
  ln -s "$PNPM_REACT" "$WEB_NM/react"
  echo "[dedup] Linked apps/web/node_modules/react → root pnpm store"
fi
if [ -d "$PNPM_REACT_DOM" ] && [ "$WEB_REACT_DOM_REAL" != "$PNPM_REACT_DOM" ]; then
  rm -rf "$WEB_NM/react-dom"
  ln -s "$PNPM_REACT_DOM" "$WEB_NM/react-dom"
  echo "[dedup] Linked apps/web/node_modules/react-dom → root pnpm store"
fi
cd "$ROOT/apps/web"
rm -rf .next
# Remove stray app-level lockfile so Next.js sees only the workspace-root
# pnpm-lock.yaml and does not over-trace files or emit a workspace-root warning.
rm -f "$ROOT/apps/web/pnpm-lock.yaml"
NODE_OPTIONS="--max-old-space-size=2048" NEXT_PUBLIC_ENV=production NEXT_PUBLIC_TENANT_CODE= GATEWAY_URL=http://127.0.0.1:5010 node "$NEXT_BIN" build

echo "====== Building control center ======"
# Deduplicate React: control-center has its own node_modules/react which creates
# a second React instance at SSR time, causing "useContext null" prerender failures.
# Replace with symlinks to the root pnpm store so both CC and react-dom share one copy.
PNPM_REACT="$ROOT/node_modules/.pnpm/react@18.3.1/node_modules/react"
PNPM_REACT_DOM="$ROOT/node_modules/.pnpm/react-dom@18.3.1_react@18.3.1/node_modules/react-dom"
CC_NM="$ROOT/apps/control-center/node_modules"
mkdir -p "$CC_NM"
if [ -d "$PNPM_REACT" ] && [ ! -L "$CC_NM/react" ]; then
  rm -rf "$CC_NM/react"
  ln -s "$PNPM_REACT" "$CC_NM/react"
  echo "[dedup] Linked control-center/node_modules/react → pnpm store"
fi
if [ -d "$PNPM_REACT_DOM" ] && [ ! -L "$CC_NM/react-dom" ]; then
  rm -rf "$CC_NM/react-dom"
  ln -s "$PNPM_REACT_DOM" "$CC_NM/react-dom"
  echo "[dedup] Linked control-center/node_modules/react-dom → pnpm store"
fi
cd "$ROOT/apps/control-center"
rm -rf .next
# The control center ships its own Next.js (15.5.15) which correctly detects
# the src/ directory layout. Using the root-level Next.js (15.2.9) causes
# validator.ts to be generated with wrong relative paths (../../app/... instead
# of ../../src/app/...), failing the TypeScript type-check phase.
CC_NEXT_BIN="$ROOT/apps/control-center/node_modules/next/dist/bin/next"
if [ ! -f "$CC_NEXT_BIN" ]; then
  echo "[control-center] Own next binary not found, falling back to root binary"
  CC_NEXT_BIN="$NEXT_BIN"
fi
echo "[control-center] Using next binary: $CC_NEXT_BIN"
NODE_OPTIONS="--max-old-space-size=512" node "$CC_NEXT_BIN" build

echo "====== Building .NET services ======"
cd "$ROOT"
if command -v dotnet &>/dev/null; then
  DOTNET_FAIL=0

  build_service() {
    local name="$1"
    local project="$2"
    echo "[dotnet] Building $name..."
    if dotnet build "$project" --configuration Release --verbosity minimal 2>&1; then
      echo "[dotnet] $name — OK"
    else
      echo "[dotnet] $name — FAILED"
      DOTNET_FAIL=$((DOTNET_FAIL + 1))
    fi
  }

  echo "[dotnet] Restoring packages..."
  dotnet restore "$ROOT/LegalSynq.sln" --verbosity minimal 2>&1 || true
  # Flow lives in its own solution — restore its packages separately so
  # the test-project dependencies don't block the service build.
  dotnet restore "$ROOT/apps/services/flow/backend/src/Flow.Api/Flow.Api.csproj" --verbosity minimal 2>&1 || true
  # Support service has its own solution boundary (not in LegalSynq.sln)
  dotnet restore "$ROOT/apps/services/support/Support.Api/Support.Api.csproj" --verbosity minimal 2>&1 || true

  build_service "Gateway"       "$ROOT/apps/gateway/Gateway.Api/Gateway.Api.csproj"
  build_service "Identity"      "$ROOT/apps/services/identity/Identity.Api/Identity.Api.csproj"
  build_service "Fund"          "$ROOT/apps/services/fund/Fund.Api/Fund.Api.csproj"
  build_service "CareConnect"   "$ROOT/apps/services/careconnect/CareConnect.Api/CareConnect.Api.csproj"
  build_service "Documents"     "$ROOT/apps/services/documents/Documents.Api/Documents.Api.csproj"
  build_service "Audit"         "$ROOT/apps/services/audit/PlatformAuditEventService.csproj"
  build_service "Notifications" "$ROOT/apps/services/notifications/Notifications.Api/Notifications.Api.csproj"
  build_service "Liens"         "$ROOT/apps/services/liens/Liens.Api/Liens.Api.csproj"
  build_service "Flow API"      "$ROOT/apps/services/flow/backend/src/Flow.Api/Flow.Api.csproj"
  build_service "Monitoring"    "$ROOT/apps/services/monitoring/Monitoring.Api/Monitoring.Api.csproj"
  build_service "Task"          "$ROOT/apps/services/task/Task.Api/Task.Api.csproj"
  build_service "Tenant"        "$ROOT/apps/services/tenant/Tenant.Api/Tenant.Api.csproj"
  build_service "Support"       "$ROOT/apps/services/support/Support.Api/Support.Api.csproj"

  if [ "$DOTNET_FAIL" -gt 0 ]; then
    echo "[dotnet] ERROR: $DOTNET_FAIL service(s) failed to build"
    exit 1
  else
    echo "[dotnet] All services built successfully"
  fi
else
  echo "[dotnet] WARNING: dotnet SDK not found — .NET services will not be available"
fi

echo "====== Post-build cleanup to reduce image size ======"
# .git, _archived, analysis/exports/downloads, attached_assets, and Replit
# state were already removed in the pre-build cleanup step above.

echo "[cleanup] Removing pnpm content-addressable store..."
rm -rf "$ROOT/.local/share/pnpm" 2>/dev/null || true

echo "[cleanup] Removing NuGet package cache..."
rm -rf "$ROOT/.local/share/NuGet" 2>/dev/null || true

echo "[cleanup] Removing .NET obj directories..."
find "$ROOT/apps" -type d -name obj -exec rm -rf {} + 2>/dev/null || true
find "$ROOT/shared" -type d -name obj -exec rm -rf {} + 2>/dev/null || true

echo "[cleanup] Removing .NET Debug build artifacts..."
find "$ROOT/apps" -path "*/bin/Debug" -type d -exec rm -rf {} + 2>/dev/null || true
find "$ROOT/shared" -path "*/bin/Debug" -type d -exec rm -rf {} + 2>/dev/null || true

echo "[cleanup] Removing test bin/obj..."
find "$ROOT" -path "*Tests/bin" -type d -exec rm -rf {} + 2>/dev/null || true
find "$ROOT" -path "*Tests/obj" -type d -exec rm -rf {} + 2>/dev/null || true

echo "[cleanup] Done"

echo "====== Build complete ======"
