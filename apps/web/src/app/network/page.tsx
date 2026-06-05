/**
 * CC2-INT-B07 — Public Network Surface.
 *
 * Route: /network
 * In production: [tenant-subdomain].[baseurl]/network
 * In development: /network?tenant=<code>  (subdomain routing is unavailable on Replit)
 *
 * Auth: None — this page is intentionally public.
 *
 * Tenant resolution order:
 *  1. Subdomain from Host header (production)
 *  2. `tenant` query parameter (development / Replit)
 *  3. NEXT_PUBLIC_TENANT_CODE env var
 *
 * Stage enforcement (CC2-INT-B06-02):
 *  - URL providers → shown on this page without restrictions.
 *  - COMMON_PORTAL providers → directory card includes a "Portal available" link.
 *  - TENANT providers → directory card includes a "Tenant portal" link.
 *
 * No redirect is enforced here for the viewer — they are always allowed to browse
 * the directory. Stage enforcement is informational, guiding providers toward
 * the appropriate portal when they click their own profile.
 */

import { headers } from 'next/headers';
import {
  resolveTenantFromCode,
  fetchPublicNetworks,
  fetchPublicNetworkDetail,
  type PublicNetworkSummary,
  type PublicNetworkDetail,
  type ResolvedTenant,
} from '@/lib/public-network-api';
import { PublicNetworkView } from '@/components/careconnect/public-network-view';

export const dynamic = 'force-dynamic';


interface PageProps {
  searchParams: Promise<{ tenant?: string; network?: string }>;
}

// ── Server Component ──────────────────────────────────────────────────────────

export default async function PublicNetworkPage({ searchParams }: PageProps) {
  const sp = await searchParams;

  // Resolve tenant code from: subdomain → query param → env var
  const tenantCode = await resolveTenantCodeServerSide(sp.tenant);

  if (!tenantCode) {
    return <TenantNotFound reason="no-code" />;
  }

  // Resolve tenant via Identity branding endpoint (anonymous)
  const tenant = await resolveTenantFromCode(tenantCode);

  if (!tenant) {
    return <TenantNotFound reason="not-found" tenantCode={tenantCode} />;
  }

  // Load networks for the tenant
  const networks = await fetchPublicNetworks(tenant.tenantId);

  if (networks.length === 0) {
    return <TenantNotFound reason="no-networks" tenantCode={tenantCode} />;
  }

  // If a specific network is requested, load it; otherwise use the first
  const targetNetworkId = sp.network ?? networks[0].id;
  const selectedNetwork  = networks.find(n => n.id === targetNetworkId) ?? networks[0];

  const detail = await fetchPublicNetworkDetail(tenant.tenantId, selectedNetwork.id);

  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <h1 className="text-2xl font-semibold text-gray-900">
          {tenant.displayName}
        </h1>
        <p className="mt-1 text-sm text-gray-500">
          Provider Network Directory
        </p>
      </div>

      {/* Network selector (shown only when there are multiple networks) */}
      {networks.length > 1 && (
        <NetworkSelector
          networks={networks}
          selectedId={selectedNetwork.id}
          tenantCode={tenantCode}
        />
      )}

      {/* Main view — interactive provider list + map */}
      {detail ? (
        <PublicNetworkView detail={detail} tenantCode={tenantCode} tenantId={tenant.tenantId} />
      ) : (
        <p className="text-sm text-gray-500">
          This network has no providers yet.
        </p>
      )}
    </div>
  );
}

// ── Tenant resolution (server-side) ──────────────────────────────────────────

async function resolveTenantCodeServerSide(
  queryParam?: string,
): Promise<string | null> {
  // 1. Subdomain from Host header (production)
  const headerStore = await headers();
  const host = headerStore.get('host') ?? headerStore.get('x-forwarded-host') ?? '';
  const subdomain = extractSubdomain(host);
  if (subdomain) return subdomain;

  // 2. Query parameter (dev / Replit)
  if (queryParam) return queryParam;

  // 3. Environment variable fallback
  return process.env.NEXT_PUBLIC_TENANT_CODE ?? null;
}

function extractSubdomain(host: string): string | null {
  // Strip port if present
  const hostname = host.split(':')[0];

  // Reject bare IP addresses (IPv4)
  if (/^(\d{1,3}\.){3}\d{1,3}$/.test(hostname)) return null;

  const parts = hostname.split('.');

  // Need at least 3 parts: subdomain.domain.tld
  // Reject common dev hostnames
  if (parts.length < 3) return null;
  if (hostname.startsWith('localhost')) return null;
  if (hostname.endsWith('.replit.dev') && parts.length < 4) return null;

  const sub = parts[0];

  // Reject reserved subdomains
  const reserved = new Set([
    'www', 'app', 'api', 'admin', 'mail', 'smtp', 'ftp', 'cdn', 'static',
  ]);
  if (reserved.has(sub)) return null;

  return sub;
}

// ── Sub-components ────────────────────────────────────────────────────────────

function NetworkSelector({
  networks,
  selectedId,
  tenantCode,
}: {
  networks:   PublicNetworkSummary[];
  selectedId: string;
  tenantCode: string;
}) {
  return (
    <nav className="flex flex-wrap gap-2" aria-label="Networks">
      {networks.map(n => {
        const isActive = n.id === selectedId;
        return (
          <a
            key={n.id}
            href={`/network?tenant=${tenantCode}&network=${n.id}`}
            className={[
              'px-3 py-1.5 rounded-full text-sm font-medium transition-colors',
              isActive
                ? 'bg-primary text-white'
                : 'bg-white text-gray-600 border border-gray-200 hover:border-primary hover:text-primary',
            ].join(' ')}
          >
            {n.name}
            <span className="ml-1.5 text-xs opacity-70">({n.providerCount})</span>
          </a>
        );
      })}
    </nav>
  );
}

function TenantNotFound({
  reason,
  tenantCode,
}: {
  reason:      'no-code' | 'not-found' | 'no-networks';
  tenantCode?: string;
}) {
  const messages: Record<typeof reason, string> = {
    'no-code':     'No tenant was specified. Please use the URL provided by your organization.',
    'not-found':   `The tenant "${tenantCode}" could not be found. Please verify the URL.`,
    'no-networks': `The organization "${tenantCode}" has not configured a provider network yet.`,
  };

  return (
    <div className="flex flex-col items-center justify-center py-24 text-center">
      <div className="w-12 h-12 rounded-full bg-gray-100 flex items-center justify-center mb-4">
        <svg className="w-6 h-6 text-gray-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5}
            d="M12 9v2m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
        </svg>
      </div>
      <h2 className="text-lg font-medium text-gray-900 mb-2">Network unavailable</h2>
      <p className="text-sm text-gray-500 max-w-sm">{messages[reason]}</p>
    </div>
  );
}
