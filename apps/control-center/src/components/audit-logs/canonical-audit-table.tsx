import type { CanonicalAuditEvent } from '@/types/control-center';

interface CanonicalAuditTableProps {
  entries: CanonicalAuditEvent[];
}

/**
 * CanonicalAuditTable — renders events from the Platform Audit Event Service.
 *
 * Columns: Time, Source, Event Type, Category, Severity, Actor, Target, Outcome, Corr ID
 *
 * Used when AUDIT_READ_MODE is 'canonical' or 'hybrid' (canonical succeeded).
 * Read-only — no actions.
 */
export function CanonicalAuditTable({ entries }: CanonicalAuditTableProps) {
  if (entries.length === 0) {
    return (
      <div className="rounded-lg border border-gray-200 bg-white px-6 py-12 text-center">
        <p className="text-sm text-gray-400">No audit events found for the current filters.</p>
      </div>
    );
  }

  return (
    <div className="overflow-x-auto rounded-lg border border-gray-200 bg-white">
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
          {entries.map((e) => (
            <tr key={e.id} className="hover:bg-gray-50 transition-colors">
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
          ))}
        </tbody>
      </table>
    </div>
  );
}

// ── Badge helpers ──────────────────────────────────────────────────────────────

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

// ── Date helper ───────────────────────────────────────────────────────────────

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
