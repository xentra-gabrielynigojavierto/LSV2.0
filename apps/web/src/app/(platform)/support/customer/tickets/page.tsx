import { redirect } from 'next/navigation';
import Link from 'next/link';
import { getServerSession } from '@/lib/session';
import {
  customerSupportServerApi,
  type CustomerTicketSummary,
  type TicketStatus,
  type TicketPriority,
} from '@/lib/support-server-api';
import { ServerApiError } from '@/lib/server-api-client';

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

function formatDate(iso: string): string {
  try {
    return new Date(iso).toLocaleDateString('en-US', {
      month: 'short',
      day:   'numeric',
      year:  'numeric',
      timeZone: 'UTC',
    });
  } catch {
    return iso;
  }
}

interface PageProps {
  searchParams: Promise<{ page?: string }>;
}

/**
 * /support/customer/tickets — Customer-facing ticket list.
 *
 * Calls ONLY GET /support/api/customer/tickets — never internal /support/api/tickets.
 *
 * Access behavior:
 *   - No platform session → redirect to /login
 *   - 403 from customer endpoint → customer portal login not yet available
 *   - 401 from customer endpoint → redirect to /login
 *   - Other errors → error card
 *
 * The customer endpoint enforces: tenantId + externalCustomerId + CustomerVisible.
 * No authorization overrides are passed from the UI.
 */
export default async function CustomerTicketListPage({ searchParams }: PageProps) {
  const session = await getServerSession();
  if (!session) redirect('/login');

  const sp   = await searchParams;
  const page = Math.max(1, parseInt(sp.page ?? '1', 10) || 1);

  let tickets:      CustomerTicketSummary[] = [];
  let total         = 0;
  let fetchErr:     string | null = null;
  let isForbidden   = false;

  try {
    const result = await customerSupportServerApi.customerTickets.list({ page, pageSize: 25 });
    tickets = result.items;
    total   = result.total;
  } catch (err) {
    if (err instanceof ServerApiError) {
      if (err.status === 401) redirect('/login');
      if (err.status === 403) { isForbidden = true; }
      else fetchErr = `Failed to load your tickets (${err.status}).`;
    } else {
      fetchErr = 'Failed to load your tickets. Please try again later.';
    }
  }

  const totalPages = Math.max(1, Math.ceil(total / 25));

  function pageHref(p: number) {
    return `/support/customer/tickets?page=${p}`;
  }

  return (
    <div className="min-h-full bg-gray-50">
      <div className="max-w-4xl mx-auto px-6 py-8">

        {/* Header */}
        <div className="mb-6">
          <div className="flex items-center gap-3 mb-1">
            <i className="ri-customer-service-2-line text-xl text-indigo-600" />
            <h1 className="text-xl font-semibold text-gray-900">My Support Tickets</h1>
          </div>
          <p className="text-sm text-gray-500">
            View the support tickets associated with your account.
          </p>
        </div>

        {/* Customer login unavailable notice */}
        {isForbidden && (
          <div className="bg-amber-50 border border-amber-200 rounded-lg px-5 py-6 flex gap-4">
            <i className="ri-lock-line text-xl text-amber-500 shrink-0 mt-0.5" />
            <div>
              <p className="text-sm font-semibold text-amber-800">Customer portal access not yet available</p>
              <p className="text-sm text-amber-700 mt-1">
                The customer support portal requires a dedicated customer sign-in, which is not yet
                activated on your account. Please contact your support team for assistance.
              </p>
            </div>
          </div>
        )}

        {/* General error */}
        {fetchErr && !isForbidden && (
          <div className="bg-red-50 border border-red-200 rounded-lg px-5 py-4">
            <p className="text-sm text-red-700 font-medium">Could not load tickets</p>
            <p className="text-xs text-red-600 mt-1">{fetchErr}</p>
          </div>
        )}

        {/* Ticket list */}
        {!fetchErr && !isForbidden && (
          <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
            {tickets.length === 0 ? (
              <div className="px-6 py-12 text-center">
                <i className="ri-inbox-line text-3xl text-gray-300 mb-3 block" />
                <p className="text-sm text-gray-400 font-medium">No tickets found</p>
                <p className="text-xs text-gray-400 mt-1">
                  You have no support tickets associated with your account.
                </p>
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
                      <th className="text-left px-4 py-3 hidden sm:table-cell">Created</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-100">
                    {tickets.map(t => (
                      <tr key={t.id} className="hover:bg-gray-50 transition-colors">
                        <td className="px-4 py-3">
                          <Link
                            href={`/support/customer/tickets/${t.id}`}
                            className="font-medium text-indigo-700 hover:text-indigo-900 hover:underline leading-snug"
                          >
                            {t.title}
                          </Link>
                          {t.category && (
                            <p className="text-xs text-gray-400 mt-0.5">{t.category}</p>
                          )}
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
                          {formatDate(t.createdAt)}
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

        {total > 0 && !fetchErr && !isForbidden && (
          <p className="text-xs text-gray-400 tabular-nums px-1 mt-3">
            {total} ticket{total !== 1 ? 's' : ''} total
          </p>
        )}

      </div>
    </div>
  );
}
