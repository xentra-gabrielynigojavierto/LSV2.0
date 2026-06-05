/**
 * TENANT-STABILIZATION — Process-level branding/logo read-source metrics.
 *
 * Module-level counters (Interlocked-equivalent in JS using plain numbers behind
 * a single-threaded event loop). These are process-memory only and reset on
 * service restart. Use GET /api/admin/branding-metrics to read them.
 *
 * Counters cover:
 *   - Tenant-source reads (succeeded / failed)
 *   - HybridFallback activations and reasons
 *   - Identity-source reads (explicit mode, deprecated)
 *
 * Used by:
 *   apps/web/src/app/api/tenant-branding/route.ts
 *   apps/web/src/app/api/branding/logo/public/route.ts
 */

export interface BrandingMetricsSnapshot {
  capturedAtUtc: string;
  tenantBranding: {
    tenantReadsAttempted:        number;
    tenantReadsSucceeded:        number;
    tenantReadsFailed:           number;
    hybridFallbackTriggered:     number;
    hybridFallbackReason: {
      timeout:          number;
      transport_error:  number;
      not_found:        number;
      incomplete:       number;
      unknown:          number;
    };
    identityFallbackSucceeded:   number;
    identityFallbackFailed:      number;
    identityModeReads:           number;
  };
  logo: {
    tenantReadsAttempted:    number;
    tenantReadsSucceeded:    number;
    hybridFallbackTriggered: number;
    identityFallbackSucceeded: number;
    identityModeReads:       number;
  };
}

// ── Tenant-branding counters ──────────────────────────────────────────────────

let tb_tenantAttempted         = 0;
let tb_tenantSucceeded         = 0;
let tb_tenantFailed            = 0;
let tb_hybridTriggered         = 0;
let tb_hybridReason_timeout    = 0;
let tb_hybridReason_transport  = 0;
let tb_hybridReason_notFound   = 0;
let tb_hybridReason_incomplete = 0;
let tb_hybridReason_unknown    = 0;
let tb_identityFallbackOk      = 0;
let tb_identityFallbackFail    = 0;
let tb_identityModeReads       = 0;

export const tenantBrandingCounters = {
  tenantAttempted:        () => { tb_tenantAttempted++; },
  tenantSucceeded:        () => { tb_tenantSucceeded++; },
  tenantFailed:           () => { tb_tenantFailed++; },
  hybridTriggered:        () => { tb_hybridTriggered++; },
  hybridReason(reason: string) {
    switch (reason) {
      case 'timeout':            tb_hybridReason_timeout++;   break;
      case 'transport_error':    tb_hybridReason_transport++; break;
      case 'not_found':          tb_hybridReason_notFound++;  break;
      case 'incomplete_payload': tb_hybridReason_incomplete++; break;
      default:                   tb_hybridReason_unknown++;   break;
    }
  },
  identityFallbackOk:     () => { tb_identityFallbackOk++; },
  identityFallbackFail:   () => { tb_identityFallbackFail++; },
  identityModeRead:       () => { tb_identityModeReads++; },
};

// ── Logo/public counters ──────────────────────────────────────────────────────

let logo_tenantAttempted         = 0;
let logo_tenantSucceeded         = 0;
let logo_hybridTriggered         = 0;
let logo_identityFallbackOk      = 0;
let logo_identityModeReads       = 0;

export const logoCounters = {
  tenantAttempted:    () => { logo_tenantAttempted++; },
  tenantSucceeded:    () => { logo_tenantSucceeded++; },
  hybridTriggered:    () => { logo_hybridTriggered++; },
  identityFallbackOk: () => { logo_identityFallbackOk++; },
  identityModeRead:   () => { logo_identityModeReads++; },
};

// ── Snapshot ──────────────────────────────────────────────────────────────────

export function getBrandingMetricsSnapshot(): BrandingMetricsSnapshot {
  return {
    capturedAtUtc: new Date().toISOString(),
    tenantBranding: {
      tenantReadsAttempted:        tb_tenantAttempted,
      tenantReadsSucceeded:        tb_tenantSucceeded,
      tenantReadsFailed:           tb_tenantFailed,
      hybridFallbackTriggered:     tb_hybridTriggered,
      hybridFallbackReason: {
        timeout:         tb_hybridReason_timeout,
        transport_error: tb_hybridReason_transport,
        not_found:       tb_hybridReason_notFound,
        incomplete:      tb_hybridReason_incomplete,
        unknown:         tb_hybridReason_unknown,
      },
      identityFallbackSucceeded:   tb_identityFallbackOk,
      identityFallbackFailed:      tb_identityFallbackFail,
      identityModeReads:           tb_identityModeReads,
    },
    logo: {
      tenantReadsAttempted:      logo_tenantAttempted,
      tenantReadsSucceeded:      logo_tenantSucceeded,
      hybridFallbackTriggered:   logo_hybridTriggered,
      identityFallbackSucceeded: logo_identityFallbackOk,
      identityModeReads:         logo_identityModeReads,
    },
  };
}
