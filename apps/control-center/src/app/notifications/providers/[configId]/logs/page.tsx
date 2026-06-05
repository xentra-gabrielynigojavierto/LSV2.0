import Link                      from 'next/link';
import { requirePlatformAdmin }  from '@/lib/auth-guards';
import { CCShell }               from '@/components/shell/cc-shell';
import { notifClient }           from '@/lib/notifications-api';
import { fetchProviderLogs }     from '@/app/notifications/actions';
import type { NotifProviderConfig } from '@/lib/notifications-api';
import { ProviderLogsTable }     from '@/components/notifications/provider-logs-table';

export const dynamic = 'force-dynamic';

interface Props {
  params:      Promise<{ configId: string }>;
  searchParams: Promise<{ status?: string; from?: string; to?: string; page?: string }>;
}

const PAGE_SIZE = 50;

export default async function ProviderLogsPage({ params, searchParams }: Props) {
  const session = await requirePlatformAdmin();

  const { configId }                   = await params;
  const { status, from, to, page: pg } = await searchParams;
  const page   = pg ? parseInt(pg, 10) : 1;
  const offset = (page - 1) * PAGE_SIZE;

  let config: NotifProviderConfig | null = null;
  let configError: string | null = null;
  try {
    const res = await notifClient.get<{ data: NotifProviderConfig }>(
      `/providers/configs/${configId}`,
      0
    );
    config = res.data;
  } catch {
    configError = 'Could not load provider config.';
  }

  const logsResult = await fetchProviderLogs(configId, {
    limit: PAGE_SIZE,
    offset,
    status: status || undefined,
    from:   from   || undefined,
    to:     to     || undefined,
  });

  const rows  = logsResult.data?.rows  ?? [];
  const total = logsResult.data?.total ?? 0;
  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE));

  const configLabel = config
    ? `${config.displayName ?? config.providerType} — ${config.channel}`
    : configId;

  return (
    <CCShell userEmail={session.email}>
      <div className="space-y-6">

        {/* Header */}
        <div className="flex items-center gap-3">
          <Link
            href="/notifications/providers"
            className="text-sm text-muted-foreground hover:text-foreground transition-colors"
          >
            ← Providers
          </Link>
          <span className="text-muted-foreground">/</span>
          <span className="text-sm text-muted-foreground">{configLabel}</span>
          <span className="text-muted-foreground">/</span>
          <span className="text-sm font-medium">Logs</span>
        </div>

        <div>
          <h1 className="text-2xl font-semibold">Provider Logs</h1>
          <p className="text-sm text-muted-foreground mt-1">
            Delivery attempts routed through this provider config.
          </p>
        </div>

        {(configError || logsResult.error) && (
          <div className="rounded-md border border-destructive/30 bg-destructive/10 px-4 py-3 text-sm text-destructive">
            {configError ?? logsResult.error}
          </div>
        )}

        {/* Filters */}
        <ProviderLogsFilters
          configId={configId}
          currentStatus={status}
          currentFrom={from}
          currentTo={to}
        />

        {/* Table */}
        <ProviderLogsTable rows={rows} />

        {/* Pagination */}
        {totalPages > 1 && (
          <ProviderLogsPagination
            configId={configId}
            page={page}
            totalPages={totalPages}
            total={total}
            status={status}
            from={from}
            to={to}
          />
        )}

        {rows.length === 0 && !logsResult.error && (
          <p className="text-center text-sm text-muted-foreground py-12">
            No delivery attempts found for this config.
          </p>
        )}
      </div>
    </CCShell>
  );
}

// ── Inline filter bar (server component, uses form GET) ───────────────────────

function ProviderLogsFilters({
  configId,
  currentStatus,
  currentFrom,
  currentTo,
}: {
  configId:      string;
  currentStatus?: string;
  currentFrom?:   string;
  currentTo?:     string;
}) {
  return (
    <form
      method="GET"
      action={`/notifications/providers/${configId}/logs`}
      className="flex flex-wrap items-end gap-3"
    >
      <div className="flex flex-col gap-1">
        <label className="text-xs text-muted-foreground">Status</label>
        <select
          name="status"
          defaultValue={currentStatus ?? ''}
          className="h-9 rounded-md border bg-background px-3 text-sm focus:outline-none focus:ring-2 focus:ring-ring"
        >
          <option value="">All</option>
          <option value="sent">Sent</option>
          <option value="failed">Failed</option>
          <option value="sending">Sending</option>
          <option value="created">Created</option>
        </select>
      </div>

      <div className="flex flex-col gap-1">
        <label className="text-xs text-muted-foreground">From</label>
        <input
          type="date"
          name="from"
          defaultValue={currentFrom ?? ''}
          className="h-9 rounded-md border bg-background px-3 text-sm focus:outline-none focus:ring-2 focus:ring-ring"
        />
      </div>

      <div className="flex flex-col gap-1">
        <label className="text-xs text-muted-foreground">To</label>
        <input
          type="date"
          name="to"
          defaultValue={currentTo ?? ''}
          className="h-9 rounded-md border bg-background px-3 text-sm focus:outline-none focus:ring-2 focus:ring-ring"
        />
      </div>

      <button
        type="submit"
        className="h-9 rounded-md bg-primary px-4 text-sm font-medium text-primary-foreground hover:bg-primary/90"
      >
        Filter
      </button>

      {(currentStatus || currentFrom || currentTo) && (
        <a
          href={`/notifications/providers/${configId}/logs`}
          className="h-9 flex items-center rounded-md border px-4 text-sm text-muted-foreground hover:bg-muted"
        >
          Clear
        </a>
      )}
    </form>
  );
}

// ── Pagination ────────────────────────────────────────────────────────────────

function ProviderLogsPagination({
  configId,
  page,
  totalPages,
  total,
  status,
  from,
  to,
}: {
  configId:   string;
  page:       number;
  totalPages: number;
  total:      number;
  status?:    string;
  from?:      string;
  to?:        string;
}) {
  function href(p: number) {
    const params = new URLSearchParams();
    params.set('page', String(p));
    if (status) params.set('status', status);
    if (from)   params.set('from',   from);
    if (to)     params.set('to',     to);
    return `/notifications/providers/${configId}/logs?${params.toString()}`;
  }

  return (
    <div className="flex items-center justify-between text-sm text-muted-foreground">
      <span>{total} total attempt{total !== 1 ? 's' : ''}</span>
      <div className="flex gap-2">
        {page > 1 && (
          <a href={href(page - 1)} className="rounded border px-3 py-1 hover:bg-muted">
            Previous
          </a>
        )}
        <span className="px-3 py-1">
          Page {page} of {totalPages}
        </span>
        {page < totalPages && (
          <a href={href(page + 1)} className="rounded border px-3 py-1 hover:bg-muted">
            Next
          </a>
        )}
      </div>
    </div>
  );
}
