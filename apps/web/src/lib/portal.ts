/**
 * Portal configuration.
 *
 * Each entry maps a raw subdomain (first hostname segment) to portal-specific
 * behaviour.  Adding a new portal here is the only change needed to restrict
 * shell UI for future product-specific portals.
 */
export const PORTAL_CONFIGS = {
  'careconnect-demo': {
    productId:       'careconnect' as const,
    landingPath:     '/careconnect/dashboard',
    showAppSwitcher: false,
    showBottomNav:   false,
    logoSrc:         '/careconnect-logo.png',
    logoLabel:       'CareConnect',
  },
} as const;

export type PortalSubdomain = keyof typeof PORTAL_CONFIGS;
export type PortalConfig    = (typeof PORTAL_CONFIGS)[PortalSubdomain];

// ── Client-side helpers (safe to call in 'use client' components) ─────────────

export function getClientSubdomain(): string | null {
  if (typeof window === 'undefined') return null;
  const parts = window.location.hostname.split('.');
  if (parts.length >= 3 && parts[0] !== 'www') return parts[0];
  return null;
}

export function getClientPortalConfig(): PortalConfig | null {
  const sub = getClientSubdomain();
  if (!sub) return null;
  return (PORTAL_CONFIGS as Record<string, PortalConfig>)[sub] ?? null;
}

// ── Server-side helpers (safe to call in server components / route handlers) ──

export function getServerSubdomain(rawHost: string): string | null {
  const host = (rawHost.split(',')[0] ?? '').trim().split(':')[0].toLowerCase();
  const parts = host.split('.');
  if (parts.length >= 3 && parts[0] !== 'www') return parts[0];
  return null;
}

export function getServerPortalConfig(rawHost: string): PortalConfig | null {
  const sub = getServerSubdomain(rawHost);
  if (!sub) return null;
  return (PORTAL_CONFIGS as Record<string, PortalConfig>)[sub] ?? null;
}
