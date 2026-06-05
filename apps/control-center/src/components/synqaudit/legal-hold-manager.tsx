'use client';

import { useState, useTransition } from 'react';
import { useRouter } from 'next/navigation';
import type { LegalHold } from '@/types/control-center';

interface Props {
  holds:   LegalHold[];
  auditId: string;
}

/**
 * LegalHoldManager — displays and manages legal holds for a single audit record.
 */
export function LegalHoldManager({ holds: initialHolds, auditId }: Props) {
  const router = useRouter();
  const [, startTransition] = useTransition();

  const [holds,          setHolds]        = useState<LegalHold[]>(initialHolds);
  const [authority,      setAuthority]    = useState('');
  const [notes,          setNotes]        = useState('');
  const [creating,       setCreating]     = useState(false);
  const [createError,    setCreateError]  = useState<string | null>(null);
  const [releasing,      setReleasing]    = useState<string | null>(null);
  const [releaseError,   setReleaseError] = useState<string | null>(null);

  async function handleCreate(e: React.FormEvent) {
    e.preventDefault();
    if (!authority.trim()) return;
    setCreating(true);
    setCreateError(null);

    startTransition(() => {});

    try {
      const res = await fetch(`/api/synqaudit/legal-holds/${encodeURIComponent(auditId)}`, {
        method:  'POST',
        headers: { 'Content-Type': 'application/json' },
        body:    JSON.stringify({ legalAuthority: authority, notes: notes || undefined }),
      });

      if (!res.ok) {
        const err = await res.json().catch(() => ({ message: `HTTP ${res.status}` })) as { message?: string };
        setCreateError(err.message ?? `HTTP ${res.status}`);
      } else {
        const hold = await res.json() as LegalHold;
        setHolds(prev => [hold, ...prev]);
        setAuthority('');
        setNotes('');
      }
    } catch (err) {
      setCreateError(err instanceof Error ? err.message : 'Request failed');
    } finally {
      setCreating(false);
    }
  }

  async function handleRelease(holdId: string) {
    setReleasing(holdId);
    setReleaseError(null);

    startTransition(() => {});

    try {
      const res = await fetch(`/api/synqaudit/legal-holds/${encodeURIComponent(holdId)}/release`, {
        method: 'POST',
      });

      if (!res.ok) {
        const err = await res.json().catch(() => ({ message: `HTTP ${res.status}` })) as { message?: string };
        setReleaseError(err.message ?? `HTTP ${res.status}`);
      } else {
        const updated = await res.json() as LegalHold;
        setHolds(prev => prev.map(h => h.holdId === holdId ? updated : h));
      }
    } catch (err) {
      setReleaseError(err instanceof Error ? err.message : 'Request failed');
    } finally {
      setReleasing(null);
    }
  }

  const inputCls = 'w-full h-9 rounded-md border border-gray-300 px-3 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 bg-white';
  const labelCls = 'block text-xs font-medium text-gray-600 mb-1';
  const activeHolds = holds.filter(h => h.isActive);
  const releasedHolds = holds.filter(h => !h.isActive);

  return (
    <div className="space-y-5">

      {/* Active holds */}
      <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
        <div className="px-4 py-3 border-b border-gray-100 bg-gray-50 flex items-center justify-between">
          <h3 className="text-sm font-semibold text-gray-700">
            Active Holds
            {activeHolds.length > 0 && (
              <span className="ml-2 inline-flex items-center px-1.5 py-0.5 rounded-full text-[10px] font-semibold bg-red-50 text-red-700 border border-red-200">
                {activeHolds.length}
              </span>
            )}
          </h3>
        </div>

        {activeHolds.length === 0 ? (
          <div className="px-5 py-6 text-sm text-gray-400 text-center">No active legal holds.</div>
        ) : (
          <div className="divide-y divide-gray-100">
            {activeHolds.map(hold => (
              <div key={hold.holdId} className="px-4 py-3 flex items-start justify-between gap-4">
                <div className="min-w-0 space-y-0.5">
                  <p className="text-xs font-semibold text-gray-700">{hold.legalAuthority}</p>
                  {hold.notes && <p className="text-[11px] text-gray-500">{hold.notes}</p>}
                  <div className="flex items-center gap-3 text-[10px] text-gray-400 flex-wrap">
                    <span className="font-mono">{hold.holdId}</span>
                    <span>Placed {hold.heldAtUtc}</span>
                    {hold.heldByUserId && <span>by {hold.heldByUserId}</span>}
                  </div>
                </div>
                <button
                  onClick={() => handleRelease(hold.holdId)}
                  disabled={releasing === hold.holdId}
                  className="shrink-0 h-7 px-3 text-xs font-medium text-red-700 bg-red-50 border border-red-200 hover:bg-red-100 disabled:opacity-50 rounded-md transition-colors"
                >
                  {releasing === hold.holdId ? 'Releasing…' : 'Release'}
                </button>
              </div>
            ))}
          </div>
        )}
      </div>

      {releaseError && (
        <div className="rounded-md border border-red-200 bg-red-50 px-4 py-2 text-sm text-red-700">{releaseError}</div>
      )}

      {/* Place new hold */}
      <form onSubmit={handleCreate} className="rounded-lg border border-gray-200 bg-white p-5 space-y-4">
        <h3 className="text-sm font-semibold text-gray-700">Place New Hold</h3>

        <div className="space-y-3">
          <div>
            <label className={labelCls}>Legal Authority <span className="text-red-500">*</span></label>
            <input
              type="text"
              required
              placeholder="Case #123, Litigation hold for…"
              value={authority}
              onChange={e => setAuthority(e.target.value)}
              className={inputCls}
            />
          </div>
          <div>
            <label className={labelCls}>Notes (optional)</label>
            <textarea
              placeholder="Additional context…"
              value={notes}
              onChange={e => setNotes(e.target.value)}
              rows={2}
              className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 bg-white resize-none"
            />
          </div>
        </div>

        {createError && (
          <p className="text-sm text-red-600">{createError}</p>
        )}

        <button
          type="submit"
          disabled={creating || !authority.trim()}
          className="h-9 px-4 text-sm font-semibold text-white bg-indigo-600 hover:bg-indigo-700 disabled:opacity-50 disabled:cursor-not-allowed rounded-md transition-colors"
        >
          {creating ? 'Placing hold…' : 'Place Legal Hold'}
        </button>
      </form>

      {/* Released holds */}
      {releasedHolds.length > 0 && (
        <div className="rounded-lg border border-gray-100 bg-gray-50 overflow-hidden">
          <div className="px-4 py-2.5 border-b border-gray-100">
            <h4 className="text-xs font-semibold text-gray-500 uppercase tracking-wide">Released Holds</h4>
          </div>
          <div className="divide-y divide-gray-100">
            {releasedHolds.map(hold => (
              <div key={hold.holdId} className="px-4 py-2.5 space-y-0.5 opacity-70">
                <p className="text-xs text-gray-600">{hold.legalAuthority}</p>
                <div className="flex items-center gap-3 text-[10px] text-gray-400">
                  <span className="font-mono">{hold.holdId}</span>
                  <span>Released {hold.releasedAtUtc ?? '—'}</span>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
