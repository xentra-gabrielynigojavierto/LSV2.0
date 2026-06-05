#!/usr/bin/env bash
# Tests for the crash-detection helpers in scripts/_startup-helpers.sh,
# which are sourced and used by scripts/run-prod.sh.
#
# Covers two things:
#   1. _svc_label_for: the .csproj-to-display-label mapping, including the
#      wildcard fallback that is used for any service not in the named list.
#   2. _run_crash_monitor: the polling loop that emits the "[dotnet] ERROR: …"
#      line when a service exits during the crash-detection window.
#
# No .NET build or Docker daemon is required.  The crash-monitor tests use
# a short poll window (CRASH_MONITOR_WINDOW=1 CRASH_MONITOR_SLEEP=0.1) so
# the suite completes in well under a second.

set -euo pipefail

HELPERS="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)/_startup-helpers.sh"
if [ ! -f "$HELPERS" ]; then
  echo "ERROR: $HELPERS not found" >&2
  exit 1
fi
# shellcheck source=scripts/_startup-helpers.sh
source "$HELPERS"

PASS=0
FAIL=0

_pass() { echo "  PASS: $1"; PASS=$(( PASS + 1 )); }
_fail() { echo "  FAIL: $1"; FAIL=$(( FAIL + 1 )); }

# ── helper: start a background process that exits immediately ─────────────────
_dead_pid() {
  (exit 1) &
  local p=$!
  wait "$p" 2>/dev/null || true
  echo "$p"
}

# ── "live" PID: the test script itself is guaranteed to be running ────────────
SELF_PID=$$

# ─────────────────────────────────────────────────────────────────────────────
# Part 1 — _svc_label_for label-mapping
# ─────────────────────────────────────────────────────────────────────────────
echo "=== _svc_label_for: named service mapping ==="

label="$(_svc_label_for "Flow.Api")"
if [ "$label" = "Flow API" ]; then
  _pass "Flow.Api maps to 'Flow API'"
else
  _fail "Flow.Api: expected 'Flow API', got '$label'"
fi

label="$(_svc_label_for "PlatformAuditEventService")"
if [ "$label" = "Audit" ]; then
  _pass "PlatformAuditEventService maps to 'Audit'"
else
  _fail "PlatformAuditEventService: expected 'Audit', got '$label'"
fi

echo "=== _svc_label_for: wildcard fallback ==="

label="$(_svc_label_for "MyNewService.Api")"
if [ "$label" = "MyNewService.Api" ]; then
  _pass "unknown service falls back to its own name"
else
  _fail "wildcard fallback: expected 'MyNewService.Api', got '$label'"
fi

# ─────────────────────────────────────────────────────────────────────────────
# Part 2 — _run_crash_monitor error message
# Overrides make the monitor complete in ~0.1 s (1 tick × 0.1 s sleep).
# ─────────────────────────────────────────────────────────────────────────────
export CRASH_MONITOR_WINDOW=1
export CRASH_MONITOR_SLEEP=0.1
export CRASH_MONITOR_TICK_MS=100

echo "=== _run_crash_monitor: named service 'Flow API' appears in crash error ==="
DEAD=$(_dead_pid)
SVC_PIDS=("$DEAD")
SVC_NAMES=("Flow API")
output=$(_run_crash_monitor 2>&1 || true)
if echo "$output" | grep -q "Flow API"; then
  _pass "error line contains 'Flow API'"
else
  _fail "error line missing 'Flow API' — got: $output"
fi
if echo "$output" | grep -q "(pid $DEAD)"; then
  _pass "error line contains pid $DEAD"
else
  _fail "error line missing pid $DEAD — got: $output"
fi
if echo "$output" | grep -q "crashed.*ms after launch"; then
  _pass "error line matches 'crashed …ms after launch' format"
else
  _fail "error line does not match expected format — got: $output"
fi

echo "=== _run_crash_monitor: wildcard fallback label in crash error ==="
DEAD=$(_dead_pid)
SVC_PIDS=("$DEAD")
SVC_NAMES=("$(_svc_label_for "MyNewService.Api")")
output=$(_run_crash_monitor 2>&1 || true)
if echo "$output" | grep -q "MyNewService.Api"; then
  _pass "error line contains wildcard label 'MyNewService.Api'"
else
  _fail "error line missing wildcard label — got: $output"
fi

echo "=== _run_crash_monitor: healthy service produces no output ==="
SVC_PIDS=("$SELF_PID")
SVC_NAMES=("Flow API")
output=$(_run_crash_monitor 2>&1 || true)
if [ -z "$output" ]; then
  _pass "no error output for a running service"
else
  _fail "unexpected error output for running service — got: $output"
fi

echo "=== _run_crash_monitor: only the crashed service is named ==="
DEAD=$(_dead_pid)
SVC_PIDS=("$SELF_PID" "$DEAD")
SVC_NAMES=("Flow API" "Audit")
output=$(_run_crash_monitor 2>&1 || true)
if echo "$output" | grep -q "Audit"; then
  _pass "crashed service 'Audit' appears in output"
else
  _fail "crashed service 'Audit' missing — got: $output"
fi
if ! echo "$output" | grep -q "Flow API"; then
  _pass "healthy service 'Flow API' not reported as crashed"
else
  _fail "healthy service 'Flow API' incorrectly reported as crashed — got: $output"
fi

# ─────────────────────────────────────────────────────────────────────────────
echo ""
echo "Results: $PASS passed, $FAIL failed"
if [ "$FAIL" -gt 0 ]; then
  exit 1
fi
