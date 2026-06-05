'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import type { CanonicalAuditEvent } from '@/types/control-center';
import { SeverityBadge, CategoryBadge, OutcomeBadge, formatUtcFull } from './synqaudit-badges';

interface Props {
  events:        CanonicalAuditEvent[];
  correlationId: string;
}

/**
 * TraceTimeline — renders a chronological timeline of all audit events sharing
 * a correlationId, useful for reconstructing request flows across services.
 */
export function TraceTimeline({ events, correlationId }: Props) {
  const [selected, setSelected] = useState<CanonicalAuditEvent | null>(null);
  const router = useRouter();

  function handleSearch(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const data = new FormData(e.currentTarget);
    const id   = (data.get('correlationId') as string ?? '').trim();
    if (id) router.push(`/synqaudit/trace?correlationId=${encodeURIComponent(id)}`);
  }

  return (
    <div className="space-y-5">

      {/* Search form */}
      <form onSubmit={handleSearch} className="flex items-end gap-3">
        <div className="flex-1 max-w-md">
          <label htmlFor="correlationId" className="block text-xs font-medium text-gray-600 mb-1">
            Correlation ID
          </label>
          <input
            id="correlationId"
            name="correlationId"
            type="text"
            defaultValue={correlationId}
            placeholder="req-xxxxxxxx or full UUID"
            className="w-full h-9 rounded-md border border-gray-300 px-3 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 bg-white font-mono"
          />
        </div>
        <button
          type="submit"
          className="h-9 px-4 text-sm font-medium text-white bg-indigo-600 hover:bg-indigo-700 rounded-md transition-colors"
        >
          Trace
        </button>
      </form>

      {/* Results */}
      {correlationId && (
        <div className="flex gap-4 items-start">
          {/* Timeline */}
          <div className="flex-1 min-w-0">
            {events.length === 0 ? (
              <div className="rounded-lg border border-gray-200 bg-white px-6 py-12 text-center">
                <p className="text-sm text-gray-500">
                  No events found for correlation ID{' '}
                  <span className="font-mono text-indigo-600">{correlationId}</span>.
                </p>
              </div>
            ) : (
              <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
                <div className="px-4 py-3 border-b border-gray-100 bg-gray-50 flex items-center justify-between">
                  <h3 className="text-sm font-semibold text-gray-700">
                    Trace for{' '}
                    <span className="font-mono text-indigo-600 text-xs">{correlationId}</span>
                  </h3>
                  <span className="text-xs text-gray-400">{events.length} event{events.length !== 1 ? 's' : ''}</span>
                </div>

                <div className="relative px-4 py-4">
                  {/* Vertical line */}
                  <div className="absolute left-8 top-4 bottom-4 w-px bg-gray-200" />

                  <div className="space-y-4">
                    {events.map((e, idx) => {
                      const isFirst    = idx === 0;
                      const isSelected = selected?.id === e.id;
                      const dotColor   = severityDotColor(e.severity);
                      return (
                        <div
                          key={e.id}
                          className="relative flex items-start gap-4 cursor-pointer group"
                          onClick={() => setSelected(isSelected ? null : e)}
                        >
                          {/* Dot */}
                          <div className={[
                            'relative z-10 flex items-center justify-center h-8 w-8 rounded-full border-2 shrink-0 transition-all',
                            isFirst ? 'border-indigo-500 bg-indigo-50' : 'border-gray-200 bg-white group-hover:border-indigo-300',
                          ].join(' ')}>
                            <span className={`h-3 w-3 rounded-full ${dotColor}`} />
                          </div>

                          {/* Card */}
                          <div className={[
                            'flex-1 min-w-0 rounded-md border px-3 py-2.5 transition-colors',
                            isSelected
                              ? 'border-indigo-300 bg-indigo-50'
                              : 'border-gray-200 bg-white hover:border-gray-300',
                          ].join(' ')}>
                            <div className="flex items-start justify-between gap-2 flex-wrap">
                              <div className="min-w-0">
                                <span className="text-xs font-mono font-semibold text-gray-700 truncate block">
                                  {e.eventType}
                                </span>
                                <span className="text-[10px] text-gray-400 font-mono block mt-0.5">
                                  {formatUtcFull(e.occurredAtUtc)}
                                </span>
                              </div>
                              <div className="flex items-center gap-1.5 shrink-0">
                                <SeverityBadge value={e.severity} />
                                <OutcomeBadge value={e.outcome} />
                              </div>
                            </div>
                            <div className="mt-1.5 flex items-center gap-3 text-[10px] text-gray-500 flex-wrap">
                              <span>{e.source}{e.sourceService ? ` / ${e.sourceService}` : ''}</span>
                              {e.actorLabel && <span className="text-gray-400">actor: {e.actorLabel}</span>}
                              {e.targetType && <span className="text-gray-400">{e.targetType}{e.targetId ? ` ${e.targetId.slice(0, 8)}…` : ''}</span>}
                            </div>
                            {isSelected && e.description && (
                              <p className="mt-2 text-[11px] text-gray-600 border-t border-gray-100 pt-1.5">{e.description}</p>
                            )}
                          </div>
                        </div>
                      );
                    })}
                  </div>
                </div>
              </div>
            )}
          </div>

          {/* Detail panel */}
          {selected && (
            <div className="w-80 shrink-0 rounded-lg border border-gray-200 bg-white shadow-sm overflow-hidden">
              <div className="flex items-center justify-between px-4 py-3 border-b border-gray-100 bg-gray-50">
                <h3 className="text-sm font-semibold text-gray-700">Event Detail</h3>
                <button onClick={() => setSelected(null)} className="text-gray-400 hover:text-gray-700">
                  <svg className="h-4 w-4" viewBox="0 0 20 20" fill="currentColor">
                    <path fillRule="evenodd" d="M4.293 4.293a1 1 0 011.414 0L10 8.586l4.293-4.293a1 1 0 111.414 1.414L11.414 10l4.293 4.293a1 1 0 01-1.414 1.414L10 11.414l-4.293 4.293a1 1 0 01-1.414-1.414L8.586 10 4.293 5.707a1 1 0 010-1.414z" clipRule="evenodd" />
                  </svg>
                </button>
              </div>
              <div className="px-4 py-3 space-y-2.5 overflow-y-auto max-h-[60vh]">
                <DRow label="Event Type" mono>{selected.eventType}</DRow>
                <DRow label="Category"><CategoryBadge value={selected.category} /></DRow>
                <DRow label="Occurred" mono>{formatUtcFull(selected.occurredAtUtc)}</DRow>
                <DRow label="Actor">{selected.actorLabel ?? selected.actorId ?? '—'}</DRow>
                {selected.targetType && <DRow label="Target">{selected.targetType} {selected.targetId}</DRow>}
                {selected.ipAddress  && <DRow label="IP" mono>{selected.ipAddress}</DRow>}
                <DRow label="Event ID" mono>{selected.id}</DRow>
                {selected.before   && <JsonBlock label="Before"   raw={selected.before} />}
                {selected.after    && <JsonBlock label="After"    raw={selected.after} />}
                {selected.metadata && <JsonBlock label="Metadata" raw={selected.metadata} />}
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  );
}

function DRow({ label, mono = false, children }: { label: string; mono?: boolean; children: React.ReactNode }) {
  return (
    <div className="flex justify-between gap-2 text-[11px]">
      <span className="text-gray-400 shrink-0">{label}</span>
      <span className={['text-gray-700 text-right break-all', mono ? 'font-mono' : ''].join(' ')}>{children}</span>
    </div>
  );
}

function JsonBlock({ label, raw }: { label: string; raw: string }) {
  let formatted = raw;
  try { formatted = JSON.stringify(JSON.parse(raw), null, 2); } catch { }
  return (
    <div>
      <p className="text-[10px] font-semibold text-gray-400 uppercase tracking-wider mb-0.5">{label}</p>
      <pre className="bg-gray-50 border border-gray-100 rounded p-1.5 text-[10px] font-mono text-gray-600 overflow-auto max-h-32 whitespace-pre-wrap break-all">
        {formatted}
      </pre>
    </div>
  );
}

function severityDotColor(severity: string): string {
  const MAP: Record<string, string> = {
    info:     'bg-blue-400',
    warn:     'bg-amber-400',
    error:    'bg-red-500',
    critical: 'bg-red-700',
  };
  return MAP[severity.toLowerCase()] ?? 'bg-gray-400';
}
