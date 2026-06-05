import type { AuditEntry } from '@/lib/system-health-audit';

interface ServicesAuditListProps {
  entries: AuditEntry[];
}

const ACTION_LABEL: Record<AuditEntry['action'], string> = {
  add:    'Added',
  update: 'Updated',
  remove: 'Removed',
};

const ACTION_BADGE: Record<AuditEntry['action'], string> = {
  add:    'bg-green-50 text-green-700 border-green-200',
  update: 'bg-blue-50 text-blue-700 border-blue-200',
  remove: 'bg-red-50 text-red-700 border-red-200',
};

function fmtTime(iso: string): string {
  try {
    return new Date(iso).toLocaleString();
  } catch {
    return iso;
  }
}

function fieldDiffs(entry: AuditEntry): { field: string; from: string; to: string }[] {
  if (entry.action !== 'update' || !entry.before || !entry.after) return [];
  const fields: (keyof Pick<NonNullable<AuditEntry['after']>, 'name' | 'url' | 'category'>)[] =
    ['name', 'url', 'category'];
  const out: { field: string; from: string; to: string }[] = [];
  for (const f of fields) {
    const a = String(entry.before[f] ?? '');
    const b = String(entry.after[f]  ?? '');
    if (a !== b) out.push({ field: f, from: a, to: b });
  }
  return out;
}

function snapshotLabel(s: AuditEntry['after']): string {
  if (!s) return '';
  return `${s.name} — ${s.url} [${s.category}]`;
}

export function ServicesAuditList({ entries }: ServicesAuditListProps) {
  return (
    <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
      <div className="px-5 py-3.5 border-b border-gray-100 bg-gray-50">
        <h2 className="text-xs font-semibold uppercase tracking-wide text-gray-500">
          Recent Changes ({entries.length})
        </h2>
      </div>

      {entries.length === 0 ? (
        <div className="px-5 py-6 text-sm text-gray-500 text-center">
          No changes recorded yet.
        </div>
      ) : (
        <ul className="divide-y divide-gray-100">
          {entries.map(entry => {
            const diffs = fieldDiffs(entry);
            const snapshot = entry.action === 'add'    ? entry.after
                           : entry.action === 'remove' ? entry.before
                           : entry.after;
            return (
              <li key={entry.id} className="px-5 py-3.5 text-sm">
                <div className="flex flex-wrap items-center gap-2">
                  <span className={`text-[11px] uppercase tracking-wide font-semibold px-2 py-0.5 rounded border ${ACTION_BADGE[entry.action]}`}>
                    {ACTION_LABEL[entry.action]}
                  </span>
                  <span className="font-medium text-gray-900">
                    {snapshot?.name ?? entry.serviceId}
                  </span>
                  <span className="text-xs text-gray-500">
                    by <span className="font-mono">{entry.actor.email}</span>
                  </span>
                  <span className="text-xs text-gray-400 ml-auto">
                    {fmtTime(entry.timestamp)}
                  </span>
                </div>

                {entry.action === 'update' && diffs.length > 0 && (
                  <ul className="mt-2 ml-2 space-y-0.5 text-xs text-gray-600">
                    {diffs.map(d => (
                      <li key={d.field}>
                        <span className="font-mono text-gray-500">{d.field}:</span>{' '}
                        <span className="line-through text-red-600">{d.from || '∅'}</span>
                        {' → '}
                        <span className="text-green-700">{d.to || '∅'}</span>
                      </li>
                    ))}
                  </ul>
                )}

                {(entry.action === 'add' || entry.action === 'remove') && snapshot && (
                  <p className="mt-1 ml-2 text-xs text-gray-500 font-mono">
                    {snapshotLabel(snapshot)}
                  </p>
                )}
              </li>
            );
          })}
        </ul>
      )}
    </div>
  );
}
