import { careConnectServerApi } from '@/lib/careconnect-server-api';
import { ServerApiError } from '@/lib/server-api-client';
import { NetworkListClient } from '@/components/careconnect/network-list-client';

export const dynamic = 'force-dynamic';


/**
 * /careconnect/networks — Provider Network Management
 *
 * CC2-INT-B06: Role-gated to CARECONNECT_NETWORK_MANAGER (enforced by layout.tsx).
 * Fetches the tenant's network list server-side and hands it to the interactive
 * client component for creation, editing, and deletion.
 */
export default async function NetworksPage() {
  let networks = null;
  let fetchError: string | null = null;

  try {
    networks = await careConnectServerApi.networks.list();
  } catch (err) {
    if (err instanceof ServerApiError) {
      fetchError = err.message;
    } else {
      fetchError = 'Unable to load networks. Please try again.';
    }
  }

  return (
    <div className="p-6 max-w-5xl mx-auto">
      <div className="mb-6">
        <h1 className="text-2xl font-semibold text-gray-900">Provider Networks</h1>
        <p className="text-sm text-gray-500 mt-1">
          Create and manage groups of providers for streamlined referral workflows.
        </p>
      </div>

      {fetchError ? (
        <div className="rounded-md bg-red-50 border border-red-200 p-4 text-sm text-red-700">
          {fetchError}
        </div>
      ) : (
        <NetworkListClient initialNetworks={networks ?? []} />
      )}
    </div>
  );
}
