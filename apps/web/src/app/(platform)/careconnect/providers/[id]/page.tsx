'use client';

import { useEffect, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useSession } from '@/hooks/use-session';
import { ProductRole } from '@/types';
import { careConnectApi } from '@/lib/careconnect-api';
import { ApiError } from '@/lib/api-client';
import { useSettings } from '@/contexts/settings-context';
import { ProviderDetailCard } from '@/components/careconnect/provider-detail-card';
import { CreateReferralForm } from '@/components/careconnect/create-referral-form';
import { ProviderAvailabilityPreview } from '@/components/careconnect/provider-availability-preview';
import { PermissionTooltip } from '@/components/ui/permission-tooltip';
import { DisabledReasons } from '@/lib/disabled-reasons';
import type { ProviderDetail } from '@/types/careconnect';

/**
 * /careconnect/providers/[id] — Provider detail.
 *
 * Implemented as a Client Component so the "Create Referral" modal can be
 * toggled without a full page reload.  Data is fetched on the client via the
 * BFF proxy (apiClient → /api/careconnect/* → gateway with Bearer).
 *
 * Access: CARECONNECT_REFERRER only (same rule as the list page).
 *   - CARECONNECT_RECEIVER users never navigate here from the UI.
 *   - Backend enforces tenant-scoped access; a 403 is surfaced gracefully.
 *
 * UX shaping:
 *   - "Create Referral" CTA is visible to CARECONNECT_REFERRER, TenantAdmin,
 *     and PlatformAdmin users — any user who can view a provider can refer.
 *   - If the provider is not accepting referrals, the button is disabled
 *     with a tooltip — the backend would reject it anyway.
 */
export default function ProviderDetailPage() {
  const params  = useParams<{ id: string }>();
  const router  = useRouter();
  const { session, isLoading: sessionLoading } = useSession();
  const { careConnect } = useSettings();

  const [provider, setProvider] = useState<ProviderDetail | null>(null);
  const [loading,  setLoading]  = useState(true);
  const [error,    setError]    = useState<string | null>(null);
  const [showForm, setShowForm] = useState(false);

  const canRefer =
    (session?.productRoles.includes(ProductRole.CareConnectReferrer) ||
     session?.isPlatformAdmin ||
     session?.isTenantAdmin) ?? false;

  useEffect(() => {
    if (sessionLoading) return;
    if (!session) { router.push('/login'); return; }

    async function loadProvider() {
      setLoading(true);
      try {
        const { data } = await careConnectApi.providers.getById(params?.id ?? '');
        setProvider(data);
      } catch (err) {
        if (err instanceof ApiError) {
          if (err.isUnauthorized) { router.push('/login'); return; }
          if (err.isForbidden)    { setError('You do not have access to this provider.'); return; }
          if (err.isNotFound)     { setError('Provider not found.'); return; }
          setError(err.message);
        } else {
          setError('Failed to load provider details.');
        }
      } finally {
        setLoading(false);
      }
    }

    loadProvider();
  }, [params?.id ?? '', session, sessionLoading, router]);

  if (sessionLoading || loading) {
    return (
      <div className="space-y-4 animate-pulse">
        <div className="h-8 bg-gray-100 rounded w-48" />
        <div className="h-40 bg-gray-100 rounded" />
      </div>
    );
  }

  if (error) {
    return (
      <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
        {error}
      </div>
    );
  }

  if (!provider) return null;

  return (
    <div className="space-y-4">
      {/* Back link */}
      <nav>
        <a
          href="/careconnect/providers"
          className="text-sm text-gray-500 hover:text-gray-800 transition-colors"
        >
          ← Back to Providers
        </a>
      </nav>

      {/* Provider detail card */}
      <ProviderDetailCard provider={provider} />

      {/* Availability preview — shown only when availability checking is enabled */}
      {careConnect.requireAvailabilityCheck && (
        <ProviderAvailabilityPreview providerId={provider.id} providerName={provider.displayLabel} />
      )}

      {/* Create Referral CTA — only for referrers */}
      {canRefer && (
        <div className="flex items-center gap-3">
          {/*
            LS-ID-TNT-015-004: Upgraded from native `title` to PermissionTooltip so
            the explanation is consistently styled and keyboard-accessible.
          */}
          <PermissionTooltip
            show={!provider.acceptingReferrals}
            message={DisabledReasons.externalBlock('This provider is not currently accepting referrals.').message}
          >
            <button
              onClick={() => setShowForm(true)}
              disabled={!provider.acceptingReferrals}
              className="bg-primary text-white text-sm font-medium px-5 py-2 rounded-md hover:opacity-90 disabled:opacity-40 disabled:cursor-not-allowed transition-opacity"
            >
              Create Referral
            </button>
          </PermissionTooltip>

          {!provider.acceptingReferrals && (
            <span className="text-sm text-gray-400">
              This provider is not accepting referrals at this time.
            </span>
          )}
        </div>
      )}

      {/* Create Referral modal form */}
      {showForm && (
        <CreateReferralForm
          providerId={provider.id}
          providerName={provider.displayLabel}
          onClose={() => setShowForm(false)}
        />
      )}
    </div>
  );
}
