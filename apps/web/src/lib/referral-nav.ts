/**
 * Referral navigation utilities (LSCC-007-01).
 *
 * Centralises the logic for:
 *   - building detail page URLs that carry list context
 *   - resolving the correct back-link from a detail page
 *
 * All functions are pure / side-effect-free so they can be unit tested
 * without a browser or Next.js runtime.
 */

// ── Types ─────────────────────────────────────────────────────────────────────

export interface ReferralNavParams {
  from?:        string;
  status?:      string;
  search?:      string;
  createdFrom?: string;
  createdTo?:   string;
}

export interface BackTarget {
  href:  string;
  label: string;
}

// ── Helpers ───────────────────────────────────────────────────────────────────

/**
 * Converts a ReferralNavParams object into a query string (no leading "?").
 * Omits undefined/empty values. Returns "" when nothing is present.
 */
export function referralNavParamsToQs(params: ReferralNavParams): string {
  const p = new URLSearchParams();
  if (params.from)        p.set('from',        params.from);
  if (params.status)      p.set('status',      params.status);
  if (params.search)      p.set('search',      params.search);
  if (params.createdFrom) p.set('createdFrom', params.createdFrom);
  if (params.createdTo)   p.set('createdTo',   params.createdTo);
  return p.toString();
}

// ── Public API ────────────────────────────────────────────────────────────────

/**
 * Builds a referral detail URL that carries the current list context so the
 * detail page can reconstruct an accurate back-link.
 *
 * @example
 *   buildReferralDetailUrl('abc-123', 'from=dashboard&status=New')
 *   // → '/careconnect/referrals/abc-123?from=dashboard&status=New'
 */
export function buildReferralDetailUrl(referralId: string, contextQs: string): string {
  return contextQs
    ? `/careconnect/referrals/${referralId}?${contextQs}`
    : `/careconnect/referrals/${referralId}`;
}

/**
 * Resolves the back-navigation target for a referral detail page.
 *
 * Priority:
 *   1. List filters present (status / search / date range)
 *      → back to the filtered referrals list, preserving all active params
 *   2. `from=dashboard` only (no list filters)
 *      → back to /careconnect/dashboard
 *   3. Fallback
 *      → back to /careconnect/referrals
 *
 * @example
 *   resolveReferralDetailBack({ from: 'dashboard', status: 'New' })
 *   // → { href: '/careconnect/referrals?from=dashboard&status=New',
 *   //     label: '← Back to Pending Referrals' }
 */
export function resolveReferralDetailBack(params: ReferralNavParams): BackTarget {
  const { from, status, search, createdFrom, createdTo } = params;
  const hasListFilters = !!(status || search || createdFrom || createdTo);

  if (hasListFilters) {
    const qs = referralNavParamsToQs({ from, status, search, createdFrom, createdTo });
    return {
      href:  `/careconnect/referrals${qs ? `?${qs}` : ''}`,
      label: buildBackLabel({ from, status }),
    };
  }

  if (from === 'dashboard') {
    return { href: '/careconnect/dashboard', label: '← Back to Dashboard' };
  }

  return { href: '/careconnect/referrals', label: '← Back to Referrals' };
}

// ── Internal helpers ──────────────────────────────────────────────────────────

function buildBackLabel({ from, status }: { from?: string; status?: string }): string {
  if (status === 'New')       return '← Back to Pending Referrals';
  if (status === 'Accepted')  return '← Back to Accepted Referrals';
  if (status === 'Declined')  return '← Back to Declined Referrals';
  if (status === 'Scheduled') return '← Back to Scheduled Referrals';
  if (status === 'Completed') return '← Back to Completed Referrals';
  if (status === 'Cancelled') return '← Back to Cancelled Referrals';
  if (from === 'dashboard')   return '← Back to Dashboard';
  return '← Back to Referrals';
}
