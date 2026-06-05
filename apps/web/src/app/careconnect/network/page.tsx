import { headers }                  from 'next/headers';
import { PublicNetworkView }        from '@/components/careconnect/public-network-view';
import { AccessCodeGate }           from '@/components/careconnect/access-code-gate';
import {
  resolveTenantFromCode,
  fetchPublicNetworks,
  fetchPublicNetworkDetail,
  type PublicNetworkDetail,
} from '@/lib/public-network-api';

export const dynamic = 'force-dynamic';

/**
 * /careconnect/network — Public preferred provider directory.
 *
 * Intentionally anonymous — no platform layout, no auth guard.
 * Tenant is resolved server-side from the request subdomain via the
 * Identity branding endpoint; the resolved tenantId is forwarded as
 * X-Tenant-Id to the AllowAnonymous CareConnect public network endpoints.
 */
export default async function PublicNetworkPage() {
  const hdrs = await headers();
  const host = hdrs.get('x-forwarded-host') ?? hdrs.get('host') ?? '';

  // ── Resolve tenant from subdomain ────────────────────────────────────────
  const parts      = host.split('.');
  const tenantCode = parts.length >= 3
    ? parts[0]
    : (process.env.NEXT_PUBLIC_TENANT_CODE ?? '');

  const tenant = tenantCode ? await resolveTenantFromCode(tenantCode) : null;

  if (!tenant) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-gray-50">
        <p className="text-sm text-gray-500">Network not found.</p>
      </div>
    );
  }

  // ── Load first (preferred) network + its providers ───────────────────────
  let detail: PublicNetworkDetail | null = null;

  const networks = await fetchPublicNetworks(tenant.tenantId);
  if (networks.length > 0) {
    detail = await fetchPublicNetworkDetail(tenant.tenantId, networks[0].id);
  }

  if (!detail) {
    // Network exists in list but detail failed, or no network at all
    detail = {
      networkId:          '',
      networkName:        'Provider Network',
      networkDescription: '',
      providers:          [],
      markers:            [],
    };
  }

  return (
    <div className="h-screen overflow-hidden">
      <AccessCodeGate>
        <PublicNetworkView
          detail={detail}
          tenantCode={tenant.tenantCode}
          tenantId={tenant.tenantId}
        />
      </AccessCodeGate>
    </div>
  );
}
