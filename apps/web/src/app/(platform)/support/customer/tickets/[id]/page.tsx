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
import { CustomerCommentForm } from '@/components/support/CustomerCommentForm';

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
    return new Date(iso).toLocaleString('en-US', {
      month:    'short',
      day:      'numeric',
      year:     'numeric',
      hour:     '2-digit',
      minute:   '2-digit',
      hour12:   false,
      timeZone: 'UTC',
    }) + ' UTC';
  } catch {
    return iso;
  }
}

interface PageProps {
  params: Promise<{ id: string }>;
}

/**
 * /support/customer/tickets/[id] — Customer ticket detail page.
 *
 * Calls ONLY GET /support/api/customer/tickets/{id} — never internal endpoints.
 *
 * Displays:
 *   - Ticket number, title, status, priority
 *   - Description
 *   - Created / updated dates
 *   - Comment submission form (CustomerCommentForm client component)
 *
 * Does NOT display:
 *   - Product references (internal deep links, not customer-safe without a dedicated endpoint)
 *   - Internal timeline or audit log
 *   - Queue/assignment/status controls
 *   - Internal notes or agent information
 *
 * Access behavior:
 *   - No platform session → redirect to /login
 *   - 403 → customer portal access not yet available
 *   - 401 → redirect to /login
 *   - 404 → not found message (no leakage of whether ticket exists for another customer)
 */
export default async function CustomerTicketDetailPage({ params }: PageProps) {
  const { id } = await params;

  const session = await getServerSession();
  if (!session) redirect('/login');

  let ticket:    CustomerTicketSummary | null = null;
  let fetchErr:  string | null = null;
  let isForbidden = false;
  let isNotFound  = false;

  try {
    ticket = await customerSupportServerApi.customerTickets.getById(id);
  } catch (err) {
    if (err instanceof ServerApiError) {
      if (err.status === 401) redirect('/login');
      if (err.status === 403) { isForbidden = true; }
      else if (err.status === 404) { isNotFound = true; }
      else fetchErr = `Failed to load this ticket (${err.status}).`;
    } else {
      fetchErr = 'Failed to load this ticket. Please try again later.';
    }
  }

  return (
    <div className="min-h-full bg-gray-50">
      <div className="max-w-3xl mx-auto px-6 py-8">

        {/* Breadcrumb */}
        <nav className="mb-5 flex items-center gap-2 text-xs text-gray-400">
          <Link href="/support/customer/tickets" className="hover:text-gray-600 transition-colors">
            My Tickets
          </Link>
          <span>/</span>
          <span className="text-gray-600 font-medium truncate max-w-xs">
            {ticket?.ticketNumber ?? id}
          </span>
        </nav>

        {/* Customer login unavailable */}
        {isForbidden && (
          <div className="bg-amber-50 border border-amber-200 rounded-lg px-5 py-6 flex gap-4">
            <i className="ri-lock-line text-xl text-amber-500 shrink-0 mt-0.5" />
            <div>
              <p className="text-sm font-semibold text-amber-800">Customer portal access not yet available</p>
              <p className="text-sm text-amber-700 mt-1">
                Viewing ticket details requires a dedicated customer sign-in, which is not yet
                activated. Please contact your support team for assistance.
              </p>
              <Link
                href="/support/customer/tickets"
                className="mt-3 inline-flex items-center gap-1.5 text-xs text-amber-700 hover:text-amber-900 font-medium"
              >
                <i className="ri-arrow-left-line" />
                Back to My Tickets
              </Link>
            </div>
          </div>
        )}

        {/* Not found */}
        {isNotFound && (
          <div className="bg-white border border-gray-200 rounded-lg px-5 py-10 text-center">
            <i className="ri-file-unknow-line text-3xl text-gray-300 mb-3 block" />
            <p className="text-sm font-medium text-gray-600">Ticket not found</p>
            <p className="text-xs text-gray-400 mt-1">
              This ticket does not exist or you do not have access to it.
            </p>
            <Link
              href="/support/customer/tickets"
              className="mt-5 inline-flex items-center gap-1.5 text-sm text-indigo-600 hover:text-indigo-800 font-medium"
            >
              <i className="ri-arrow-left-line" />
              Back to My Tickets
            </Link>
          </div>
        )}

        {/* General error */}
        {fetchErr && !isForbidden && !isNotFound && (
          <div className="mb-6 bg-red-50 border border-red-200 rounded-lg px-5 py-4">
            <p className="text-sm text-red-700 font-medium">Failed to load ticket</p>
            <p className="text-xs text-red-600 mt-1">{fetchErr}</p>
            <Link
              href="/support/customer/tickets"
              className="mt-3 inline-flex items-center gap-1.5 text-xs text-red-600 hover:text-red-800 font-medium"
            >
              <i className="ri-arrow-left-line" />
              Back to My Tickets
            </Link>
          </div>
        )}

        {/* Ticket content */}
        {ticket && (
          <>
            {/* Ticket header */}
            <div className="mb-6">
              <div className="flex items-start gap-3 flex-wrap">
                <h1 className="text-xl font-semibold text-gray-900 flex-1 min-w-0 leading-snug">
                  {ticket.title}
                </h1>
                <span className={`inline-flex items-center px-2.5 py-1 rounded-full text-xs font-semibold border shrink-0 ${PRIORITY_STYLES[ticket.priority]}`}>
                  {ticket.priority} Priority
                </span>
              </div>
              <div className="flex items-center gap-2 mt-1.5 flex-wrap">
                <span className="text-xs font-mono text-gray-400">{ticket.ticketNumber}</span>
                <span className="text-gray-300">·</span>
                <span className={`inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold border ${STATUS_STYLES[ticket.status]}`}>
                  {STATUS_LABELS[ticket.status]}
                </span>
                {ticket.category && (
                  <>
                    <span className="text-gray-300">·</span>
                    <span className="text-xs text-gray-500">{ticket.category}</span>
                  </>
                )}
              </div>
            </div>

            <div className="space-y-5">

              {/* Ticket metadata */}
              <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
                <div className="px-5 py-3.5 border-b border-gray-100 bg-gray-50">
                  <h2 className="text-xs font-semibold uppercase tracking-wide text-gray-500">Ticket Details</h2>
                </div>
                <dl className="divide-y divide-gray-100">
                  <MetaRow label="Created"  value={formatDate(ticket.createdAt)} />
                  <MetaRow label="Updated"  value={formatDate(ticket.updatedAt)} />
                  {ticket.resolvedAt && (
                    <MetaRow label="Resolved" value={formatDate(ticket.resolvedAt)} />
                  )}
                  {ticket.closedAt && (
                    <MetaRow label="Closed"   value={formatDate(ticket.closedAt)} />
                  )}
                </dl>
              </div>

              {/* Description */}
              {ticket.description && (
                <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
                  <div className="px-5 py-3.5 border-b border-gray-100 bg-gray-50">
                    <h2 className="text-xs font-semibold uppercase tracking-wide text-gray-500">Description</h2>
                  </div>
                  <div className="px-5 py-4">
                    <p className="text-sm text-gray-700 leading-relaxed whitespace-pre-wrap">
                      {ticket.description}
                    </p>
                  </div>
                </div>
              )}

              {/* Comment notice (read not yet available) */}
              <div className="bg-blue-50 border border-blue-100 rounded-lg px-5 py-4 flex gap-3">
                <i className="ri-information-line text-blue-400 shrink-0 mt-0.5" />
                <p className="text-xs text-blue-700">
                  Conversation history view is not yet available in the customer portal.
                  You can submit a new comment below and your support team will respond via your
                  preferred contact method.
                </p>
              </div>

              {/* Comment submission form */}
              <CustomerCommentForm ticketId={id} />

            </div>
          </>
        )}

      </div>
    </div>
  );
}

function MetaRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-baseline gap-4 px-5 py-2.5">
      <dt className="text-xs text-gray-400 font-medium w-24 shrink-0">{label}</dt>
      <dd className="text-sm text-gray-700">{value}</dd>
    </div>
  );
}
