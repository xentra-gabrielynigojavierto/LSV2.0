'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { careConnectApi } from '@/lib/careconnect-api';
import type { ReferralDetail, ReferralNotification } from '@/types/careconnect';

interface ReferralDeliveryCardProps {
  referral: ReferralDetail;
}

// LSCC-005-02: supports derived retry states (Retrying, RetryExhausted) in addition to base states
function statusBadge(status: string | undefined): { label: string; css: string } {
  switch (status) {
    case 'Sent':          return { label: 'Sent',           css: 'text-green-700  bg-green-50   border-green-200'  };
    case 'Failed':        return { label: 'Failed',         css: 'text-red-700    bg-red-50     border-red-200'    };
    case 'Retrying':      return { label: 'Retrying…',      css: 'text-yellow-700 bg-yellow-50  border-yellow-200' };
    case 'RetryExhausted':return { label: 'Retry Exhausted',css: 'text-red-800    bg-red-100    border-red-300'    };
    case 'Pending':       return { label: 'Pending',        css: 'text-yellow-700 bg-yellow-50  border-yellow-200' };
    default:              return { label: 'Not sent',       css: 'text-gray-500   bg-gray-50    border-gray-200'   };
  }
}

function NotifTypePill({ type, source }: { type: string; source?: string }) {
  const baseLabel = type === 'ReferralEmailResent'    ? 'Resent'
                  : type === 'ReferralEmailAutoRetry' ? 'Auto-Retry'
                  : type.replace('Referral', '');
  const srcBadge  = source === 'ManualResend' ? ' · Manual'
                  : source === 'AutoRetry'    ? ' · Auto'
                  : '';
  return (
    <span className="inline-flex items-center px-1.5 py-0.5 rounded text-[10px] font-medium bg-gray-100 text-gray-600">
      {baseLabel}{srcBadge}
    </span>
  );
}

export function ReferralDeliveryCard({ referral }: ReferralDeliveryCardProps) {
  const router = useRouter();

  const [busy,     setBusy]     = useState(false);
  const [errorMsg, setErrorMsg] = useState('');

  const [showHistory,     setShowHistory]     = useState(false);
  const [notifications,   setNotifications]   = useState<ReferralNotification[] | null>(null);
  const [historyLoading,  setHistoryLoading]  = useState(false);

  const canResend = referral.status === 'New' || referral.status === 'NewOpened';

  async function handleResend() {
    if (!canResend) return;
    setBusy(true);
    setErrorMsg('');
    try {
      await careConnectApi.referrals.resendEmail(referral.id);
      setNotifications(null); // reset history cache
      router.refresh();
    } catch (err: unknown) {
      setErrorMsg(err instanceof Error ? err.message : 'Failed to resend email.');
    } finally {
      setBusy(false);
    }
  }

  async function handleRevoke() {
    if (!confirm(
      'Revoke this referral token?\n\n' +
      'The current email link will stop working. You can resend a new link afterwards.'
    )) return;
    setBusy(true);
    setErrorMsg('');
    try {
      await careConnectApi.referrals.revokeToken(referral.id);
      setNotifications(null);
      router.refresh();
    } catch (err: unknown) {
      setErrorMsg(err instanceof Error ? err.message : 'Failed to revoke token.');
    } finally {
      setBusy(false);
    }
  }

  async function toggleHistory() {
    if (showHistory) { setShowHistory(false); return; }
    setShowHistory(true);
    if (notifications !== null) return;
    setHistoryLoading(true);
    try {
      const response = await careConnectApi.referrals.getNotifications(referral.id);
      setNotifications(response.data);
    } catch {
      setNotifications([]);
    } finally {
      setHistoryLoading(false);
    }
  }

  const { label: emailLabel, css: emailCss } = statusBadge(referral.providerEmailStatus);

  return (
    <div className="bg-white border border-gray-200 rounded-lg px-5 py-4 space-y-3">
      {/* Section header */}
      <div className="flex items-center justify-between">
        <h3 className="text-xs font-semibold text-gray-400 uppercase tracking-wider">
          Email Delivery
        </h3>
        <button
          onClick={toggleHistory}
          className="text-xs text-primary hover:underline"
        >
          {showHistory ? 'Hide history' : 'View history'}
        </button>
      </div>

      {/* Status pill row */}
      <div className="flex flex-wrap items-center gap-2.5">
        <span className={`inline-flex items-center px-2.5 py-1 rounded-full text-xs font-medium border ${emailCss}`}>
          {emailLabel}
        </span>

        {(referral.providerEmailAttempts ?? 0) > 0 && (
          <span className="text-xs text-gray-400">
            {referral.providerEmailAttempts} attempt{referral.providerEmailAttempts !== 1 ? 's' : ''}
          </span>
        )}

        {(referral.tokenVersion ?? 1) > 1 && (
          <span className="text-xs text-gray-400" title="Token was revoked and reissued">
            Link v{referral.tokenVersion}
          </span>
        )}
      </div>

      {/* Failure reason */}
      {referral.providerEmailStatus === 'Failed' && referral.providerEmailFailureReason && (
        <p className="text-xs text-red-600 bg-red-50 border border-red-100 rounded px-3 py-2">
          Failure: {referral.providerEmailFailureReason}
        </p>
      )}

      {/* Action error */}
      {errorMsg && (
        <p className="text-xs text-red-600 bg-red-50 border border-red-100 rounded px-3 py-2">
          {errorMsg}
        </p>
      )}

      {/* Action buttons */}
      <div className="flex flex-wrap gap-2">
        {canResend && (
          <button
            onClick={handleResend}
            disabled={busy}
            className="text-xs font-medium px-3 py-1.5 bg-primary text-white rounded hover:opacity-90 disabled:opacity-60 transition-opacity"
          >
            {busy ? 'Sending…' : 'Resend Email'}
          </button>
        )}
        <button
          onClick={handleRevoke}
          disabled={busy}
          className="text-xs font-medium px-3 py-1.5 border border-gray-300 text-gray-700 rounded hover:bg-gray-50 disabled:opacity-60 transition-colors"
        >
          Revoke Link
        </button>
      </div>

      {/* Notification history drawer */}
      {showHistory && (
        <div className="border-t border-gray-100 pt-3 space-y-2">
          <p className="text-xs font-medium text-gray-500">Delivery history</p>

          {historyLoading && <p className="text-xs text-gray-400">Loading…</p>}

          {!historyLoading && notifications?.length === 0 && (
            <p className="text-xs text-gray-400">No notification records yet.</p>
          )}

          {!historyLoading && notifications && notifications.length > 0 && (
            <ul className="space-y-2">
              {notifications.map(n => {
                // LSCC-005-02: prefer derivedStatus (Retrying / RetryExhausted) if available,
                // fall back to the raw persisted status.
                const displayStatus = n.derivedStatus || n.status;
                const { label, css } = statusBadge(displayStatus);
                return (
                  <li key={n.id} className="flex items-start gap-2 text-xs">
                    <span className={`mt-0.5 inline-flex items-center px-1.5 py-0.5 rounded border text-[10px] font-medium shrink-0 ${css}`}>
                      {label}
                    </span>
                    <span className="flex-1 text-gray-600 min-w-0 space-y-0.5">
                      <span className="flex items-center gap-1 flex-wrap">
                        <NotifTypePill type={n.notificationType} source={n.triggerSource} />
                        {n.recipientAddress && (
                          <span className="text-gray-400 truncate">→ {n.recipientAddress}</span>
                        )}
                        <span className="text-gray-400">
                          · {n.attemptCount} attempt{n.attemptCount !== 1 ? 's' : ''}
                        </span>
                      </span>
                      {/* Failure reason */}
                      {n.failureReason && displayStatus !== 'Sent' && (
                        <span className="block text-red-500 truncate text-[11px]" title={n.failureReason}>
                          {n.failureReason}
                        </span>
                      )}
                      {/* LSCC-005-02: next retry hint */}
                      {displayStatus === 'Retrying' && n.nextRetryAfterUtc && (
                        <span className="block text-yellow-600 text-[11px]">
                          Next retry after {new Date(n.nextRetryAfterUtc).toLocaleTimeString('en-US', {
                            hour: '2-digit', minute: '2-digit', hour12: false
                          })} UTC
                        </span>
                      )}
                      {/* LSCC-005-02: retry exhausted hint */}
                      {displayStatus === 'RetryExhausted' && (
                        <span className="block text-red-700 text-[11px] font-medium">
                          All automatic retries exhausted — use Resend to try again.
                        </span>
                      )}
                    </span>
                    <span className="text-gray-300 whitespace-nowrap text-[11px]">
                      {new Date(n.createdAtUtc).toLocaleDateString()}
                    </span>
                  </li>
                );
              })}
            </ul>
          )}
        </div>
      )}
    </div>
  );
}
