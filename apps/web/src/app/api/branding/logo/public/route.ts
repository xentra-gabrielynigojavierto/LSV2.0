import { NextRequest, NextResponse } from 'next/server';
import { logoCounters } from '@/lib/branding-metrics';

/**
 * TENANT-B07 — Source-aware public logo proxy.
 * TENANT-STABILIZATION — Default changed from 'Identity' → 'Tenant'.
 *
 * Reads TENANT_BRANDING_READ_SOURCE to decide where to fetch the tenant's
 * logoDocumentId from:
 *
 *   Tenant        — Tenant service /tenant/api/v1/public/branding/by-code/{code} (DEFAULT)
 *   HybridFallback — Tenant first, Identity fallback on failure/no-logo
 *   Identity      — Identity service /identity/api/tenants/current/branding (ROLLBACK ONLY)
 *
 * The actual image bytes are always proxied from the Documents service
 * (/documents/public/logo/{docId}) regardless of the branding read source.
 *
 * Metrics: logoCounters (branding-metrics.ts) are incremented for each path.
 * Rollback: set TENANT_BRANDING_READ_SOURCE=Identity in env to revert.
 */

const GATEWAY_URL = process.env.GATEWAY_URL ?? 'http://127.0.0.1:5010';
// TENANT-STABILIZATION: Default changed from 'Identity' to 'Tenant'. Identity remains available for rollback.
const READ_SOURCE = (process.env.TENANT_BRANDING_READ_SOURCE ?? 'Tenant') as ReadSource;

const BRANDING_TIMEOUT_MS = 4_000;

type ReadSource = 'Identity' | 'Tenant' | 'HybridFallback';

// ── Tenant code resolution ────────────────────────────────────────────────────

function resolveTenantCode(req: NextRequest): string | null {
  const param = req.nextUrl.searchParams.get('tenantCode');
  if (param) return param;

  const host  = req.headers.get('x-forwarded-host') ?? req.headers.get('host') ?? '';
  const parts = host.split('.');
  if (parts.length >= 3) return parts[0];

  const env = process.env.NEXT_PUBLIC_ENV;
  if (env === 'development') return process.env.NEXT_PUBLIC_TENANT_CODE ?? null;

  return null;
}

// ── Branding fetch helpers ────────────────────────────────────────────────────

async function fetchLogoDocFromIdentity(tenantCode: string): Promise<string | null> {
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), BRANDING_TIMEOUT_MS);
  try {
    const res = await fetch(
      `${GATEWAY_URL}/identity/api/tenants/current/branding`,
      { headers: { 'X-Tenant-Code': tenantCode }, signal: controller.signal },
    );
    if (!res.ok) return null;
    const data = await res.json();
    return data?.logoDocumentId ?? null;
  } catch {
    return null;
  } finally {
    clearTimeout(timer);
  }
}

interface TenantLogoIds {
  logoDocumentId:      string | null;
  logoWhiteDocumentId: string | null;
}

async function fetchLogoIdsFromTenant(tenantCode: string): Promise<TenantLogoIds> {
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), BRANDING_TIMEOUT_MS);
  try {
    // Try by-code first; fall back to by-subdomain when code ≠ subdomain (e.g. liens-company).
    let res = await fetch(
      `${GATEWAY_URL}/tenant/api/v1/public/branding/by-code/${encodeURIComponent(tenantCode)}`,
      { signal: controller.signal },
    );
    if (res.status === 404) {
      res = await fetch(
        `${GATEWAY_URL}/tenant/api/v1/public/branding/by-subdomain/${encodeURIComponent(tenantCode)}`,
        { signal: controller.signal },
      );
    }
    if (!res.ok) return { logoDocumentId: null, logoWhiteDocumentId: null };
    const data = await res.json();
    return {
      logoDocumentId:      data?.logoDocumentId      ?? null,
      logoWhiteDocumentId: data?.logoWhiteDocumentId ?? null,
    };
  } catch {
    return { logoDocumentId: null, logoWhiteDocumentId: null };
  } finally {
    clearTimeout(timer);
  }
}

// ── Route handler ─────────────────────────────────────────────────────────────

/** Fetch image bytes from the Documents service for a given document ID.
 *  Returns the Response on success (2xx), null on 404, throws on network error. */
async function fetchLogoBytes(docId: string): Promise<Response | null> {
  const res = await fetch(`${GATEWAY_URL}/documents/public/logo/${docId}`);
  if (res.status === 404) return null;
  return res;
}

export async function GET(req: NextRequest) {
  const tenantCode = resolveTenantCode(req);

  if (!tenantCode) {
    return new NextResponse(null, { status: 404 });
  }

  let ids: TenantLogoIds = { logoDocumentId: null, logoWhiteDocumentId: null };
  let source = 'none';

  if (READ_SOURCE === 'Tenant') {
    logoCounters.tenantAttempted();
    ids = await fetchLogoIdsFromTenant(tenantCode);
    if (ids.logoDocumentId || ids.logoWhiteDocumentId) {
      logoCounters.tenantSucceeded();
      source = 'tenant';
    }

  } else if (READ_SOURCE === 'HybridFallback') {
    logoCounters.tenantAttempted();
    ids = await fetchLogoIdsFromTenant(tenantCode);
    if (ids.logoDocumentId || ids.logoWhiteDocumentId) {
      logoCounters.tenantSucceeded();
      source = 'tenant';
    } else {
      logoCounters.hybridTriggered();
      console.warn('[logo-public] HybridFallback: Tenant fetch returned no logo ids, falling back to Identity', { tenantCode });
      const identityDocId = await fetchLogoDocFromIdentity(tenantCode);
      if (identityDocId) {
        logoCounters.identityFallbackOk();
        source = 'identity_fallback';
        ids = { logoDocumentId: identityDocId, logoWhiteDocumentId: null };
      }
    }

  } else {
    // Identity mode — ROLLBACK ONLY.
    console.warn(
      '[DEPRECATION] [logo-public] TENANT_BRANDING_READ_SOURCE=Identity is deprecated. ' +
      'Switch to Tenant. Identity mode retained for emergency rollback only.',
      { tenantCode },
    );
    logoCounters.identityModeRead();
    const identityDocId = await fetchLogoDocFromIdentity(tenantCode);
    source = identityDocId ? 'identity' : 'none';
    ids = { logoDocumentId: identityDocId, logoWhiteDocumentId: null };
  }

  const hasAnyId = !!(ids.logoDocumentId || ids.logoWhiteDocumentId);
  console.log('[logo-public]', JSON.stringify({ mode: READ_SOURCE, source, tenantCode, hasDoc: hasAnyId }));

  if (!hasAnyId) {
    return new NextResponse(null, { status: 404 });
  }

  // Try logoDocumentId first, then logoWhiteDocumentId as a fallback.
  // This mirrors the client-side TenantLogo fallback chain.
  const candidates = [ids.logoDocumentId, ids.logoWhiteDocumentId].filter(Boolean) as string[];

  try {
    for (const docId of candidates) {
      const logoRes = await fetchLogoBytes(docId);
      if (!logoRes) continue;          // 404 from Documents — try next candidate
      if (!logoRes.ok) {
        return new NextResponse(null, { status: logoRes.status });
      }
      const contentType = logoRes.headers.get('content-type') ?? 'image/png';
      const buffer      = await logoRes.arrayBuffer();
      return new NextResponse(buffer, {
        status: 200,
        headers: {
          'Content-Type':  contentType,
          'Cache-Control': 'public, max-age=3600, s-maxage=3600',
        },
      });
    }
    // All candidates returned 404 from Documents service
    return new NextResponse(null, { status: 404 });
  } catch {
    return new NextResponse(null, { status: 502 });
  }
}
