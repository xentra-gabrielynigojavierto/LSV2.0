import { NextRequest, NextResponse } from 'next/server';
import { tenantBrandingCounters } from '@/lib/branding-metrics';

/**
 * Read-source-aware tenant branding BFF endpoint.
 *
 * TENANT-B07: Hardened with explicit timeouts + improved failure logging.
 * TENANT-B09: Default read source changed from Identity → Tenant.
 *             Identity mode is now deprecated. A deprecation warning is logged
 *             whenever Identity mode is active. Rollback: set
 *             TENANT_BRANDING_READ_SOURCE=Identity or HybridFallback.
 * TENANT-STABILIZATION: Added branding-metrics counters for observability.
 *             HybridFallback activations now increment telemetry counters.
 *             Identity fallback events are measurable, not just logged.
 *
 * Mode selection (TENANT_BRANDING_READ_SOURCE env var, default: Tenant):
 *   Tenant        — reads from Tenant service public branding endpoint (default, B09)
 *   HybridFallback — tries Tenant first (4s timeout), falls back to Identity on
 *                    timeout / transport failure / 404 / incomplete payload
 *   Identity      — DEPRECATED: legacy path — forwards to Identity service
 *
 * Response is always in TenantBranding shape regardless of source.
 * The client (TenantBrandingProvider) is fully source-agnostic.
 *
 * Observability: every call logs mode, source, fallbackTriggered + fallbackReason.
 * Failure categories: timeout | transport_error | not_found | incomplete_payload
 */

const GATEWAY_URL = process.env.GATEWAY_URL ?? 'http://127.0.0.1:5010';
// TENANT-B09: Default changed from 'Identity' to 'Tenant'. See TENANT-B09-report.md §4.
const READ_SOURCE = (process.env.TENANT_BRANDING_READ_SOURCE ?? 'Tenant') as ReadSource;

const TENANT_TIMEOUT_MS  = 4_000;
const IDENTITY_TIMEOUT_MS = 5_000;

type ReadSource = 'Identity' | 'Tenant' | 'HybridFallback';

interface TenantBrandingShape {
  tenantId?:            string;
  tenantCode?:          string;
  displayName?:         string;
  primaryColor?:        string;
  logoDocumentId?:      string;
  logoWhiteDocumentId?: string;
  faviconUrl?:          string;
}

// ── Tenant code resolution ────────────────────────────────────────────────────

function resolveTenantCode(req: NextRequest): string | null {
  const headerCode = req.headers.get('x-tenant-code');
  if (headerCode) return headerCode;

  const host  = req.headers.get('x-forwarded-host') ?? req.headers.get('host') ?? '';
  const parts = host.split('.');
  if (parts.length >= 3 && !host.startsWith('localhost')) return parts[0];

  const devCode = process.env.NEXT_PUBLIC_TENANT_CODE;
  if (devCode) return devCode;

  return null;
}

// ── Fetch helpers ─────────────────────────────────────────────────────────────

type FetchFailReason = 'timeout' | 'transport_error' | 'not_found' | 'error_response' | null;

async function fetchWithTimeout(
  url: string,
  init: RequestInit,
  timeoutMs: number,
): Promise<{ res: Response | null; failReason: FetchFailReason }> {
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), timeoutMs);
  try {
    const res = await fetch(url, { ...init, signal: controller.signal });
    return { res, failReason: null };
  } catch (err: unknown) {
    const isAbort = err instanceof Error && err.name === 'AbortError';
    return { res: null, failReason: isAbort ? 'timeout' : 'transport_error' };
  } finally {
    clearTimeout(timer);
  }
}

async function fetchFromIdentity(
  tenantCode: string,
  req: NextRequest,
): Promise<{ data: TenantBrandingShape | null; failReason: FetchFailReason }> {
  const host    = req.headers.get('x-forwarded-host') ?? req.headers.get('host');
  const headers: Record<string, string> = { 'X-Tenant-Code': tenantCode };
  if (host) headers['X-Forwarded-Host'] = host;

  const { res, failReason } = await fetchWithTimeout(
    `${GATEWAY_URL}/identity/api/tenants/current/branding`,
    { headers },
    IDENTITY_TIMEOUT_MS,
  );

  if (failReason) return { data: null, failReason };
  if (!res!.ok)   return { data: null, failReason: res!.status === 404 ? 'not_found' : 'error_response' };

  const data = await res!.json() as TenantBrandingShape;
  return { data, failReason: null };
}

async function fetchFromTenant(
  tenantCode: string,
): Promise<{ data: TenantBrandingShape | null; failReason: FetchFailReason }> {
  // Try by-code first; fall back to by-subdomain when code ≠ subdomain (e.g. liens-company).
  let { res, failReason } = await fetchWithTimeout(
    `${GATEWAY_URL}/tenant/api/v1/public/branding/by-code/${encodeURIComponent(tenantCode)}`,
    {},
    TENANT_TIMEOUT_MS,
  );

  if (!failReason && res!.status === 404) {
    ({ res, failReason } = await fetchWithTimeout(
      `${GATEWAY_URL}/tenant/api/v1/public/branding/by-subdomain/${encodeURIComponent(tenantCode)}`,
      {},
      TENANT_TIMEOUT_MS,
    ));
  }

  if (failReason) return { data: null, failReason };
  if (!res!.ok)   return { data: null, failReason: res!.status === 404 ? 'not_found' : 'error_response' };

  const raw = await res!.json();
  return {
    data: {
      tenantId:            raw.tenantId            ?? undefined,
      tenantCode:          raw.code                ?? undefined,
      displayName:         raw.displayName         ?? undefined,
      primaryColor:        raw.primaryColor        ?? undefined,
      logoDocumentId:      raw.logoDocumentId      ?? undefined,
      logoWhiteDocumentId: raw.logoWhiteDocumentId ?? undefined,
    },
    failReason: null,
  };
}

function isUsable(b: TenantBrandingShape | null): b is TenantBrandingShape {
  return !!(b && b.tenantId && b.tenantCode && b.displayName);
}

// ── Route handler ─────────────────────────────────────────────────────────────

export async function GET(req: NextRequest): Promise<NextResponse> {
  const tenantCode = resolveTenantCode(req);

  if (!tenantCode) {
    console.warn('[tenant-branding] Tenant code could not be resolved from request');
    return NextResponse.json({ message: 'Tenant code could not be resolved' }, { status: 404 });
  }

  let branding: TenantBrandingShape | null = null;
  let source: string = 'none';
  let fallbackTriggered = false;
  let fallbackReason: string | undefined;

  if (READ_SOURCE === 'Tenant') {
    tenantBrandingCounters.tenantAttempted();
    const { data, failReason } = await fetchFromTenant(tenantCode);
    if (data) {
      tenantBrandingCounters.tenantSucceeded();
      branding = data;
      source   = 'tenant';
    } else {
      tenantBrandingCounters.tenantFailed();
      fallbackReason = failReason ?? 'unknown';
      console.warn('[tenant-branding] Tenant mode: read failed, no Identity fallback', {
        tenantCode,
        failReason,
      });
    }

  } else if (READ_SOURCE === 'HybridFallback') {
    tenantBrandingCounters.tenantAttempted();
    const { data: tenantData, failReason: tenantFail } = await fetchFromTenant(tenantCode);

    if (isUsable(tenantData)) {
      tenantBrandingCounters.tenantSucceeded();
      branding = tenantData;
      source   = 'tenant';
    } else {
      tenantBrandingCounters.tenantFailed();
      fallbackTriggered = true;
      fallbackReason    = tenantFail === null ? 'incomplete_payload' : tenantFail;

      // Increment detailed fallback metrics
      tenantBrandingCounters.hybridTriggered();
      tenantBrandingCounters.hybridReason(fallbackReason);

      console.warn('[tenant-branding] HybridFallback: Tenant fetch failed, falling back to Identity', {
        tenantCode,
        fallbackReason,
      });

      const { data: idData, failReason: idFail } = await fetchFromIdentity(tenantCode, req);
      if (idData) {
        tenantBrandingCounters.identityFallbackOk();
        branding = idData;
        source   = 'identity';
      } else {
        tenantBrandingCounters.identityFallbackFail();
        console.error('[tenant-branding] HybridFallback: Identity fallback also failed', {
          tenantCode,
          idFail,
        });
      }
    }

  } else {
    // Identity mode — DEPRECATED [TENANT-B09]. Retained for rollback only.
    tenantBrandingCounters.identityModeRead();
    console.warn(
      '[DEPRECATION] [tenant-branding] TENANT_BRANDING_READ_SOURCE=Identity is deprecated as of TENANT-B09. ' +
      'Switch to Tenant or HybridFallback. See TENANT-B09-report.md §4.',
      { tenantCode },
    );
    const { data, failReason } = await fetchFromIdentity(tenantCode, req);
    if (data) {
      tenantBrandingCounters.identityFallbackOk();
      branding = data;
      source   = 'identity';
    } else {
      tenantBrandingCounters.identityFallbackFail();
      fallbackReason = failReason ?? 'unknown';
      console.error('[tenant-branding] Identity read failed', { tenantCode, failReason });
    }
  }

  console.log('[tenant-branding]', JSON.stringify({
    mode:             READ_SOURCE,
    source,
    tenantCode,
    fallbackTriggered,
    fallbackReason,
    resolved:         !!branding,
  }));

  if (!branding) {
    return NextResponse.json({ message: 'Tenant branding not found' }, { status: 404 });
  }

  return NextResponse.json(branding);
}
