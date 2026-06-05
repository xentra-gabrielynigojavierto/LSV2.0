'use client';

import { useState } from 'react';
import type { CanonicalAuditEvent } from '@/types/control-center';

interface Props {
  entries: CanonicalAuditEvent[];
}

/**
 * CanonicalAuditTableInteractive — client-side wrapper around the canonical audit
 * table that adds row-click selection and a detail side-panel.
 *
 * The table layout is identical to the read-only CanonicalAuditTable; the only
 * additions are:
 *   - rows have cursor-pointer and a highlight ring on selection
 *   - clicking a row opens a slide-in detail panel on the right
 *   - the panel shows all fields including description, metadata, Before/After
 *     JSON, actor detail, IP, and ingestion timestamp
 */
export function CanonicalAuditTableInteractive({ entries }: Props) {
  const [selected, setSelected] = useState<CanonicalAuditEvent | null>(null);

  if (entries.length === 0) {
    return (
      <div className="rounded-lg border border-gray-200 bg-white px-6 py-12 text-center">
        <p className="text-sm text-gray-400">No audit events found for the current filters.</p>
      </div>
    );
  }

  return (
    <div className="flex gap-4 items-start">
      {/* ── Table ─────────────────────────────────────────────────────────── */}
      <div className="flex-1 overflow-x-auto rounded-lg border border-gray-200 bg-white min-w-0">
        <table className="min-w-full divide-y divide-gray-100 text-sm">
          <thead className="bg-gray-50 text-xs text-gray-500 uppercase tracking-wide">
            <tr>
              <th className="px-4 py-3 text-left font-medium whitespace-nowrap">Time (UTC)</th>
              <th className="px-4 py-3 text-left font-medium whitespace-nowrap">Source</th>
              <th className="px-4 py-3 text-left font-medium whitespace-nowrap">Event Type</th>
              <th className="px-4 py-3 text-left font-medium whitespace-nowrap">Category</th>
              <th className="px-4 py-3 text-left font-medium whitespace-nowrap">Severity</th>
              <th className="px-4 py-3 text-left font-medium whitespace-nowrap">Actor</th>
              <th className="px-4 py-3 text-left font-medium whitespace-nowrap">Target</th>
              <th className="px-4 py-3 text-left font-medium whitespace-nowrap">Outcome</th>
              <th className="px-4 py-3 text-left font-medium whitespace-nowrap">Correlation</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {entries.map((e) => {
              const isSelected = selected?.id === e.id;
              return (
                <tr
                  key={e.id}
                  onClick={() => setSelected(isSelected ? null : e)}
                  className={[
                    'cursor-pointer transition-colors',
                    isSelected
                      ? 'bg-indigo-50 ring-1 ring-inset ring-indigo-200'
                      : 'hover:bg-gray-50',
                  ].join(' ')}
                >
                  <td className="px-4 py-2.5 text-gray-500 whitespace-nowrap font-mono text-[11px]">
                    {formatUtc(e.occurredAtUtc)}
                  </td>
                  <td className="px-4 py-2.5">
                    <span className="inline-flex items-center px-1.5 py-0.5 rounded text-[10px] font-semibold bg-gray-100 text-gray-600 border border-gray-200">
                      {e.source}
                    </span>
                  </td>
                  <td className="px-4 py-2.5 text-gray-700 font-mono text-[11px] whitespace-nowrap">
                    {e.eventType}
                  </td>
                  <td className="px-4 py-2.5">
                    <CategoryBadge value={e.category} />
                  </td>
                  <td className="px-4 py-2.5">
                    <SeverityBadge value={e.severity} />
                  </td>
                  <td className="px-4 py-2.5 text-gray-700 whitespace-nowrap">
                    <span className="block text-xs font-medium">
                      {e.actorLabel ?? e.actorId ?? <span className="text-gray-400 italic">system</span>}
                    </span>
                    {e.actorId && e.actorLabel && (
                      <span className="block text-[10px] text-gray-400 font-mono">{e.actorId}</span>
                    )}
                  </td>
                  <td className="px-4 py-2.5 text-gray-600 whitespace-nowrap text-xs">
                    {e.targetType && (
                      <span className="mr-1 text-[10px] font-semibold text-gray-400 uppercase">{e.targetType}</span>
                    )}
                    {e.targetId && (
                      <span className="font-mono text-[11px]">{e.targetId}</span>
                    )}
                    {!e.targetType && !e.targetId && (
                      <span className="text-gray-300 italic text-[11px]">—</span>
                    )}
                  </td>
                  <td className="px-4 py-2.5">
                    <OutcomeBadge value={e.outcome} />
                  </td>
                  <td className="px-4 py-2.5 text-gray-400 font-mono text-[10px] whitespace-nowrap">
                    {e.correlationId ? (
                      <span title={e.correlationId}>
                        {e.correlationId.length > 12
                          ? `${e.correlationId.slice(0, 12)}…`
                          : e.correlationId}
                      </span>
                    ) : (
                      <span className="text-gray-200">—</span>
                    )}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      {/* ── Detail Side Panel ─────────────────────────────────────────────── */}
      {selected && (
        <div className="w-96 shrink-0 rounded-lg border border-gray-200 bg-white shadow-sm overflow-hidden">
          {/* Panel header */}
          <div className="flex items-center justify-between px-4 py-3 border-b border-gray-100 bg-gray-50">
            <h3 className="text-sm font-semibold text-gray-700">Event Detail</h3>
            <button
              onClick={() => setSelected(null)}
              className="text-gray-400 hover:text-gray-700 transition-colors"
              aria-label="Close panel"
            >
              <svg className="h-4 w-4" viewBox="0 0 20 20" fill="currentColor">
                <path fillRule="evenodd" d="M4.293 4.293a1 1 0 011.414 0L10 8.586l4.293-4.293a1 1 0 111.414 1.414L11.414 10l4.293 4.293a1 1 0 01-1.414 1.414L10 11.414l-4.293 4.293a1 1 0 01-1.414-1.414L8.586 10 4.293 5.707a1 1 0 010-1.414z" clipRule="evenodd" />
              </svg>
            </button>
          </div>

          {/* Panel body */}
          <div className="px-4 py-3 space-y-3 overflow-y-auto max-h-[70vh]">

            {/* Event identity */}
            <Section label="Event">
              <Row label="Type" mono>{selected.eventType}</Row>
              <Row label="Source">{selected.source}</Row>
              <Row label="Category"><CategoryBadge value={selected.category} /></Row>
              <Row label="Severity"><SeverityBadge value={selected.severity} /></Row>
              <Row label="Outcome"><OutcomeBadge value={selected.outcome} /></Row>
            </Section>

            {/* Timing */}
            <Section label="Timing">
              <Row label="Occurred" mono>{formatUtcFull(selected.occurredAtUtc)}</Row>
              <Row label="Ingested" mono>{formatUtcFull(selected.ingestedAtUtc)}</Row>
            </Section>

            {/* Actor */}
            <Section label="Actor">
              {selected.actorLabel && <Row label="Name">{selected.actorLabel}</Row>}
              {selected.actorId    && <Row label="ID" mono>{selected.actorId}</Row>}
              {!selected.actorLabel && !selected.actorId && (
                <p className="text-[11px] text-gray-400 italic">System / anonymous</p>
              )}
            </Section>

            {/* Target entity */}
            {(selected.targetType || selected.targetId) && (
              <Section label="Target Entity">
                {selected.targetType && <Row label="Type">{selected.targetType}</Row>}
                {selected.targetId   && <Row label="ID" mono>{selected.targetId}</Row>}
              </Section>
            )}

            {/* Scope */}
            {selected.tenantId && (
              <Section label="Scope">
                <Row label="Tenant ID" mono>{selected.tenantId}</Row>
              </Section>
            )}

            {/* Network / correlation */}
            <Section label="Tracing">
              {selected.correlationId && (
                <Row label="Correlation ID" mono>{selected.correlationId}</Row>
              )}
              {selected.ipAddress && (
                <Row label="IP Address" mono>{selected.ipAddress}</Row>
              )}
              <Row label="Event ID" mono>{selected.id}</Row>
            </Section>

            {/* Description */}
            {selected.description && (
              <Section label="Description">
                <p className="text-[12px] text-gray-700 leading-relaxed">{selected.description}</p>
              </Section>
            )}

            {/* Metadata — render as formatted JSON if parseable */}
            {selected.metadata && <JsonSection label="Metadata" raw={selected.metadata} />}

          </div>
        </div>
      )}
    </div>
  );
}

// ── Panel sub-components ────────────────────────────────────────────────────

function Section({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <p className="text-[10px] font-semibold text-gray-400 uppercase tracking-wider mb-1">{label}</p>
      <div className="space-y-0.5">{children}</div>
    </div>
  );
}

function Row({
  label,
  mono = false,
  children,
}: {
  label:     string;
  mono?:     boolean;
  children:  React.ReactNode;
}) {
  return (
    <div className="flex justify-between gap-2 text-[11px] py-0.5">
      <span className="text-gray-400 shrink-0">{label}</span>
      <span className={[
        'text-gray-700 text-right break-all',
        mono ? 'font-mono' : '',
      ].join(' ')}>
        {children}
      </span>
    </div>
  );
}

function JsonSection({ label, raw }: { label: string; raw: string }) {
  let formatted = raw;
  try {
    formatted = JSON.stringify(JSON.parse(raw), null, 2);
  } catch { /* render as-is if not valid JSON */ }
  return (
    <div>
      <p className="text-[10px] font-semibold text-gray-400 uppercase tracking-wider mb-1">{label}</p>
      <pre className="bg-gray-50 border border-gray-100 rounded p-2 text-[10px] font-mono text-gray-600 overflow-auto max-h-40 whitespace-pre-wrap break-all">
        {formatted}
      </pre>
    </div>
  );
}

// ── Badge helpers ────────────────────────────────────────────────────────────

function SeverityBadge({ value }: { value: string }) {
  const MAP: Record<string, string> = {
    info:     'bg-blue-50  text-blue-700  border border-blue-200',
    warn:     'bg-amber-50 text-amber-700 border border-amber-300',
    error:    'bg-red-50   text-red-700   border border-red-200',
    critical: 'bg-red-100  text-red-800   border border-red-400 font-bold',
  };
  const cls = MAP[value.toLowerCase()] ?? 'bg-gray-100 text-gray-500 border border-gray-200';
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[10px] font-semibold uppercase tracking-wide ${cls}`}>
      {value}
    </span>
  );
}

function CategoryBadge({ value }: { value: string }) {
  const MAP: Record<string, string> = {
    security:       'bg-red-50    text-red-700   border border-red-200',
    access:         'bg-orange-50 text-orange-700 border border-orange-200',
    business:       'bg-green-50  text-green-700  border border-green-200',
    administrative: 'bg-gray-100  text-gray-600   border border-gray-200',
    compliance:     'bg-purple-50 text-purple-700  border border-purple-200',
    datachange:     'bg-blue-50   text-blue-700    border border-blue-200',
  };
  const key = value.toLowerCase().replace(/\s+/g, '');
  const cls = MAP[key] ?? 'bg-gray-100 text-gray-500 border border-gray-200';
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[10px] font-medium capitalize ${cls}`}>
      {value}
    </span>
  );
}

function OutcomeBadge({ value }: { value: string }) {
  const lower = value.toLowerCase();
  if (lower === 'success' || lower === 'succeeded') {
    return (
      <span className="inline-flex items-center gap-1 text-[11px] text-green-700 font-medium">
        <span className="h-1.5 w-1.5 rounded-full bg-green-500" /> success
      </span>
    );
  }
  if (lower === 'failure' || lower === 'failed') {
    return (
      <span className="inline-flex items-center gap-1 text-[11px] text-red-700 font-medium">
        <span className="h-1.5 w-1.5 rounded-full bg-red-500" /> failed
      </span>
    );
  }
  return (
    <span className="inline-flex items-center gap-1 text-[11px] text-gray-500">
      <span className="h-1.5 w-1.5 rounded-full bg-gray-300" /> {value}
    </span>
  );
}

// ── Date helpers ─────────────────────────────────────────────────────────────

function formatUtc(iso: string): string {
  try {
    const d = new Date(iso);
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${d.getUTCFullYear()}-${pad(d.getUTCMonth() + 1)}-${pad(d.getUTCDate())} `
         + `${pad(d.getUTCHours())}:${pad(d.getUTCMinutes())}:${pad(d.getUTCSeconds())}`;
  } catch {
    return iso;
  }
}

function formatUtcFull(iso: string): string {
  try {
    const d = new Date(iso);
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${d.getUTCFullYear()}-${pad(d.getUTCMonth() + 1)}-${pad(d.getUTCDate())} `
         + `${pad(d.getUTCHours())}:${pad(d.getUTCMinutes())}:${pad(d.getUTCSeconds())}.${String(d.getUTCMilliseconds()).padStart(3, '0')} UTC`;
  } catch {
    return iso;
  }
}
