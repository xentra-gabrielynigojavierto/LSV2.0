'use client';

import { useEffect, useState } from 'react';
import { careConnectApi } from '@/lib/careconnect-api';
import type { ReferralHistoryItem } from '@/types/careconnect';

interface ReferralTimelineProps {
  referralId: string;
}

const STATUS_DOT: Record<string, string> = {
  New:       'bg-gray-400',
  Received:  'bg-blue-300',
  Contacted: 'bg-yellow-400',
  Accepted:  'bg-green-400',
  Declined:  'bg-red-400',
  Scheduled: 'bg-blue-500',
  Completed: 'bg-green-600',
  Cancelled: 'bg-gray-500',
};

function formatDateTime(iso: string): string {
  return new Date(iso).toLocaleString('en-US', {
    month:   'short',
    day:     'numeric',
    year:    'numeric',
    hour:    'numeric',
    minute:  '2-digit',
    hour12:  true,
  });
}

export function ReferralTimeline({ referralId }: ReferralTimelineProps) {
  const [history, setHistory] = useState<ReferralHistoryItem[] | null>(null);
  const [error,   setError]   = useState(false);

  useEffect(() => {
    careConnectApi.referrals.getHistory(referralId)
      .then(({ data }) => setHistory(data))
      .catch(() => setError(true));
  }, [referralId]);

  if (error) {
    return <p className="text-xs text-red-400">Could not load activity history.</p>;
  }

  if (history === null) {
    return (
      <div className="space-y-3 animate-pulse">
        {[1, 2].map(i => (
          <div key={i} className="flex gap-3">
            <div className="w-3 h-3 rounded-full bg-gray-100 mt-1 shrink-0" />
            <div className="flex-1 space-y-1.5">
              <div className="h-3.5 w-32 bg-gray-100 rounded" />
              <div className="h-3 w-48 bg-gray-100 rounded" />
            </div>
          </div>
        ))}
      </div>
    );
  }

  if (history.length === 0) {
    return <p className="text-sm text-gray-400">No activity yet.</p>;
  }

  const sorted = [...history].sort(
    (a, b) => new Date(b.changedAtUtc).getTime() - new Date(a.changedAtUtc).getTime(),
  );

  return (
    <ol className="relative border-l border-gray-200 space-y-4 ml-2">
      {sorted.map((item, idx) => (
        <li key={idx} className="pl-5 relative">
          <span
            className={`absolute -left-1.5 top-1.5 w-3 h-3 rounded-full border-2 border-white ${
              STATUS_DOT[item.newStatus] ?? 'bg-gray-400'
            }`}
          />
          <div>
            <p className="text-sm font-medium text-gray-900">
              {item.oldStatus
                ? <><span className="text-gray-400 font-normal">{item.oldStatus}</span> → {item.newStatus}</>
                : item.newStatus
              }
            </p>
            <p className="text-xs text-gray-400 mt-0.5">
              {formatDateTime(item.changedAtUtc)}
            </p>
            {item.notes && (
              <p className="text-xs text-gray-500 mt-1 whitespace-pre-wrap">{item.notes}</p>
            )}
          </div>
        </li>
      ))}
    </ol>
  );
}
