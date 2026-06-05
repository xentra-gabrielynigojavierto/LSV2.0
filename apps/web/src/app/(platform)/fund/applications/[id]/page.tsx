'use client';

import { useEffect, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import Link from 'next/link';
import { useSession } from '@/hooks/use-session';
import { ProductRole } from '@/types';
import { fundApi } from '@/lib/fund-api';
import { ApiError } from '@/lib/api-client';
import { FundingApplicationDetailPanel } from '@/components/fund/funding-application-detail-panel';
import { SubmitApplicationPanel } from '@/components/fund/submit-application-panel';
import { ReviewDecisionPanel } from '@/components/fund/review-decision-panel';
import type { FundingApplicationDetail } from '@/types/fund';

/**
 * /fund/applications/[id] — Application detail.
 *
 * Implemented as a Client Component because action panels mutate state
 * (submit / begin-review / approve / deny) and update the page optimistically
 * without a full reload.
 *
 * UX role shaping:
 *   - SYNQFUND_REFERRER + Draft:     shows SubmitApplicationPanel
 *   - SYNQFUND_REFERRER + other:     read-only (no further actions)
 *   - SYNQFUND_FUNDER + Submitted:   shows ReviewDecisionPanel (Begin Review)
 *   - SYNQFUND_FUNDER + InReview:    shows ReviewDecisionPanel (Approve / Deny)
 *   - Approved / Rejected:           read-only for both roles
 *
 * Backend is the real security boundary; 403 / 409 are surfaced gracefully.
 */
export default function ApplicationDetailPage() {
  const params = useParams<{ id: string }>();
  const router = useRouter();
  const { session, isLoading: sessionLoading } = useSession();

  const isReferrer = session?.productRoles.includes(ProductRole.SynqFundReferrer) ?? false;
  const isFunder   = session?.productRoles.includes(ProductRole.SynqFundFunder)   ?? false;

  const [application, setApplication] = useState<FundingApplicationDetail | null>(null);
  const [loading,     setLoading]     = useState(true);
  const [error,       setError]       = useState<string | null>(null);

  useEffect(() => {
    if (sessionLoading) return;
    if (!session) { router.push('/login'); return; }

    async function load() {
      setLoading(true);
      try {
        const { data } = await fundApi.applications.getById(params?.id ?? '');
        setApplication(data);
      } catch (err) {
        if (err instanceof ApiError) {
          if (err.isUnauthorized) { router.push('/login'); return; }
          if (err.isNotFound)     { setError('Application not found.'); return; }
          if (err.isForbidden)    { setError('You do not have access to this application.'); return; }
          setError(err.message);
        } else {
          setError('Failed to load application.');
        }
      } finally {
        setLoading(false);
      }
    }

    load();
  }, [params?.id ?? '', session, sessionLoading, router]);

  if (sessionLoading || loading) {
    return (
      <div className="space-y-4 animate-pulse">
        <div className="h-6 w-32 bg-gray-100 rounded" />
        <div className="h-48 bg-gray-100 rounded" />
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

  if (!application) return null;

  const { status } = application;
  const isTerminal = status === 'Approved' || status === 'Rejected';

  // Determine which action panel to show
  const showSubmitPanel  = isReferrer && status === 'Draft';
  const showReviewPanel  = isFunder   && (status === 'Submitted' || status === 'InReview');

  return (
    <div className="space-y-4">
      {/* Back link */}
      <nav>
        <Link
          href="/fund/applications"
          className="text-sm text-gray-500 hover:text-gray-800 transition-colors"
        >
          ← Back to Applications
        </Link>
      </nav>

      {/* Main detail */}
      <FundingApplicationDetailPanel application={application} />

      {/* Referrer: submit panel (Draft only) */}
      {showSubmitPanel && (
        <SubmitApplicationPanel
          application={application}
          onUpdated={setApplication}
        />
      )}

      {/* Funder: review / decision panel (Submitted or InReview) */}
      {showReviewPanel && (
        <ReviewDecisionPanel
          application={application}
          onUpdated={setApplication}
        />
      )}

      {/* Terminal state note for referrer */}
      {isReferrer && isTerminal && (
        <div className="bg-gray-50 border border-gray-200 rounded-lg px-4 py-3 text-xs text-gray-400">
          This application is {status === 'Approved' ? 'approved' : 'denied'} — no further actions are available.
        </div>
      )}
    </div>
  );
}
