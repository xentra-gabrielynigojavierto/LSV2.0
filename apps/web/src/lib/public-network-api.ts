/**
 * CC2-INT-B07 — Server-side public network API helpers.
 * TENANT-STABILIZATION — Tenant resolution switched from Identity to Tenant service.
 * BLK-SEC-02-02 — Trust boundary hardening: server-side calls now sign X-Tenant-Id.
 *
 * Used exclusively by Server Components (e.g., /network/page.tsx).
 * These functions call the CareConnect backend directly via the gateway,
 * passing the X-Tenant-Id resolved from the tenant subdomain.
 * No authentication token is required — endpoints are AllowAnonymous.
 *
 * Resolution: GET /tenant/api/v1/public/resolve/by-code/{code}
 *   Previously called /identity/api/tenants/current/branding.
 *   Switched to Tenant service as the canonical resolution source.
 */

import { createHmac } from 'crypto';

const GATEWAY_URL             = process.env.GATEWAY_URL ?? 'http://127.0.0.1:5010';
// Canonical secret — matches PublicTrustBoundary:InternalRequestSecret in .NET config.
// Prefer this over the legacy INTERNAL_REQUEST_SECRET alias so that deployed
// environments always use the value configured in the Replit secrets / env,
// rather than a dev-only value that may be present in .env.local.
const INTERNAL_REQUEST_SECRET =
  process.env['PublicTrustBoundary__InternalRequestSecret'] ??
  process.env.INTERNAL_REQUEST_SECRET ??
  '';

/**
 * BLK-SEC-02-02: Computes HMAC-SHA256(tenantId, secret) as a base64 string.
 * Sent as X-Tenant-Id-Sig so CareConnect can verify the tenant ID was resolved
 * server-side and not injected by an untrusted caller (Layer 2 of the trust boundary).
 * Returns empty string when the secret is not configured (dev fallback — validation
 * is disabled on the CareConnect side when the secret is absent).
 */
function signTenantId(tenantId: string): string {
  if (!INTERNAL_REQUEST_SECRET) return '';
  return createHmac('sha256', INTERNAL_REQUEST_SECRET).update(tenantId).digest('base64');
}

/**
 * Builds the headers required by the public CareConnect trust boundary.
 * X-Tenant-Id       — the resolved tenant GUID
 * X-Tenant-Id-Sig   — HMAC-SHA256 signature (Layer 2 of BLK-SEC-02-02)
 * The gateway injects X-Internal-Gateway-Secret automatically (Layer 1).
 */
function publicHeaders(tenantId: string): Record<string, string> {
  const sig = signTenantId(tenantId);
  return {
    'X-Tenant-Id': tenantId,
    ...(sig ? { 'X-Tenant-Id-Sig': sig } : {}),
  };
}

export interface PublicNetworkSummary {
  id:            string;
  name:          string;
  description:   string;
  providerCount: number;
}

export interface PublicProviderItem {
  id:               string;
  name:             string;
  organizationName: string | null;
  phone:            string;
  city:             string;
  state:            string;
  postalCode:       string;
  isActive:         boolean;
  acceptingReferrals: boolean;
  accessStage:      string;
  primaryCategory:  string | null;
}

export interface PublicProviderMarker {
  id:               string;
  name:             string;
  organizationName: string | null;
  city:             string;
  state:            string;
  acceptingReferrals: boolean;
  latitude:         number;
  longitude:        number;
}

export interface PublicNetworkDetail {
  networkId:          string;
  networkName:        string;
  networkDescription: string;
  providers:          PublicProviderItem[];
  markers:            PublicProviderMarker[];
}

export interface ResolvedTenant {
  tenantId:    string;
  tenantCode:  string;
  displayName: string;
}

// ── Tenant resolution ──────────────────────────────────────────────────────

/**
 * Resolves a tenant from a tenant code by calling the Tenant service
 * resolution endpoint (anonymous).
 *
 * Primary: GET /tenant/api/v1/public/resolve/by-code/{code}
 * Fallback: GET /identity/api/tenants/current/branding (only if TENANT_RESOLUTION_FALLBACK_IDENTITY=true)
 *
 * @param tenantCode - The tenant slug/code extracted from the request subdomain.
 * @returns ResolvedTenant or null when the tenant is not found.
 */
export async function resolveTenantFromCode(
  tenantCode: string,
): Promise<ResolvedTenant | null> {
  // Primary: Tenant service resolution (Tenant-first, TENANT-STABILIZATION)
  // Try by-code first; if not found fall through to by-subdomain.
  // This covers tenants whose subdomain prefix differs from their code
  // (e.g. subdomain="liens-company", code="lienscom").
  try {
    const url = `${GATEWAY_URL}/tenant/api/v1/public/resolve/by-code/${encodeURIComponent(tenantCode)}`;
    const res = await fetch(url, { cache: 'no-store' });
    if (res.ok) {
      const data = await res.json();
      if (data.tenantId) {
        return {
          tenantId:    data.tenantId,
          tenantCode:  data.code ?? tenantCode,
          displayName: data.displayName ?? tenantCode,
        };
      }
    }
  } catch {
    // Fall through to subdomain lookup
  }

  // Secondary: resolve by subdomain (handles tenants where code ≠ subdomain prefix)
  try {
    const url = `${GATEWAY_URL}/tenant/api/v1/public/resolve/by-subdomain/${encodeURIComponent(tenantCode)}`;
    const res = await fetch(url, { cache: 'no-store' });
    if (res.ok) {
      const data = await res.json();
      if (data.tenantId) {
        return {
          tenantId:    data.tenantId,
          tenantCode:  data.code ?? tenantCode,
          displayName: data.displayName ?? tenantCode,
        };
      }
    }
  } catch {
    // Fall through to Identity fallback
  }

  // Fallback: Identity branding endpoint (only if explicitly enabled for rollback)
  if (process.env.TENANT_RESOLUTION_FALLBACK_IDENTITY === 'true') {
    console.warn('[public-network-api] Tenant resolution failed; falling back to Identity', { tenantCode });
    try {
      const url = `${GATEWAY_URL}/identity/api/tenants/current/branding`;
      const res = await fetch(url, {
        headers: { 'X-Tenant-Code': tenantCode },
        cache:   'no-store',
      });
      if (res.ok) {
        const data = await res.json();
        if (data.tenantId) {
          return {
            tenantId:    data.tenantId,
            tenantCode:  data.tenantCode ?? tenantCode,
            displayName: data.displayName ?? tenantCode,
          };
        }
      }
    } catch {
      // Both failed
    }
  }

  return null;
}

// ── Public network endpoints ───────────────────────────────────────────────

/**
 * Fetches all networks for the given tenant (by GUID).
 */
export async function fetchPublicNetworks(
  tenantId: string,
): Promise<PublicNetworkSummary[]> {
  const url = `${GATEWAY_URL}/careconnect/api/public/network`;

  let res: Response;
  try {
    res = await fetch(url, {
      headers: publicHeaders(tenantId),
      cache:   'no-store',
    });
  } catch {
    return [];
  }

  if (!res.ok) return [];
  return res.json();
}

/**
 * Fetches the combined detail (providers + markers) for a single network.
 */
export async function fetchPublicNetworkDetail(
  tenantId:  string,
  networkId: string,
): Promise<PublicNetworkDetail | null> {
  const url = `${GATEWAY_URL}/careconnect/api/public/network/${networkId}/detail`;

  let res: Response;
  try {
    res = await fetch(url, {
      headers: publicHeaders(tenantId),
      cache:   'no-store',
    });
  } catch {
    return null;
  }

  if (!res.ok) return null;
  return res.json();
}

// ── CC2-INT-B08: Public referral initiation ─────────────────────────────────

export interface PublicReferralRequest {
  providerId:            string;
  senderName:            string;
  senderEmail:           string;
  patientFirstName:      string;
  patientLastName:       string;
  patientPhone:          string;
  patientEmail?:         string;
  patientDateOfBirth?:   string;   // YYYY-MM-DD
  patientDateOfAccident?: string;  // YYYY-MM-DD
  patientAddress?:       string;
  serviceType?:          string;
  notes?:                string;
}

export interface PublicReferralResponse {
  referralId:    string;
  providerId:    string;
  providerName:  string;
  providerStage: string;
  message:       string;
}

export interface PublicReferralError {
  message: string;
  errors?: Record<string, string>;
}
