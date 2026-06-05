import Link from 'next/link';
import { notFound } from 'next/navigation';
import { careConnectServerApi } from '@/lib/careconnect-server-api';
import { ServerApiError } from '@/lib/server-api-client';
import { NetworkDetailClient } from '@/components/careconnect/network-detail-client';

interface NetworkDetailPageProps {
  params: Promise<{ id: string }>;
}

/**
 * /careconnect/networks/[id] — Network Detail
 *
 * CC2-INT-B06: Role-gated by networks/layout.tsx.
 * Fetches network detail and map markers server-side; renders interactive
 * client component for provider management and the Leaflet map.
 */
export default async function NetworkDetailPage({ params }: NetworkDetailPageProps) {
  const { id } = await params;

  let network = null;
  let markers = null;
  let fetchError: string | null = null;

  try {
    [network, markers] = await Promise.all([
      careConnectServerApi.networks.getById(id),
      careConnectServerApi.networks.getMarkers(id),
    ]);
  } catch (err) {
    if (err instanceof ServerApiError && err.status === 404) {
      notFound();
    }
    fetchError = err instanceof ServerApiError ? err.message : 'Unable to load network.';
  }

  if (!network) {
    return (
      <div className="p-6 max-w-5xl mx-auto">
        <div className="rounded-md bg-red-50 border border-red-200 p-4 text-sm text-red-700">
          {fetchError}
        </div>
        <Link href="/careconnect/networks" className="mt-4 inline-block text-sm text-blue-600 hover:underline">
          ← Back to Networks
        </Link>
      </div>
    );
  }

  return (
    <div className="p-6 max-w-5xl mx-auto">
      <div className="mb-4">
        <Link href="/careconnect/networks" className="text-sm text-blue-600 hover:underline">
          ← Back to Networks
        </Link>
      </div>

      <NetworkDetailClient
        network={network}
        initialMarkers={markers ?? []}
      />
    </div>
  );
}
