#!/usr/bin/env bash
# Shared helper functions for scripts/run-prod.sh.
# Sourced by run-prod.sh at startup and by tests in scripts/tests/.
#
# All functions are pure and side-effect-free so they can be tested in
# isolation without a running application.

# _svc_label_for <csproj-basename>
#   Maps a .csproj filename (without extension) to the human-readable service
#   label that appears in log lines and crash-detection messages.
#   The wildcard arm falls back to the raw basename, which is the correct
#   behaviour for any new service added to BUILD_PROJECTS.
_svc_label_for() {
  local svc_name="$1"
  case "$svc_name" in
    PlatformAuditEventService) echo "Audit" ;;
    Notifications.Api)         echo "Notifications" ;;
    Flow.Api)                  echo "Flow API" ;;
    Gateway.Api)               echo "Gateway" ;;
    Identity.Api)              echo "Identity API" ;;
    Fund.Api)                  echo "Fund API" ;;
    CareConnect.Api)           echo "CareConnect" ;;
    Documents.Api)             echo "Documents" ;;
    Liens.Api)                 echo "Liens" ;;
    Monitoring.Api)            echo "Monitoring" ;;
    Task.Api)                  echo "Task" ;;
    Tenant.Api)                echo "Tenant" ;;
    Support.Api)               echo "Support" ;;
    *)                         echo "$svc_name" ;;
  esac
}

# _run_crash_monitor
#   Polls the global SVC_PIDS / SVC_NAMES arrays for crashed processes.
#   Emits one "[dotnet] ERROR: …" line per crashed service and returns 1 as
#   soon as the first crash is detected.
#
#   Environment overrides (for tests — do not set in production):
#     CRASH_MONITOR_WINDOW  Number of poll ticks        (default: 10)
#     CRASH_MONITOR_SLEEP   Seconds between ticks       (default: 0.5)
#     CRASH_MONITOR_TICK_MS Milliseconds per tick label (default: 500)
_run_crash_monitor() {
  local window="${CRASH_MONITOR_WINDOW:-10}"
  local interval="${CRASH_MONITOR_SLEEP:-0.5}"
  local tick_ms="${CRASH_MONITOR_TICK_MS:-500}"
  local tick idx pid name
  for ((tick = 1; tick <= window; tick++)); do
    sleep "$interval"
    for idx in "${!SVC_PIDS[@]}"; do
      pid="${SVC_PIDS[$idx]}"
      name="${SVC_NAMES[$idx]}"
      if ! kill -0 "$pid" 2>/dev/null; then
        echo "[dotnet] ERROR: ${name} (pid ${pid}) crashed $((tick * tick_ms))ms after launch"
        return 1
      fi
    done
  done
  return 0
}
