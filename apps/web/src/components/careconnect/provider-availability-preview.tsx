'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { careConnectApi } from '@/lib/careconnect-api';
import type { AvailabilitySlot } from '@/types/careconnect';

interface ProviderAvailabilityPreviewProps {
  providerId:   string;
  providerName: string;
}

function isoDate(d: Date): string {
  return d.toLocaleDateString('en-CA');
}

function formatSlot(slot: AvailabilitySlot): string {
  const start = new Date(slot.startUtc);
  return start.toLocaleString('en-US', {
    weekday: 'short',
    month:   'short',
    day:     'numeric',
    hour:    'numeric',
    minute:  '2-digit',
    hour12:  true,
  });
}

/**
 * Shows the next 5 available slots for a provider.
 * Linked to the full availability page.
 * Non-critical: errors and empty states show a quiet fallback.
 */
export function ProviderAvailabilityPreview({ providerId, providerName }: ProviderAvailabilityPreviewProps) {
  const [slots,   setSlots]   = useState<AvailabilitySlot[] | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const today = new Date();
    const to    = new Date();
    to.setDate(today.getDate() + 13);

    careConnectApi.providers
      .getAvailability(providerId, { from: isoDate(today), to: isoDate(to) })
      .then(({ data }) => {
        const available = data.slots.filter((s: { isAvailable: boolean }) => s.isAvailable).slice(0, 5);
        setSlots(available);
      })
      .catch(() => setSlots([]))
      .finally(() => setLoading(false));
  }, [providerId]);

  return (
    <div className="bg-white border border-gray-200 rounded-lg">
      <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100">
        <h3 className="text-sm font-semibold text-gray-900">Upcoming Availability</h3>
        <Link
          href={`/careconnect/providers/${providerId}/availability`}
          className="text-xs text-primary font-medium hover:underline"
        >
          View all →
        </Link>
      </div>

      <div className="px-5 py-4">
        {loading && (
          <div className="space-y-2 animate-pulse">
            {[1, 2, 3].map(i => (
              <div key={i} className="h-7 bg-gray-100 rounded" />
            ))}
          </div>
        )}

        {!loading && slots !== null && slots.length === 0 && (
          <p className="text-sm text-gray-400">
            No availability in the next 2 weeks.{' '}
            <Link
              href={`/careconnect/providers/${providerId}/availability`}
              className="text-primary hover:underline"
            >
              Check further dates
            </Link>
          </p>
        )}

        {!loading && slots && slots.length > 0 && (
          <ul className="space-y-1.5">
            {slots.map(slot => (
              <li key={slot.id} className="flex items-center justify-between gap-3">
                <span className="text-sm text-gray-700">{formatSlot(slot)}</span>
                <div className="flex items-center gap-2 shrink-0">
                  {slot.serviceType && (
                    <span className="text-xs text-gray-400">{slot.serviceType}</span>
                  )}
                  <span className="text-xs text-gray-300">{slot.durationMinutes} min</span>
                </div>
              </li>
            ))}
          </ul>
        )}

        {!loading && slots && slots.length > 0 && (
          <div className="mt-4 pt-3 border-t border-gray-100">
            <Link
              href={`/careconnect/providers/${providerId}/availability`}
              className="text-sm text-primary font-medium hover:underline"
            >
              Book an appointment →
            </Link>
          </div>
        )}
      </div>
    </div>
  );
}
