'use client';

import type { ProviderLogRow } from '@/app/notifications/actions';
import { formatFailureCategory } from '@/lib/notifications-formatters';

interface Props {
  rows: ProviderLogRow[];
}

export function ProviderLogsTable({ rows }: Props) {
  if (rows.length === 0) return null;

  return (
    <div className="rounded-md border overflow-hidden">
      <div className="overflow-x-auto">
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b bg-muted/50">
              <th className="px-4 py-3 text-left font-medium text-muted-foreground">Status</th>
              <th className="px-4 py-3 text-left font-medium text-muted-foreground">Channel</th>
              <th className="px-4 py-3 text-left font-medium text-muted-foreground">Recipient</th>
              <th className="px-4 py-3 text-left font-medium text-muted-foreground">Subject / Template</th>
              <th className="px-4 py-3 text-left font-medium text-muted-foreground">Attempt #</th>
              <th className="px-4 py-3 text-left font-medium text-muted-foreground">Started</th>
              <th className="px-4 py-3 text-left font-medium text-muted-foreground">Duration</th>
              <th className="px-4 py-3 text-left font-medium text-muted-foreground">Error</th>
            </tr>
          </thead>
          <tbody className="divide-y">
            {rows.map((row) => (
              <tr key={row.id} className="hover:bg-muted/30 transition-colors">
                <td className="px-4 py-3">
                  <AttemptStatusBadge status={row.status} />
                </td>
                <td className="px-4 py-3 text-muted-foreground capitalize">
                  {row.channel ?? '—'}
                </td>
                <td className="px-4 py-3 font-mono text-xs max-w-[180px] truncate" title={row.recipient ?? undefined}>
                  {row.recipient ?? '—'}
                </td>
                <td className="px-4 py-3 max-w-[220px]">
                  {row.renderedSubject ? (
                    <span className="truncate block" title={row.renderedSubject}>
                      {row.renderedSubject}
                    </span>
                  ) : row.templateKey ? (
                    <span className="text-muted-foreground font-mono text-xs">{row.templateKey}</span>
                  ) : (
                    <span className="text-muted-foreground">—</span>
                  )}
                </td>
                <td className="px-4 py-3 text-center text-muted-foreground">
                  #{row.attemptNumber}
                  {row.platformFallbackUsed && (
                    <span className="ml-1 text-xs text-amber-600" title="Platform fallback was used">
                      ⚠
                    </span>
                  )}
                </td>
                <td className="px-4 py-3 text-xs text-muted-foreground whitespace-nowrap">
                  {row.startedAt ? formatDateTime(row.startedAt) : '—'}
                </td>
                <td className="px-4 py-3 text-xs text-muted-foreground whitespace-nowrap">
                  {row.startedAt && row.completedAt
                    ? formatDuration(row.startedAt, row.completedAt)
                    : '—'}
                </td>
                <td className="px-4 py-3 max-w-[240px]">
                  {row.errorMessage ? (
                    <span
                      className="text-xs text-destructive truncate block"
                      title={row.errorMessage}
                    >
                      {row.failureCategory && (
                        <span className="font-medium mr-1">[{formatFailureCategory(row.failureCategory)}]</span>
                      )}
                      {row.errorMessage}
                    </span>
                  ) : (
                    <span className="text-muted-foreground">—</span>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

// ── Status badge ──────────────────────────────────────────────────────────────

function AttemptStatusBadge({ status }: { status: string }) {
  const map: Record<string, { label: string; className: string }> = {
    sent:     { label: 'Sent',     className: 'bg-green-100 text-green-800 dark:bg-green-900/40 dark:text-green-300' },
    failed:   { label: 'Failed',   className: 'bg-red-100 text-red-800 dark:bg-red-900/40 dark:text-red-300' },
    sending:  { label: 'Sending',  className: 'bg-blue-100 text-blue-800 dark:bg-blue-900/40 dark:text-blue-300' },
    created:  { label: 'Created',  className: 'bg-muted text-muted-foreground' },
  };
  const { label, className } = map[status] ?? { label: status, className: 'bg-muted text-muted-foreground' };
  return (
    <span className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${className}`}>
      {label}
    </span>
  );
}

// ── Helpers ───────────────────────────────────────────────────────────────────

function formatDateTime(iso: string): string {
  try {
    return new Date(iso).toLocaleString(undefined, {
      month:  'short',
      day:    'numeric',
      hour:   '2-digit',
      minute: '2-digit',
      second: '2-digit',
    });
  } catch {
    return iso;
  }
}

function formatDuration(startIso: string, endIso: string): string {
  const ms = new Date(endIso).getTime() - new Date(startIso).getTime();
  if (ms < 0) return '—';
  if (ms < 1000) return `${ms}ms`;
  return `${(ms / 1000).toFixed(1)}s`;
}
