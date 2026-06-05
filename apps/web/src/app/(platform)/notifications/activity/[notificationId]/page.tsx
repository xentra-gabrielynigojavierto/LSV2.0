import Link from 'next/link';
import { notFound } from 'next/navigation';
import { requireOrg } from '@/lib/auth-guards';
import {
  notificationsServerApi,
  parseRecipient,
  NotifApiError,
  type NotifDetail,
  type NotifEvent,
  type NotifIssue,
  type NotifFanOutSummary,
  type NotifFanOutRecipient,
} from '@/lib/notifications-server-api';
import { PRODUCT_TYPE_LABELS, formatFailureCategory, type ProductType } from '@/lib/notifications-shared';
import DeliveryActionsClient from './delivery-actions-client';

const STATUS_CLS: Record<string, string> = {
  sent:       'bg-emerald-50 text-emerald-700 border border-emerald-200',
  delivered:  'bg-emerald-50 text-emerald-700 border border-emerald-200',
  accepted:   'bg-blue-50    text-blue-700    border border-blue-200',
  processing: 'bg-indigo-50  text-indigo-700  border border-indigo-200',
  queued:     'bg-indigo-50  text-indigo-700  border border-indigo-200',
  failed:     'bg-red-50     text-red-700     border border-red-200',
  blocked:    'bg-amber-50   text-amber-700   border border-amber-200',
};

function StatusBadge({ status, size = 'sm' }: { status: string; size?: 'sm' | 'lg' }) {
  const cls = STATUS_CLS[status.toLowerCase()] ?? 'bg-gray-100 text-gray-500 border border-gray-200';
  const sizeClass = size === 'lg' ? 'px-3 py-1 text-xs' : 'px-2 py-0.5 text-[10px]';
  return (
    <span className={`inline-flex items-center rounded-full font-semibold uppercase tracking-wide ${cls} ${sizeClass}`}>
      {status}
    </span>
  );
}

const CHANNEL_CLS: Record<string, string> = {
  email:    'bg-sky-50    text-sky-700    border border-sky-200',
  sms:      'bg-violet-50 text-violet-700 border border-violet-200',
  push:     'bg-orange-50 text-orange-700 border border-orange-200',
  'in-app': 'bg-teal-50   text-teal-700   border border-teal-200',
};

function ChannelBadge({ channel }: { channel: string }) {
  const cls = CHANNEL_CLS[channel.toLowerCase()] ?? 'bg-gray-100 text-gray-500 border border-gray-200';
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[10px] font-medium capitalize ${cls}`}>
      {channel}
    </span>
  );
}

function fmtDateTime(iso: string): string {
  try {
    return new Date(iso).toLocaleString('en-US', {
      month: 'short', day: 'numeric', year: 'numeric',
      hour: 'numeric', minute: '2-digit', second: '2-digit',
    });
  } catch { return iso; }
}

function fmtTime(iso: string): string {
  try {
    return new Date(iso).toLocaleString('en-US', {
      month: 'short', day: 'numeric',
      hour: 'numeric', minute: '2-digit', second: '2-digit',
    });
  } catch { return iso; }
}

function InfoRow({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="flex items-start gap-4 py-2">
      <dt className="w-36 shrink-0 text-xs font-semibold uppercase tracking-wide text-gray-400">{label}</dt>
      <dd className="text-sm text-gray-700 min-w-0">{children}</dd>
    </div>
  );
}

function parseMetadata(json: string | null): Record<string, unknown> | null {
  if (!json) return null;
  try { return JSON.parse(json) as Record<string, unknown>; } catch { return null; }
}

function SafeHtmlFrame({ html, title }: { html: string; title?: string }) {
  const csp = "default-src 'none'; style-src 'unsafe-inline'; img-src * data:; font-src *;";
  const wrapped = `<!DOCTYPE html><html><head><meta http-equiv="Content-Security-Policy" content="${csp}"><meta name="viewport" content="width=device-width,initial-scale=1"><style>body{margin:0;padding:16px;font-family:system-ui,sans-serif;font-size:14px;color:#333;}</style></head><body>${html}</body></html>`;
  const src = `data:text/html;charset=utf-8,${encodeURIComponent(wrapped)}`;
  return (
    <iframe
      src={src}
      title={title ?? 'Content preview'}
      sandbox="allow-same-origin"
      className="w-full border border-gray-200 rounded-lg bg-white"
      style={{ minHeight: 200, height: 400 }}
    />
  );
}

function TemplateUsageSection({ notification }: { notification: NotifDetail }) {
  const meta = parseMetadata(notification.metadataJson);
  const templateKey = notification.templateKey ?? (meta?.['templateKey'] as string | undefined) ?? null;
  const templateName = notification.templateName ?? (meta?.['templateName'] as string | undefined) ?? null;
  const templateSource = notification.templateSource ?? (meta?.['templateSource'] as string | undefined) ?? null;
  const productType = notification.productType ?? (meta?.['productType'] as string | undefined) ?? null;

  if (!templateKey && !templateName && !templateSource) {
    return (
      <div className="bg-white rounded-lg border border-gray-200 p-5">
        <h2 className="text-sm font-semibold text-gray-700 mb-3">Template</h2>
        <p className="text-sm text-gray-400">No template information available for this notification.</p>
      </div>
    );
  }

  const productLabel = productType && PRODUCT_TYPE_LABELS[productType as ProductType];

  return (
    <div className="bg-white rounded-lg border border-gray-200 p-5">
      <h2 className="text-sm font-semibold text-gray-700 mb-3">Template Usage</h2>
      <dl className="divide-y divide-gray-50">
        {templateKey && (
          <InfoRow label="Template Key">{templateKey}</InfoRow>
        )}
        {templateName && (
          <InfoRow label="Template Name">{templateName}</InfoRow>
        )}
        {templateSource && (
          <InfoRow label="Source">
            <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[10px] font-semibold uppercase tracking-wide ${
              templateSource.toLowerCase().includes('tenant') || templateSource.toLowerCase().includes('override')
                ? 'bg-violet-50 text-violet-700 border border-violet-200'
                : 'bg-gray-100 text-gray-600 border border-gray-200'
            }`}>
              {templateSource.toLowerCase().includes('tenant') || templateSource.toLowerCase().includes('override')
                ? 'Tenant Override'
                : 'Global Template'}
            </span>
          </InfoRow>
        )}
        {productLabel && (
          <InfoRow label="Product">{productLabel}</InfoRow>
        )}
        {notification.templateVersionId && (
          <InfoRow label="Version ID">
            <code className="text-xs font-mono text-gray-500">{notification.templateVersionId}</code>
          </InfoRow>
        )}
      </dl>
    </div>
  );
}

function parseFanOutSummary(meta: Record<string, unknown> | null): NotifFanOutSummary | null {
  if (!meta) return null;
  const raw = meta['fanout'];
  if (!raw || typeof raw !== 'object') return null;
  const obj = raw as Record<string, unknown>;
  if (typeof obj.totalResolved !== 'number') return null;
  return obj as unknown as NotifFanOutSummary;
}

const RECIPIENT_STATUS_CLS: Record<string, string> = {
  sent:    'bg-emerald-50 text-emerald-700 border border-emerald-200',
  failed:  'bg-red-50     text-red-700     border border-red-200',
  blocked: 'bg-amber-50   text-amber-700   border border-amber-200',
  skipped: 'bg-gray-100   text-gray-600    border border-gray-200',
};

function humanReason(reason: string): string {
  return reason
    .replace(/_/g, ' ')
    .replace(/\b\w/g, (c) => c.toUpperCase());
}

function FanOutSummarySection({ summary }: { summary: NotifFanOutSummary }) {
  const reachedCount = summary.sentCount;
  const blockedTotal = summary.blockedCount + summary.skippedCount + summary.failedCount;
  const subjectLabel = summary.mode?.toLowerCase() === 'org'
    ? `org ${summary.orgId ?? ''}`.trim()
    : `role ${summary.roleKey ?? ''}`.trim();

  return (
    <div className="bg-white rounded-lg border border-gray-200 p-5">
      <div className="flex items-center justify-between mb-3">
        <h2 className="text-sm font-semibold text-gray-700">
          Fan-out Summary{summary.mode ? ` — ${summary.mode}` : ''}
        </h2>
        {subjectLabel && (
          <span className="text-xs text-gray-400 font-mono">{subjectLabel}</span>
        )}
      </div>

      <div className="grid grid-cols-2 sm:grid-cols-5 gap-3 mb-4">
        <Stat label="Resolved"  value={summary.totalResolved} tone="neutral" />
        <Stat label="Reached"   value={reachedCount}          tone="emerald" />
        <Stat label="Failed"    value={summary.failedCount}   tone="red" />
        <Stat label="Blocked"   value={summary.blockedCount}  tone="amber" />
        <Stat label="Skipped"   value={summary.skippedCount}  tone="gray" />
      </div>

      {summary.totalResolved === 0 && (
        <p className="text-sm text-amber-700 bg-amber-50 border border-amber-200 rounded-md px-3 py-2 mb-3">
          No members were resolved for this {summary.mode?.toLowerCase() ?? 'fan-out'} envelope. Nobody received this notification.
        </p>
      )}

      {summary.totalResolved > 0 && reachedCount === 0 && (
        <p className="text-sm text-amber-700 bg-amber-50 border border-amber-200 rounded-md px-3 py-2 mb-3">
          {summary.totalResolved} member{summary.totalResolved === 1 ? '' : 's'} resolved, but none were reached. See breakdown below.
        </p>
      )}

      {summary.deliveredByChannel && Object.keys(summary.deliveredByChannel).length > 0 && (
        <ReasonList title="Delivered per channel" items={summary.deliveredByChannel} tone="emerald" />
      )}
      {summary.blockedByReason && Object.keys(summary.blockedByReason).length > 0 && (
        <ReasonList title="Blocked reasons"       items={summary.blockedByReason} tone="amber" />
      )}
      {summary.skippedByReason && Object.keys(summary.skippedByReason).length > 0 && (
        <ReasonList title="Skipped reasons"       items={summary.skippedByReason} tone="gray" />
      )}

      {summary.recipients && summary.recipients.length > 0 && (
        <div className="mt-4">
          <p className="text-xs font-semibold uppercase tracking-wide text-gray-400 mb-2">
            Per-recipient outcome ({summary.recipients.length})
          </p>
          <div className="overflow-x-auto border border-gray-100 rounded-md">
            <table className="w-full text-sm">
              <thead className="bg-gray-50 text-xs text-gray-500 uppercase tracking-wide">
                <tr>
                  <th className="px-3 py-2 text-left font-medium">Member</th>
                  <th className="px-3 py-2 text-left font-medium">Status</th>
                  <th className="px-3 py-2 text-left font-medium">Reason</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {summary.recipients.map((r, i) => (
                  <RecipientRow key={i} recipient={r} />
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      <p className="mt-3 text-[11px] text-gray-400">
        {reachedCount} of {summary.totalResolved} resolved member{summary.totalResolved === 1 ? '' : 's'} reached
        {blockedTotal > 0 && ` · ${blockedTotal} not reached`}.
      </p>
    </div>
  );
}

function Stat({ label, value, tone }: { label: string; value: number; tone: 'neutral' | 'emerald' | 'red' | 'amber' | 'gray' }) {
  const toneCls: Record<typeof tone, string> = {
    neutral: 'bg-gray-50  text-gray-800',
    emerald: 'bg-emerald-50 text-emerald-700',
    red:     'bg-red-50   text-red-700',
    amber:   'bg-amber-50 text-amber-700',
    gray:    'bg-gray-50  text-gray-600',
  };
  return (
    <div className={`rounded-lg border border-gray-100 px-3 py-2 ${toneCls[tone]}`}>
      <p className="text-[10px] font-semibold uppercase tracking-wide opacity-70">{label}</p>
      <p className="text-lg font-semibold tabular-nums">{value}</p>
    </div>
  );
}

function ReasonList({ title, items, tone }: { title: string; items: Record<string, number>; tone: 'emerald' | 'amber' | 'gray' }) {
  const toneCls: Record<typeof tone, string> = {
    emerald: 'bg-emerald-50 text-emerald-700 border-emerald-200',
    amber:   'bg-amber-50   text-amber-700   border-amber-200',
    gray:    'bg-gray-50    text-gray-600    border-gray-200',
  };
  return (
    <div className="mt-3">
      <p className="text-xs font-semibold uppercase tracking-wide text-gray-400 mb-1">{title}</p>
      <div className="flex flex-wrap gap-1.5">
        {Object.entries(items).map(([key, count]) => (
          <span key={key} className={`inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs border ${toneCls[tone]}`}>
            <span className="font-medium">{humanReason(key)}</span>
            <span className="tabular-nums opacity-80">×{count}</span>
          </span>
        ))}
      </div>
    </div>
  );
}

function RecipientRow({ recipient }: { recipient: NotifFanOutRecipient }) {
  const statusCls = RECIPIENT_STATUS_CLS[recipient.status.toLowerCase()] ?? 'bg-gray-100 text-gray-500 border border-gray-200';
  const member = recipient.email ?? recipient.userId ?? '—';
  return (
    <tr>
      <td className="px-3 py-2">
        <code className="text-xs text-gray-700 font-mono break-all">{member}</code>
      </td>
      <td className="px-3 py-2">
        <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[10px] font-semibold uppercase tracking-wide ${statusCls}`}>
          {recipient.status}
        </span>
      </td>
      <td className="px-3 py-2 text-xs text-gray-500">
        {recipient.reason ? humanReason(recipient.reason) : <span className="text-gray-300">—</span>}
      </td>
    </tr>
  );
}

function FailureReasonPanel({ notification }: { notification: NotifDetail }) {
  const isFailedOrBlocked = ['failed', 'blocked'].includes(notification.status.toLowerCase());
  if (!isFailedOrBlocked && !notification.lastErrorMessage && !notification.blockedReason && !notification.suppressionReason) {
    return null;
  }

  const isFailed = notification.status.toLowerCase() === 'failed';
  const isBlocked = notification.status.toLowerCase() === 'blocked';

  return (
    <div className={`rounded-lg border p-5 ${
      isFailed ? 'bg-red-50 border-red-200' : isBlocked ? 'bg-amber-50 border-amber-200' : 'bg-gray-50 border-gray-200'
    }`}>
      <h2 className={`text-sm font-semibold mb-3 ${
        isFailed ? 'text-red-800' : isBlocked ? 'text-amber-800' : 'text-gray-700'
      }`}>
        {isFailed ? 'Delivery Failed' : isBlocked ? 'Notification Blocked' : 'Issue Details'}
      </h2>
      <dl className="space-y-2">
        {notification.failureCategory && (
          <InfoRow label="Category">
            <span className="font-medium">{formatFailureCategory(notification.failureCategory)}</span>
          </InfoRow>
        )}
        {notification.lastErrorMessage && (
          <InfoRow label="Error">
            <span className="text-red-700">{notification.lastErrorMessage}</span>
          </InfoRow>
        )}
        {notification.blockedReason && (
          <InfoRow label="Blocked Reason">
            <span className="text-amber-700">{notification.blockedReason}</span>
          </InfoRow>
        )}
        {notification.suppressionReason && (
          <InfoRow label="Suppression">
            <span className="text-amber-700">{notification.suppressionReason}</span>
          </InfoRow>
        )}
      </dl>
    </div>
  );
}

function EventTimeline({ events }: { events: NotifEvent[] }) {
  if (events.length === 0) return null;
  return (
    <div className="bg-white rounded-lg border border-gray-200 p-5">
      <h2 className="text-sm font-semibold text-gray-700 mb-4">Delivery Timeline</h2>
      <div className="relative">
        <div className="absolute left-3 top-2 bottom-2 w-px bg-gray-200" />
        <ul className="space-y-4">
          {events.map((evt, idx) => {
            const isLast = idx === events.length - 1;
            const dotColor = evt.status === 'failed' ? 'bg-red-500'
              : evt.status === 'blocked' ? 'bg-amber-500'
              : evt.status === 'sent' || evt.status === 'delivered' ? 'bg-emerald-500'
              : 'bg-blue-500';
            return (
              <li key={evt.id} className="relative pl-8">
                <div className={`absolute left-1.5 top-1 w-3 h-3 rounded-full ${dotColor} ring-2 ring-white`} />
                <div className="flex items-start justify-between gap-4">
                  <div>
                    <p className="text-sm font-medium text-gray-800 capitalize">{evt.type.replace(/_/g, ' ')}</p>
                    {evt.detail && (
                      <p className="text-xs text-gray-500 mt-0.5">{evt.detail}</p>
                    )}
                    {evt.provider && (
                      <p className="text-xs text-gray-400 mt-0.5">Provider: {evt.provider}</p>
                    )}
                  </div>
                  <span className="text-[11px] text-gray-400 whitespace-nowrap shrink-0">
                    {fmtTime(evt.timestamp)}
                  </span>
                </div>
              </li>
            );
          })}
        </ul>
      </div>
    </div>
  );
}

function IssuesList({ issues }: { issues: NotifIssue[] }) {
  if (issues.length === 0) return null;

  const severityCls: Record<string, string> = {
    critical: 'bg-red-50 text-red-700 border-red-200',
    high:     'bg-red-50 text-red-700 border-red-200',
    medium:   'bg-amber-50 text-amber-700 border-amber-200',
    low:      'bg-gray-50 text-gray-600 border-gray-200',
  };

  return (
    <div className="bg-white rounded-lg border border-gray-200 p-5">
      <h2 className="text-sm font-semibold text-gray-700 mb-4">Related Issues</h2>
      <div className="space-y-3">
        {issues.map(issue => (
          <div key={issue.id} className={`rounded-lg border p-4 ${severityCls[issue.severity.toLowerCase()] ?? 'bg-gray-50 border-gray-200'}`}>
            <div className="flex items-center gap-2 mb-1">
              <span className="text-[10px] font-semibold uppercase tracking-wide">{issue.severity}</span>
              <span className="text-[10px] uppercase tracking-wide text-gray-400">{issue.category}</span>
            </div>
            <p className="text-sm font-medium">{issue.message}</p>
            {issue.detail && <p className="text-xs mt-1 opacity-75">{issue.detail}</p>}
            <p className="text-[10px] text-gray-400 mt-2">
              {fmtDateTime(issue.createdAt)}
              {issue.resolvedAt && ` — Resolved ${fmtDateTime(issue.resolvedAt)}`}
            </p>
          </div>
        ))}
      </div>
    </div>
  );
}

function ContentPreview({ notification }: { notification: NotifDetail }) {
  const meta = parseMetadata(notification.metadataJson);
  const subject = notification.subject ?? (meta?.['subject'] as string | undefined) ?? null;
  const bodyHtml = notification.bodyHtml ?? (meta?.['bodyHtml'] as string | undefined) ?? null;
  const bodyText = notification.bodyText ?? (meta?.['bodyText'] as string | undefined) ?? (meta?.['body'] as string | undefined) ?? null;

  if (!subject && !bodyHtml && !bodyText) {
    return null;
  }

  return (
    <div className="bg-white rounded-lg border border-gray-200 p-5 space-y-4">
      <h2 className="text-sm font-semibold text-gray-700">Content</h2>
      {subject && (
        <div>
          <p className="text-xs font-semibold uppercase tracking-wide text-gray-400 mb-1">Subject</p>
          <p className="text-sm text-gray-800 font-medium">{subject}</p>
        </div>
      )}
      {bodyHtml && (
        <div>
          <p className="text-xs font-semibold uppercase tracking-wide text-gray-400 mb-2">HTML Body</p>
          <SafeHtmlFrame html={bodyHtml} title="Email content" />
        </div>
      )}
      {bodyText && !bodyHtml && (
        <div>
          <p className="text-xs font-semibold uppercase tracking-wide text-gray-400 mb-1">Text Body</p>
          <pre className="text-sm text-gray-700 whitespace-pre-wrap bg-gray-50 rounded-lg p-4 border border-gray-200 font-mono">
            {bodyText}
          </pre>
        </div>
      )}
      {bodyText && bodyHtml && (
        <div>
          <p className="text-xs font-semibold uppercase tracking-wide text-gray-400 mb-1">Text Version</p>
          <pre className="text-sm text-gray-700 whitespace-pre-wrap bg-gray-50 rounded-lg p-3 border border-gray-200 font-mono text-xs max-h-40 overflow-auto">
            {bodyText}
          </pre>
        </div>
      )}
    </div>
  );
}

export default async function NotificationDetailPage({
  params,
}: {
  params: Promise<{ notificationId: string }>;
}) {
  const { notificationId } = await params;
  const session = await requireOrg();
  const { tenantId } = session;

  let notification: NotifDetail | null = null;
  let fetchErr: string | null = null;
  let events: NotifEvent[] = [];
  let eventsUnavailable = false;
  let issues: NotifIssue[] = [];
  let issuesUnavailable = false;

  try {
    const res = await notificationsServerApi.get(tenantId, notificationId);
    notification = res.data;
  } catch (err: unknown) {
    if (err instanceof NotifApiError && err.status === 404) {
      notFound();
    }
    fetchErr = err instanceof Error ? err.message : 'Unable to load notification.';
  }

  if (notification) {
    const [eventsResult, issuesResult] = await Promise.allSettled([
      notificationsServerApi.events(tenantId, notificationId),
      notificationsServerApi.issues(tenantId, notificationId),
    ]);

    if (eventsResult.status === 'fulfilled') {
      events = eventsResult.value.data;
    } else {
      eventsUnavailable = true;
    }

    if (issuesResult.status === 'fulfilled') {
      issues = issuesResult.value.data;
    } else {
      issuesUnavailable = true;
    }
  }

  if (fetchErr) {
    return (
      <div className="max-w-4xl mx-auto space-y-6">
        <div className="flex items-center gap-2 mb-1">
          <Link href="/notifications" className="text-sm text-gray-400 hover:text-gray-600 transition-colors">Notifications</Link>
          <i className="ri-arrow-right-s-line text-gray-300" />
          <Link href="/notifications/activity" className="text-sm text-gray-400 hover:text-gray-600 transition-colors">Activity</Link>
          <i className="ri-arrow-right-s-line text-gray-300" />
          <span className="text-sm text-gray-700 font-medium">Detail</span>
        </div>
        <div className="rounded-lg bg-red-50 border border-red-200 px-5 py-4 text-sm text-red-700">
          <i className="ri-error-warning-line mr-1.5" />
          {fetchErr}
        </div>
      </div>
    );
  }

  if (!notification) {
    notFound();
  }

  const recipient = parseRecipient(notification.recipientJson);
  const meta = parseMetadata(notification.metadataJson);
  const fanOutSummary = parseFanOutSummary(meta);
  const productLabel = notification.productType
    ? PRODUCT_TYPE_LABELS[notification.productType as ProductType] ?? notification.productType
    : (meta?.['productType'] as string | undefined) ?? null;

  return (
    <div className="max-w-4xl mx-auto space-y-6">
      <div>
        <div className="flex items-center gap-2 mb-1">
          <Link href="/notifications" className="text-sm text-gray-400 hover:text-gray-600 transition-colors">Notifications</Link>
          <i className="ri-arrow-right-s-line text-gray-300" />
          <Link href="/notifications/activity" className="text-sm text-gray-400 hover:text-gray-600 transition-colors">Activity</Link>
          <i className="ri-arrow-right-s-line text-gray-300" />
          <span className="text-sm text-gray-700 font-medium">Detail</span>
        </div>
        <div className="flex items-center gap-3 mt-2">
          <h1 className="text-2xl font-bold text-gray-900">Notification Detail</h1>
          <StatusBadge status={notification.status} size="lg" />
        </div>
        <p className="mt-1 text-sm text-gray-500">
          <code className="text-xs font-mono text-gray-400">{notification.id}</code>
        </p>
      </div>

      <div className="bg-white rounded-lg border border-gray-200 p-5">
        <h2 className="text-sm font-semibold text-gray-700 mb-3">Notification Details</h2>
        <dl className="divide-y divide-gray-50">
          <InfoRow label="Recipient">
            <span className="font-mono">{recipient}</span>
          </InfoRow>
          <InfoRow label="Channel">
            <ChannelBadge channel={notification.channel} />
          </InfoRow>
          <InfoRow label="Status">
            <StatusBadge status={notification.status} />
          </InfoRow>
          {notification.providerUsed && (
            <InfoRow label="Provider">
              {notification.providerUsed}
            </InfoRow>
          )}
          {productLabel && (
            <InfoRow label="Product">
              {productLabel}
            </InfoRow>
          )}
          <InfoRow label="Created">
            {fmtDateTime(notification.createdAt)}
          </InfoRow>
          <InfoRow label="Last Updated">
            {fmtDateTime(notification.updatedAt)}
          </InfoRow>
        </dl>
      </div>

      {fanOutSummary && <FanOutSummarySection summary={fanOutSummary} />}

      <FailureReasonPanel notification={notification} />

      <DeliveryActionsClient notification={notification} />

      <TemplateUsageSection notification={notification} />

      <ContentPreview notification={notification} />

      {events.length > 0 && <EventTimeline events={events} />}

      {eventsUnavailable && (
        <div className="bg-gray-50 rounded-lg border border-gray-200 p-5 text-center">
          <i className="ri-time-line text-2xl text-gray-300" />
          <p className="mt-1 text-sm text-gray-400">Delivery timeline is not available for this notification.</p>
        </div>
      )}

      {issues.length > 0 && <IssuesList issues={issues} />}

      {issuesUnavailable && (
        <div className="bg-gray-50 rounded-lg border border-gray-200 p-5 text-center">
          <i className="ri-alert-line text-2xl text-gray-300" />
          <p className="mt-1 text-sm text-gray-400">Issue details are not available for this notification.</p>
        </div>
      )}

      <div className="pt-2">
        <Link
          href="/notifications/activity"
          className="inline-flex items-center gap-1.5 text-sm text-indigo-600 hover:text-indigo-500 font-medium"
        >
          <i className="ri-arrow-left-line" />
          Back to Activity
        </Link>
      </div>
    </div>
  );
}
