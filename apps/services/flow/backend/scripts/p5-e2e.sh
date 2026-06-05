#!/usr/bin/env bash
# LS-FLOW-MERGE-P5 — end-to-end smoke for the workflow execution surface.
#
# What this verifies (per product, against the live dev workflow):
#   1. A workflow instance is reachable via GET /api/v1/workflow-instances/{id}.
#   2. Its current step is reported via .../current-step.
#   3. Advance / complete are accepted (or correctly rejected with 409 on
#      a stale `expectedCurrentStepKey`).
#
# Auth — two modes, in priority order:
#   • If FLOW_SERVICE_TOKEN_SECRET is exported and `python3` is on PATH,
#     the script mints an HS256 service token directly (matching what
#     BuildingBlocks/ServiceTokenIssuer would produce).
#   • Otherwise, set USER_BEARER to a valid user JWT and the script will
#     fall through to user-bearer auth on the same endpoints.
#
# Required env:
#   FLOW_BASE_URL              default http://localhost:5010 (gateway)
#   TENANT_ID                  tenant uuid (claim `tenant_id` / `tid`)
#   WORKFLOW_INSTANCE_ID       a real instance id to interrogate
#   WORKFLOW_INSTANCE_ID__LIENS|CARECONNECT|FUND  optional — per-product ids
#
# Exit code: 0 if every probed product reaches the GET endpoint with 200.
# Advance/complete are best-effort (state-dependent) and only logged.

set -uo pipefail

FLOW_BASE_URL="${FLOW_BASE_URL:-http://localhost:5010}"
TENANT_ID="${TENANT_ID:-}"
SERVICE_NAME="${SERVICE_NAME:-p5-e2e}"

if [[ -z "${TENANT_ID}" ]]; then
  echo "[p5-e2e] TENANT_ID is required (export TENANT_ID=<uuid>)" >&2
  exit 2
fi

mint_service_token() {
  local actor="${1:-}"
  python3 - "$FLOW_SERVICE_TOKEN_SECRET" "$TENANT_ID" "$SERVICE_NAME" "$actor" <<'PY'
import base64, hmac, hashlib, json, os, sys, time

secret, tenant_id, service_name, actor = sys.argv[1], sys.argv[2], sys.argv[3], sys.argv[4]

def b64u(b): return base64.urlsafe_b64encode(b).rstrip(b"=").decode("ascii")

now = int(time.time())
header = {"alg": "HS256", "typ": "JWT"}
payload = {
    "sub":  f"service:{service_name}",
    "aud":  "flow-service",
    "iss":  "legalsynq.servicetokens",
    "tid":  tenant_id,
    "tenant_id": tenant_id,
    "role": "service",
    "iat":  now,
    "nbf":  now,
    "exp":  now + 300,
}
if actor:
    payload["actor"] = f"user:{actor}"
signing = b64u(json.dumps(header,  separators=(",", ":")).encode()) + "." + \
          b64u(json.dumps(payload, separators=(",", ":")).encode())
sig = hmac.new(secret.encode(), signing.encode(), hashlib.sha256).digest()
print(signing + "." + b64u(sig))
PY
}

build_auth_header() {
  if [[ -n "${FLOW_SERVICE_TOKEN_SECRET:-}" ]] && command -v python3 >/dev/null 2>&1; then
    local tok; tok="$(mint_service_token "${ACTOR_USER_ID:-}")"
    echo "Authorization: Bearer ${tok}"
  elif [[ -n "${USER_BEARER:-}" ]]; then
    echo "Authorization: Bearer ${USER_BEARER}"
  else
    echo "[p5-e2e] No FLOW_SERVICE_TOKEN_SECRET (+python3) or USER_BEARER available." >&2
    exit 2
  fi
}

probe_instance() {
  local label="$1" inst="$2"
  if [[ -z "$inst" ]]; then
    echo "[p5-e2e][${label}] skipped — no instance id provided"
    return 0
  fi

  local auth; auth="$(build_auth_header)" || return $?

  echo "[p5-e2e][${label}] GET ${FLOW_BASE_URL}/api/v1/workflow-instances/${inst}"
  local body status
  body="$(curl -sS -o /tmp/p5_get.json -w '%{http_code}' \
            -H "$auth" "${FLOW_BASE_URL}/api/v1/workflow-instances/${inst}")" || true
  status="$body"
  echo "  ↳ status=${status} body=$(head -c 240 /tmp/p5_get.json)"
  if [[ "$status" != "200" ]]; then
    echo "[p5-e2e][${label}] FAIL on GET" >&2
    return 1
  fi

  echo "[p5-e2e][${label}] GET .../current-step"
  status="$(curl -sS -o /tmp/p5_step.json -w '%{http_code}' \
              -H "$auth" "${FLOW_BASE_URL}/api/v1/workflow-instances/${inst}/current-step")"
  echo "  ↳ status=${status} body=$(head -c 240 /tmp/p5_step.json)"

  local current
  current="$(python3 -c 'import json,sys;print(json.load(open("/tmp/p5_step.json")).get("currentStepKey") or "")' 2>/dev/null || true)"
  if [[ -n "$current" ]]; then
    echo "[p5-e2e][${label}] POST .../advance (expected=${current}) — best-effort"
    status="$(curl -sS -o /tmp/p5_adv.json -w '%{http_code}' \
                -X POST -H "$auth" -H 'Content-Type: application/json' \
                -d "{\"expectedCurrentStepKey\":\"${current}\"}" \
                "${FLOW_BASE_URL}/api/v1/workflow-instances/${inst}/advance")"
    echo "  ↳ status=${status} body=$(head -c 240 /tmp/p5_adv.json)"
  fi
}

failures=0
probe_instance "synqlien"    "${WORKFLOW_INSTANCE_ID__LIENS:-${WORKFLOW_INSTANCE_ID:-}}"        || failures=$((failures+1))
probe_instance "careconnect" "${WORKFLOW_INSTANCE_ID__CARECONNECT:-${WORKFLOW_INSTANCE_ID:-}}"  || failures=$((failures+1))
probe_instance "synqfund"    "${WORKFLOW_INSTANCE_ID__FUND:-${WORKFLOW_INSTANCE_ID:-}}"         || failures=$((failures+1))

if (( failures > 0 )); then
  echo "[p5-e2e] ${failures} probe(s) failed" >&2
  exit 1
fi
echo "[p5-e2e] OK"
