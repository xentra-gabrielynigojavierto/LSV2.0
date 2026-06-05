#!/usr/bin/env bash
# Flow environment-variable pre-flight checks.
# Sourced by scripts/run-prod.sh at runtime and by tests in scripts/tests/.

# check_flow_env_vars
#
# Sets SKIP_FLOW=1 and emits [flow] WARNING when required Flow environment
# variables are absent, so the operator gets a clear message before any
# service process is spawned. Flow is skipped gracefully rather than
# aborting all other services.
#
# Required variables (either form accepted for the database connection):
#   FLOW_DB_CONNECTION_STRING  — or —  ConnectionStrings__FlowDb
#   FLOW_SERVICE_TOKEN_SECRET  (min 32 chars — HS256 key for service-to-service
#                               auth; read by BuildingBlocks AddServiceTokenBearer)
#
# Optional variables (warnings only — missing values degrade functionality
# but do not prevent startup):
#   Jwt__SigningKey, Jwt__Issuer, Jwt__Audience
#   Cors__AllowedOrigins
#
# After this function returns, callers should check $SKIP_FLOW and skip
# launching Flow when it equals 1.
check_flow_env_vars() {
  SKIP_FLOW=0

  # Flow.Infrastructure.DependencyInjection reads FLOW_DB_CONNECTION_STRING
  # first, then ConnectionStrings:FlowDb. Accept either form here so the
  # check mirrors the actual runtime resolution order.
  if [ -z "${FLOW_DB_CONNECTION_STRING:-}" ] && [ -z "${ConnectionStrings__FlowDb:-}" ]; then
    echo "[flow] WARNING: neither FLOW_DB_CONNECTION_STRING nor ConnectionStrings__FlowDb is set — Flow will be skipped"
    SKIP_FLOW=1
  fi

  # FLOW_SERVICE_TOKEN_SECRET — required in Production; Flow's Program.cs
  # calls AddServiceTokenBearer with failFastIfMissingSecret:true outside
  # Development, so startup crashes if this secret is absent or shorter than
  # 32 characters. The BuildingBlocks library reads this env var directly
  # (ServiceTokenAuthenticationDefaults.SecretEnvVar).
  if [ -z "${FLOW_SERVICE_TOKEN_SECRET:-}" ]; then
    echo "[flow] WARNING: FLOW_SERVICE_TOKEN_SECRET is not set — Flow will be skipped (failFastIfMissingSecret is true in Production)"
    SKIP_FLOW=1
  fi

  # JWT keys are optional — Program.cs registers a no-op JWT bearer when
  # Jwt:SigningKey is absent (user tokens simply won't validate), so these
  # are warnings rather than hard failures.
  for key in "Jwt__SigningKey" "Jwt__Issuer" "Jwt__Audience"; do
    if [ -z "${!key:-}" ]; then
      echo "[flow] WARNING: $key is not set — user-token auth will be disabled"
    fi
  done

  # CORS is not required for service startup but an empty AllowedOrigins
  # means browser clients will be blocked by CORS policy.
  if [ -z "${Cors__AllowedOrigins:-}" ]; then
    echo "[flow] WARNING: Cors__AllowedOrigins is not set — browser cross-origin requests to Flow will be rejected"
  fi
}
