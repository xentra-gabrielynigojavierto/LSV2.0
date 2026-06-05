/**
 * CareConnect analytics metric computation.
 *
 * All functions are pure (no side effects, no I/O).
 * Denominator-zero handling: safeRate returns 0, never NaN.
 */

// ── Types ─────────────────────────────────────────────────────────────────────

export interface ReferralFunnelMetrics {
  total:     number;
  accepted:  number;
  declined:  number;
  scheduled: number;
  completed: number;
  /** Accepted / Total (spec definition) */
  acceptanceRate: number;
  /** Scheduled / Accepted */
  schedulingRate: number;
  /** Completed / Scheduled */
  completionRate: number;
}

export interface AppointmentMetrics {
  total:     number;
  completed: number;
  cancelled: number;
  noShow:    number;
  /** Completed / Total */
  completionRate: number;
  /** NoShow / Total */
  noShowRate: number;
}

export interface ProviderPerformanceRow {
  providerId:            string;
  providerName:          string;
  referralsReceived:     number;
  /** Count of referrals currently in Accepted, Scheduled, or Completed status. */
  everAccepted:          number;
  /**
   * everAccepted / referralsReceived.
   * NOTE: uses "ever accepted" (Accepted | Scheduled | Completed) rather than
   * the strict funnel definition so the rate remains meaningful when referrals
   * advance past Accepted status.
   */
  acceptanceRate:        number;
  appointmentsCompleted: number;
}

// ── Core helpers ──────────────────────────────────────────────────────────────

/**
 * Safe division — returns 0 (not NaN) when denominator is zero.
 * Result is a proportion in [0, 1].
 */
export function safeRate(numerator: number, denominator: number): number {
  if (denominator === 0) return 0;
  return numerator / denominator;
}

/** Format a [0, 1] proportion as "42%" string. */
export function formatRate(proportion: number): string {
  return `${Math.round(proportion * 100)}%`;
}

// ── Referral funnel ───────────────────────────────────────────────────────────

/**
 * Compute referral funnel metrics from pre-fetched counts.
 *
 * Rates follow spec definitions exactly:
 *   Acceptance Rate = accepted / total
 *   Scheduling Rate = scheduled / accepted
 *   Completion Rate = completed / scheduled
 *
 * Limitation: counts reflect CURRENT status. Referrals that advanced beyond
 * "Accepted" to "Scheduled" are counted under Scheduled, not Accepted. This
 * means Acceptance Rate can undercount if the funnel is flowing well.
 * See LSCC-004 report for details.
 */
export function computeReferralFunnel(
  total:     number,
  accepted:  number,
  declined:  number,
  scheduled: number,
  completed: number,
): ReferralFunnelMetrics {
  return {
    total,
    accepted,
    declined,
    scheduled,
    completed,
    acceptanceRate: safeRate(accepted, total),
    schedulingRate: safeRate(scheduled, accepted),
    completionRate: safeRate(completed, scheduled),
  };
}

// ── Appointment metrics ───────────────────────────────────────────────────────

export function computeAppointmentMetrics(
  total:     number,
  completed: number,
  cancelled: number,
  noShow:    number,
): AppointmentMetrics {
  return {
    total,
    completed,
    cancelled,
    noShow,
    completionRate: safeRate(completed, total),
    noShowRate:     safeRate(noShow, total),
  };
}

// ── Provider performance ──────────────────────────────────────────────────────

/**
 * Aggregate provider performance from referral and appointment item arrays.
 *
 * Provider acceptance rate uses "ever accepted" definition:
 *   - Accepted | Scheduled | Completed = referral was accepted by the provider
 *
 * Sorted by referralsReceived DESC, capped at top 10 providers.
 */
export function computeProviderPerformance(
  referralItems:    Array<{ providerId: string; providerName: string; status: string }>,
  appointmentItems: Array<{ providerId: string; status: string }>,
): ProviderPerformanceRow[] {
  type Entry = { name: string; total: number; everAccepted: number };
  const providerMap = new Map<string, Entry>();

  for (const r of referralItems) {
    const entry = providerMap.get(r.providerId) ?? { name: r.providerName, total: 0, everAccepted: 0 };
    entry.total++;
    if (r.status === 'Accepted' || r.status === 'Scheduled' || r.status === 'Completed') {
      entry.everAccepted++;
    }
    providerMap.set(r.providerId, entry);
  }

  const apptCompleted = new Map<string, number>();
  for (const a of appointmentItems) {
    if (a.status === 'Completed') {
      apptCompleted.set(a.providerId, (apptCompleted.get(a.providerId) ?? 0) + 1);
    }
  }

  return [...providerMap.entries()]
    .map(([providerId, { name, total, everAccepted }]) => ({
      providerId,
      providerName:          name,
      referralsReceived:     total,
      everAccepted,
      acceptanceRate:        safeRate(everAccepted, total),
      appointmentsCompleted: apptCompleted.get(providerId) ?? 0,
    }))
    .sort((a, b) => b.referralsReceived - a.referralsReceived)
    .slice(0, 10);
}
