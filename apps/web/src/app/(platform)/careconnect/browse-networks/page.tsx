import { redirect }             from 'next/navigation';
import { requireProductRole }    from '@/lib/auth-guards';
import { careConnectServerApi }  from '@/lib/careconnect-server-api';
import { ServerApiError }        from '@/lib/server-api-client';
import type { NetworkSummary }   from '@/types/careconnect';
import { NetworkCard }            from '@/components/careconnect/network-card';
import { ProductRole, OrgType }  from '@/types';

export const dynamic = 'force-dynamic';

/**
 * /careconnect/browse-networks — Available provider networks directory.
 * CC-REFERRER-BROWSE: Accessible only to elevated law firm referrers
 * (CareConnectReferrer role). Network managers (lien companies) are
 * redirected to the dashboard — they manage networks, they don't browse them.
 */
export default async function BrowseNetworksPage() {
  // Requires CareConnectReferrer role; redirects to /dashboard if absent.
  const session = await requireProductRole(ProductRole.CareConnectReferrer);

  // Lien company users must never access browse-networks, regardless of which
  // product roles their account carries. NetworkManager is the typical case;
  // the OrgType check covers any edge case where a lien-owner user only holds
  // CareConnectReferrer (e.g. during a partial provisioning state).
  if (
    session.productRoles.includes(ProductRole.CareConnectNetworkManager) ||
    session.orgType === OrgType.LienOwner
  ) redirect('/careconnect/dashboard');

  let networks: NetworkSummary[] = [];
  let fetchError: string | null  = null;

  try {
    networks = await careConnectServerApi.browseNetworks.list();
  } catch (err) {
    if (err instanceof ServerApiError) {
      fetchError = err.message;
    } else {
      fetchError = 'Unable to load networks. Please try again.';
    }
  }

  if (fetchError) {
    return (
      <div className="rounded-lg border border-red-200 bg-red-50 p-6 text-sm text-red-700">
        {fetchError}
      </div>
    );
  }

  if (networks.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center py-24 text-center">
        <i className="ri-share-circle-line text-5xl text-gray-200 mb-4" />
        <p className="text-sm font-medium text-gray-500">No provider networks are available yet.</p>
        <p className="text-xs text-gray-400 mt-1">Check back later or contact your coordinator.</p>
      </div>
    );
  }

  const tenantLogoUrl = `/api/branding/logo/public?tenantCode=${encodeURIComponent(session.tenantCode)}`;

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-xl font-semibold text-gray-900">Available Networks</h1>
        <p className="text-sm text-gray-500 mt-0.5">
          Select a network to browse providers and submit a referral.
        </p>
      </div>

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {networks.map(network => (
          <NetworkCard key={network.id} network={network} tenantLogoUrl={tenantLogoUrl} />
        ))}
      </div>
    </div>
  );
}
