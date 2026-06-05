'use client';

import { useCallback, useEffect, useState, useTransition } from 'react';
import { useRouter } from 'next/navigation';
import { BASE_PATH } from '@/lib/app-config';
import type {
  OutboxStatus,
  OutboxEntrySummary,
} from '@/lib/system-health-audit-outbox';

interface AuditOutboxBannerProps {
  status:  OutboxStatus;
  entries: OutboxEntrySummary[];
}

// Auto-refresh while the banner is visible. The banner is only rendered
// when there are queued entries, so this poll loop stops naturally as
// soon as the queue drains.
const REFRESH_INTERVAL_MS = 15_000;

function fmtTime(iso: string | null | undefined): string {
  if (!iso) return '';
  try {
    return new Date(iso).toLocaleString();
  } catch {
    return iso;
  }
}

export function AuditOutboxBanner({
  status:  initialStatus,
  entries: initialEntries,
}: AuditOutboxBannerProps) {
  const router = useRouter();
  const [status,  setStatus]  = useState<OutboxStatus>(initialStatus);
  const [entries, setEntries] = useState<OutboxEntrySummary[]>(initialEntries);
  const [error,   setError]   = useState<string | null>(null);
  const [busy,    setBusy]    = useState<'idle' | 'retry' | string>('idle');
  const [showDetails, setShowDetails] = useState(false);
  const [, startTransition]   = useTransition();

  const refresh = useCallback(async (): Promise<void> => {
    try {
      const res = await fetch(`${BASE_PATH}/api/monitoring/audit-outbox`, {
        method: 'GET',
        cache:  'no-store',
      });
      if (!res.ok) return;
      const body = await res.json() as {
        status:  OutboxStatus;
        entries: OutboxEntrySummary[];
      };
      setStatus(body.status);
      setEntries(body.entries);
    } catch {
      // transient network error — next poll will retry
    }
  }, []);

  useEffect(() => {
    if (status.pending === 0) return;
    const t = setInterval(() => { void refresh(); }, REFRESH_INTERVAL_MS);
    return () => clearInterval(t);
  }, [status.pending, refresh]);

  async function handleRetry() {
    setError(null);
    setBusy('retry');
    try {
      const res = await fetch(`${BASE_PATH}/api/monitoring/audit-outbox`, {
        method:  'POST',
        headers: { 'Content-Type': 'application/json' },
        body:    JSON.stringify({ action: 'retry' }),
      });
      if (!res.ok) {
        const body = await safeJson(res);
        throw new Error(body?.error ?? `Retry failed (${res.status})`);
      }
      const body = await res.json() as {
        result:  { delivered: number; failed: number };
        status:  OutboxStatus;
        entries: OutboxEntrySummary[];
      };
      setStatus(body.status);
      setEntries(body.entries);
      // Refresh the page in the background so the "Recent Changes" panel
      // picks up any newly-delivered events without forcing a manual reload.
      startTransition(() => router.refresh());
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Retry failed');
    } finally {
      setBusy('idle');
    }
  }

  async function handleDiscard(entry: OutboxEntrySummary) {
    const summary = entry.description ?? `${entry.eventType} / ${entry.action}`;
    if (!confirm(
      `Discard this queued audit event?\n\n${summary}\n\n` +
      `It will be removed from the retry queue and will NOT appear in the ` +
      `central Audit Logs. The discard itself will be recorded as an audit event.`,
    )) return;

    setError(null);
    setBusy(entry.id);
    try {
      const res = await fetch(
        `${BASE_PATH}/api/monitoring/audit-outbox/${encodeURIComponent(entry.id)}`,
        { method: 'DELETE' },
      );
      if (!res.ok) {
        const body = await safeJson(res);
        throw new Error(body?.error ?? `Discard failed (${res.status})`);
      }
      const body = await res.json() as {
        status:  OutboxStatus;
        entries: OutboxEntrySummary[];
      };
      setStatus(body.status);
      setEntries(body.entries);
      startTransition(() => router.refresh());
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Discard failed');
    } finally {
      setBusy('idle');
    }
  }

  if (status.pending === 0 && status.persistentFailures === 0) return null;

  const hasPersistent = status.persistentFailures > 0;
  const stillRetrying = status.pending - status.persistentFailures;
  const tone = hasPersistent
    ? 'bg-red-50 border-red-200 text-red-900'
    : 'bg-amber-50 border-amber-200 text-amber-900';
  const headline = hasPersistent
    ? 'Some monitoring-config audit events could not be delivered to the central Audit Logs'
    : 'Monitoring-config audit events are queued for delivery to the central Audit Logs';

  return (
    <div className={`border rounded-md px-4 py-3 text-sm ${tone}`} role="alert">
      <p className="font-semibold">{headline}</p>
      <p className="mt-1 text-xs">
        {hasPersistent ? (
          <>
            {status.persistentFailures} event{status.persistentFailures === 1 ? '' : 's'} have
            exhausted automatic retries
            {stillRetrying > 0 && (
              <> and {stillRetrying} more {stillRetrying === 1 ? 'is' : 'are'} still being retried</>
            )}
            . The local audit copy below is intact, but the central Audit Logs page may be
            missing these entries until the audit service recovers and an operator triggers
            redelivery.
          </>
        ) : (
          <>
            {status.pending} event{status.pending === 1 ? '' : 's'} pending. Retries continue
            automatically; entries will appear in the central Audit Logs once the audit
            service is reachable, with their original timestamps preserved.
          </>
        )}
      </p>
      {status.oldestEnqueuedAt && (
        <p className="mt-1 text-xs opacity-75">
          Oldest queued event: <span className="font-mono">{fmtTime(status.oldestEnqueuedAt)}</span>
        </p>
      )}
      {status.lastError && (
        <p className="mt-1 text-xs opacity-75">
          Last error: <span className="font-mono">{status.lastError}</span>
        </p>
      )}

      <div className="mt-3 flex flex-wrap items-center gap-2">
        <button
          type="button"
          onClick={handleRetry}
          disabled={busy !== 'idle'}
          className={
            'text-xs px-3 py-1.5 rounded font-medium border ' +
            (hasPersistent
              ? 'bg-red-600 text-white border-red-600 hover:bg-red-700'
              : 'bg-amber-600 text-white border-amber-600 hover:bg-amber-700') +
            ' disabled:opacity-50'
          }
        >
          {busy === 'retry' ? 'Retrying…' : 'Retry now'}
        </button>
        {entries.length > 0 && (
          <button
            type="button"
            onClick={() => setShowDetails(s => !s)}
            disabled={busy !== 'idle'}
            className="text-xs px-3 py-1.5 rounded font-medium border border-current bg-transparent hover:bg-black/5 disabled:opacity-50"
          >
            {showDetails ? 'Hide queued entries' : `Show queued entries (${entries.length})`}
          </button>
        )}
      </div>

      {error && (
        <p className="mt-2 text-xs font-mono break-all" role="status">
          {error}
        </p>
      )}

      {showDetails && entries.length > 0 && (
        <ul className="mt-3 space-y-2">
          {entries.map(entry => (
            <li
              key={entry.id}
              className="bg-white/60 border border-current/20 rounded px-3 py-2 text-xs flex flex-wrap items-start gap-3"
            >
              <div className="flex-1 min-w-[220px]">
                <div className="font-mono">
                  {entry.eventType} <span className="opacity-60">/</span> {entry.action}
                  {entry.entityId && (
                    <span className="opacity-60"> · {entry.entityId}</span>
                  )}
                </div>
                <div className="opacity-75 mt-0.5">
                  Queued {fmtTime(entry.enqueuedAt)} · {entry.attempts} attempt{entry.attempts === 1 ? '' : 's'}
                  {entry.persistentFailure && (
                    <span className="ml-1 inline-block px-1.5 py-0.5 rounded bg-red-200 text-red-900 font-semibold">
                      max retries reached
                    </span>
                  )}
                </div>
                {entry.lastError && (
                  <div className="opacity-75 font-mono break-all mt-0.5">
                    Last error: {entry.lastError}
                  </div>
                )}
              </div>
              <button
                type="button"
                onClick={() => handleDiscard(entry)}
                disabled={busy !== 'idle'}
                className="text-xs px-2.5 py-1 rounded border border-red-400 text-red-700 font-medium hover:bg-red-50 disabled:opacity-50"
              >
                {busy === entry.id ? 'Discarding…' : 'Discard'}
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

async function safeJson(res: Response): Promise<{ error?: string } | null> {
  try { return await res.json() as { error?: string }; } catch { return null; }
}
