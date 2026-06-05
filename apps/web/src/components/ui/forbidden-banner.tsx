'use client';

/**
 * LS-ID-TNT-015: Shared inline permission-denied notice.
 *
 * Renders an amber informational banner when a user can view a section
 * but lacks the permission to take action within it.
 *
 * This is NOT an error — it's a UX guide. The backend still enforces
 * the actual security boundary on any API call.
 *
 * Usage:
 *   <ForbiddenBanner action="approve funding applications" />
 *   <ForbiddenBanner message="Your role does not include referral management." />
 */

interface ForbiddenBannerProps {
  /** Short description of what the user cannot do, e.g. "accept referrals" */
  action?: string;
  /** Full custom message (overrides action-based default) */
  message?: string;
  className?: string;
}

export function ForbiddenBanner({ action, message, className = '' }: ForbiddenBannerProps) {
  const text =
    message ??
    (action
      ? `You do not have permission to ${action}. Contact your administrator if you believe this is incorrect.`
      : 'You do not have permission to perform this action. Contact your administrator if you believe this is incorrect.');

  return (
    <div
      className={`flex items-start gap-2 bg-amber-50 border border-amber-200 rounded-md px-3 py-2.5 text-sm text-amber-800 ${className}`}
      role="status"
    >
      <i className="ri-lock-line mt-0.5 flex-shrink-0 text-amber-600" aria-hidden="true" />
      <span>{text}</span>
    </div>
  );
}
