#!/usr/bin/env bash
# Tests for the _probe_svc health-probe helper — Node.js services.
#
# Extracts the real _probe_svc implementation from scripts/run-prod.sh so that
# any change to the production function is immediately reflected here.
# Run with:   bash scripts/tests/test-nodejs-probes.sh

set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
RUN_PROD="$SCRIPT_DIR/../run-prod.sh"

# ── Source only the _probe_svc function from run-prod.sh ──────────────────────
eval "$(sed -n '/^_probe_svc()/,/^}/p' "$RUN_PROD")"

PASS=0
FAIL=0

pass() { echo "  PASS: $1"; PASS=$((PASS + 1)); }
fail() { echo "  FAIL: $1"; FAIL=$((FAIL + 1)); }

# ── Stubs ─────────────────────────────────────────────────────────────────────
# Override curl to always fail (simulate an unresponsive service).
curl() { return 1; }
export -f curl

# Override sleep to be a no-op so the probe loop completes instantly.
sleep() { return 0; }
export -f sleep

# ── Helper ────────────────────────────────────────────────────────────────────
# Run _probe_svc, wait for the background probe to finish, and capture output.
# Sets OUTPUT (full output) and WARNING_LINE (the WARNING line only).
run_probe() {
  set +e
  OUTPUT="$( _probe_svc "$@"; wait )"
  WARNING_LINE="$(echo "$OUTPUT" | grep 'WARNING' | tail -n1)"
  set -e
}

# ── Test 1: WARNING line emitted for Web (:3050 /api/health) ─────────────────
echo "Test 1: WARNING line emitted when Web (:3050) never responds"
run_probe "Web" 3050 /api/health
if [ -n "$WARNING_LINE" ]; then
  pass "WARNING line is present"
else
  fail "expected a WARNING line in output; got: $OUTPUT"
fi
if echo "$WARNING_LINE" | grep -qF "Web"; then
  pass "WARNING line names the service (Web)"
else
  fail "WARNING line missing 'Web'; got: $WARNING_LINE"
fi
if echo "$WARNING_LINE" | grep -qF "3050"; then
  pass "WARNING line includes port 3050"
else
  fail "WARNING line missing port 3050; got: $WARNING_LINE"
fi

# ── Test 2: WARNING line emitted for Proxy (:5000 /health) ───────────────────
echo "Test 2: WARNING line emitted when Proxy (:5000) never responds"
run_probe "Proxy" 5000 /health
if [ -n "$WARNING_LINE" ]; then
  pass "WARNING line is present"
else
  fail "expected a WARNING line in output; got: $OUTPUT"
fi
if echo "$WARNING_LINE" | grep -qF "Proxy"; then
  pass "WARNING line names the service (Proxy)"
else
  fail "WARNING line missing 'Proxy'; got: $WARNING_LINE"
fi
if echo "$WARNING_LINE" | grep -qF "5000"; then
  pass "WARNING line includes port 5000"
else
  fail "WARNING line missing port 5000; got: $WARNING_LINE"
fi

# ── Test 3: WARNING line emitted for CC (:5004 /api/health) ──────────────────
echo "Test 3: WARNING line emitted when CC (:5004) never responds"
run_probe "CC" 5004 /api/health
if [ -n "$WARNING_LINE" ]; then
  pass "WARNING line is present"
else
  fail "expected a WARNING line in output; got: $OUTPUT"
fi
if echo "$WARNING_LINE" | grep -qF "CC"; then
  pass "WARNING line names the service (CC)"
else
  fail "WARNING line missing 'CC'; got: $WARNING_LINE"
fi
if echo "$WARNING_LINE" | grep -qF "5004"; then
  pass "WARNING line includes port 5004"
else
  fail "WARNING line missing port 5004; got: $WARNING_LINE"
fi

# ── Test 4: WARNING line emitted for Artifacts (:5020 /api/health) ───────────
echo "Test 4: WARNING line emitted when Artifacts (:5020) never responds"
run_probe "Artifacts" 5020 /api/health
if [ -n "$WARNING_LINE" ]; then
  pass "WARNING line is present"
else
  fail "expected a WARNING line in output; got: $OUTPUT"
fi
if echo "$WARNING_LINE" | grep -qF "Artifacts"; then
  pass "WARNING line names the service (Artifacts)"
else
  fail "WARNING line missing 'Artifacts'; got: $WARNING_LINE"
fi
if echo "$WARNING_LINE" | grep -qF "5020"; then
  pass "WARNING line includes port 5020"
else
  fail "WARNING line missing port 5020; got: $WARNING_LINE"
fi

# ── Test 5: healthy service does NOT emit a WARNING ───────────────────────────
echo "Test 5: no WARNING emitted when Web responds successfully"
# Override curl to succeed for this one test.
curl() { return 0; }
export -f curl
run_probe "Web" 3050 /api/health
if [ -n "$WARNING_LINE" ]; then
  fail "unexpected WARNING for a healthy service; WARNING line: $WARNING_LINE"
else
  pass "no WARNING line when service is healthy"
fi
if echo "$OUTPUT" | grep -q "healthy after"; then
  pass "'healthy after' message is present"
else
  fail "'healthy after' message missing; got: $OUTPUT"
fi
# Restore failing curl for any future tests.
curl() { return 1; }
export -f curl

# ── Summary ───────────────────────────────────────────────────────────────────
echo ""
echo "Results: $PASS passed, $FAIL failed"
[ "$FAIL" -eq 0 ] && exit 0 || exit 1
