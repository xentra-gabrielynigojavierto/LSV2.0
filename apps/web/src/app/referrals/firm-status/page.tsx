import { redirect } from 'next/navigation';
import { FirmStatusClient } from './firm-status-client';

export const dynamic = 'force-dynamic';

const GATEWAY_URL          = process.env.GATEWAY_URL          ?? 'http://127.0.0.1:5010';
const PROVISIONING_TOKEN   = process.env.IdentityService__ProvisioningToken ?? '';

interface Props {
  searchParams: Promise<{ token?: string }>;
}

/**
 * Law firm referral status page.
 * Reachable only via a secure HMAC-signed token from the referral confirmation email.
 * Shows referral status progress, provider info, messaging, and a CTA to upgrade to
 * the full portal for managing all referrals in one place.
 *
 * CC-PORTAL-CHECK: if referrerEmail already has an active portal account the upgrade
 * panel is replaced by a simple login prompt.
 */
export default async function FirmStatusPage({ searchParams }: Props) {
  const sp    = await searchParams;
  const token = sp.token?.trim();

  if (!token) {
    redirect('/referrals/accept/invalid?reason=missing-token');
  }

  let threadData = null;

  try {
    const resp = await fetch(
      `${GATEWAY_URL}/careconnect/api/public/referrals/thread?token=${encodeURIComponent(token)}`,
      { cache: 'no-store' },
    );

    if (resp.ok) {
      threadData = await resp.json();
    } else if (resp.status === 404) {
      redirect('/referrals/accept/invalid?reason=expired-or-invalid');
    }
  } catch {
    threadData = null;
  }

  if (!threadData) {
    redirect('/referrals/accept/invalid?reason=expired-or-invalid');
  }

  // CC-PORTAL-CHECK: check if referrer email already has an active portal account.
  // Failure → safe default (false) so the upgrade CTA is shown instead.
  let hasPortalAccess = false;
  const referrerEmail = threadData.referrerEmail as string | null;
  if (referrerEmail) {
    try {
      const checkResp = await fetch(
        `${GATEWAY_URL}/identity/api/internal/users/portal-access?email=${encodeURIComponent(referrerEmail)}`,
        {
          cache:   'no-store',
          headers: { 'X-Provisioning-Token': PROVISIONING_TOKEN },
        },
      );
      if (checkResp.ok) {
        const checkData = await checkResp.json() as { hasPortalAccess?: boolean };
        hasPortalAccess = checkData.hasPortalAccess === true;
      }
    } catch {
      // non-fatal — keep false
    }
  }

  return <FirmStatusClient token={token} data={threadData} hasPortalAccess={hasPortalAccess} />;
}
