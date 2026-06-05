import Link from 'next/link';

/**
 * LSCC-01-002-02: Blocking state for non-provisioned / misconfigured providers.
 *
 * Shown when an authenticated user attempts to access CareConnect referral pages
 * but lacks the required CareConnectReceiver product role or associated capabilities.
 *
 * Rules:
 *   - no referral data is rendered
 *   - no accept CTA is rendered
 *   - user is directed to contact their administrator
 *   - no self-provisioning path is suggested
 */

interface ReferralAccessBlockedProps {
  /** Optional machine-readable reason for internal diagnostics (not shown to user). */
  reason?: string;
}

export function ReferralAccessBlocked({ reason: _reason }: ReferralAccessBlockedProps) {
  return (
    <div className="min-h-[60vh] flex items-center justify-center p-4">
      <div className="max-w-md w-full bg-white rounded-xl shadow-sm border border-gray-200 overflow-hidden">
        <div className="h-1.5 w-full bg-amber-400" />
        <div className="p-8 text-center">

          <div className="w-14 h-14 bg-amber-100 rounded-full flex items-center justify-center mx-auto mb-5">
            <svg
              className="w-7 h-7 text-amber-600"
              fill="none"
              viewBox="0 0 24 24"
              stroke="currentColor"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2}
                d="M12 15v2m0 0v2m0-2h2m-2 0H10m2-10v4m0 0a5 5 0 00-5 5h10a5 5 0 00-5-5z"
              />
            </svg>
          </div>

          <h2 className="text-xl font-semibold text-gray-900 mb-2">
            Access Not Available
          </h2>

          <p className="text-sm text-gray-500 leading-relaxed mb-6">
            Your account is not yet activated for CareConnect.
            Please contact your administrator to request access.
          </p>

          <div className="bg-gray-50 border border-gray-200 rounded-lg p-4 text-left mb-6">
            <p className="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-2">
              What to do next
            </p>
            <ol className="space-y-2 text-sm text-gray-600">
              <li className="flex gap-2">
                <span className="font-semibold text-gray-400 shrink-0">1.</span>
                Contact your organisation&apos;s platform administrator.
              </li>
              <li className="flex gap-2">
                <span className="font-semibold text-gray-400 shrink-0">2.</span>
                Ask them to assign you the CareConnect Receiver role.
              </li>
              <li className="flex gap-2">
                <span className="font-semibold text-gray-400 shrink-0">3.</span>
                Once activated, log in again to access your referrals.
              </li>
            </ol>
          </div>

          <Link
            href="/dashboard"
            className="inline-block bg-primary text-white text-sm font-medium px-5 py-2 rounded-lg hover:opacity-90 transition-opacity"
          >
            Return to Dashboard
          </Link>

          <p className="mt-4 text-xs text-gray-400">
            If you believe this is an error, please contact your system administrator.
          </p>
        </div>
      </div>
    </div>
  );
}
