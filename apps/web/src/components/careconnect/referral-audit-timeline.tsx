'use client';

import { useState, useCallback } from 'react';
import { careConnectApi } from '@/lib/careconnect-api';
import type { ReferralAuditEvent } from '@/types/careconnect';

interface ReferralAuditTimelineProps {
  referralId: string;
}

// ── Category colour map ───────────────────────────────────────────────────────

const DOT_CSS: Record<string, string> = {
  success:  'bg-green-500',
  error:    'bg-red-500',
  warning:  'bg-yellow-500',
  security: 'bg-purple-500',
  info:     'bg-blue-400',
};

const LABEL_CSS: Record<string, string> = {
  success:  'text-green-700',
  error:    'text-red-700',
  warning:  'text-yellow-700',
  security: 'text-purple-700',
  info:     'text-blue-700',
};

function dot(category: string) {
  return DOT_CSS[category] ?? DOT_CSS.info;
}

function labelCss(category: string) {
  return LABEL_CSS[category] ?? LABEL_CSS.info;
}

function formatTs(iso: string): string {
  const d = new Date(iso);
  return d.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' }) +
    ' ' +
    d.toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit', hour12: false }) +
    ' UTC';
}

// ── Component ─────────────────────────────────────────────────────────────────

export function ReferralAuditTimeline({ referralId }: ReferralAuditTimelineProps) {
  const [open,    setOpen]    = useState(false);
  const [events,  setEvents]  = useState<ReferralAuditEvent[] | null>(null);
  const [loading, setLoading] = useState(false);
  const [error,   setError]   = useState('');

  const load = useCallback(async () => {
    if (events !== null) return;
    setLoading(true);
    setError('');
    try {
      const response = await careConnectApi.referrals.getAuditTimeline(referralId);
      setEvents(response.data);
    } catch {
      setError('Failed to load audit timeline.');
      setEvents([]);
    } finally {
      setLoading(false);
    }
  }, [referralId, events]);

  async function toggle() {
    if (!open) await load();
    setOpen(v => !v);
  }

  return (
    <div className="bg-white border border-gray-200 rounded-lg px-5 py-4">
      {/* Header */}
      <div className="flex items-center justify-between">
        <h3 className="text-xs font-semibold text-gray-400 uppercase tracking-wider">
          Operational Audit
        </h3>
        <button
          onClick={toggle}
          className="text-xs text-primary hover:underline"
        >
          {open ? 'Collapse' : 'View timeline'}
        </button>
      </div>

      {open && (
        <div className="mt-4">
          {loading && (
            <p className="text-xs text-gray-400">Loading…</p>
          )}

          {error && (
            <p className="text-xs text-red-500">{error}</p>
          )}

          {!loading && !error && events?.length === 0 && (
            <p className="text-xs text-gray-400">No events recorded yet.</p>
          )}

          {!loading && !error && events && events.length > 0 && (
            <ol className="relative border-l border-gray-100 ml-1.5 space-y-4">
              {events.map((ev, i) => (
                <li key={i} className="ml-5">
                  {/* Timeline dot */}
                  <span
                    className={`absolute -left-[5px] mt-1 w-2.5 h-2.5 rounded-full border-2 border-white ${dot(ev.category)}`}
                  />

                  {/* Event label */}
                  <p className={`text-xs font-semibold leading-tight ${labelCss(ev.category)}`}>
                    {ev.label}
                  </p>

                  {/* Timestamp */}
                  <p className="text-[11px] text-gray-400 mt-0.5">
                    {formatTs(ev.occurredAt)}
                  </p>

                  {/* Optional detail */}
                  {ev.detail && (
                    <p className="text-[11px] text-gray-500 mt-0.5 leading-snug">
                      {ev.detail}
                    </p>
                  )}
                </li>
              ))}
            </ol>
          )}
        </div>
      )}
    </div>
  );
}
