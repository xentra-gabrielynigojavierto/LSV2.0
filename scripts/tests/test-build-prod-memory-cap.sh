#!/usr/bin/env bash
# Asserts that both build invocations in scripts/build-prod.sh carry the
# NODE_OPTIONS="--max-old-space-size= heap cap that prevents SIGBUS/OOM
# crashes in memory-constrained containers.
#
# Run with:  bash scripts/tests/test-build-prod-memory-cap.sh

set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUILD_SCRIPT="$SCRIPT_DIR/../build-prod.sh"

PASS=0
FAIL=0

pass() { echo "  PASS: $1"; PASS=$((PASS + 1)); }
fail() { echo "  FAIL: $1"; FAIL=$((FAIL + 1)); }

# ── Helper ─────────────────────────────────────────────────────────────────────
# Count lines in build-prod.sh that both set NODE_OPTIONS with the heap cap
# AND invoke `node` on the same line.
lines_with_cap() {
  grep -c 'NODE_OPTIONS="--max-old-space-size=[0-9]\+.*node ' "$BUILD_SCRIPT" || true
}

# ── Test 1: build script exists ────────────────────────────────────────────────
echo "Test 1: build-prod.sh exists"
if [ -f "$BUILD_SCRIPT" ]; then
  pass "scripts/build-prod.sh found"
else
  fail "scripts/build-prod.sh not found at $BUILD_SCRIPT"
fi

# ── Test 2: web-app build carries the memory cap ──────────────────────────────
echo "Test 2: web-app build line includes NODE_OPTIONS heap cap"
if grep -q 'NODE_OPTIONS="--max-old-space-size=' "$BUILD_SCRIPT" && \
   grep 'NODE_OPTIONS="--max-old-space-size=' "$BUILD_SCRIPT" | grep -q 'NEXT_PUBLIC_ENV=production'; then
  pass "web-app build line has NODE_OPTIONS heap cap"
else
  fail "web-app build line is missing NODE_OPTIONS=\"--max-old-space-size=\" in $BUILD_SCRIPT"
fi

# ── Test 3: control-center build carries the memory cap ───────────────────────
# Match the control-center build line by its distinctive variable ($CC_NEXT_BIN)
# rather than by the absence of web-app vars, so the check stays correct even
# if the control-center command ever gains extra env vars.
echo "Test 3: control-center build line includes NODE_OPTIONS heap cap"
if grep -q 'NODE_OPTIONS="--max-old-space-size=.*CC_NEXT_BIN' "$BUILD_SCRIPT" || \
   grep -q 'NODE_OPTIONS="--max-old-space-size=.*\$CC_NEXT_BIN' "$BUILD_SCRIPT"; then
  pass "control-center build line has NODE_OPTIONS heap cap"
else
  fail "control-center build line is missing NODE_OPTIONS=\"--max-old-space-size=\" in $BUILD_SCRIPT"
fi

# ── Test 4: at least two distinct build lines carry the cap ───────────────────
echo "Test 4: both build invocations (web-app and control-center) have the cap"
COUNT="$(lines_with_cap)"
if [ "$COUNT" -ge 2 ]; then
  pass "found $COUNT build line(s) with NODE_OPTIONS heap cap (expected >= 2)"
else
  fail "found only $COUNT build line(s) with NODE_OPTIONS heap cap; expected >= 2"
fi

# ── Test 5: cap value is non-zero ─────────────────────────────────────────────
echo "Test 5: heap cap value is a positive integer"
CAP_VALUES="$(grep -o 'NODE_OPTIONS="--max-old-space-size=[0-9]\+"' "$BUILD_SCRIPT" \
  | grep -o '[0-9]\+' || true)"
ALL_POSITIVE=true
for v in $CAP_VALUES; do
  if [ "$v" -le 0 ] 2>/dev/null; then
    ALL_POSITIVE=false
    fail "heap cap value '$v' is not a positive integer"
  fi
done
if [ "$ALL_POSITIVE" = "true" ] && [ -n "$CAP_VALUES" ]; then
  pass "all heap cap values are positive: $(echo "$CAP_VALUES" | tr '\n' ' ')"
elif [ -z "$CAP_VALUES" ]; then
  fail "no NODE_OPTIONS heap cap values found"
fi

# ── Summary ───────────────────────────────────────────────────────────────────
echo ""
echo "Results: $PASS passed, $FAIL failed"
[ "$FAIL" -eq 0 ] && exit 0 || exit 1
