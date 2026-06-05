#!/usr/bin/env bash
# Tests for the Flow env-var pre-flight block in scripts/run-prod.sh.
#
# Sources scripts/lib/flow-preflight.sh directly so that any change to the
# production function is immediately reflected here.
# Run with:   bash scripts/tests/test-run-prod-preflight.sh

set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../lib/flow-preflight.sh
source "$SCRIPT_DIR/../lib/flow-preflight.sh"

PASS=0
FAIL=0

pass() { echo "  PASS: $1"; PASS=$((PASS + 1)); }
fail() { echo "  FAIL: $1"; FAIL=$((FAIL + 1)); }

# ── Helper ─────────────────────────────────────────────────────────────────────
# Run check_flow_env_vars in a clean subshell with a controlled environment so
# variables set in earlier tests never bleed into later ones.
# Sets OUTPUT, EXIT_CODE, and SKIP_FLOW_VALUE on return.
run_preflight() {
  set +e
  OUTPUT="$(env -i bash -c "
    source '$SCRIPT_DIR/../lib/flow-preflight.sh'
    $(printf '%s\n' "$@")
    check_flow_env_vars
    echo \"__SKIP_FLOW=\$SKIP_FLOW\"
  " 2>&1)"
  EXIT_CODE=$?
  SKIP_FLOW_VALUE="$(echo "$OUTPUT" | grep '^__SKIP_FLOW=' | cut -d= -f2)"
  OUTPUT="$(echo "$OUTPUT" | grep -v '^__SKIP_FLOW=')"
  set -e
}

# ── Test 1: both DB vars absent → SKIP_FLOW=1 ─────────────────────────────────
echo "Test 1: sets SKIP_FLOW=1 when DB connection vars are absent"
run_preflight \
  "export ServiceToken__SigningKey=dummy"
if [ "$SKIP_FLOW_VALUE" = "1" ]; then
  pass "SKIP_FLOW=1 when DB vars absent"
else
  fail "expected SKIP_FLOW=1, got '$SKIP_FLOW_VALUE'"
fi

# ── Test 2: warning message mentions the missing DB vars ──────────────────────
echo "Test 2: warning message names the missing DB vars"
if echo "$OUTPUT" | grep -qF "FLOW_DB_CONNECTION_STRING"; then
  pass "warning message mentions FLOW_DB_CONNECTION_STRING"
else
  fail "warning message missing FLOW_DB_CONNECTION_STRING; got: $OUTPUT"
fi
if echo "$OUTPUT" | grep -qF "ConnectionStrings__FlowDb"; then
  pass "warning message mentions ConnectionStrings__FlowDb"
else
  fail "warning message missing ConnectionStrings__FlowDb; got: $OUTPUT"
fi

# ── Test 3: ServiceToken__SigningKey absent → SKIP_FLOW=1 ─────────────────────
echo "Test 3: sets SKIP_FLOW=1 when ServiceToken__SigningKey is absent"
run_preflight \
  "export FLOW_DB_CONNECTION_STRING=dummy"
if [ "$SKIP_FLOW_VALUE" = "1" ]; then
  pass "SKIP_FLOW=1 when ServiceToken__SigningKey absent"
else
  fail "expected SKIP_FLOW=1, got '$SKIP_FLOW_VALUE'"
fi

# ── Test 4: warning message mentions ServiceToken__SigningKey ──────────────────
echo "Test 4: warning message names ServiceToken__SigningKey"
if echo "$OUTPUT" | grep -qF "ServiceToken__SigningKey"; then
  pass "warning message mentions ServiceToken__SigningKey"
else
  fail "warning message missing ServiceToken__SigningKey; got: $OUTPUT"
fi

# ── Test 5: FLOW_DB_CONNECTION_STRING + SigningKey set → SKIP_FLOW=0 ──────────
echo "Test 5: sets SKIP_FLOW=0 when FLOW_DB_CONNECTION_STRING and SigningKey are set"
run_preflight \
  "export FLOW_DB_CONNECTION_STRING=dummy" \
  "export ServiceToken__SigningKey=dummy"
if [ "$EXIT_CODE" -ne 0 ]; then
  fail "expected exit 0, got $EXIT_CODE"
elif [ "$SKIP_FLOW_VALUE" = "0" ]; then
  pass "SKIP_FLOW=0 when all required vars are set"
else
  fail "expected SKIP_FLOW=0, got '$SKIP_FLOW_VALUE'"
fi

# ── Test 6: ConnectionStrings__FlowDb accepted as DB var ──────────────────────
echo "Test 6: ConnectionStrings__FlowDb is accepted as an alternative DB var"
run_preflight \
  "export ConnectionStrings__FlowDb=dummy" \
  "export ServiceToken__SigningKey=dummy"
if [ "$EXIT_CODE" -ne 0 ]; then
  fail "expected exit 0 when ConnectionStrings__FlowDb is set, got $EXIT_CODE"
elif [ "$SKIP_FLOW_VALUE" = "0" ]; then
  pass "SKIP_FLOW=0 with ConnectionStrings__FlowDb"
else
  fail "expected SKIP_FLOW=0 with ConnectionStrings__FlowDb, got '$SKIP_FLOW_VALUE'"
fi

# ── Test 7: both required vars absent → SKIP_FLOW=1 ──────────────────────────
echo "Test 7: sets SKIP_FLOW=1 when both required vars are absent"
run_preflight
if [ "$SKIP_FLOW_VALUE" = "1" ]; then
  pass "SKIP_FLOW=1 when both vars absent"
else
  fail "expected SKIP_FLOW=1 when both vars absent, got '$SKIP_FLOW_VALUE'"
fi

# ── Test 8: clean run does not exit non-zero ───────────────────────────────────
echo "Test 8: exits zero when all required vars are present"
run_preflight \
  "export FLOW_DB_CONNECTION_STRING=dummy" \
  "export ServiceToken__SigningKey=dummy"
if [ "$EXIT_CODE" -ne 0 ]; then
  fail "expected exit 0, got $EXIT_CODE"
else
  pass "exit 0 when all required vars are set"
fi

# ── Summary ───────────────────────────────────────────────────────────────────
echo ""
echo "Results: $PASS passed, $FAIL failed"
[ "$FAIL" -eq 0 ] && exit 0 || exit 1
