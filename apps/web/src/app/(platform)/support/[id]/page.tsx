import { redirect, notFound } from 'next/navigation';
import Link from 'next/link';
import { getServerSession } from '@/lib/session';
import {
  supportServerApi,
  type TicketSummary,
  type TicketStatus,
  type TicketPriority,
  type ProductRefResponse,
  type CustomerCommentResponse,
  type TicketAttachmentResponse,
} from '@/lib/support-server-api';
import { resolveDeepLink, getProductDisplayName } from '@/lib/product-deep-links';
import { TicketReplyForm } from '@/components/support/TicketReplyForm';

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

interface TicketDetailPageProps {
  params: Promise<{ id: string }>;
}

/**
 * /support/[id] — Support ticket detail page.
 *
 * Access: PlatformAdmin or TenantAdmin.
 * Data: ticket + product refs fetched in parallel from Support service via gateway.
 *
 * Product refs are displayed with deep links to their target product pages.
 * Deep links are relative paths — tenant session + product auth enforces access.
 */
export default async function TicketDetailPage({ params }: TicketDetailPageProps) {
  const { id } = await params;

  const session = await getServerSession();
  if (!session || (!session.isPlatformAdmin && !session.isTenantAdmin)) {
    redirect('/access-denied');
  }

  let ticket:      TicketSummary | null = null;
  let refs:        ProductRefResponse[] = [];
  let comments:    CustomerCommentResponse[] = [];
  let attachments: TicketAttachmentResponse[] = [];
  let fetchErr:    string | null = null;

  try {
    const [ticketResult, refsResult, commentsResult, attachmentsResult] = await Promise.allSettled([
      supportServerApi.tickets.getById(id),
      supportServerApi.productRefs.list(id),
      supportServerApi.tickets.getComments(id),
      supportServerApi.attachments.list(id),
    ]);

    if (ticketResult.status === 'fulfilled') {
      ticket = ticketResult.value;
    } else {
      const err = ticketResult.reason;
      if (err && typeof err === 'object' && 'status' in err && err.status === 404) {
        notFound();
      }
      fetchErr = err instanceof Error ? err.message : 'Failed to load ticket.';
    }

    if (refsResult.status === 'fulfilled') {
      refs = Array.isArray(refsResult.value) ? refsResult.value : [];
    }

    if (commentsResult.status === 'fulfilled') {
      comments = Array.isArray(commentsResult.value) ? commentsResult.value : [];
    }

    if (attachmentsResult.status === 'fulfilled') {
      attachments = Array.isArray(attachmentsResult.value) ? attachmentsResult.value : [];
    }
  } catch {
    fetchErr = 'Failed to load ticket.';
  }

  const visibleComments = comments
    .filter(c => c.visibility === 'CustomerVisible')
    .sort((a, b) => a.createdAt.localeCompare(b.createdAt));

  if (!ticket && !fetchErr) notFound();

  return (
    <div className="min-h-full bg-gray-50">
      <div className="max-w-3xl mx-auto px-6 py-8">

        {/* Breadcrumb */}
        <nav className="mb-5 flex items-center gap-2 text-xs text-gray-400">
          <Link href="/support" className="hover:text-gray-600 transition-colors">
            Support
          </Link>
          <span>/</span>
          <span className="text-gray-600 font-medium truncate max-w-xs">
            {ticket?.ticketNumber ?? id}
          </span>
        </nav>

        {fetchErr && (
          <div className="mb-6 bg-red-50 border border-red-200 rounded-lg px-5 py-4">
            <p className="text-sm text-red-700 font-medium">Failed to load ticket</p>
            <p className="text-xs text-red-600 mt-1">{fetchErr}</p>
          </div>
        )}

        {ticket && (
          <>
            {/* Header */}
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

              {/* Metadata card */}
              <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
                <div className="px-5 py-3.5 border-b border-gray-100 bg-gray-50">
                  <h2 className="text-xs font-semibold uppercase tracking-wide text-gray-500">Ticket Details</h2>
                </div>
                <dl className="divide-y divide-gray-100">
                  {ticket.requesterName  && <MetaRow label="Requester" value={ticket.requesterName} />}
                  {ticket.requesterEmail && <MetaRow label="Email"     value={ticket.requesterEmail} />}
                  <MetaRow label="Source"  value={ticket.source} />
                  <MetaRow label="Created" value={formatDate(ticket.createdAt)} />
                  <MetaRow label="Updated" value={formatDate(ticket.updatedAt)} />
                  {ticket.dueAt     && <MetaRow label="Due"      value={formatDate(ticket.dueAt)} />}
                  {ticket.resolvedAt && <MetaRow label="Resolved" value={formatDate(ticket.resolvedAt)} />}
                </dl>
              </div>

              {/* Description */}
              {ticket.description && (
                <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
                  <div className="px-5 py-3.5 border-b border-gray-100 bg-gray-50">
                    <h2 className="text-xs font-semibold uppercase tracking-wide text-gray-500">Description</h2>
                  </div>
                  <div className="px-5 py-4">
                    <p className="text-sm text-gray-700 leading-relaxed whitespace-pre-wrap">{ticket.description}</p>
                  </div>
                </div>
              )}

              {/* Conversation thread */}
              <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
                <div className="px-5 py-3.5 border-b border-gray-100 bg-gray-50 flex items-center justify-between">
                  <h2 className="text-xs font-semibold uppercase tracking-wide text-gray-500">
                    Conversation
                  </h2>
                  <span className="text-xs text-gray-400 tabular-nums">
                    {visibleComments.length} message{visibleComments.length !== 1 ? 's' : ''}
                  </span>
                </div>

                {visibleComments.length === 0 ? (
                  <p className="px-5 py-4 text-sm text-gray-400">
                    No messages yet. Use the form below to send a message to support.
                  </p>
                ) : (
                  <div className="divide-y divide-gray-50">
                    {visibleComments.map(comment => {
                      const isSupport = comment.commentType !== 'Normal' || comment.authorEmail?.endsWith('@legalsynq.com');
                      const fromPortal = !isSupport;
                      return (
                        <div
                          key={comment.id}
                          className={`px-5 py-4 flex flex-col ${fromPortal ? 'items-end' : 'items-start'} gap-1`}
                        >
                          <div className={`flex items-center gap-2 text-xs text-gray-400 ${fromPortal ? 'justify-end' : 'justify-start'}`}>
                            <span className="font-semibold text-gray-600">
                              {comment.authorName ?? comment.authorEmail ?? 'Support'}
                            </span>
                            <span className="text-gray-300">·</span>
                            <span>{formatDate(comment.createdAt)}</span>
                          </div>
                          <div className={`max-w-prose px-3.5 py-2.5 rounded-xl text-sm leading-relaxed ${
                            fromPortal
                              ? 'bg-indigo-600 text-white'
                              : 'bg-gray-100 text-gray-800'
                          }`}>
                            {comment.body}
                          </div>
                        </div>
                      );
                    })}
                  </div>
                )}

                {/* Reply form — hidden if ticket is closed or cancelled */}
                {ticket.status !== 'Closed' && ticket.status !== 'Cancelled' && (
                  <div className="border-t border-gray-100 px-5 py-4">
                    <label className="block text-xs font-semibold text-gray-500 uppercase tracking-wide mb-2">
                      Send a Reply
                    </label>
                    <TicketReplyForm ticketId={ticket.id} />
                  </div>
                )}
              </div>

              {/* Attachments */}
              <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
                <div className="px-5 py-3.5 border-b border-gray-100 bg-gray-50 flex items-center justify-between">
                  <h2 className="text-xs font-semibold uppercase tracking-wide text-gray-500">
                    Attachments
                  </h2>
                  <span className="text-xs text-gray-400 tabular-nums">
                    {attachments.length} file{attachments.length !== 1 ? 's' : ''}
                  </span>
                </div>
                {attachments.length === 0 ? (
                  <p className="px-5 py-4 text-sm text-gray-400">No attachments yet.</p>
                ) : (
                  <ul className="divide-y divide-gray-100">
                    {attachments.map(att => (
                      <li key={att.id} className="flex items-center gap-3 px-5 py-3">
                        <i className="ri-file-line text-gray-400 shrink-0" aria-hidden="true" />
                        <div className="flex-1 min-w-0">
                          <p className="text-sm text-gray-800 truncate">{att.fileName}</p>
                          {att.fileSizeBytes != null && (
                            <p className="text-xs text-gray-400">
                              {att.fileSizeBytes < 1024 * 1024
                                ? `${(att.fileSizeBytes / 1024).toFixed(0)} KB`
                                : `${(att.fileSizeBytes / (1024 * 1024)).toFixed(1)} MB`}
                            </p>
                          )}
                        </div>
                        <a
                          href={`/api/support/api/tickets/${encodeURIComponent(ticket.id)}/attachments/${encodeURIComponent(att.id)}/download`}
                          target="_blank"
                          rel="noreferrer"
                          className="shrink-0 text-xs text-indigo-600 hover:text-indigo-800 font-medium flex items-center gap-1"
                        >
                          <i className="ri-download-line text-xs" aria-hidden="true" />
                          Download
                        </a>
                      </li>
                    ))}
                  </ul>
                )}
              </div>

              {/* Product References */}
              <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
                <div className="px-5 py-3.5 border-b border-gray-100 bg-gray-50 flex items-center justify-between">
                  <h2 className="text-xs font-semibold uppercase tracking-wide text-gray-500">
                    Product References
                  </h2>
                  <span className="text-xs text-gray-400 tabular-nums">
                    {refs.length} linked
                  </span>
                </div>
                {refs.length === 0 ? (
                  <p className="px-5 py-4 text-sm text-gray-400">No product references linked.</p>
                ) : (
                  <ul className="divide-y divide-gray-100">
                    {refs.map(ref => (
                      <ProductRefRow key={ref.id} ref={ref} />
                    ))}
                  </ul>
                )}
              </div>

            </div>
          </>
        )}

      </div>
    </div>
  );
}

// ── Sub-components ─────────────────────────────────────────────────────────────

function MetaRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-baseline gap-4 px-5 py-2.5">
      <dt className="text-xs text-gray-400 font-medium w-24 shrink-0">{label}</dt>
      <dd className="text-sm text-gray-700">{value}</dd>
    </div>
  );
}

function ProductRefRow({ ref }: { ref: ProductRefResponse }) {
  const productName = getProductDisplayName(ref.productCode);
  const deepLink    = resolveDeepLink(ref.productCode, ref.entityType, ref.entityId);
  const label       = ref.displayLabel || ref.entityId;

  const code = ref.productCode.toLowerCase();
  const badgeClass =
    code === 'careconnect' ? 'bg-teal-100   text-teal-700'   :
    code === 'liens'       ? 'bg-blue-100   text-blue-700'   :
    code === 'fund'        ? 'bg-violet-100 text-violet-700' :
                             'bg-gray-100   text-gray-600';

  return (
    <li className="px-5 py-3 flex items-start gap-3">
      <span className={`shrink-0 mt-0.5 inline-flex items-center px-2 py-0.5 rounded text-[10px] font-bold uppercase tracking-wide ${badgeClass}`}>
        {ref.productCode}
      </span>
      <div className="min-w-0 flex-1">
        <div className="flex items-center gap-1.5 flex-wrap">
          <span className="text-xs font-semibold text-gray-700">{productName}</span>
          <span className="text-gray-300 text-xs">·</span>
          <span className="text-xs text-gray-500 capitalize">{ref.entityType}</span>
        </div>
        <div className="mt-0.5">
          {deepLink ? (
            <Link
              href={deepLink}
              className="text-sm text-indigo-700 hover:text-indigo-900 hover:underline font-medium"
              title={`Open ${productName} ${ref.entityType}`}
            >
              {label}
              <span className="ml-1 text-indigo-400 text-[10px]" aria-hidden="true">↗</span>
            </Link>
          ) : (
            <span className="text-sm text-gray-600 font-medium">{label}</span>
          )}
        </div>
        {ref.displayLabel && ref.displayLabel !== ref.entityId && (
          <p className="text-[11px] text-gray-400 mt-0.5 font-mono">{ref.entityId}</p>
        )}
      </div>
    </li>
  );
}

// ── Helpers ────────────────────────────────────────────────────────────────────

function formatDate(iso: string): string {
  try {
    return new Date(iso).toLocaleString('en-US', {
      month:   'short',
      day:     'numeric',
      year:    'numeric',
      hour:    '2-digit',
      minute:  '2-digit',
      hour12:  false,
      timeZone: 'UTC',
    }) + ' UTC';
  } catch {
    return iso;
  }
}
