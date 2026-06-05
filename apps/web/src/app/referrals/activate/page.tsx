/**
 * LSCC-008: Provider activation intent capture page.
 *
 * Reached by clicking "Activate & Accept Referral" on the activation landing page.
 * Server component — fetches referral context, renders the activation form (client).
 *
 * Flow:
 *   /referrals/activate?referralId=...&token=...
 *     → validates token via public-summary endpoint
 *     → shows referral context + ActivationForm (email + name capture)
 *     → on submit: emits ActivationStarted funnel event, shows confirmation
 *
 * Deferred step (documented in LSCC-008 report):
 *   Automated tenant provisioning is not yet implemented.
 *   The activation form records intent and an admin will provision the account manually.
 *   After provisioning, the provider logs in and accepts the referral from the portal.
 */

import { redirect } from 'next/navigation';
import Link from 'next/link';
import { ActivationForm } from './activation-form';

export const dynamic = 'force-dynamic';


const GATEWAY_URL = process.env.GATEWAY_URL ?? 'http://127.0.0.1:5010';

interface PageProps {
  searchParams: Promise<{ referralId?: string; token?: string }>;
}

interface PublicSummary {
  referralId:       string;
  clientFirstName:  string;
  clientLastName:   string;
  referrerName:     string;
  providerName:     string;
  requestedService: string;
  status:           string;
  isAlreadyAccepted: boolean;
}

export default async function ActivatePage({ searchParams }: PageProps) {
  const sp = await searchParams;
  const referralId = sp.referralId?.trim() ?? '';
  const token      = sp.token?.trim() ?? '';

  if (!referralId || !token) {
    redirect('/referrals/accept/invalid?reason=missing-token');
  }

  let summary: PublicSummary | null = null;
  try {
    const resp = await fetch(
      `${GATEWAY_URL}/careconnect/api/referrals/${referralId}/public-summary?token=${encodeURIComponent(token)}`,
      { cache: 'no-store' },
    );
    if (resp.ok) summary = await resp.json();
  } catch {
    // fall through
  }

  if (!summary) {
    redirect('/referrals/accept/invalid?reason=expired-or-invalid');
  }

  // If already accepted, send them to the accepted state screen
  if (summary.isAlreadyAccepted) {
    redirect(`/referrals/accept/${referralId}?token=${encodeURIComponent(token)}`);
  }

  const clientName = [summary.clientFirstName, summary.clientLastName].filter(Boolean).join(' ');
  // CC2-INT-B05: land providers in the Common Portal after login, not the Tenant Portal.
  const loginUrl   = `/login?returnTo=${encodeURIComponent(`/provider/referrals/${referralId}`)}&reason=referral-view`;

  return (
    <main className="min-h-screen bg-gray-50 flex items-center justify-center p-4">
      <div className="max-w-lg w-full space-y-4">

        {/* Header */}
        <div>
          <Link
            href={`/referrals/accept/${referralId}?token=${encodeURIComponent(token)}`}
            className="text-sm text-gray-500 hover:text-gray-700 transition-colors"
          >
            ← Back to referral
          </Link>
        </div>

        {/* Referral context banner */}
        <div className="bg-white rounded-xl shadow-sm border border-gray-200 px-6 py-4">
          <p className="text-xs font-semibold text-gray-400 uppercase tracking-wide mb-2">Referral context</p>
          <div className="flex flex-wrap gap-x-6 gap-y-1">
            {clientName && (
              <span className="text-sm text-gray-700">
                <span className="font-medium">Client:</span> {clientName}
              </span>
            )}
            {summary.referrerName && (
              <span className="text-sm text-gray-700">
                <span className="font-medium">Referred by:</span> {summary.referrerName}
              </span>
            )}
            {summary.requestedService && (
              <span className="text-sm text-gray-700">
                <span className="font-medium">Service:</span> {summary.requestedService}
              </span>
            )}
          </div>
        </div>

        {/* Page heading */}
        <div className="text-center">
          <h1 className="text-xl font-semibold text-gray-900">Activate your CareConnect account</h1>
          <p className="text-sm text-gray-500 mt-1">
            Enter your details below and we&apos;ll get your account set up.
          </p>
        </div>

        {/* Activation form */}
        <ActivationForm summary={summary} token={token} referralId={referralId} />

        {/* Already have access */}
        <p className="text-center text-xs text-gray-400 pb-4">
          Already have platform access?{' '}
          <Link href={loginUrl} className="text-primary hover:underline">
            Log in to accept this referral
          </Link>
        </p>

      </div>
    </main>
  );
}
