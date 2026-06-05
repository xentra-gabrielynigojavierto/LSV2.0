import { ProductRole } from '@/types';
import type { PlatformSession } from '@/types';

/**
 * LSCC-01-002-02: Provider access-readiness check (client-side / server-component).
 *
 * Centralised, deterministic readiness evaluation based on the session product roles.
 * Uses the canonical CareConnect receiver-ready access bundle:
 *   - CareConnectReceiver product role
 *   - implies: ReferralReadAddressed + ReferralAccept capabilities
 *
 * Rules:
 *   - read-only; no side effects
 *   - does NOT create users, assign roles, or provision anything
 *   - same inputs → same result (deterministic)
 */

export interface ProviderAccessReadiness {
  /** True when the session holds the full receiver-ready access bundle. */
  isProvisioned:     boolean;
  /** True when the session includes the CareConnectReceiver product role. */
  hasReceiverRole:   boolean;
  /** True when receiver can read addressed referrals (implied by receiver role). */
  hasReferralAccess: boolean;
  /** Machine-readable reason when isProvisioned is false. */
  reason?:           string;
}

/**
 * Evaluates whether the authenticated session is provider-ready for CareConnect
 * referral access (receiver path).
 *
 * A session is considered provider-ready ONLY if it includes the
 * CareConnectReceiver product role.  Admins (isTenantAdmin / isPlatformAdmin)
 * bypass capability checks at the API layer but this utility is focused on
 * the provider receiver flow — callers should handle admin bypass separately
 * if needed.
 */
export function checkCareConnectReceiverAccess(
  session: PlatformSession,
): ProviderAccessReadiness {
  const hasReceiverRole = session.productRoles.includes(ProductRole.CareConnectReceiver);

  if (!hasReceiverRole) {
    return {
      isProvisioned:     false,
      hasReceiverRole:   false,
      hasReferralAccess: false,
      reason:            'missing-receiver-role',
    };
  }

  return {
    isProvisioned:     true,
    hasReceiverRole:   true,
    hasReferralAccess: true,
  };
}
