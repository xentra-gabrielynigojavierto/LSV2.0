'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { careConnectApi } from '@/lib/careconnect-api';
import { ApiError } from '@/lib/api-client';
import { useToast } from '@/lib/toast-context';
import { buildReferralDetailUrl } from '@/lib/referral-nav';
import type { ReferralSummary } from '@/types/careconnect';

interface ReferralQuickActionsProps {
  referral:   ReferralSummary;
  isReferrer: boolean;
  isReceiver: boolean;
  contextQs?: string;
}

const ACTIONABLE_FOR_RECEIVER = ['New', 'NewOpened', 'Received', 'Contacted'];

export function ReferralQuickActions({ referral, isReferrer, isReceiver, contextQs = '' }: ReferralQuickActionsProps) {
  const router          = useRouter();
  const { show: toast } = useToast();

  const [busy, setBusy] = useState<string | null>(null);

  const canAccept = isReceiver && ACTIONABLE_FOR_RECEIVER.includes(referral.status);

  async function handleAccept() {
    setBusy('accept');
    try {
      await careConnectApi.referrals.update(referral.id, {
        requestedService: referral.requestedService,
        urgency:          referral.urgency,
        status:           'Accepted',
      });
      toast('Referral accepted.', 'success');
      router.refresh();
    } catch (err) {
      const msg = err instanceof ApiError ? err.message : 'Failed to accept referral.';
      toast(msg, 'error');
    } finally {
      setBusy(null);
    }
  }

  return (
    <div className="flex items-center gap-2 flex-wrap">
      <Link
        href={buildReferralDetailUrl(referral.id, contextQs)}
        className="text-xs font-medium px-2.5 py-1 border border-gray-200 text-gray-700 rounded hover:bg-gray-50 transition-colors whitespace-nowrap"
      >
        View
      </Link>

      {canAccept && (
        <button
          onClick={handleAccept}
          disabled={!!busy}
          className="text-xs font-medium px-2.5 py-1 bg-green-600 text-white rounded hover:bg-green-700 disabled:opacity-60 transition-colors whitespace-nowrap"
        >
          {busy === 'accept' ? 'Accepting…' : 'Accept'}
        </button>
      )}
    </div>
  );
}
