/**
 * CC2-INT-B05 — Provider Dashboard.
 *
 * Lists referrals assigned to the authenticated provider's organisation.
 * Data scoping is enforced server-side by the CareConnect API based on
 * the JWT orgId / orgType claims — no client-side filtering required.
 */

import Link from 'next/link';
import { requireExternalPortal } from '@/lib/auth-guards';
import { careConnectServerApi } from '@/lib/careconnect-server-api';
import { ServerApiError } from '@/lib/server-api-client';
import type { ReferralSummary } from '@/types/careconnect';

export const dynamic = 'force-dynamic';


// ── Onboarding CTA banner (COMMON_PORTAL stage) ───────────────────────────────

function OnboardingCtaBanner() {
  return (
    <div className="rounded-xl border border-indigo-200 bg-indigo-50 px-5 py-4 flex items-start gap-4">
      <div className="mt-0.5 flex-shrink-0 w-9 h-9 rounded-lg bg-indigo-600 flex items-center justify-center">
        <svg className="w-5 h-5 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.75}
            d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4" />
        </svg>
      </div>
      <div className="flex-1 min-w-0">
        <p className="text-sm font-semibold text-indigo-900">Set up your dedicated workspace</p>
        <p className="text-sm text-indigo-700 mt-0.5">
          Create your own secure tenant portal with CareConnect, custom subdomain, and team management.
        </p>
      </div>
      <Link
        href="/provider/onboarding"
        className="flex-shrink-0 self-center inline-flex items-center gap-1.5 px-4 py-2 rounded-lg
                   bg-indigo-600 hover:bg-indigo-700 text-white text-sm font-medium transition-colors"
      >
        Get started
        <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5l7 7-7 7" />
        </svg>
      </Link>
    </div>
  );
}

// ── Status badge ──────────────────────────────────────────────────────────────

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
    <span className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-medium ${cls}`}>
      {status}
    </span>
  );
}

// ── Urgency badge ─────────────────────────────────────────────────────────────

const URGENCY_STYLES: Record<string, string> = {
  Urgent:   'text-red-600 font-semibold',
  High:     'text-orange-600',
  Normal:   'text-gray-500',
  Low:      'text-gray-400',
};

function UrgencyLabel({ urgency }: { urgency: string }) {
  const cls = URGENCY_STYLES[urgency] ?? 'text-gray-500';
  return <span className={`text-xs ${cls}`}>{urgency}</span>;
}

// ── Date formatter ────────────────────────────────────────────────────────────

function formatDate(iso: string) {
  try {
    return new Intl.DateTimeFormat('en-AU', {
      day:   '2-digit',
      month: 'short',
      year:  'numeric',
    }).format(new Date(iso));
  } catch {
    return iso;
  }
}

// ── Empty state ───────────────────────────────────────────────────────────────

function EmptyState() {
  return (
    <div className="bg-white rounded-xl border border-gray-200 p-12 text-center">
      <div className="w-12 h-12 bg-gray-100 rounded-full flex items-center justify-center mx-auto mb-4">
        <svg className="w-6 h-6 text-gray-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5}
            d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
        </svg>
      </div>
      <p className="text-sm font-medium text-gray-700 mb-1">No referrals assigned</p>
      <p className="text-xs text-gray-400">
        Referrals sent to your practice will appear here.
      </p>
    </div>
  );
}

// ── Referral row ──────────────────────────────────────────────────────────────

function ReferralRow({ referral }: { referral: ReferralSummary }) {
  const clientName = [referral.clientFirstName, referral.clientLastName]
    .filter(Boolean).join(' ') || '—';

  return (
    <Link
      href={`/provider/referrals/${referral.id}`}
      className="grid grid-cols-[1fr_1fr_auto_auto_auto] gap-4 items-center
                 px-4 py-3.5 hover:bg-gray-50 transition-colors border-b border-gray-100
                 last:border-b-0"
    >
      <div>
        <p className="text-sm font-medium text-gray-900 truncate">{clientName}</p>
        {referral.caseNumber && (
          <p className="text-xs text-gray-400 mt-0.5">{referral.caseNumber}</p>
        )}
      </div>

      <p className="text-sm text-gray-600 truncate">{referral.requestedService || '—'}</p>

      <UrgencyLabel urgency={referral.urgency} />

      <StatusBadge status={referral.status} />

      <p className="text-xs text-gray-400 whitespace-nowrap">
        {formatDate(referral.createdAtUtc)}
      </p>
    </Link>
  );
}

// ── Page ──────────────────────────────────────────────────────────────────────

export default async function ProviderDashboardPage() {
  await requireExternalPortal('/provider/dashboard');

  let referrals: ReferralSummary[] = [];
  let fetchError: string | null = null;
  let showOnboardingCta = false;

  // Fetch referrals and onboarding status in parallel.
  const [referralsResult, onboardingStatus] = await Promise.allSettled([
    careConnectServerApi.referrals.search({ pageSize: 100 }),
    careConnectServerApi.onboarding.getStatus(),
  ]);

  if (referralsResult.status === 'fulfilled') {
    referrals = referralsResult.value.items ?? [];
  } else {
    const err = referralsResult.reason;
    if (err instanceof ServerApiError) {
      fetchError = err.isForbidden
        ? 'You do not have access to this data. Please contact your administrator.'
        : err.message;
    } else {
      fetchError = 'Failed to load referrals. Please try again.';
    }
  }

  if (onboardingStatus.status === 'fulfilled') {
    showOnboardingCta = onboardingStatus.value.canOnboard === true;
  }
  // If onboarding status fails (e.g. not a provider with a record), silently omit CTA.

  return (
    <div className="space-y-5">
      {/* Workspace setup CTA — shown to COMMON_PORTAL stage providers only */}
      {showOnboardingCta && <OnboardingCtaBanner />}

      {/* Page header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold text-gray-900">My Referrals</h1>
          <p className="text-sm text-gray-500 mt-0.5">
            Referrals assigned to your practice
          </p>
        </div>
        {referrals.length > 0 && (
          <span className="text-xs text-gray-400 bg-gray-100 px-2.5 py-1 rounded-full">
            {referrals.length} referral{referrals.length !== 1 ? 's' : ''}
          </span>
        )}
      </div>

      {/* Error state */}
      {fetchError && (
        <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
          {fetchError}
        </div>
      )}

      {/* Referral list */}
      {!fetchError && referrals.length === 0 && <EmptyState />}

      {!fetchError && referrals.length > 0 && (
        <div className="bg-white rounded-xl border border-gray-200 overflow-hidden">
          {/* Table header */}
          <div className="grid grid-cols-[1fr_1fr_auto_auto_auto] gap-4 items-center
                          px-4 py-2.5 bg-gray-50 border-b border-gray-200">
            <span className="text-xs font-semibold text-gray-500 uppercase tracking-wide">Client</span>
            <span className="text-xs font-semibold text-gray-500 uppercase tracking-wide">Service</span>
            <span className="text-xs font-semibold text-gray-500 uppercase tracking-wide">Urgency</span>
            <span className="text-xs font-semibold text-gray-500 uppercase tracking-wide">Status</span>
            <span className="text-xs font-semibold text-gray-500 uppercase tracking-wide">Received</span>
          </div>

          {/* Rows */}
          <div>
            {referrals.map(r => (
              <ReferralRow key={r.id} referral={r} />
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
