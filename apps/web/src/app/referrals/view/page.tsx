import { redirect } from 'next/navigation';

export const dynamic = 'force-dynamic';


/**
 * LSCC-005 / LSCC-01-002-01: Public referral view router.
 *
 * This page is intentionally outside (platform) — no auth middleware,
 * no session required. It decodes the secure view token and routes the
 * provider into the authenticated referral flow.
 *
 * LSCC-01-002-01: Both "pending" and "active" providers are now routed
 * to login with a safe returnTo so acceptance always happens from the
 * authenticated referral detail page. Direct token-only acceptance
 * is no longer supported.
 *
 *   "pending" provider (OrganizationId = null)
 *     → /login?returnTo=/careconnect/referrals/{referralId}&reason=referral-view
 *
 *   "active" tenant provider (OrganizationId != null)
 *     → /login?returnTo=/careconnect/referrals/{referralId}&reason=referral-view
 *
 *   "invalid" / "notfound" token
 *     → /referrals/accept/invalid  (error page)
 */

const GATEWAY_URL = process.env.GATEWAY_URL ?? 'http://127.0.0.1:5010';

interface Props {
  searchParams: Promise<{ token?: string }>;
}

export default async function ReferralViewPage({ searchParams }: Props) {
  const sp = await searchParams;
  const token = sp.token?.trim();

  if (!token) {
    redirect('/referrals/accept/invalid?reason=missing-token');
  }

  let routeType = 'invalid';
  let referralId: string | null = null;

  try {
    const resp = await fetch(
      `${GATEWAY_URL}/careconnect/api/referrals/resolve-view-token?token=${encodeURIComponent(token)}`,
      { cache: 'no-store' },
    );

    if (resp.ok) {
      const data = await resp.json();
      routeType  = data.routeType  ?? 'invalid';
      referralId = data.referralId ?? null;
    }
  } catch {
    routeType = 'invalid';
  }

  // LSCC-01-002-01 / CC2-INT-B05:
  // Both pending and active providers go to login; returnTo lands them in the
  // Common Portal (provider section) rather than the Tenant Portal.
  if ((routeType === 'pending' || routeType === 'active') && referralId) {
    const returnTo = encodeURIComponent(`/provider/referrals/${referralId}`);
    redirect(`/login?returnTo=${returnTo}&reason=referral-view`);
  }

  redirect('/referrals/accept/invalid?reason=expired-or-invalid');
}
