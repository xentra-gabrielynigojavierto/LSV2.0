'use client';

import {
  createContext,
  useContext,
  useEffect,
  useState,
  type ReactNode,
} from 'react';
import type { TenantBranding } from '@/types';

const DEFAULT_BRANDING: TenantBranding = {
  tenantId:    '',
  tenantCode:  '',
  displayName: 'LegalSynq',
};

const TenantBrandingContext = createContext<TenantBranding>(DEFAULT_BRANDING);

/**
 * Fetches tenant branding from /api/tenant-branding (the BFF branding route).
 *
 * The BFF route is source-aware and reads from the Tenant service by default
 * (TENANT_BRANDING_READ_SOURCE=Tenant). Identity mode is retained for rollback
 * via env config only. See TENANT-B09-report.md and TENANT-STABILIZATION-report.md.
 *
 * This provider is source-agnostic — it never calls Identity directly.
 * Injects CSS variables (--color-primary) and updates the favicon.
 *
 * Loaded before auth — the login page must show correct tenant branding.
 */
export function TenantBrandingProvider({ children }: { children: ReactNode }) {
  const [branding, setBranding] = useState<TenantBranding>(DEFAULT_BRANDING);

  useEffect(() => {
    async function loadBranding() {
      try {
        const tenantCode = resolveTenantCode();
        if (!tenantCode) return;

        const res = await fetch('/api/tenant-branding', {
          headers: { 'X-Tenant-Code': tenantCode },
          cache: 'no-store',
        });
        if (!res.ok) return;
        const data: TenantBranding = await res.json();
        setBranding(data);
        applyBrandingToDOM(data);
      } catch {
        // Keep default branding on error
      }
    }
    loadBranding();
  }, []);

  return (
    <TenantBrandingContext.Provider value={branding}>
      {children}
    </TenantBrandingContext.Provider>
  );
}

export function useTenantBranding(): TenantBranding {
  return useContext(TenantBrandingContext);
}

// ── Tenant code resolution ───────────────────────────────────────────────────

function resolveTenantCode(): string | null {
  // 1. Cookie — set by the auth layer after login; most reliable source.
  const cookieTenant = document.cookie
    .split('; ')
    .find(c => c.startsWith('tenant_code='))
    ?.split('=')[1];
  if (cookieTenant) return cookieTenant;

  // 2. Subdomain — only in production. In dev/Replit the hostname subdomain is
  //    a container ID (e.g. "abc123.kirk.replit.dev"), not a tenant code, so
  //    we skip this step and fall through to NEXT_PUBLIC_TENANT_CODE instead.
  if (process.env.NEXT_PUBLIC_ENV !== 'development') {
    const host  = window.location.hostname;
    const parts = host.split('.');
    if (parts.length >= 3 && !host.startsWith('localhost')) {
      return parts[0];
    }
  }

  // 3. Explicit env var — used in dev / Replit.
  const envTenantCode = process.env.NEXT_PUBLIC_TENANT_CODE;
  if (envTenantCode) return envTenantCode;

  return null;
}

// ── DOM mutation helpers ──────────────────────────────────────────────────────

function applyBrandingToDOM(branding: TenantBranding): void {
  if (branding.primaryColor) {
    document.documentElement.style.setProperty('--color-primary', branding.primaryColor);
  }

  if (branding.displayName) {
    document.title = branding.displayName;
  }

  if (branding.faviconUrl) {
    let link = document.querySelector<HTMLLinkElement>("link[rel~='icon']");
    if (!link) {
      link = document.createElement('link');
      link.rel = 'icon';
      document.head.appendChild(link);
    }
    link.href = branding.faviconUrl;
  }
}
