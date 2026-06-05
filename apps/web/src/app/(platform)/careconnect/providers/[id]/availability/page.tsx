'use client';

import { useEffect, useState, useCallback } from 'react';
import { useParams, useRouter, useSearchParams } from 'next/navigation';
import Link from 'next/link';
import { useSession } from '@/hooks/use-session';
import { ProductRole } from '@/types';
import { careConnectApi } from '@/lib/careconnect-api';
import { ApiError } from '@/lib/api-client';
import { AvailabilityList } from '@/components/careconnect/availability-list';
import { BookingPanel } from '@/components/careconnect/booking-panel';
import type { AvailabilitySlot, ProviderAvailabilityResponse, ReferralDetail } from '@/types/careconnect';

/**
 * /careconnect/providers/[id]/availability
 *
 * Client Component — needs:
 *   1. Slot selection state (highlights chosen slot)
 *   2. Date range picker that re-fetches on change
 *   3. BookingPanel modal triggered by slot selection
 *
 * Query string:
 *   - referralId  — (optional) pre-populates client fields in BookingPanel
 *                   and shows a context badge linking back to the referral.
 *   - from / to   — ISO date range (defaults to today → today+6 days)
 *
 * Access: CARECONNECT_REFERRER only.
 */

function isoDate(d: Date): string {
  return d.toLocaleDateString('en-CA');   // yyyy-MM-dd
}

function addDays(d: Date, n: number): Date {
  const r = new Date(d);
  r.setDate(r.getDate() + n);
  return r;
}

export default function AvailabilityPage() {
  const params       = useParams<{ id: string }>();
  const router       = useRouter();
  const searchParams = useSearchParams();

  const { session, isLoading: sessionLoading } = useSession();
  const isReferrer = session?.productRoles.includes(ProductRole.CareConnectReferrer) ?? false;

  // Date range state (defaults: today → today+6 days)
  const today    = new Date();
  const [from,   setFrom] = useState(searchParams?.get('from') ?? isoDate(today));
  const [to,     setTo]   = useState(searchParams?.get('to')   ?? isoDate(addDays(today, 6)));

  // Optional referral context
  const referralId = searchParams?.get('referralId') ?? undefined;
  const [referral, setReferral] = useState<ReferralDetail | null>(null);

  // Availability data
  const [availability, setAvailability] = useState<ProviderAvailabilityResponse | null>(null);
  const [loading,      setLoading]      = useState(true);
  const [error,        setError]        = useState<string | null>(null);

  // Selected slot + booking modal
  const [selectedSlot, setSelectedSlot] = useState<AvailabilitySlot | null>(null);
  const [showBooking,  setShowBooking]  = useState(false);

  // Load availability
  const loadAvailability = useCallback(async () => {
    if (!session) return;
    setLoading(true);
    setError(null);
    try {
      const { data } = await careConnectApi.providers.getAvailability(params?.id ?? '', { from, to });
      setAvailability(data);
    } catch (err) {
      if (err instanceof ApiError) {
        if (err.isUnauthorized) { router.push('/login'); return; }
        if (err.isNotFound)     { setError('Provider not found.'); return; }
        if (err.isForbidden)    { setError('You do not have access to this provider\'s schedule.'); return; }
        setError(err.message);
      } else {
        setError('Failed to load availability.');
      }
    } finally {
      setLoading(false);
    }
  }, [params?.id ?? '', from, to, session, router]);

  // Load optional referral context
  useEffect(() => {
    if (!referralId || !session) return;
    careConnectApi.referrals.getById(referralId)
      .then(({ data }) => setReferral(data))
      .catch(() => {});   // non-critical; booking works without it
  }, [referralId, session]);

  // Re-fetch whenever dates change (after session is ready)
  useEffect(() => {
    if (sessionLoading) return;
    if (!session) { router.push('/login'); return; }
    if (!isReferrer) { router.push('/dashboard'); return; }
    loadAvailability();
  }, [sessionLoading, session, isReferrer, loadAvailability, router]);

  function handleSlotSelect(slot: AvailabilitySlot) {
    setSelectedSlot(slot);
    setShowBooking(true);
  }

  // ── Skeleton ───────────────────────────────────────────────────────────────
  if (sessionLoading || (loading && !availability)) {
    return (
      <div className="space-y-4 animate-pulse">
        <div className="h-6 w-48 bg-gray-100 rounded" />
        <div className="h-32 bg-gray-100 rounded" />
        <div className="grid grid-cols-3 gap-2">
          {Array.from({ length: 9 }).map((_, i) => (
            <div key={i} className="h-12 bg-gray-100 rounded" />
          ))}
        </div>
      </div>
    );
  }

  const providerName = availability?.providerName ?? 'Provider';

  return (
    <div className="space-y-4">
      {/* Back link */}
      <nav>
        <Link
          href={`/careconnect/providers/${params?.id ?? ''}`}
          className="text-sm text-gray-500 hover:text-gray-800 transition-colors"
        >
          ← Back to {providerName}
        </Link>
      </nav>

      {/* Header */}
      <div className="flex items-start justify-between gap-4 flex-wrap">
        <div>
          <h1 className="text-xl font-semibold text-gray-900">Availability</h1>
          <p className="text-sm text-gray-500 mt-0.5">{providerName}</p>
        </div>

        {/* Referral context badge */}
        {referral && (
          <div className="bg-blue-50 border border-blue-200 rounded-md px-3 py-2 text-xs text-blue-700">
            Booking for referral
            {referral.caseNumber ? ` #${referral.caseNumber}` : ''}
            {' · '}
            <Link
              href={`/careconnect/referrals/${referral.id}`}
              className="underline hover:text-blue-900"
            >
              Back to referral
            </Link>
          </div>
        )}
      </div>

      {/* Date range picker */}
      <div className="bg-white border border-gray-200 rounded-lg p-4">
        <div className="flex items-end gap-4 flex-wrap">
          <div>
            <label className="block text-xs font-medium text-gray-600 mb-1">From</label>
            <input
              type="date"
              value={from}
              min={isoDate(today)}
              onChange={e => setFrom(e.target.value)}
              className="border border-gray-300 rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-primary"
            />
          </div>
          <div>
            <label className="block text-xs font-medium text-gray-600 mb-1">To</label>
            <input
              type="date"
              value={to}
              min={from}
              onChange={e => setTo(e.target.value)}
              className="border border-gray-300 rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-primary"
            />
          </div>
          <button
            onClick={loadAvailability}
            disabled={loading}
            className="bg-primary text-white text-sm font-medium px-4 py-1.5 rounded-md hover:opacity-90 disabled:opacity-60 transition-opacity"
          >
            {loading ? 'Loading…' : 'Refresh'}
          </button>

          {/* Quick range shortcuts */}
          <div className="flex items-center gap-2 ml-auto">
            {[
              { label: 'This week', days: 6 },
              { label: 'Next 2 weeks', days: 13 },
              { label: 'Next month', days: 29 },
            ].map(({ label, days }) => (
              <button
                key={days}
                onClick={() => {
                  setFrom(isoDate(today));
                  setTo(isoDate(addDays(today, days)));
                }}
                className="text-xs text-primary hover:underline transition-colors"
              >
                {label}
              </button>
            ))}
          </div>
        </div>
      </div>

      {/* Error */}
      {error && (
        <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
          {error}
        </div>
      )}

      {/* Instruction */}
      {!selectedSlot && !error && availability && (
        <p className="text-xs text-gray-400">
          Select an available time slot to book an appointment.
        </p>
      )}

      {/* Slot list */}
      {availability && (
        <AvailabilityList
          slots={availability.slots}
          selectedSlotId={selectedSlot?.id ?? null}
          onSelectSlot={handleSlotSelect}
        />
      )}

      {/* Booking modal */}
      {showBooking && selectedSlot && (
        <BookingPanel
          providerId={params?.id ?? ''}
          providerName={providerName}
          slot={selectedSlot}
          referral={referral ?? undefined}
          onClose={() => {
            setShowBooking(false);
            setSelectedSlot(null);
          }}
        />
      )}
    </div>
  );
}
