import { redirect } from 'next/navigation';
import Link from 'next/link';
import { getServerSession } from '@/lib/session';
import { supportServerApi, type TicketSummary, type TicketStatus, type TicketPriority } from '@/lib/support-server-api';
import { NewTicketModal } from '@/components/support/NewTicketModal';

export const dynamic = 'force-dynamic';

const STATUS_STYLES: Record<TicketStatus, string> = {
  Open:        'bg-blue-100   text-blue-700   border-blue-300',
  Pending:     'bg-yellow-100 text-yellow-700  border-yellow-300',
  InProgress:  'bg-amber-100  text-amber-700   border-amber-300',
  Resolved:    'bg-green-100  text-green-700   border-green-300',
  Closed:      'bg-gray-100   text-gray-500    border-gray-300',
  Cancelled:   'bg-red-50     text-red-400     border-red-200',
};

const STATUS_LABELS: Record<TicketStatus, string> = {
  Open:       'Open',
  Pending:    'Pending',
  InProgress: 'In Progress',
  Resolved:   'Resolved',
  Closed:     'Closed',
  Cancelled:  'Cancelled',
};

const PRIORITY_STYLES: Record<TicketPriority, string> = {
  Low:    'bg-gray-100  text-gray-500   border-gray-200',
  Normal: 'bg-blue-50   text-blue-600   border-blue-200',
  High:   'bg-amber-50  text-amber-600  border-amber-300',
  Urgent: 'bg-red-100   text-red-700    border-red-300',
};

function formatRelative(iso: string): string {
  try {
    const diffMs  = Date.now() - new Date(iso).getTime();
    const minutes = Math.floor(diffMs / 60_000);
    if (minutes < 1)  return 'just now';
    if (minutes < 60) return `${minutes}m ago`;
    const hours = Math.floor(minutes / 60);
    if (hours < 24)   return `${hours}h ago`;
    return `${Math.floor(hours / 24)}d ago`;
  } catch {
    return iso;
  }
}

interface SupportPageProps {
  searchParams: Promise<{
    page?:     string;
    status?:   string;
    priority?: string;
  }>;
}

/**
 * /support — Support entry point for TenantAdmin and PlatformAdmin users.
 *
 * Access: PlatformAdmin or TenantAdmin (adminOnly nav item + page guard).
 * Data:   Fetched from Support service via gateway at /support/api/tickets.
 *
 * Layout: inherits (platform) layout → AppShell.
 */
export default async function SupportPage({ searchParams }: SupportPageProps) {
  const session = await getServerSession();
  if (!session || (!session.isPlatformAdmin && !session.isTenantAdmin)) {
    redirect('/access-denied');
  }

  const sp       = await searchParams;
  const page     = Math.max(1, parseInt(sp.page ?? '1', 10) || 1);
  const status   = sp.status   ?? '';
  const priority = sp.priority ?? '';

  let tickets: TicketSummary[] = [];
  let total    = 0;
  let fetchErr: string | null  = null;

  try {
    const result = await supportServerApi.tickets.list({
      page,
      pageSize: 25,
      status:   status   || undefined,
      priority: priority || undefined,
    });
    tickets = result.items;
    total   = result.total;
  } catch (err) {
    fetchErr = err instanceof Error ? err.message : 'Failed to load support tickets.';
  }

  const openCount     = tickets.filter(t => t.status === 'Open').length;
  const urgentCount   = tickets.filter(t => t.priority === 'Urgent').length;
  const totalPages    = Math.max(1, Math.ceil(total / 25));

  function pageHref(p: number) {
    const params = new URLSearchParams();
    if (status)   params.set('status',   status);
    if (priority) params.set('priority', priority);
    params.set('page', String(p));
    return `/support?${params.toString()}`;
  }

  return (
    <div className="min-h-full bg-gray-50">
      <div className="max-w-5xl mx-auto px-6 py-8">

        {/* Page header */}
        <div className="mb-6 flex items-start justify-between gap-4 flex-wrap">
          <div>
            <div className="flex items-center gap-3">
              <i className="ri-customer-service-2-line text-xl text-indigo-600" />
              <h1 className="text-xl font-semibold text-gray-900">Support</h1>
            </div>
            <p className="text-sm text-gray-500 mt-1">
              Manage and track support tickets for your organization.
            </p>
          </div>

          {/* Quick-glance counts + new ticket */}
          <div className="flex items-center gap-3 shrink-0">
            {openCount > 0 && (
              <span className="text-xs font-semibold px-2.5 py-1 rounded-full bg-blue-100 text-blue-700 border border-blue-300">
                {openCount} Open
              </span>
            )}
            {urgentCount > 0 && (
              <span className="text-xs font-semibold px-2.5 py-1 rounded-full bg-red-100 text-red-700 border border-red-300">
                {urgentCount} Urgent
              </span>
            )}
            <NewTicketModal />
          </div>
        </div>

        {/* Error state */}
        {fetchErr && (
          <div className="mb-6 bg-red-50 border border-red-200 rounded-lg px-5 py-4">
            <p className="text-sm text-red-700 font-medium">Failed to load support tickets</p>
            <p className="text-xs text-red-600 mt-1">{fetchErr}</p>
          </div>
        )}

        {/* Filters */}
        <form method="GET" action="/support" className="mb-4 flex flex-wrap gap-2">
          <select
            name="status"
            defaultValue={status}
            className="text-sm border border-gray-300 rounded-md px-2 py-1.5 focus:outline-none focus:ring-2 focus:ring-indigo-500"
          >
            <option value="">All Statuses</option>
            {(['Open', 'Pending', 'InProgress', 'Resolved', 'Closed', 'Cancelled'] as TicketStatus[]).map(s => (
              <option key={s} value={s}>{STATUS_LABELS[s]}</option>
            ))}
          </select>
          <select
            name="priority"
            defaultValue={priority}
            className="text-sm border border-gray-300 rounded-md px-2 py-1.5 focus:outline-none focus:ring-2 focus:ring-indigo-500"
          >
            <option value="">All Priorities</option>
            {(['Low', 'Normal', 'High', 'Urgent'] as TicketPriority[]).map(p => (
              <option key={p} value={p}>{p}</option>
            ))}
          </select>
          <button
            type="submit"
            className="px-4 py-1.5 text-sm font-medium bg-indigo-600 text-white rounded-md hover:bg-indigo-700 transition-colors"
          >
            Filter
          </button>
          {(status || priority) && (
            <a
              href="/support"
              className="px-3 py-1.5 text-sm text-gray-500 hover:text-gray-700 rounded-md border border-gray-200 hover:border-gray-300 transition-colors"
            >
              Clear
            </a>
          )}
        </form>

        {/* Ticket table */}
        {!fetchErr && (
          <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
            {tickets.length === 0 ? (
              <div className="px-6 py-12 text-center">
                <i className="ri-customer-service-2-line text-3xl text-gray-300 mb-3 block" />
                <p className="text-sm text-gray-400">No tickets match the current filters.</p>
              </div>
            ) : (
              <>
                <table className="w-full text-sm">
                  <thead>
                    <tr className="bg-gray-50 border-b border-gray-100 text-xs font-semibold text-gray-500 uppercase tracking-wide">
                      <th className="text-left px-4 py-3">Ticket</th>
                      <th className="text-left px-4 py-3 hidden md:table-cell">Number</th>
                      <th className="text-left px-4 py-3">Priority</th>
                      <th className="text-left px-4 py-3">Status</th>
                      <th className="text-left px-4 py-3 hidden sm:table-cell">Updated</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-100">
                    {tickets.map(t => (
                      <tr key={t.id} className="hover:bg-gray-50 transition-colors">
                        <td className="px-4 py-3">
                          <div className="flex flex-col gap-1">
                            <Link
                              href={`/support/${t.id}`}
                              className="font-medium text-indigo-700 hover:text-indigo-900 hover:underline leading-snug"
                            >
                              {t.title}
                            </Link>
                            {(t.requesterName || t.requesterEmail) && (
                              <span className="inline-flex items-center gap-1 text-[11px] text-gray-500">
                                <svg className="w-3 h-3 shrink-0 text-gray-400" viewBox="0 0 16 16" fill="currentColor">
                                  <path d="M8 8a3 3 0 1 0 0-6 3 3 0 0 0 0 6Zm-5 6a5 5 0 0 1 10 0H3Z"/>
                                </svg>
                                {t.requesterName && t.requesterEmail ? (
                                  <>
                                    <span className="font-medium text-gray-700">{t.requesterName}</span>
                                    <span className="text-gray-400">·</span>
                                    <span className="text-gray-400">{t.requesterEmail}</span>
                                  </>
                                ) : t.requesterName ? (
                                  <span className="font-medium text-gray-700">{t.requesterName}</span>
                                ) : (
                                  <span className="text-gray-400">{t.requesterEmail}</span>
                                )}
                              </span>
                            )}
                          </div>
                        </td>
                        <td className="px-4 py-3 text-xs font-mono text-gray-500 hidden md:table-cell">
                          {t.ticketNumber}
                        </td>
                        <td className="px-4 py-3">
                          <span className={`inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold border ${PRIORITY_STYLES[t.priority]}`}>
                            {t.priority}
                          </span>
                        </td>
                        <td className="px-4 py-3">
                          <span className={`inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold border ${STATUS_STYLES[t.status]}`}>
                            {STATUS_LABELS[t.status]}
                          </span>
                        </td>
                        <td className="px-4 py-3 text-xs text-gray-400 hidden sm:table-cell tabular-nums">
                          {formatRelative(t.updatedAt)}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>

                {/* Pagination */}
                {totalPages > 1 && (
                  <div className="px-4 py-3 border-t border-gray-100 flex items-center justify-between bg-gray-50">
                    <span className="text-xs text-gray-500 tabular-nums">
                      {total} ticket{total !== 1 ? 's' : ''} total
                    </span>
                    <div className="flex gap-1">
                      {page > 1 && (
                        <a
                          href={pageHref(page - 1)}
                          className="px-3 py-1 text-xs font-medium rounded border border-gray-300 bg-white text-gray-600 hover:bg-gray-50"
                        >
                          ← Prev
                        </a>
                      )}
                      {page < totalPages && (
                        <a
                          href={pageHref(page + 1)}
                          className="px-3 py-1 text-xs font-medium rounded border border-gray-300 bg-white text-gray-600 hover:bg-gray-50"
                        >
                          Next →
                        </a>
                      )}
                    </div>
                  </div>
                )}
              </>
            )}
          </div>
        )}

        {total > 0 && !fetchErr && (
          <p className="text-xs text-gray-400 tabular-nums px-1 mt-3">
            {total} ticket{total !== 1 ? 's' : ''} total
          </p>
        )}

      </div>
    </div>
  );
}
