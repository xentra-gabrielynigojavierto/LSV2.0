#!/usr/bin/env bash
# Tests for the launch_svc missing-binary error path.
#
# Sources the real implementation from scripts/lib/dotnet-helpers.sh so that
# any change to the production function is immediately reflected here.
# Run with:   bash scripts/tests/test-launch-svc.sh

set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../lib/dotnet-helpers.sh
source "$SCRIPT_DIR/../lib/dotnet-helpers.sh"

PASS=0
FAIL=0

pass() { echo "  PASS: $1"; PASS=$((PASS + 1)); }
fail() { echo "  FAIL: $1"; FAIL=$((FAIL + 1)); }

# ── Helpers ───────────────────────────────────────────────────────────────────
# Run launch_svc in an isolated subshell; capture stdout+stderr and exit code
# in the OUTPUT and EXIT_CODE variables.
run_launch_svc() {
  set +e
  OUTPUT="$( (launch_svc "$@") 2>&1 )"
  EXIT_CODE=$?
  set -e
}

# ── Fixtures ──────────────────────────────────────────────────────────────────
TMP_DIR="$(mktemp -d)"
FAKE_CSPROJ="$TMP_DIR/MyService.csproj"
touch "$FAKE_CSPROJ"

cleanup() { rm -rf "$TMP_DIR"; }
trap cleanup EXIT

# ── Test 1: non-zero exit code when DLL is missing ────────────────────────────
echo "Test 1: exits non-zero when the DLL is missing"
run_launch_svc "MyService" "$FAKE_CSPROJ"
if [ "$EXIT_CODE" -ne 0 ]; then
  pass "exit code is non-zero (got $EXIT_CODE)"
else
  fail "expected non-zero exit code, got $EXIT_CODE"
fi

# ── Test 2: error message contains the service name ───────────────────────────
echo "Test 2: error message contains the service name"
if echo "$OUTPUT" | grep -qF "MyService"; then
  pass "error message contains service name 'MyService'"
else
  fail "error message missing service name; got: $OUTPUT"
fi

# ── Test 3: error message contains the expected DLL path ─────────────────────
echo "Test 3: error message contains the DLL path"
EXPECTED_DLL="$TMP_DIR/bin/Release/net10.0/MyService.dll"
if echo "$OUTPUT" | grep -qF "$EXPECTED_DLL"; then
  pass "error message contains DLL path '$EXPECTED_DLL'"
else
  fail "error message missing DLL path '$EXPECTED_DLL'; got: $OUTPUT"
fi

# ── Test 4: exits zero and prints no ERROR when the DLL is present ────────────
echo "Test 4: exits zero and prints no error when the DLL is present"
mkdir -p "$TMP_DIR/bin/Release/net10.0"
touch "$TMP_DIR/bin/Release/net10.0/MyService.dll"
# Pass 'true' as the command prefix so the background process does not try
# to invoke the real 'dotnet' binary (which may not be present on the runner).
run_launch_svc "MyService" "$FAKE_CSPROJ" true
if echo "$OUTPUT" | grep -qF "ERROR"; then
  fail "unexpected ERROR in output when DLL is present: $OUTPUT"
elif [ "$EXIT_CODE" -ne 0 ]; then
  fail "expected exit 0 when DLL is present, got $EXIT_CODE"
else
  pass "no error and exit 0 when DLL is present"
fi

# ── Summary ───────────────────────────────────────────────────────────────────
echo ""
echo "Results: $PASS passed, $FAIL failed"
[ "$FAIL" -eq 0 ] && exit 0 || exit 1
