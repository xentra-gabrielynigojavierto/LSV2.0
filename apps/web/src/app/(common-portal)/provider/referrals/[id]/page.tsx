/**
 * CC2-INT-B05 — Provider Referral Detail.
 *
 * Shows a single referral assigned to the authenticated provider.
 * Access is enforced at the CareConnect API level (403 if not receiver).
 *
 * Components reused from Tenant Portal where appropriate.
 * AttachmentPanel: canUpload=false — providers cannot upload documents.
 */

import { notFound } from 'next/navigation';
import Link from 'next/link';
import { requireExternalPortal } from '@/lib/auth-guards';
import { careConnectServerApi } from '@/lib/careconnect-server-api';
import { ServerApiError } from '@/lib/server-api-client';
import { ReferralDetailPanel } from '@/components/careconnect/referral-detail-panel';
import { ReferralStatusActions } from '@/components/careconnect/referral-status-actions';
import { ReferralTimeline } from '@/components/careconnect/referral-timeline';
import { AttachmentPanel } from '@/components/careconnect/attachment-panel';

interface Props {
  params: Promise<{ id: string }>;
}

// ── Status badge (standalone — no tenant layout dependency) ───────────────────

const STATUS_STYLES: Record<string, string> = {
  New:       'bg-blue-100 text-blue-700',
  Pending:   'bg-yellow-100 text-yellow-700',
  Accepted:  'bg-green-100 text-green-700',
  Completed: 'bg-gray-100 text-gray-600',
  Declined:  'bg-red-100 text-red-700',
  Cancelled: 'bg-red-50 text-red-400',
};

function StatusBadge({ status }: { status: string }) {
  const cls = STATUS_STYLES[status] ?? 'bg-gray-100 text-gray-600';
  return (
    <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${cls}`}>
      {status}
    </span>
  );
}

// ── Page ──────────────────────────────────────────────────────────────────────

export default async function ProviderReferralDetailPage({ params }: Props) {
  const { id } = await params;
  await requireExternalPortal(`/provider/referrals/${id}`);

  let referral = null;
  let fetchError: string | null = null;

  try {
    referral = await careConnectServerApi.referrals.getById(id);
  } catch (err) {
    if (err instanceof ServerApiError) {
      if (err.isNotFound) notFound();
      fetchError = err.isForbidden
        ? 'You do not have access to this referral. Your practice is not a participant.'
        : err.message;
    } else {
      fetchError = 'Failed to load referral.';
    }
  }

  return (
    <div className="space-y-5">
      {/* Back navigation */}
      <nav>
        <Link
          href="/provider/dashboard"
          className="text-sm text-gray-500 hover:text-gray-800 transition-colors"
        >
          ← Back to my referrals
        </Link>
      </nav>

      {/* Error state */}
      {fetchError && (
        <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
          {fetchError}
        </div>
      )}

      {referral && (
        <>
          {/* Referral heading */}
          <div className="flex items-start justify-between gap-4 flex-wrap">
            <div>
              <h1 className="text-xl font-semibold text-gray-900">
                {[referral.clientFirstName, referral.clientLastName].filter(Boolean).join(' ') || 'Referral'}
              </h1>
              {referral.caseNumber && (
                <p className="text-sm text-gray-400 mt-0.5">{referral.caseNumber}</p>
              )}
            </div>
            <StatusBadge status={referral.status} />
          </div>

          {/* Core detail panels */}
          <div className="space-y-4">
            <ReferralDetailPanel referral={referral} />

            {/* Accept / Decline actions — providers are always receivers, never referrers */}
            <ReferralStatusActions referral={referral} isReceiver={true} isReferrer={false} />

            {/* Documents — view only; providers cannot upload */}
            <AttachmentPanel
              entityType="referral"
              entityId={referral.id}
              canUpload={false}
            />

            {/* Status history timeline */}
            <ReferralTimeline referralId={referral.id} />
          </div>
        </>
      )}
    </div>
  );
}
