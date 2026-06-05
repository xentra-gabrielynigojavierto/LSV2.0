/**
 * LSCC-005 / LSCC-008 / LSCC-01-002-01: Legacy referral landing.
 *
 * Server component — fetches referral context from the backend before rendering.
 * No authentication required; the view token is the proof-of-identity.
 *
 * LSCC-01-002-01: New email links are now routed directly to login via
 * /referrals/view → /login?returnTo=... This page is retained as a safe
 * handler for legacy links (old emails pointing to /referrals/accept/{id}).
 * Direct token-based acceptance is no longer available here.
 *
 * Routing:
 *   referralId === 'invalid'
 *     → <InvalidScreen> (bad/missing/expired token)
 *
 *   token validates but referral already accepted
 *     → <AlreadyAcceptedScreen>
 *
 *   token validates, referral is New
 *     → <ActivationLanding> (auth-required: Activate & Accept | Log in)
 *
 *   token cannot be validated (null summary)
 *     → redirect to invalid screen
 */

import { redirect } from 'next/navigation';
import Link from 'next/link';
import { ActivationLanding } from './activation-landing';

const GATEWAY_URL  = process.env.GATEWAY_URL ?? 'http://127.0.0.1:5010';
const INVALID_ID   = 'invalid';

interface PageProps {
  params:       Promise<{ referralId: string }>;
  searchParams: Promise<{ token?: string; reason?: string }>;
}

interface PublicAttachmentInfo {
  id:            string;
  fileName:      string;
  contentType:   string;
  fileSizeBytes: number;
}

interface PublicSummary {
  referralId:           string;
  clientFirstName:      string;
  clientLastName:       string;
  referrerName:         string;
  providerName:         string;
  providerPhone:        string;
  providerEmail:        string;
  providerAddressLine1: string;
  providerCity:         string;
  providerState:        string;
  providerPostalCode:   string;
  requestedService:     string;
  status:               string;
  isAlreadyAccepted:    boolean;
  attachments:          PublicAttachmentInfo[];
}

// ── Static screen components (no interactivity needed) ────────────────────────

function InvalidScreen({ reason }: { reason: string }) {
  const isRevoked = reason === 'revoked';
  const isMissing = reason === 'missing-token';

  return (
    <main className="min-h-screen bg-gray-50 flex items-center justify-center p-4">
      <div className="max-w-lg w-full bg-white rounded-xl shadow-sm border border-gray-200 overflow-hidden">
        <div className={`h-1.5 w-full ${isRevoked ? 'bg-orange-400' : 'bg-red-400'}`} />
        <div className="p-8 text-center">
          <div className="w-14 h-14 rounded-full flex items-center justify-center mx-auto mb-5 bg-gray-100">
            {isRevoked ? (
              <svg className="w-7 h-7 text-orange-500" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                  d="M12 15v2m0 0v2m0-2h2m-2 0H10m7-7V9a5 5 0 00-10 0v1M5 12h14a1 1 0 011 1v7a1 1 0 01-1 1H5a1 1 0 01-1-1v-7a1 1 0 011-1z" />
              </svg>
            ) : (
              <svg className="w-7 h-7 text-red-500" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                  d="M12 9v2m0 4h.01M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z" />
              </svg>
            )}
          </div>

          <h1 className="text-xl font-semibold text-gray-900 mb-2">
            {isMissing  && 'Link Missing'}
            {isRevoked  && 'Link Revoked'}
            {!isMissing && !isRevoked && 'Link Expired or Invalid'}
          </h1>

          <p className="text-sm text-gray-500 leading-relaxed mb-6">
            {isMissing && 'No access token was found in the link. Please use the original email link sent to you.'}
            {isRevoked && (
              'This referral link has been revoked by the sending organisation. ' +
              'A new link may have been sent to you — please check your inbox, ' +
              'or contact the referring party to request a fresh invitation.'
            )}
            {!isMissing && !isRevoked && (
              'This referral link has expired or is no longer valid. ' +
              'Links are valid for 30 days from the date the referral was sent. ' +
              'Please contact the referring party to request a new link.'
            )}
          </p>

          <div className="bg-gray-50 border border-gray-200 rounded-lg p-4 text-left mb-6">
            <p className="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-3">What to do next</p>
            <ol className="space-y-2 text-sm text-gray-600">
              <li className="flex gap-2">
                <span className="font-semibold text-gray-400 shrink-0">1.</span>
                Check your inbox for a more recent email from the referring party.
              </li>
              <li className="flex gap-2">
                <span className="font-semibold text-gray-400 shrink-0">2.</span>
                If you cannot find a newer link, contact the referring party and ask them to resend the referral invitation.
              </li>
              <li className="flex gap-2">
                <span className="font-semibold text-gray-400 shrink-0">3.</span>
                <span>
                  If you are an existing platform user, you can{' '}
                  <Link href="/login" className="text-primary hover:underline">log in</Link>
                  {' '}to view referrals sent to your organisation.
                </span>
              </li>
            </ol>
          </div>

          <p className="text-xs text-gray-400">
            If you believe this is an error, please contact your system administrator.
          </p>
        </div>
      </div>
    </main>
  );
}

function AlreadyAcceptedScreen({ summary }: { summary: PublicSummary }) {
  const clientName = [summary.clientFirstName, summary.clientLastName].filter(Boolean).join(' ');
  // CC2-INT-B05: land providers in the Common Portal after login.
  const loginUrl   = `/login?returnTo=${encodeURIComponent(`/provider/referrals/${summary.referralId}`)}&reason=referral-view`;
  return (
    <main className="min-h-screen bg-gray-50 flex items-center justify-center p-4">
      <div className="max-w-md w-full bg-white rounded-xl shadow-sm border border-gray-200 overflow-hidden">
        <div className="h-1.5 w-full bg-green-400" />
        <div className="p-8 text-center">
          <div className="w-14 h-14 bg-green-100 rounded-full flex items-center justify-center mx-auto mb-4">
            <svg className="w-7 h-7 text-green-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
            </svg>
          </div>
          <h1 className="text-xl font-semibold text-gray-900 mb-2">Referral Already Accepted</h1>
          <p className="text-sm text-gray-500 mb-2">
            {clientName
              ? `The referral for ${clientName} has already been accepted.`
              : 'This referral has already been accepted.'}
          </p>
          <p className="text-sm text-gray-500 mb-6">
            The referring party has been notified. No further action is required from this link.
          </p>
          <Link
            href={loginUrl}
            className="inline-block bg-primary text-white text-sm font-medium px-5 py-2 rounded-lg hover:opacity-90 transition-opacity"
          >
            Log in to view in portal
          </Link>
          <p className="mt-4 text-xs text-gray-400">
            Log in to track this referral and manage future referrals in your dashboard.
          </p>
        </div>
      </div>
    </main>
  );
}

// ── Server Component ──────────────────────────────────────────────────────────

export default async function ReferralAcceptPage({ params, searchParams }: PageProps) {
  const { referralId } = await params;
  const sp = await searchParams;
  const token  = sp.token?.trim()  ?? '';
  const reason = sp.reason?.trim() ?? '';

  // Static invalid route (e.g. /referrals/accept/invalid?reason=...)
  if (referralId === INVALID_ID) {
    return <InvalidScreen reason={reason} />;
  }

  if (!token) {
    redirect('/referrals/accept/invalid?reason=missing-token');
  }

  // Fetch limited public summary — token is validated server-side
  let summary: PublicSummary | null = null;
  try {
    const resp = await fetch(
      `${GATEWAY_URL}/careconnect/api/referrals/${referralId}/public-summary?token=${encodeURIComponent(token)}`,
      { cache: 'no-store' },
    );
    if (resp.ok) {
      summary = await resp.json();
    }
  } catch {
    // network error — fall through to invalid
  }

  if (!summary) {
    redirect('/referrals/accept/invalid?reason=expired-or-invalid');
  }

  if (summary.isAlreadyAccepted) {
    return <AlreadyAcceptedScreen summary={summary} />;
  }

  return <ActivationLanding summary={summary} token={token} referralId={referralId} />;
}
