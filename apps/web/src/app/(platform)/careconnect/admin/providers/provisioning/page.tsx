/**
 * LSCC-01-003: Admin Provider Provisioning Interface.
 * Route: /careconnect/admin/providers/provisioning[?userId=<id>]
 *
 * Server component — admin only.
 * When a userId is provided in the query string, fetches readiness diagnostics
 * and passes them to the interactive provisioning panel.
 */

import { requireAdmin } from '@/lib/auth-guards';
import { careConnectServerApi } from '@/lib/careconnect-server-api';
import { ServerApiError } from '@/lib/server-api-client';
import type { ProviderReadinessDiagnostics } from '@/types/careconnect';
import { ProviderProvisioningPanel } from '@/components/careconnect/admin/provider-provisioning-panel';

export const dynamic = 'force-dynamic';


interface PageProps {
  searchParams: Promise<{ userId?: string }>;
}

export default async function ProviderProvisioningPage({ searchParams }: PageProps) {
  await requireAdmin();

  const { userId } = await searchParams;

  let diagnostics: ProviderReadinessDiagnostics | null = null;
  let loadError: string | null = null;

  if (userId) {
    try {
      diagnostics = await careConnectServerApi.adminProvisioning.getReadiness(userId);
    } catch (err) {
      if (err instanceof ServerApiError) {
        if (err.status === 404) {
          loadError = `User '${userId}' was not found.`;
        } else if (err.status === 403) {
          loadError = 'You do not have permission to view this user.';
        } else {
          loadError = `Failed to load readiness diagnostics (HTTP ${err.status}).`;
        }
      } else {
        loadError = 'Failed to load readiness diagnostics. Please try again.';
      }
    }
  }

  return (
    <div className="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      <div className="mb-6">
        <h1 className="text-xl font-semibold text-gray-900">CareConnect Provider Provisioning</h1>
        <p className="text-sm text-gray-500 mt-0.5">
          Provision a provider organization to receive CareConnect referrals.
          Enter a user ID to check readiness and run provisioning.
        </p>
      </div>

      {loadError && (
        <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700 mb-6">
          {loadError}
        </div>
      )}

      <ProviderProvisioningPanel
        initialUserId={userId ?? ''}
        initialDiagnostics={diagnostics}
      />
    </div>
  );
}
