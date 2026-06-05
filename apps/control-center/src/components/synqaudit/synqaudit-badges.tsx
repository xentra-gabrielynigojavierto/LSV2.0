/**
 * synqaudit-badges.tsx — Shared badge components for the SynqAudit UI.
 *
 * Exports: SeverityBadge, CategoryBadge, OutcomeBadge
 * These are pure presentation components — no client directive needed.
 */

export function SeverityBadge({ value }: { value: string }) {
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

export function CategoryBadge({ value }: { value: string }) {
  const MAP: Record<string, string> = {
    security:       'bg-red-50    text-red-700   border border-red-200',
    access:         'bg-orange-50 text-orange-700 border border-orange-200',
    business:       'bg-green-50  text-green-700  border border-green-200',
    administrative: 'bg-gray-100  text-gray-600   border border-gray-200',
    compliance:     'bg-purple-50 text-purple-700  border border-purple-200',
    datachange:     'bg-blue-50   text-blue-700    border border-blue-200',
    dataChange:     'bg-blue-50   text-blue-700    border border-blue-200',
  };
  const key = value.toLowerCase().replace(/\s+/g, '');
  const cls = MAP[key] ?? 'bg-gray-100 text-gray-500 border border-gray-200';
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[10px] font-medium capitalize ${cls}`}>
      {value}
    </span>
  );
}

export function OutcomeBadge({ value }: { value: string }) {
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

export function formatUtc(iso: string): string {
  try {
    const d = new Date(iso);
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${d.getUTCFullYear()}-${pad(d.getUTCMonth() + 1)}-${pad(d.getUTCDate())} `
         + `${pad(d.getUTCHours())}:${pad(d.getUTCMinutes())}:${pad(d.getUTCSeconds())}`;
  } catch {
    return iso;
  }
}

export function formatUtcFull(iso: string): string {
  try {
    const d = new Date(iso);
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${d.getUTCFullYear()}-${pad(d.getUTCMonth() + 1)}-${pad(d.getUTCDate())} `
         + `${pad(d.getUTCHours())}:${pad(d.getUTCMinutes())}:${pad(d.getUTCSeconds())}.${String(d.getUTCMilliseconds()).padStart(3, '0')} UTC`;
  } catch {
    return iso;
  }
}
