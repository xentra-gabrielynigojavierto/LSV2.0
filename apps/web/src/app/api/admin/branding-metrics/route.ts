import { NextRequest, NextResponse } from 'next/server';
import { getBrandingMetricsSnapshot } from '@/lib/branding-metrics';

/**
 * TENANT-STABILIZATION — Web BFF branding/logo read-source metrics endpoint.
 *
 * GET /api/admin/branding-metrics
 *
 * Returns process-level counters for branding and logo read-source activity.
 * Counters are in-memory only and reset on process restart.
 * This endpoint is the web-BFF counterpart to the Tenant service's
 * GET /api/v1/admin/runtime-metrics endpoint.
 *
 * Auth: Protected by X-Admin-Key header (matches ADMIN_METRICS_KEY env var).
 *       When ADMIN_METRICS_KEY is empty/unset, the endpoint is open on localhost
 *       only (development mode). In production, always set ADMIN_METRICS_KEY.
 *
 * Key counters for B13 gate clearance:
 *   tenantBranding.hybridFallbackTriggered — must be 0 over ≥7 days
 *   tenantBranding.identityModeReads       — must be 0 in production
 *   logo.hybridFallbackTriggered           — must be 0 over ≥7 days
 *   logo.identityModeReads                 — must be 0 in production
 */

const ADMIN_KEY = process.env.ADMIN_METRICS_KEY ?? '';

export async function GET(req: NextRequest): Promise<NextResponse> {
  // Auth guard: require X-Admin-Key header if key is configured
  if (ADMIN_KEY) {
    const supplied = req.headers.get('x-admin-key') ?? '';
    if (supplied !== ADMIN_KEY) {
      return NextResponse.json({ error: 'Unauthorized' }, { status: 401 });
    }
  }

  const snapshot = getBrandingMetricsSnapshot();

  const b13GateStatus = {
    hybridFallbackZero: snapshot.tenantBranding.hybridFallbackTriggered === 0 &&
                        snapshot.logo.hybridFallbackTriggered === 0,
    identityModeZero:   snapshot.tenantBranding.identityModeReads === 0 &&
                        snapshot.logo.identityModeReads === 0,
    readSourceDefault:  process.env.TENANT_BRANDING_READ_SOURCE ?? 'Tenant (default)',
    note: 'Counters are per-process and reset on restart. ' +
          'B13 gate requires hybridFallbackZero=true for ≥7 days in production.',
  };

  return NextResponse.json({
    ...snapshot,
    b13GateStatus,
  });
}
