#!/usr/bin/env bash
# LS-FLOW-HARDEN-A1.1 — end-to-end smoke for the A1 atomic ownership endpoints.
#
# What this verifies (per product, against the live dev workflow):
#   1. GET  /api/v1/product-workflows/{slug}/{type}/{id}/{instance}
#      returns 200 with the owned instance, OR returns 404
#      `workflow_instance_not_owned` (proving the ownership guard is wired).
#   2. POST .../advance   — best-effort, requires server-supplied current step.
#   3. Negative probes (when WORKFLOW_INSTANCE_ID__OTHER_TENANT and/or
#      WORKFLOW_INSTANCE_ID__WRONG_PARENT are exported) confirm 404 ownership
#      denial without leaking existence.
#
# Auth — same two modes as p5-e2e.sh:
#   • FLOW_SERVICE_TOKEN_SECRET (+ python3) → HS256 service token.
#   • USER_BEARER → user JWT fallback.
#
# Required env:
#   FLOW_BASE_URL                 default http://localhost:5010
#   TENANT_ID                     tenant uuid
#   WORKFLOW_INSTANCE_ID__LIENS              <uuid>      optional per-product
#   WORKFLOW_INSTANCE_ID__CARECONNECT        <uuid>      optional per-product
#   WORKFLOW_INSTANCE_ID__FUND               <uuid>      optional per-product
#   SOURCE_ENTITY_TYPE__LIENS                default `lien`
#   SOURCE_ENTITY_TYPE__CARECONNECT          default `appointment`
#   SOURCE_ENTITY_TYPE__FUND                 default `fund-deal`
#   SOURCE_ENTITY_ID__LIENS|CARECONNECT|FUND <uuid>      parent entity id
#   WORKFLOW_INSTANCE_ID__OTHER_TENANT       optional negative probe
#   WORKFLOW_INSTANCE_ID__WRONG_PARENT       optional negative probe
#
# Exit code: 0 if every available product probe returns 200 OR a clean 404
# with the canonical `workflow_instance_not_owned` code; non-zero on any
# 5xx, 401, 403, or any 404 missing the canonical code.

set -uo pipefail

FLOW_BASE_URL="${FLOW_BASE_URL:-http://localhost:5010}"
TENANT_ID="${TENANT_ID:-}"
SERVICE_NAME="${SERVICE_NAME:-harden-a1-e2e}"

if [[ -z "${TENANT_ID}" ]]; then
  echo "[harden-a1-e2e] TENANT_ID is required (export TENANT_ID=<uuid>)" >&2
  exit 2
fi

mint_service_token() {
  local actor="${1:-}"
  python3 - "$FLOW_SERVICE_TOKEN_SECRET" "$TENANT_ID" "$SERVICE_NAME" "$actor" <<'PY'
import base64, hmac, hashlib, json, sys, time
secret, tenant_id, service_name, actor = sys.argv[1:5]
def b64u(b): return base64.urlsafe_b64encode(b).rstrip(b"=").decode("ascii")
now = int(time.time())
header  = {"alg": "HS256", "typ": "JWT"}
payload = {
    "sub":  f"service:{service_name}",
    "aud":  "flow-service",
    "iss":  "legalsynq.servicetokens",
    "tid":  tenant_id,
    "tenant_id": tenant_id,
    "role": "service",
    "iat":  now, "nbf": now, "exp": now + 300,
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
    echo "[harden-a1-e2e] No FLOW_SERVICE_TOKEN_SECRET (+python3) or USER_BEARER available." >&2
    exit 2
  fi
}

# Strict classifier (default for positive product probes):
#   • 200 → success.
#   • Anything else → failure (caller owns the seed instance, so 404 is wrong).
classify_strict_owned() {
  local status="$1"
  [[ "$status" == "200" ]]
}

# Permissive classifier (opt-in via HARDEN_E2E_ALLOW_NOT_OWNED=1):
#   • 200 (owned), or
#   • 404 with canonical "workflow_instance_not_owned" code.
# Use only when the caller does not have a confirmed-owned seed available
# and merely wants to confirm the route + ownership guard are reachable.
classify_owned_or_not_owned() {
  local status="$1" body_file="$2"
  if [[ "$status" == "200" ]]; then return 0; fi
  if [[ "$status" == "404" ]] && grep -q '"workflow_instance_not_owned"' "$body_file"; then
    return 0
  fi
  return 1
}

probes_executed=0

probe_product() {
  local label="$1" slug="$2" entity_type="$3" entity_id="$4" instance="$5"
  if [[ -z "$instance" || -z "$entity_id" ]]; then
    echo "[harden-a1-e2e][${label}] skipped — missing instance id or source entity id"
    return 0
  fi
  probes_executed=$((probes_executed+1))
  local auth; auth="$(build_auth_header)" || return $?

  local url="${FLOW_BASE_URL}/api/v1/product-workflows/${slug}/${entity_type}/${entity_id}/${instance}"
  echo "[harden-a1-e2e][${label}] GET ${url}"
  local status
  status="$(curl -sS -o /tmp/harden_get.json -w '%{http_code}' -H "$auth" "$url")"
  echo "  ↳ status=${status} body=$(head -c 240 /tmp/harden_get.json)"

  if [[ "${HARDEN_E2E_ALLOW_NOT_OWNED:-0}" == "1" ]]; then
    if ! classify_owned_or_not_owned "$status" /tmp/harden_get.json; then
      echo "[harden-a1-e2e][${label}] FAIL on GET (status=${status} — expected 200 or 404 workflow_instance_not_owned)" >&2
      return 1
    fi
  else
    if ! classify_strict_owned "$status"; then
      echo "[harden-a1-e2e][${label}] FAIL on GET (status=${status} — expected 200; set HARDEN_E2E_ALLOW_NOT_OWNED=1 to accept 404 workflow_instance_not_owned)" >&2
      return 1
    fi
  fi

  if [[ "$status" == "200" ]]; then
    local current
    current="$(python3 -c 'import json,sys;d=json.load(open("/tmp/harden_get.json"));print(d.get("currentStepKey") or d.get("CurrentStepKey") or "")' 2>/dev/null || true)"
    if [[ -n "$current" ]]; then
      echo "[harden-a1-e2e][${label}] POST .../advance (expected=${current}) — best-effort"
      status="$(curl -sS -o /tmp/harden_adv.json -w '%{http_code}' \
                  -X POST -H "$auth" -H 'Content-Type: application/json' \
                  -d "{\"expectedCurrentStepKey\":\"${current}\",\"tenantId\":\"${TENANT_ID}\"}" \
                  "${url}/advance")"
      echo "  ↳ advance status=${status} body=$(head -c 240 /tmp/harden_adv.json)"
    fi
  fi
}

probe_negative() {
  local label="$1" slug="$2" entity_type="$3" entity_id="$4" instance="$5"
  if [[ -z "$instance" || -z "$entity_id" ]]; then return 0; fi
  local auth; auth="$(build_auth_header)" || return $?
  local url="${FLOW_BASE_URL}/api/v1/product-workflows/${slug}/${entity_type}/${entity_id}/${instance}"
  echo "[harden-a1-e2e][${label}] GET ${url}  (expect 404 + workflow_instance_not_owned)"
  local status
  status="$(curl -sS -o /tmp/harden_neg.json -w '%{http_code}' -H "$auth" "$url")"
  echo "  ↳ status=${status} body=$(head -c 240 /tmp/harden_neg.json)"
  if [[ "$status" != "404" ]] || ! grep -q '"workflow_instance_not_owned"' /tmp/harden_neg.json; then
    echo "[harden-a1-e2e][${label}] FAIL — expected 404 workflow_instance_not_owned" >&2
    return 1
  fi
}

failures=0

probe_product "synqlien"    "synqlien"    "${SOURCE_ENTITY_TYPE__LIENS:-lien}"             \
              "${SOURCE_ENTITY_ID__LIENS:-}"        "${WORKFLOW_INSTANCE_ID__LIENS:-}"        || failures=$((failures+1))
probe_product "careconnect" "careconnect" "${SOURCE_ENTITY_TYPE__CARECONNECT:-appointment}" \
              "${SOURCE_ENTITY_ID__CARECONNECT:-}"  "${WORKFLOW_INSTANCE_ID__CARECONNECT:-}"  || failures=$((failures+1))
probe_product "synqfund"    "synqfund"    "${SOURCE_ENTITY_TYPE__FUND:-fund-deal}"          \
              "${SOURCE_ENTITY_ID__FUND:-}"         "${WORKFLOW_INSTANCE_ID__FUND:-}"         || failures=$((failures+1))

# Optional negative probes — proves the ownership guard refuses cross-tenant
# / wrong-parent traversal at the edge.
probe_negative "wrong-parent" "synqlien" "${SOURCE_ENTITY_TYPE__LIENS:-lien}" \
               "${SOURCE_ENTITY_ID__LIENS:-}" "${WORKFLOW_INSTANCE_ID__WRONG_PARENT:-}" \
               || failures=$((failures+1))
probe_negative "other-tenant" "synqlien" "${SOURCE_ENTITY_TYPE__LIENS:-lien}" \
               "${SOURCE_ENTITY_ID__LIENS:-}" "${WORKFLOW_INSTANCE_ID__OTHER_TENANT:-}" \
               || failures=$((failures+1))

if (( probes_executed == 0 )); then
  echo "[harden-a1-e2e] no product probes executed — set WORKFLOW_INSTANCE_ID__{LIENS,CARECONNECT,FUND} + SOURCE_ENTITY_ID__* to run" >&2
  exit 2
fi
if (( failures > 0 )); then
  echo "[harden-a1-e2e] ${failures} probe(s) failed" >&2
  exit 1
fi
echo "[harden-a1-e2e] OK (${probes_executed} product probe(s) executed)"
