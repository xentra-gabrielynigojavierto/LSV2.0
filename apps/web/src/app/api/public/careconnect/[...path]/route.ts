import { createHmac } from 'crypto';
import { type NextRequest, NextResponse } from 'next/server';

const GATEWAY_URL = process.env.GATEWAY_URL ?? 'http://127.0.0.1:5010';

// BLK-SEC-02-02: Shared secret for public CareConnect trust boundary.
// Used to HMAC-sign the resolved X-Tenant-Id before forwarding.
// Must match PublicTrustBoundary:InternalRequestSecret in Gateway and CareConnect configs.
// Prefer PublicTrustBoundary__InternalRequestSecret (canonical .NET env key) so the
// deployed app always uses the value from Replit secrets, not a dev alias from .env.local.
// Empty string in unconfigured environments — validation disabled fallback in CareConnect.
const INTERNAL_REQUEST_SECRET =
  process.env['PublicTrustBoundary__InternalRequestSecret'] ??
  process.env.INTERNAL_REQUEST_SECRET ??
  '';

/**
 * CC2-INT-B07 — Public CareConnect BFF proxy.
 * TENANT-STABILIZATION — Tenant resolution switched from Identity to Tenant service.
 * BLK-SEC-02-02 — Trust boundary hardening: BFF now signs X-Tenant-Id with HMAC-SHA256.
 *
 * Handles unauthenticated requests to the public network surface.
 * Unlike the authenticated /api/careconnect proxy, this handler does NOT
 * inject an Authorization header — the backend endpoints are AllowAnonymous.
 *
 * Tenant isolation: The X-Tenant-Id header is resolved server-side from the
 * incoming request's Host header by calling the Tenant resolution endpoint.
 * The client-supplied X-Tenant-Id header is never trusted or forwarded.
 *
 * Trust boundary: X-Tenant-Id is signed with HMAC-SHA256 using INTERNAL_REQUEST_SECRET.
 * The signature is sent as X-Tenant-Id-Sig. CareConnect validates both:
 *   - X-Internal-Gateway-Secret (injected by YARP — proves gateway origin, Layer 1)
 *   - X-Tenant-Id-Sig HMAC (proves BFF computed the tenant ID, Layer 2)
 *
 * Resolution: GET /tenant/api/v1/public/resolve/by-host?host={host}
 *   Returns { tenantId, code, displayName, ... } from Tenant service.
 *   Falls back to Identity branding endpoint if TENANT_RESOLUTION_FALLBACK_IDENTITY=true
 *   and TENANT_RESOLUTION_FALLBACK_IDENTITY=true (default: false).
 *
 * Routing:
 *   Browser fetch → /api/public/careconnect/api/public/network
 *   → This handler
 *   → ${GATEWAY_URL}/careconnect/api/public/network  (anonymous gateway route)
 *   → CareConnect service at :5003
 */
type RouteContext = { params: Promise<{ path: string[] }> };

const ENABLE_IDENTITY_FALLBACK = process.env.TENANT_RESOLUTION_FALLBACK_IDENTITY === 'true';

/**
 * Resolves the tenant GUID from the incoming request's Host header.
 *
 * Strategy (same pattern as public-network-api.ts and tenant-branding route):
 *   1. Extract the subdomain from the host (first segment of a 3+-part hostname)
 *   2. Try Tenant service /by-code/{subdomain} — works when code == subdomain
 *   3. Fall back to /by-subdomain/{subdomain} — handles code ≠ subdomain (e.g. liens-company)
 *   4. Optionally fall back to Identity branding endpoint if TENANT_RESOLUTION_FALLBACK_IDENTITY=true
 *
 * Note: /by-host is NOT used here — it resolves custom domains registered in the
 * TenantDomain table, not the platform's own *.demo.legalsynq.com subdomain routing.
 */
async function resolveTenantIdFromHost(host: string): Promise<string | null> {
  // Extract subdomain (first segment) from hostname
  const parts = host.split('.');
  const subdomain = parts.length >= 3 ? parts[0] : null;

  if (subdomain) {
    // Try by-code first (fast path when code matches subdomain)
    try {
      const res = await fetch(
        `${GATEWAY_URL}/tenant/api/v1/public/resolve/by-code/${encodeURIComponent(subdomain)}`,
        { method: 'GET' },
      );
      if (res.ok) {
        const body = await res.json() as { tenantId?: string };
        if (body.tenantId && body.tenantId !== '') return body.tenantId;
      }
    } catch { /* fall through */ }

    // Fall back to by-subdomain (handles code ≠ subdomain, e.g. lienscom vs liens-company)
    try {
      const res = await fetch(
        `${GATEWAY_URL}/tenant/api/v1/public/resolve/by-subdomain/${encodeURIComponent(subdomain)}`,
        { method: 'GET' },
      );
      if (res.ok) {
        const body = await res.json() as { tenantId?: string };
        if (body.tenantId && body.tenantId !== '') return body.tenantId;
      }
    } catch { /* fall through */ }
  }

  // Fallback: Identity branding endpoint (only if explicitly enabled for rollback)
  if (ENABLE_IDENTITY_FALLBACK) {
    console.warn('[careconnect-proxy] Tenant resolution failed; falling back to Identity branding endpoint', { host });
    try {
      const res = await fetch(`${GATEWAY_URL}/identity/api/tenants/current/branding`, {
        method: 'GET',
        headers: { 'X-Forwarded-Host': host, 'Host': host },
      });
      if (res.ok) {
        const body = await res.json() as { tenantId?: string };
        if (body.tenantId && body.tenantId !== '') return body.tenantId;
      }
    } catch {
      // All resolution paths failed
    }
  }

  return null;
}

/**
 * Computes HMAC-SHA256(data, secret) and returns the result as a base64 string.
 * Used to sign X-Tenant-Id before forwarding — CareConnect validates the signature.
 * Returns empty string if secret is not configured (trust boundary validation
 * disabled in CareConnect for unconfigured environments).
 */
function signTenantId(tenantId: string): string {
  if (!INTERNAL_REQUEST_SECRET) return '';
  return createHmac('sha256', INTERNAL_REQUEST_SECRET).update(tenantId).digest('base64');
}

async function proxy(request: NextRequest, { params }: RouteContext): Promise<NextResponse> {
  const { path: pathSegments } = await params;
  const gatewayPath = `/careconnect/${pathSegments.join('/')}`;
  const qs = request.nextUrl.searchParams.toString();
  const url = `${GATEWAY_URL}${gatewayPath}${qs ? `?${qs}` : ''}`;

  // Resolve tenant ID server-side from the Host header — never from the client-supplied header.
  const host = request.headers.get('x-forwarded-host') ?? request.headers.get('host') ?? request.nextUrl.host;
  const tenantId = await resolveTenantIdFromHost(host);

  if (!tenantId) {
    return NextResponse.json({ message: 'Tenant could not be resolved.' }, { status: 400 });
  }

  // BLK-SEC-02-02: Sign the resolved tenant ID with HMAC-SHA256.
  // CareConnect validates this signature alongside the gateway origin marker (Layer 2).
  const sig = signTenantId(tenantId);

  const reqHeaders: Record<string, string> = {
    'Content-Type': 'application/json',
    'X-Tenant-Id': tenantId,
    ...(sig ? { 'X-Tenant-Id-Sig': sig } : {}),
  };

  let body: string | undefined;
  if (!['GET', 'HEAD'].includes(request.method)) {
    try { body = await request.text(); } catch { /* empty */ }
  }

  let gatewayRes: Response;
  try {
    gatewayRes = await fetch(url, {
      method:  request.method,
      headers: reqHeaders,
      body,
    });
  } catch {
    return NextResponse.json({ message: 'Gateway unavailable' }, { status: 503 });
  }

  const responseBody = await gatewayRes.text();

  const resHeaders: Record<string, string> = {
    'Content-Type': gatewayRes.headers.get('Content-Type') ?? 'application/json',
  };
  const correlationId = gatewayRes.headers.get('X-Correlation-Id');
  if (correlationId) resHeaders['X-Correlation-Id'] = correlationId;

  return new NextResponse(responseBody, {
    status:  gatewayRes.status,
    headers: resHeaders,
  });
}

export const GET    = proxy;
export const POST   = proxy;
export const PUT    = proxy;
export const PATCH  = proxy;
export const DELETE = proxy;
